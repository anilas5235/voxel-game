using Runtime.Engine.Components;
using Runtime.Engine.Jobs;
using Runtime.Engine.Jobs.Chunk;
using Runtime.Engine.Jobs.Collider;
using Runtime.Engine.Jobs.Mesh;
using Runtime.Engine.Noise;
using Runtime.Engine.Settings;
using Runtime.Engine.Utils;
using Runtime.Engine.Utils.Extensions;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Runtime.Engine.World {

    public class VoxelWorld : MonoBehaviour {

        [FormerlySerializedAs("_Focus")] [SerializeField] private Transform focus;
        [FormerlySerializedAs("_Settings")] [SerializeField] private VoxelEngineSettings settings;
        
        #region API
        public Transform Focus => focus;
        public VoxelEngineSettings Settings => settings;
        public int3 FocusChunkCoord { get; private set; }
        
        
        public VoxelEngineScheduler Scheduler { get; private set; }
        public NoiseProfile NoiseProfile { get; private set; }
        public ChunkManager ChunkManager { get; private set; }

        #endregion
        
        private ChunkPool _chunkPool;
        private MeshBuildScheduler _meshBuildScheduler;
        private ChunkScheduler _chunkScheduler;
        private ColliderBuildScheduler _colliderBuildScheduler;

        private bool _isFocused;

        private byte _updateFrame = 1;

        #region Virtual

        protected virtual VoxelEngineProvider Provider() => new();
        protected virtual void WorldConfigure() { }
        protected virtual void WorldInitialize() { }
        protected virtual void WorldAwake() { }
        protected virtual void WorldStart() { }
        protected virtual void WorldUpdate() { }
        protected virtual void WorldFocusUpdate() { }
        protected virtual void WorldSchedulerUpdate() { }
        protected virtual void WorldLateUpdate() {}

        #endregion

        #region Unity

        private void Awake() {
            VoxelEngineProvider.Initialize(Provider(), provider => {
                ConfigureSettings();
                
                provider.Settings = Settings;
                WorldInitialize();
            });

            ConstructEngineComponents();
            
            FocusChunkCoord = new int3(1,1,1) * int.MinValue;

            WorldAwake();
        }

        private void Start() {
            _isFocused = focus != null;

            WorldStart();
        }

        private void Update() {
            var newFocusChunkCoord = _isFocused ? VoxelUtils.GetChunkCoords(focus.position) : int3.zero;

            if (!(newFocusChunkCoord == FocusChunkCoord).AndReduce()) {
                FocusChunkCoord = newFocusChunkCoord;
                Scheduler.FocusUpdate(FocusChunkCoord);
                WorldFocusUpdate();
            }
            
            // We can change this, so that update happens only when required
            Scheduler.SchedulerUpdate(FocusChunkCoord);

            // Schedule every 'x' frames (throttling)
            if (_updateFrame % Settings.Scheduler.TickRate == 0) {
                _updateFrame = 1;

                Scheduler.JobUpdate();

                WorldSchedulerUpdate();
            } else {
                _updateFrame++;
            }

            WorldUpdate();
        }

        private void LateUpdate() {
            Scheduler.SchedulerLateUpdate();

            WorldLateUpdate();
        }

        private void OnDestroy() {
            Scheduler.Dispose();
            ChunkManager.Dispose();
        }
        
        #endregion

        private void ConfigureSettings() {
            Settings.Chunk.LoadDistance = Settings.Chunk.DrawDistance + 2;
            Settings.Chunk.UpdateDistance = math.max(Settings.Chunk.DrawDistance - 2, 2);

            // TODO : these need to be dynamic or exposed ?
            Settings.Scheduler.MeshingBatchSize = 4;
            Settings.Scheduler.StreamingBatchSize = 8;
            Settings.Scheduler.ColliderBatchSize = 4;

            WorldConfigure();
        }
        
        private void ConstructEngineComponents() {
            NoiseProfile = VoxelEngineProvider.Current.NoiseProfile();
            ChunkManager = VoxelEngineProvider.Current.ChunkManager();

            _chunkPool = VoxelEngineProvider.Current.ChunkPool(transform);

            _meshBuildScheduler = VoxelEngineProvider.Current.MeshBuildScheduler(
                ChunkManager, 
                _chunkPool
            );
            
            _chunkScheduler = VoxelEngineProvider.Current.ChunkDataScheduler(
                ChunkManager,
                NoiseProfile
            );

            _colliderBuildScheduler = VoxelEngineProvider.Current.ColliderBuildScheduler(
                ChunkManager,
                _chunkPool
            );

            Scheduler = VoxelEngineProvider.Current.VoxelEngineScheduler(
                _meshBuildScheduler, 
                _chunkScheduler,
                _colliderBuildScheduler,
                ChunkManager,
                _chunkPool
            );


        }

    }

}