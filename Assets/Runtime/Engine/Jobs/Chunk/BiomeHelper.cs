using Unity.Burst;

namespace Runtime.Engine.Jobs.Chunk
{
    [BurstCompile]
    public static class BiomeHelper
    {
        [BurstCompile]
        internal static Biome SelectBiome(float temp, float hum, float elev, int groundY, int waterThreshold)
        {
            if (groundY < waterThreshold - 2) return Biome.Ocean;

            // enforce clearer separation between hot and cold by requiring stronger deviations from mid-temp
            // require more extreme temps for hot biomes
            if (temp > 0.82f && hum < 0.30f && temp - 0.5f > 0.25f) return Biome.Desert;
            if (temp > 0.86f && hum > 0.66f && temp - 0.5f > 0.30f) return Biome.Jungle;
            if (temp > 0.60f && hum > 0.5f && temp - 0.5f > 0.08f) return Biome.Forest;

            // High, rocky peaks: prefer when elevation is very high
            // prefer HighStone and GreyMountain at very high elevation and moderately cool temps
            if (elev > 0.90f && temp < 0.62f) return Biome.HighStone;
            if (elev > 0.78f && temp < 0.56f) return Biome.GreyMountain;

            // cold variants
            if (temp < 0.18f && hum < 0.38f && 0.5f - temp > 0.25f) return Biome.Tundra;
            if (temp < 0.32f && elev > 0.7f) return Biome.Mountain;
            if (hum > 0.7f && temp > 0.3f && temp < 0.6f) return Biome.Swamp;
            if (temp < 0.15f) return Biome.Snow;
            return Biome.Plains;
        }

        public static Biome SelectBiome(NoiseData noiseDataEntry, int groundY, int waterThreshold)
        {
            return SelectBiome(noiseDataEntry.temperature, noiseDataEntry.humidity, noiseDataEntry.height, groundY, waterThreshold);
        }
    }
}