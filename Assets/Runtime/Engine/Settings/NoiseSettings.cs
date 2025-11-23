using UnityEngine;

namespace Runtime.Engine.Settings
{
    /// <summary>
    /// Konfiguration für Welt-Noise und Höhenniveaus (Wasser). Wird zur Generierung genutzt.
    /// </summary>
    [CreateAssetMenu(fileName = "NoiseSettings2D", menuName = "Data/NoiseSettings", order = 0)]
    public class NoiseSettings : ScriptableObject
    {
        /// <summary>
        /// Wasseroberflächen-Level in Weltkoordinaten (Y).
        /// </summary>
        public int WaterLevel = 96;

        // Alphanumeric seed: if non-empty, this will be used (hashed) as the world seed.
        // Numeric seed was removed to simplify configuration; use SeedString for reproducible worlds.
        /// <summary>
        /// Skalierungsfaktor für Basis-Noise.
        /// </summary>
        public float Scale = 256;
        /// <summary>
        /// Persistenz (Amplitude Reduktion pro Oktave).
        /// </summary>
        public float Persistance = 0.5f;
        /// <summary>
        /// Lacunarity (Frequenzsteigerung pro Oktave).
        /// </summary>
        public float Lacunarity = 2f;
        /// <summary>
        /// Anzahl der Oktaven.
        /// </summary>
        public int Octaves = 4;
    }
}