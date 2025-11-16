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
        private const int MountainSnowline = 215;
        public struct ChunkColumn
        {
            public int Height;
            public Biome Biome;
            public ushort TopBlock;
            public ushort UnderBlock;
            public ushort StoneBlock;
        }

        public static void PrepareChunkMaps(ref int3 chunkSize, ref NoiseProfile noiseProfile, int randomSeed,
            ref GeneratorConfig config, ref int3 chunkWordPos, NativeArray<ChunkColumn> chunkColumns,
            float biomeScale)
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

                float humidity = noise.cnoise((noiseSamplePos - 789f) * biomeScale);
                float temperature = noise.cnoise((noiseSamplePos + 543) * biomeScale);

                float rawHeight = noiseProfile.GetNoise(worldPos);

                int height = math.clamp(
                    (int)(math.lerp(1 / 3f, .85f,
                        rawHeight * math.min(1f, 1f - temperature) * math.min(1f, 1f - humidity)) * chunkSize.y),
                    1,
                    chunkSize.y - 1);

                humidity = humidity * .5f + .5f;
                temperature = temperature * .5f + .5f;

                ChunkColumn col = new()
                {
                    Height = height,
                    Biome = BiomeHelper.SelectBiome(temperature, humidity,
                        (float)height / sy, height, config.WaterLevel)
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
