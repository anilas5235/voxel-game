// csharp

using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;
using static Unity.Mathematics.noise;

namespace Runtime.Engine.Noise
{
    [BurstCompile]
    public readonly struct NoiseProfile
    {
        private readonly Settings _settings;

        public NoiseProfile(Settings settings)
        {
            // Kopiere settings, bevor Felder verändert werden (vermeidet Mutation eines readonly-Felds)
            Settings s = settings;
            if (s.Scale <= 0f)
            {
                s.Scale = 0.0001f;
            }

            _settings = s;
        }

        public float GetNoise(float2 position)
        {
            return ComputeNoise(position) * 0.5f + 0.5f;
        }

        private float ComputeNoise(float2 position)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseSum = 0f;

            // Seed als Float hinzufügen
            float2 samplePos = (position + _settings.Seed) / _settings.Scale;

            for (int i = 0; i < _settings.Octaves; i++)
            {
                float n = cnoise(samplePos * frequency);
                noiseSum += n * amplitude;

                amplitude *= _settings.Persistance;
                frequency *= _settings.Lacunarity;
            }

            return noiseSum;
        }

        [Serializable]
        public struct Settings
        {
            public int Seed;
            public float Scale;
            public float Persistance;
            public float Lacunarity;
            public int Octaves;
        }
    }

    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    public struct NoiseValue
    {
        public float Humidity;
        public float Temperature;
        public float Height;
    }
}