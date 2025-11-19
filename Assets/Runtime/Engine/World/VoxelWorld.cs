using Runtime.Engine.Components;
using Runtime.Engine.Jobs;
using Runtime.Engine.Jobs.Chunk;
using Runtime.Engine.Jobs.Collider;
using Runtime.Engine.Jobs.Mesh;
using Runtime.Engine.Noise;
using Runtime.Engine.Settings;
using Runtime.Engine.Utils;
using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Mathematics;
using UnityEngine;
using Utils;

namespace Runtime.Engine.World
{
    public class VoxelWorld : Singleton<VoxelWorld>
    {
        [SerializeField] private Transform focus;

        [SerializeField] private VoxelEngineSettings settings;

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

        private static VoxelEngineProvider Provider() => new();

        #region Unity

        protected override void Awake()
        {
            base.Awake();
            VoxelEngineProvider.Initialize(Provider(), provider =>
            {
                ConfigureSettings();

                provider.Settings = Settings;
            });

            ConstructEngineComponents();

            FocusChunkCoord = new int3(1, 1, 1) * int.MinValue;
        }

        private void Start()
        {
            _isFocused = focus;
        }

        private void Update()
        {
            int3 newFocusChunkCoord = _isFocused ? VoxelUtils.GetChunkCoords(focus.position) : int3.zero;

            if (!(newFocusChunkCoord == FocusChunkCoord).AndReduce())
            {
                FocusChunkCoord = newFocusChunkCoord;
                Scheduler.FocusUpdate(FocusChunkCoord);
            }

            Scheduler.ScheduleUpdate(FocusChunkCoord);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Scheduler.Dispose();
            ChunkManager.Dispose();
        }

        #endregion

        public ushort GetVoxel(Vector3Int position) => ChunkManager.GetVoxel(position);

        public bool SetVoxel(ushort voxelId, Vector3Int position, bool remesh = true) =>
            ChunkManager.SetVoxel(voxelId, position, remesh);

        private void ConfigureSettings()
        {
            Settings.Chunk.LoadDistance = Settings.Chunk.DrawDistance + 2;
            Settings.Chunk.UpdateDistance = math.max(Settings.Chunk.DrawDistance - 2, 2);
        }

        private void ConstructEngineComponents()
        {
            NoiseProfile = VoxelEngineProvider.Current.NoiseProfile();
            ChunkManager = VoxelEngineProvider.Current.ChunkManager();

            _chunkPool = VoxelEngineProvider.Current.ChunkPool(transform);

            _meshBuildScheduler = VoxelEngineProvider.Current.MeshBuildScheduler(
                ChunkManager,
                _chunkPool,
                VoxelDataImporter.Instance.VoxelRegistry
            );

            _chunkScheduler = VoxelEngineProvider.Current.ChunkDataScheduler(
                ChunkManager,
                NoiseProfile,
                VoxelDataImporter.Instance.CreateConfig()
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