using System;
using UnityEngine;

namespace Runtime.Engine.Settings {

    [Serializable]
    public class SchedulerSettings {
        [Tooltip("Number of chunks to process per update for meshing")]
        public int MeshingBatchSize = 4;
        
        [Tooltip("Number of chunks to process per update for streaming")]
        public int StreamingBatchSize = 8;

        [Tooltip("Number of chunks to process per update for collider generation")]
        public int ColliderBatchSize = 4;

        [Tooltip("Framerate at which the scheduler updates")]
        public int TickRate = 4;

    }

}