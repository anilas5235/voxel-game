using System;
using UnityEngine;

namespace Runtime.Engine.Settings
{
    /// <summary>
    /// Konfiguration für Update-Batchgrößen einzelner Scheduler (Meshing, Streaming, Collider).
    /// Höhere Werte erhöhen Parallelisierung pro Frame, können aber zu Framedrops führen.
    /// </summary>
    [Serializable]
    public class SchedulerSettings
    {
        /// <summary>
        /// Anzahl Chunks pro Update für Meshing.
        /// </summary>
        [Tooltip("Number of chunks to process per update for meshing")]
        public int MeshingBatchSize = 4;

        /// <summary>
        /// Anzahl Chunks pro Update für Daten Streaming / Generierung.
        /// </summary>
        [Tooltip("Number of chunks to process per update for streaming")]
        public int StreamingBatchSize = 8;

        /// <summary>
        /// Anzahl Chunks pro Update für Collider-Erstellung.
        /// </summary>
        [Tooltip("Number of chunks to process per update for collider generation")]
        public int ColliderBatchSize = 4;
    }
}