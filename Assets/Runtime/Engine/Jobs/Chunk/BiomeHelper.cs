using Unity.Burst;

namespace Runtime.Engine.Jobs.Chunk
{
    [BurstCompile]
    public static class BiomeHelper
    {
        [BurstCompile]
        internal static Biome SelectBiome(float temp, float hum, float elev, int groundY, int waterThreshold)
        {
            // --- NEUE, KORREKTE BIOME-LOGIK ---

            // 1. OZEAN: Alles, was tief genug unter Wasser ist (z.B. mehr als 3 Blöcke tief)
            if (groundY < waterThreshold - 3) return Biome.Ocean;

            // 2. STRAND: Alles, was knapp unter, auf, oder knapp über dem Wasser ist.
            //    (z.B. von 3 Blöcke tief bis 3 Blöcke hoch)
            //    Dies fängt die "Rampe" ab, die deine Glättung erzeugt.
            if (groundY <= waterThreshold + 3)
            {
                // Optional: Verhindere Eis-Strände
                if (temp > 0.15f) 
                    return Biome.Beach;
                
                // Wenn es zu kalt für Strand ist, wird es Eis oder Schnee
            }

            // --- Ab hier beginnt normales Land (groundY > waterThreshold + 3) ---

            // Ice biome: very cold and wet or very low temp
            if (temp < 0.12f && hum > 0.45f) return Biome.Ice;
            if (temp < 0.10f) return Biome.Snow;

            // Red desert variant: hot and very dry, prefer at lower humidity
            if (temp > 0.82f && hum < 0.22f && temp - 0.5f > 0.25f) return Biome.RedDesert;
            if (temp > 0.82f && hum < 0.30f && temp - 0.5f > 0.25f) return Biome.Desert;

            if (temp > 0.86f && hum > 0.66f && temp - 0.5f > 0.30f) return Biome.Jungle;
            if (temp > 0.60f && hum > 0.5f && temp - 0.5f > 0.08f) return Biome.Forest;

            // High, rocky peaks: prefer when elevation is very high
            if (elev > 0.90f && temp < 0.62f) return Biome.HighStone;
            if (elev > 0.78f && temp < 0.56f) return Biome.GreyMountain;

            // cold variants
            if (temp < 0.18f && hum < 0.38f && 0.5f - temp > 0.25f) return Biome.Tundra;
            if (temp < 0.32f && elev > 0.7f) return Biome.Mountain;
            if (hum > 0.7f && temp > 0.3f && temp < 0.6f) return Biome.Swamp;

            return Biome.Plains;
        }

        public static Biome SelectBiome(NoiseData noiseDataEntry, int groundY, int waterThreshold)
        {
            return SelectBiome(noiseDataEntry.temperature, noiseDataEntry.humidity, noiseDataEntry.height, groundY, waterThreshold);
        }
    }
}