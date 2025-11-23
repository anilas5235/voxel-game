using Runtime.Engine.Components;
using Runtime.Engine.Jobs;
using Runtime.Engine.Jobs.Chunk;
using Runtime.Engine.Jobs.Collider;
using Runtime.Engine.Jobs.Mesh;
using Runtime.Engine.Noise;
using Runtime.Engine.Settings;
using Runtime.Engine.Utils.Provider;
using Runtime.Engine.VoxelConfig.Data;
using UnityEngine;

namespace Runtime.Engine
{
    /// <summary>
    /// Factory/provider für Kern-Komponenten des Voxel-Engine-Subsystems (Manager, Scheduler, Pools, Profile).
    /// Stellt konfigurierte Instanzen basierend auf <see cref="VoxelEngineSettings"/> bereit.
    /// </summary>
    public class VoxelEngineProvider : Provider<VoxelEngineProvider>
    {
        /// <summary>
        /// Globale Engine-Konfiguration (Seed, Noise, Chunk-, Renderer-, Scheduler-Settings).
        /// </summary>
        public VoxelEngineSettings Settings { get; set; }

        /// <summary>
        /// Erzeugt ein neues NoiseProfil aus den aktuellen <see cref="Settings"/>.
        /// </summary>
        /// <returns>Initialisiertes NoiseProfile.</returns>
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

        /// <summary>
        /// Erstellt einen neuen <see cref="ChunkManager"/> für das Verwalten von Chunks im Speicher.
        /// </summary>
        internal ChunkManager ChunkManager() => new(Settings);

        /// <summary>
        /// Erstellt einen neuen <see cref="ChunkPool"/> zum Recyclen von Chunk-Mesh GameObjects.
        /// </summary>
        /// <param name="transform">Parent Transform für instanzierte Chunk-Objekte.</param>
        internal ChunkPool ChunkPool(Transform transform) => new(transform, Settings);

        /// <summary>
        /// Erstellt den top-level <see cref="VoxelEngineScheduler"/> welcher alle Sub-Scheduler koordiniert.
        /// </summary>
        internal VoxelEngineScheduler VoxelEngineScheduler(
            MeshBuildScheduler meshBuildScheduler,
            ChunkScheduler chunkScheduler,
            ColliderBuildScheduler colliderBuildScheduler,
            ChunkManager chunkManager,
            ChunkPool chunkPool
        ) => new(Settings, meshBuildScheduler, chunkScheduler, colliderBuildScheduler, chunkManager, chunkPool);

        /// <summary>
        /// Erstellt einen konfigurierten <see cref="ChunkScheduler"/> für Daten- / Generation-Jobs.
        /// Füllt fehlende Felder im übergebenen <paramref name="generatorConfig"/> (WaterLevel, GlobalSeed).
        /// </summary>
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

        /// <summary>
        /// Erstellt den <see cref="MeshBuildScheduler"/> zum Bauen von Chunk-Meshes.
        /// </summary>
        internal MeshBuildScheduler MeshBuildScheduler(
            ChunkManager chunkManager,
            ChunkPool chunkPool,
            VoxelRegistry voxelRegistry
        ) => new(Settings, chunkManager, chunkPool, voxelRegistry);

        /// <summary>
        /// Erstellt den <see cref="ColliderBuildScheduler"/> für Collider-Generierung.
        /// </summary>
        internal ColliderBuildScheduler ColliderBuildScheduler(
            ChunkManager chunkManager,
            ChunkPool chunkPool
        ) => new(chunkManager, chunkPool);
    }
}