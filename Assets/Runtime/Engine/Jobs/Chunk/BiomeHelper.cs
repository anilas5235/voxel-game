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

            if (temp > 0.75f && hum < 0.25f) return Biome.Desert;
            if (temp > 0.8f && hum > 0.65f) return Biome.Jungle;
            if (temp > 0.5f && hum > 0.5f) return Biome.Forest;
            if (temp < 0.2f && hum < 0.4f) return Biome.Tundra;
            if (temp < 0.3f && elev > 0.7f) return Biome.Mountain;
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