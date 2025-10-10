using Runtime.Engine.Components;
using Runtime.Engine.Jobs;
using Runtime.Engine.Jobs.Chunk;
using Runtime.Engine.Jobs.Collider;
using Runtime.Engine.Jobs.Mesh;
using Runtime.Engine.Noise;
using Runtime.Engine.Settings;
using Runtime.Engine.Utils.Provider;
using UnityEngine;

namespace Runtime.Engine
{
    public class VoxelEngineProvider : Provider<VoxelEngineProvider>
    {
        public VoxelEngineSettings Settings { get; set; }

        internal NoiseProfile NoiseProfile() => new(new NoiseProfile.Settings
        {
            Height = Settings.Noise.Height,
            WaterLevel = Settings.Noise.WaterLevel,
            Seed = Settings.Noise.Seed,
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
            NoiseProfile noiseProfile
        ) => new(Settings, chunkManager, noiseProfile);

        internal MeshBuildScheduler MeshBuildScheduler(
            ChunkManager chunkManager,
            ChunkPool chunkPool
        ) => new(Settings, chunkManager, chunkPool);

        internal ColliderBuildScheduler ColliderBuildScheduler(
            ChunkManager chunkManager,
            ChunkPool chunkPool
        ) => new(chunkManager, chunkPool);
    }
}