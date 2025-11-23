using UnityEngine;

namespace Runtime.Engine.Settings
{
    /// <summary>
    /// ScriptableObject mit zentraler Konfiguration für Voxel Engine (Seed, Noise, Chunk, Renderer, Scheduler).
    /// </summary>
    [CreateAssetMenu(fileName = "VoxelEngineSettings", menuName = "Data/EngineSettings", order = 0)]
    public class VoxelEngineSettings : ScriptableObject
    {
        /// <summary>
        /// Globaler Seed für deterministische Welt-Generierung.
        /// </summary>
        public int Seed = 1337;
        /// <summary>
        /// Noise Parameter (Skalierung, Persistenz, etc.).
        /// </summary>
        public NoiseSettings Noise;
        /// <summary>
        /// Chunk-bezogene Einstellungen (Größen, Distanzen, Prefabs).
        /// </summary>
        public ChunkSettings Chunk;
        /// <summary>
        /// Renderer Optionen (Schatten, Materialnutzung).
        /// </summary>
        public RendererSettings Renderer;
        /// <summary>
        /// Scheduler Batch Größen.
        /// </summary>
        public SchedulerSettings Scheduler;
    }
}