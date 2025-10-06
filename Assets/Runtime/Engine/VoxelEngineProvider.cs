using Runtime.Engine.Components;
using Runtime.Engine.Jobs;
using Runtime.Engine.Jobs.Chunk;
using Runtime.Engine.Jobs.Collider;
using Runtime.Engine.Jobs.Mesh;
using Runtime.Engine.Noise;
using Runtime.Engine.Settings;
using Runtime.Engine.Utils.Provider;
using UnityEngine;

namespace Runtime.Engine {

    public class VoxelEngineProvider : Provider<VoxelEngineProvider> {

        public VoxelEngineSettings Settings { get; set; }

        internal virtual NoiseProfile NoiseProfile() => new (new NoiseProfile.Settings {
            Height = Settings.Noise.Height,
            WaterLevel = Settings.Noise.WaterLevel,
            Seed = Settings.Noise.Seed,
            Scale = Settings.Noise.Scale,
            Lacunarity = Settings.Noise.Lacunarity,
            Persistance = Settings.Noise.Persistance,
            Octaves = Settings.Noise.Octaves,
        });

        internal virtual ChunkManager ChunkManager() => new(Settings);

        internal virtual ChunkPool ChunkPool(Transform transform) => new (transform, Settings);

        internal virtual VoxelEngineScheduler VoxelEngineScheduler(
            MeshBuildScheduler meshBuildScheduler,
            ChunkScheduler ChunkScheduler,
            ColliderBuildScheduler colliderBuildScheduler,
            ChunkManager ChunkManager,
            ChunkPool chunkPool
        ) => new(Settings, meshBuildScheduler, ChunkScheduler, colliderBuildScheduler, ChunkManager, chunkPool);

        internal virtual ChunkScheduler ChunkDataScheduler(
            ChunkManager ChunkManager,
            NoiseProfile noiseProfile
        ) => new(Settings, ChunkManager, noiseProfile);

        internal virtual MeshBuildScheduler MeshBuildScheduler(
            ChunkManager ChunkManager,
            ChunkPool chunkPool
        ) => new(Settings, ChunkManager, chunkPool);

        internal virtual ColliderBuildScheduler ColliderBuildScheduler(
            ChunkManager chunkManager,
            ChunkPool chunkPool
        ) => new(chunkManager, chunkPool);

    }

}