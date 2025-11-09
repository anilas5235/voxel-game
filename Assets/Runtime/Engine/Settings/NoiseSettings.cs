using UnityEngine;

namespace Runtime.Engine.Settings
{
    [CreateAssetMenu(fileName = "NoiseSettings2D", menuName = "Data/NoiseSettings", order = 0)]
    public class NoiseSettings : ScriptableObject
    {
        public int WaterLevel = 96;

        // Alphanumeric seed: if non-empty, this will be used (hashed) as the world seed.
        // Numeric seed was removed to simplify configuration; use SeedString for reproducible worlds.
        public float Scale = 256;
        public float Persistance = 0.5f;
        public float Lacunarity = 2f;
        public int Octaves = 4;
    }
}