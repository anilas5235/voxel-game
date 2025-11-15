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

        internal NoiseProfile NoiseProfile() => new(
            new NoiseProfile.Settings
            {
                Seed = Settings.Seed,
                Scale = Settings.Noise.Scale,
                Lacunarity = Settings.Noise.Lacunarity,
                Persistance = Settings.Noise.Persistance,
                Octaves = Settings.Noise.Octaves,
            }
        );

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
        )
        {
            // ensure generator config has water level and global seed populated from settings
            GeneratorConfig cfg = generatorConfig;
            cfg.WaterLevel = Settings.Noise.WaterLevel;
            cfg.GlobalSeed = Settings.Seed;

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