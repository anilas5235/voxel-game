using Runtime.Engine.Noise;
using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Chunk
{
    [BurstCompile]
    internal static class ChunkGenerationTerrain
    {
        private const float BiomeScale = 0.0012f;

        private const int MountainSnowline = 215;

        public static void PrepareChunkMaps(ref int3 chunkSize, ref NoiseProfile noiseProfile, int randomSeed,
            ref GeneratorConfig config, ref int3 chunkWordPos, NativeArray<ChunkColumn> chunkColumns)
        {
            int sx = chunkSize.x;
            int sz = chunkSize.z;
            int sy = chunkSize.y;

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int i = ChunkGenerationUtils.GetColumnIdx(x, z, sz);
                float2 worldPos = new(chunkWordPos.x + x, chunkWordPos.z + z);

                float2 noiseSamplePos = worldPos + new float2(-randomSeed, randomSeed);

                // Klima-Noise sauber normalisieren (0..1)
                float humidityRaw = noise.cnoise((noiseSamplePos - 789f) * BiomeScale);
                float temperatureRaw = noise.cnoise((noiseSamplePos + 543f) * BiomeScale);
                float humidity = humidityRaw * 0.5f + 0.5f;
                float temperature = temperatureRaw * 0.5f + 0.5f;

                // Basis-Höhen-Noise
                float rawHeight = noiseProfile.GetNoise(worldPos);
                float rawHeight01 = math.saturate(rawHeight); // kein zusätzliches Remap nach oben

                // Optionaler Gebirgs-Noise (einfacher Mask-Wert 0..1)
                const float mountainScale = 0.0012f; // etwas höher → kleinere, seltenere Berge
                const float mountainThreshold = 0.90f; // seltener über Schwelle
                const float mountainHeightMultiplier = 1.25f; // schwächerer Anhebe-Faktor

                float mountainRaw = noise.cnoise(worldPos * mountainScale);
                float mountainMask = math.saturate(mountainRaw * 0.5f + 0.5f);
                float mountainFactor = math.smoothstep(mountainThreshold, 1f, mountainMask);

                // Optionaler Kontinental-Noise (sehr grob skaliert)
                const float continentalScale = 0.0004f;
                const float continentalAmplitude = 0.06f; // geringerer Einfluss
                float continentalRaw = noise.cnoise(worldPos * continentalScale);
                float continentality = math.saturate(continentalRaw * 0.5f + 0.5f);

                // Basis-Höhenanteil (klima-unabhängig)
                const float minHeightFrac = 0.36f;
                const float maxHeightFrac = 0.86f;
                float baseHeightFrac = math.lerp(minHeightFrac, maxHeightFrac, rawHeight01);

                // Kontinentaler Bias: hebt/senkt Landmassen leicht
                float continentalBias = math.lerp(-continentalAmplitude, continentalAmplitude, continentality);
                baseHeightFrac = math.saturate(baseHeightFrac + continentalBias);

                // Gebirge verstärken die Höhe
                baseHeightFrac *= math.lerp(1f, mountainHeightMultiplier, mountainFactor);

                // Leichter Klima-Einfluss auf die Höhe (Option B)
                const float climateHeightInfluence = 0.1f; // etwas geringer
                const float temperatureHeightBias = 0.5f; // kälter -> etwas höher
                const float humidityHeightBias = -0.4f; // trockener -> etwas höher

                float tempDeviation = temperature - 0.5f; // -0.5 .. 0.5
                float humDeviation = humidity - 0.5f; // -0.5 .. 0.5

                float climateMod = 1f + climateHeightInfluence *
                    (temperatureHeightBias * tempDeviation + humidityHeightBias * humDeviation);

                // Clamp Klima-Modifikator in sinnvollen Bereich
                const float minClimateMod = 1f - climateHeightInfluence;
                const float maxClimateMod = 1f + climateHeightInfluence;
                climateMod = math.clamp(climateMod, minClimateMod, maxClimateMod);

                baseHeightFrac = math.saturate(baseHeightFrac * climateMod);

                // Mappe finale Höhe mit Sicherheitsabstand zur Weltobergrenze (Top-Margin)
                const int topMarginY = 8; // verhindert Abschneiden an WorldHeight
                const int minY = 1;
                int maxY = math.max(minY + 1, sy - topMarginY);
                int height = math.clamp(minY + (int)(baseHeightFrac * (maxY - minY)), minY, maxY);

                ChunkColumn col = new()
                {
                    Height = height,
                    Biome = BiomeHelper.SelectBiome(
                        temperature,
                        humidity,
                        (float)height / maxY,
                        height,
                        config.WaterLevel,
                        continentality,
                        mountainMask),
                    Temperature = temperature,
                    Humidity = humidity
                };
                uint seed = (uint)((chunkWordPos.x + x) ^ (chunkWordPos.y + z) ^ randomSeed ^ 0x85ebca6b);
                Random rng = new(seed == 0 ? 1u : seed);
                SelectSurfaceMaterials(ref config, ref col, ref rng);
                chunkColumns[i] = col;
            }
        }

        public static void FillTerrain(ref int3 chunkSize, NativeArray<ushort> vox,
            int waterLevel, NativeArray<ChunkColumn> chunkColumns, ref GeneratorConfig config)
        {
            int sx = chunkSize.x;
            int sz = chunkSize.z;
            int sy = chunkSize.y;
            const ushort air = 0;
            ushort waterBlock = config.Water;
            ushort stone = config.Stone;
            ushort dirt = config.Dirt;

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int i = ChunkGenerationUtils.GetColumnIdx(x, z, sz);

                ChunkColumn col = chunkColumns[i];

                ushort st = col.StoneBlock;
                ushort under = col.UnderBlock;
                ushort top = col.TopBlock;

                if (st == 0) st = stone;
                if (under == 0) under = dirt;
                if (top == 0) top = dirt;

                int gy = col.Height;

                for (int y = 0; y < sy; y++)
                {
                    ushort v;
                    if (y < gy - 4) v = st;
                    else if (y < gy) v = under;
                    else if (y == gy) v = gy < waterLevel ? waterBlock : top;
                    else v = y < waterLevel ? waterBlock : air;
                    
                    if(y == waterLevel && v== waterBlock && col.Temperature < .2f) v = config.Ice;
                    
                    vox[chunkSize.Flatten(x, y, z)] = v;
                }
            }
        }

        private static void SelectSurfaceMaterials(ref GeneratorConfig config, ref ChunkColumn col, ref Random rng)
        {
            ushort stone = config.Stone;
            ushort dirt = config.Dirt;

            ushort topBlock = config.Grass;
            ushort underBlock = dirt;

            switch (col.Biome)
            {
                case Biome.Desert:
                    topBlock = config.Sand;
                    underBlock = config.SandStoneRed;
                    break;
                case Biome.RedDesert:
                    topBlock = config.SandRed;
                    underBlock = config.SandStoneRed;
                    break;
                case Biome.Beach:
                    topBlock = config.Sand;
                    underBlock = config.SandStoneRedSandy;
                    break;
                case Biome.Ice:
                    topBlock = config.DirtSnowy;
                    underBlock = dirt;
                    break;
                case Biome.Snow:
                    topBlock = config.DirtSnowy;
                    underBlock = dirt;
                    break;
                case Biome.Swamp:
                    topBlock = dirt;
                    underBlock = dirt;
                    break;
                case Biome.Mountain:
                    topBlock = col.Height >= MountainSnowline
                        ? config.StoneSnowy
                        : stone;
                    underBlock = stone;
                    break;
                case Biome.HighStone:
                    topBlock = col.Height >= MountainSnowline
                        ? config.Snow
                        : config.StoneGrey;
                    underBlock = stone;
                    break;
                case Biome.GreyMountain:
                    topBlock = col.Height >= MountainSnowline
                        ? config.Snow
                        : config.StoneGrey;
                    underBlock = stone;
                    break;
                case Biome.Tundra:
                    topBlock = rng.NextFloat() < .1f ? config.Dirt :
                        config.DirtSnowy != 0 ? config.DirtSnowy : topBlock;
                    underBlock = dirt;
                    break;
            }

            col.TopBlock = topBlock;
            col.UnderBlock = underBlock;
            col.StoneBlock = stone;
        }
    }
}