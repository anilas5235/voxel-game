using Runtime.Engine.Components;
using Runtime.Engine.Jobs;
using Runtime.Engine.Jobs.Chunk;
using Runtime.Engine.Jobs.Collider;
using Runtime.Engine.Jobs.Mesh;
using Runtime.Engine.Noise;
using Runtime.Engine.Settings;
using Runtime.Engine.Utils.Provider;
using Runtime.Engine.Voxels.Data;
using UnityEngine;
using System.Text;

namespace Runtime.Engine
{
    public class VoxelEngineProvider : Provider<VoxelEngineProvider>
    {
        public VoxelEngineSettings Settings { get; set; }

        // Compute a stable 32-bit hash from an alphanumeric seed string (FNV-1a)
        private static uint HashSeedString(string s)
        {
            unchecked
            {
                const uint fnvOffset = 2166136261u;
                const uint fnvPrime = 16777619u;
                uint hash = fnvOffset;
                if (string.IsNullOrEmpty(s)) return 0u;
                byte[] data = Encoding.UTF8.GetBytes(s);
                for (int i = 0; i < data.Length; i++)
                {
                    hash ^= data[i];
                    hash *= fnvPrime;
                }
                return hash == 0 ? 1u : hash;
            }
        }

        private uint ComputeSeed()
        {
            // If settings or noise settings are missing, fall back to a runtime-random seed instead of a fixed debug seed
            if (Settings == null || Settings.Noise == null)
            {
                return (uint)UnityEngine.Random.Range(1, int.MaxValue);
            }

            if (!string.IsNullOrEmpty(Settings.Noise.SeedString))
            {
                return HashSeedString(Settings.Noise.SeedString);
            }


            // fallback random if nothing set
            return (uint)UnityEngine.Random.Range(1, int.MaxValue);
        }

        internal NoiseProfile NoiseProfile() => new(new NoiseProfile.Settings
        {
            Height = Settings.Noise.Height,
            WaterLevel = Settings.Noise.WaterLevel,
            Seed = (int)ComputeSeed(),
            Scale = Settings.Noise.Scale,
            Lacunarity = Settings.Noise.Lacunarity,
            Persistance = Settings.Noise.Persistance,
            Octaves = Settings.Noise.Octaves,
        });

        internal ChunkManager ChunkManager() => new(Settings);

        internal ChunkPool ChunkPool(Transform transform) => new(transform, Settings);

        internal VoxelEngineScheduler VoxelEngineScheduler(
            MeshBuildScheduler meshBuildScheduler,
            ChunkScheduler chunkScheduler,
            ColliderBuildScheduler colliderBuildScheduler,
            ChunkManager chunkManager,
            ChunkPool chunkPool
        ) => new(Settings, meshBuildScheduler, chunkScheduler, colliderBuildScheduler, chunkManager, chunkPool);

        internal ChunkScheduler ChunkDataScheduler(
            ChunkManager chunkManager,
            NoiseProfile noiseProfile,
            GeneratorConfig generatorConfig
        ) {
            // ensure generator config has water level and global seed populated from settings
            GeneratorConfig cfg = generatorConfig;
            cfg.WaterLevel = Settings.Noise.WaterLevel;
            // Preserve manually-provided seed in generatorConfig if non-zero.
            if (cfg.GlobalSeed == 0)
            {
                cfg.GlobalSeed = ComputeSeed();
            }
            return new ChunkScheduler(Settings, chunkManager, noiseProfile, cfg);
        }
        

        internal MeshBuildScheduler MeshBuildScheduler(
            ChunkManager chunkManager,
            ChunkPool chunkPool,
            VoxelRegistry voxelRegistry
        ) => new(Settings, chunkManager, chunkPool, voxelRegistry);

        internal ColliderBuildScheduler ColliderBuildScheduler(
            ChunkManager chunkManager,
            ChunkPool chunkPool
        ) => new(chunkManager, chunkPool);
    }
}