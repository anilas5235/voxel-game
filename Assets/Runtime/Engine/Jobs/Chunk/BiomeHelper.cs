using Unity.Burst;
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
        internal static Biome SelectBiome(float temp, float hum, float elev, int groundY, int waterThreshold)
        {
            // Water first
            if (groundY < waterThreshold - 3) return Biome.Ocean;

            if (groundY <= waterThreshold + 3)
            {
                // Verhindere Eis-Strände: nur bei ausreichend warmer Temperatur wird Strand gewählt
                if (temp > 0.15f)
                    return Biome.Beach;
                // sonst weiter entscheiden (kalte Küsten können Schnee/Eis/Tundra sein)
            }

            switch (elev)
            {
                // 1) Elevation zuerst
                // Hinweis: Aufgrund der Höhengenerierung liegt elev effektiv im Bereich ~0.33 bis <0.85.
                // Daher war 0.90 nie erreichbar. Wir setzen HighStone leicht unter das Maximum.
                case >= 0.84f: return Biome.HighStone;
                case >= 0.78f: return Biome.GreyMountain;
                // Schneekappen auf kühleren Bergen, sonst normales Gebirge
                case >= 0.70f when temp <= 0.12f && hum > 0.45f: return Biome.Ice;
                case >= 0.70f when temp <= 0.15f: return Biome.Snow;
                case >= 0.70f: return Biome.Mountain;
            }

            // 2) Temperatur danach
            if (temp <= 0.10f)
            {
                // sehr kalt: feucht -> Eis, sonst Schnee
                return hum > 0.45f ? Biome.Ice : Biome.Snow;
            }

            if (temp < 0.18f)
            {
                // kalt: trocken -> Tundra, sonst Ebene
                return hum < 0.38f ? Biome.Tundra : Biome.Plains;
            }

            if (temp < 0.30f)
            {
                // kühl bis mild: keine speziellen Biome außer evtl. Sumpf (kommt über Feuchte später)
                // Platzhalter: Ebene
                // (Sumpf wird etwas weiter unten über Humidity entschieden, um Reihenfolge T->H zu wahren)
                // Wir entscheiden hier noch nicht final, sondern fallen weiter.
            }

            if (temp < 0.60f)
            {
                // 3) Feuchte zuletzt: in diesem Temp-Bereich kann Sumpf entstehen
                if (hum >= 0.70f) return Biome.Swamp;
                return Biome.Plains;
            }

            if (temp < 0.82f)
            {
                // warm: feucht -> Wald, sonst Ebene
                return hum > 0.50f ? Biome.Forest : Biome.Plains;
            }

            // sehr warm/heiß (temp >= 0.82)
            if (temp >= 0.86f && hum > 0.66f) return Biome.Jungle;
            return hum switch
            {
                < 0.22f => Biome.RedDesert,
                < 0.30f => Biome.Desert,
                > 0.50f => Biome.Forest,
                _ => Biome.Plains
            };
        }

#if UNITY_EDITOR
        // Kleines Diagnose-Tool: prüft über ein Raster, welche Biome erreichbar sind.
        internal static Dictionary<Biome, int> SampleCoverage()
        {
            var counts = new Dictionary<Biome, int>();
            foreach (Biome b in Enum.GetValues(typeof(Biome))) counts[b] = 0;

            // Wir samplen einen Parameterraum; groundY und waterThreshold werden so gewählt,
            // dass sowohl tiefes Wasser, Küste als auch Inland vorkommen können.
            int water = 64;
            for (int gy = 56; gy <= 72; gy += 4) // unter Wasser, Küste, über Wasser
            {
                for (int ei = 33; ei <= 85; ei += 4) // Elevation ca. 0.33 .. 0.85
                {
                    float elev = ei / 100f;
                    for (int ti = 0; ti <= 100; ti += 10)
                    {
                        float temp = ti / 100f;
                        for (int hi = 0; hi <= 100; hi += 10)
                        {
                            float hum = hi / 100f;
                            var b = SelectBiome(temp, hum, elev, gy, water);
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