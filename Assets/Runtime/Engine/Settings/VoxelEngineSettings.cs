using UnityEngine;

namespace Runtime.Engine.Settings {

    [CreateAssetMenu(fileName = "VoxelEngineSettings", menuName = "Data/EngineSettings", order = 0)]
    public class VoxelEngineSettings : ScriptableObject {

        public NoiseSettings Noise;
        public ChunkSettings Chunk;
        public RendererSettings Renderer;
        public SchedulerSettings Scheduler;

    }

}