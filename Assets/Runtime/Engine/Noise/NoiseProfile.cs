using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;

namespace Runtime.Engine.Noise
{
    /// <summary>
    /// ! Height is shifted by h - h/2 to make 0 actual 0
    /// </summary>
    [BurstCompile]
    public readonly struct NoiseProfile
    {
        private readonly Settings _settings;

        private readonly int _halfHeight;
        private readonly int _waterLevel;

        public NoiseValue GetNoise(int3 position) => new()
        {
            Position = position,
            WaterLevel = _waterLevel,
            Height = math.clamp((int)math.round(ComputeNoise(position) * _halfHeight), -_halfHeight, _halfHeight),
        };

        public NoiseProfile(Settings settings)
        {
            _settings = settings;

            if (_settings.Scale <= 0)
            {
                _settings.Scale = 0.0001f;
            }

            _halfHeight = _settings.Height / 2;
            _waterLevel = _settings.WaterLevel - _halfHeight;
        }

        public float ComputeNoise(float3 position)
        {
            float amplitude = 1;
            float frequency = 1;
            float height = 0;

            float sampleX = (position.x + _settings.Seed) / _settings.Scale;
            float sampleZ = (position.z + _settings.Seed) / _settings.Scale;

            for (int i = 0; i < _settings.Octaves; i++)
            {
                float noise = Unity.Mathematics.noise.cnoise(new float2(sampleX * frequency, sampleZ * frequency));

                height += noise * amplitude;

                amplitude *= _settings.Persistance;
                frequency *= _settings.Lacunarity;
            }

            return height;
        }

        public struct Settings
        {
            public int Height;
            public int WaterLevel;
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
        public int3 Position;
        public int WaterLevel;
        public int Height;
    }
}