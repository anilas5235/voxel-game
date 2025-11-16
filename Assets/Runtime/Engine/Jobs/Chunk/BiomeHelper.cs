using Unity.Burst;
using Unity.Mathematics;
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
#endif

namespace Runtime.Engine.Jobs.Chunk
{
    [BurstCompile]
    public static class BiomeHelper
    {
        [BurstCompile]
        internal static Biome SelectBiome(float temp, float hum, float elev, int groundY, int waterThreshold,
            float continentality, float mountainMask)
        {
            // --- 0) Wasser & Strand (harte Extrema) ---
            if (groundY < waterThreshold - 3) return Biome.Ocean;
            if (groundY <= waterThreshold + 3 && temp > 0.15f) return Biome.Beach;

            // --- 1) Höhenextreme / Gebirge ---
            float elevEffective = elev + mountainMask * 0.04f;
            if (elevEffective > 1f) elevEffective = 1f;

            if (elevEffective >= 0.90f) return Biome.HighStone;
            if (elevEffective >= 0.84f) return Biome.GreyMountain;
            if (elevEffective >= 0.76f)
            {
                if (temp <= 0.12f && hum > 0.45f) return Biome.Ice;
                if (temp <= 0.18f) return Biome.Snow;
                return Biome.Mountain;
            }

            // --- 2) Klimaextreme (sehr kalt / sehr heiß) ---
            bool nearWater = groundY <= waterThreshold + 10;
            float var = Variation(temp, hum, elev, continentality);

            // 2a) Sehr kalt (temp <= 0.15)
            if (temp <= 0.15f)
            {
                if (hum > 0.70f) return Biome.Ice;     // extrem kalt + sehr feucht
                if (hum > 0.50f) return Biome.Snow;    // sehr kalt + feucht
                if (hum < 0.30f) return Biome.Tundra;  // sehr kalt + trocken
                // Übergangsbereich
                return var > 0.5f ? Biome.Snow : Biome.Tundra;
            }

            // 2b) Sehr heiß (temp >= 0.85)
            if (temp >= 0.8f)
            {
                if (hum > 0.70f) return Biome.Jungle; // heiß + sehr feucht

                // sehr trocken & weit im Binnenland -> rote Wüste
                if (hum < 0.2f) return Biome.RedDesert;

                if (hum < 0.45f) return Biome.Desert; // heiß + trocken

                // moderat feucht
                return hum >= 0.50f ? Biome.Forest : Biome.Plains;
            }

            // --- 3) gemäßigt-kalte Zonen (0.15 < temp < 0.40) ---
            if (temp < 0.40f)
            {
                // sehr feucht in Wassernähe -> Sumpf
                if (hum > 0.80f && nearWater) return Biome.Swamp;

                if (hum > 0.60f) return Biome.Forest; // feucht -> Wald

                if (hum < 0.4f) return Biome.Tundra; // trocken + kühl

                // Übergangsband: leichte Mischung Forest/Plains
                return var > 0.6f ? Biome.Forest : Biome.Plains;
            }

            // --- 4) gemäßigt-warme Zonen (0.40 <= temp < 0.70) ---
            if (temp < 0.70f )
            {
                if (hum > 0.7f && nearWater) return Biome.Swamp;

                if (hum > 0.62f) return Biome.Forest; // feucht -> Wald

                // leicht trocken -> Plains mit etwas Forest
                if (hum < 0.2f && temp > .6f) return Biome.Desert; 

                // trocken, aber nicht heiß genug für echte Wüste
                return var > 0.65f ? Biome.Forest : Biome.Plains;
            }

            // --- 5) warme Zonen (0.70 <= temp < 0.8) ---
            {
                if (hum > 0.76f && nearWater) return Biome.Swamp;

                // Trockene, warme Binnenregionen -> Wüste
                if (continentality >= 0.40f)
                {
                    switch (hum)
                    {
                        case < .2f:
                            return Biome.RedDesert;
                        case < .4f:
                            return Biome.Desert;
                    }
                }

                // Übergangsband
                return var > 0.55f ? Biome.Forest : Biome.Plains;
            }
        }

        // Deterministische, Burst-freundliche Variation 0..1 basierend auf den Eingangsparametern
        private static float Variation(float a, float b, float c, float d)
        {
            float t = a * 12.9898f + b * 78.233f + c * 37.719f + d * 11.123f;
            float s = math.sin(t) * 43758.5453f;
            return math.frac(s);
        }

#if UNITY_EDITOR
        // Kleines Diagnose-Tool: prüft über ein Raster, welche Biome erreichbar sind.
        internal static Dictionary<Biome, int> SampleCoverage()
        {
            var counts = new Dictionary<Biome, int>();
            foreach (Biome b in Enum.GetValues(typeof(Biome))) counts[b] = 0;

            int water = 64;
            for (int gy = 56; gy <= 72; gy += 4)
            {
                for (int ei = 33; ei <= 85; ei += 4)
                {
                    float elev = ei / 100f;
                    for (int ti = 0; ti <= 100; ti += 10)
                    {
                        float temp = ti / 100f;
                        for (int hi = 0; hi <= 100; hi += 10)
                        {
                            float hum = hi / 100f;
                            var b = SelectBiome(temp, hum, elev, gy, water, 1f, 0f);
                            counts[b]++;
                        }
                    }
                }
            }

            return counts;
        }
#endif
    }
}