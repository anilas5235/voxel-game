using System;
using UnityEngine;

namespace Runtime.Engine.Settings
{
    /// <summary>
    /// Configuration for per-update batch sizes of individual schedulers (meshing, streaming, collider).
    /// Higher values increase parallel work per frame but may cause frame spikes.
    /// </summary>
    [Serializable]
    public class SchedulerSettings
    {
        /// <summary>
        /// Number of chunks processed per update for meshing.
        /// </summary>
        [Tooltip("Number of chunks to process per update for meshing")]
        public int MeshingBatchSize = 4;
        /// <summary>
        /// Number of chunks processed per update for data streaming/generation.
        /// </summary>
        [Tooltip("Number of chunks to process per update for streaming")]
        public int StreamingBatchSize = 8;
        /// <summary>
        /// Number of chunks processed per update for collider generation.
        /// </summary>
        [Tooltip("Number of chunks to process per update for collider generation")]
        public int ColliderBatchSize = 4;
    }
}