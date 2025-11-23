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

namespace Runtime.Engine.World
{
    /// <summary>
    /// Top-level world controller that wires together chunk generation, meshing and colliders
    /// and exposes a simple API for querying and modifying voxels around a focus transform.
    /// </summary>
    public class VoxelWorld : Singleton<VoxelWorld>
    {
        [SerializeField] private Transform focus;

        [SerializeField] private VoxelEngineSettings settings;

        #region API

        /// <summary>
        /// Gets the current focus transform that drives which chunks are loaded and updated.
        /// </summary>
        public Transform Focus => focus;

        /// <summary>
        /// Gets the voxel engine settings used to configure chunk sizes and distances.
        /// </summary>
        public VoxelEngineSettings Settings => settings;

        /// <summary>
        /// Gets the chunk coordinates of the current focus position.
        /// </summary>
        public int3 FocusChunkCoord { get; private set; }

        /// <summary>
        /// Gets the central scheduler that coordinates chunk data, mesh and collider jobs.
        /// </summary>
        public VoxelEngineScheduler Scheduler { get; private set; }

        /// <summary>
        /// Gets the noise profile used for procedural terrain generation.
        /// </summary>
        public NoiseProfile NoiseProfile { get; private set; }

        /// <summary>
        /// Gets the chunk manager that stores and accesses chunk data.
        /// </summary>
        public ChunkManager ChunkManager { get; private set; }

        #endregion

        private ChunkPool _chunkPool;
        private MeshBuildScheduler _meshBuildScheduler;
        private ChunkScheduler _chunkScheduler;
        private ColliderBuildScheduler _colliderBuildScheduler;

        private bool _isFocused;

        private static VoxelEngineProvider Provider() => new();

        #region Unity

        /// <summary>
        /// Initializes the singleton, configures engine settings via the provider
        /// and constructs all core engine components.
        /// </summary>
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

        /// <summary>
        /// Initializes focus state once all objects are created.
        /// </summary>
        private void Start()
        {
            _isFocused = focus;
        }

        /// <summary>
        /// Updates the focus chunk coordinate and lets the scheduler perform focus and
        /// regular scheduling logic every frame.
        /// </summary>
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

        /// <summary>
        /// Cleans up engine components and disposes schedulers on destruction.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            Scheduler.Dispose();
            ChunkManager.Dispose();
        }

        #endregion

        /// <summary>
        /// Gets the voxel ID at the given world voxel position.
        /// </summary>
        /// <param name="position">World voxel position.</param>
        /// <returns>Voxel ID at the given position.</returns>
        public ushort GetVoxel(Vector3Int position) => ChunkManager.GetVoxel(position);

        /// <summary>
        /// Sets the voxel ID at a given world voxel position and optionally triggers a remesh.
        /// </summary>
        /// <param name="voxelId">Voxel ID to set.</param>
        /// <param name="position">World voxel position.</param>
        /// <param name="remesh">If true, the affected chunks will be re-meshed.</param>
        /// <returns><c>true</c> if the voxel could be set; otherwise, <c>false</c>.</returns>
        public bool SetVoxel(ushort voxelId, Vector3Int position, bool remesh = true) =>
            ChunkManager.SetVoxel(voxelId, position, remesh);

        /// <summary>
        /// Adjusts derived chunk settings such as load and update distance based on the
        /// configured draw distance.
        /// </summary>
        private void ConfigureSettings()
        {
            Settings.Chunk.LoadDistance = Settings.Chunk.DrawDistance + 2;
            Settings.Chunk.UpdateDistance = math.max(Settings.Chunk.DrawDistance - 2, 2);
        }

        /// <summary>
        /// Constructs all engine components (noise profile, chunk manager, pools and schedulers)
        /// via the <see cref="VoxelEngineProvider"/>.
        /// </summary>
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