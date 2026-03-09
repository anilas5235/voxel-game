using System.Collections.Generic;
using Runtime.Engine.Components;
using Runtime.Engine.Utils;
using Runtime.Engine.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Behaviour
{
    /// <summary>
    /// GPU-based voxel renderer using compute shaders for mesh generation and indirect rendering.
    /// </summary>
    public class VoxelWorldRenderer : MonoBehaviour
    {
        [Header("Materials")] [SerializeField] private Material solidMaterial;

        [Header("Compute Shaders")] [SerializeField]
        private ComputeShader meshBuilderCompute;

        // Kernel IDs
        private int _kernelRebuild;
        private int _kernelCullChunks;
        private int _kernelCullPartitions;
        private int _kernelBuildArgs;

        // GPU Buffers
        private ComputeBuffer _voxelDataUploadBuffer; // uint[32^3 × MaxDirtyUploadsPerFrame]
        private ComputeBuffer _partitionMetadataBuffer; // PartitionMetadata[maxPartitions]
        private ComputeBuffer _chunkMetadataBuffer; // ChunkMetadata[maxChunks]
        private ComputeBuffer _solidPointsBuffer; // PointData[maxPartitions × MaxPointsPerPartition]
        private ComputeBuffer _visibleChunksBuffer; // AppendStructuredBuffer<uint>
        private ComputeBuffer _visiblePartitionsBuffer; // AppendStructuredBuffer<uint>
        private ComputeBuffer _indirectArgsBuffer; // uint[5]
        private ComputeBuffer _counterBuffer; // uint[4] debug counters, must be Raw/IndirectArguments for CopyCount

        // State
        private int _maxActivePartitions;
        private int _maxActiveChunks;
        private int _maxPointsPerPartition;
        private ChunkManager _chunkManager;
        private Plane[] _frustumPlanes = new Plane[6];
        private Vector4[] _frustumPlanesData = new Vector4[6];
        private Dictionary<int3, int> _partitionIndexMap = new();
        private int _nextPartitionIndex;

        // Debug
        [Header("Debug")] [SerializeField] private bool debugBypassCulling = true;
        [SerializeField] private bool debugBootstrapDirtyPartition = true;
        private readonly uint[] _debugCounterReadback = new uint[4];
        private readonly uint[] _debugIndirectArgs = new uint[5];
        private int _debugFrameCounter;

        // Keep a safety margin below Unity's hard 2 GB ComputeBuffer limit.
        private const long SolidPointsBufferBudgetBytes = 1L << 30; // 1 GiB

        private void Awake()
        {
            // Use importer material as default to ensure required global buffers (e.g. quad_buffer) are bound.
            if (solidMaterial == null && Runtime.Engine.VoxelConfig.Data.VoxelDataImporter.Instance != null)
            {
                solidMaterial = Runtime.Engine.VoxelConfig.Data.VoxelDataImporter.Instance.voxelSolidMaterial;
            }

            // Get ChunkManager reference from VoxelWorld
            _chunkManager = VoxelWorld.Instance?.ChunkManager;
            if (_chunkManager == null)
            {
                Debug.LogError("VoxelWorldRenderer: ChunkManager not found!");
                enabled = false;
                return;
            }

            // Calculate max capacities based on draw distance
            int drawDist = ResolveDrawDistance();
            _maxActiveChunks = VoxelRenderConstants.MaxActiveChunks(drawDist);
            _maxActivePartitions = VoxelRenderConstants.MaxActivePartitions(drawDist);
            _maxPointsPerPartition = ResolveMaxPointsPerPartition(_maxActivePartitions);

            InitializeBuffers();
            InitializeKernels();

            // Hook GPU rebuild trigger
            _chunkManager.OnGpuRebuildReady += OnGpuRebuildReady;

            if (debugBootstrapDirtyPartition)
            {
                int3 bootstrapPartition = int3.zero;
                Camera mainCam = Camera.main;
                if (mainCam != null)
                    bootstrapPartition = VoxelUtils.GetPartitionCoords(mainCam.transform.position);

                _chunkManager.MarkPartitionDirty(bootstrapPartition);
                OnGpuRebuildReady();
            }

            if (solidMaterial == null)
            {
                Debug.LogError("VoxelWorldRenderer: Solid material is null. Assign a material based on Custom/VoxelShader.");
                enabled = false;
                return;
            }

            if (Runtime.Engine.VoxelConfig.Data.VoxelDataImporter.Instance != null &&
                solidMaterial != Runtime.Engine.VoxelConfig.Data.VoxelDataImporter.Instance.voxelSolidMaterial)
            {
                Debug.LogWarning("VoxelWorldRenderer: Using a different solid material than VoxelDataImporter.voxelSolidMaterial. Ensure quad_buffer is bound on this material.");
            }
        }

        private void InitializeBuffers()
        {
            int uploadSlots = VoxelRenderConstants.MaxDirtyUploadsPerFrame;
            int voxelsPerPartition = 32 * 32 * 32;

            _voxelDataUploadBuffer = new ComputeBuffer(
                voxelsPerPartition * uploadSlots,
                sizeof(uint));

            _partitionMetadataBuffer = new ComputeBuffer(
                _maxActivePartitions,
                VoxelRenderConstants.PartitionMetadataStride);

            _chunkMetadataBuffer = new ComputeBuffer(
                _maxActiveChunks,
                VoxelRenderConstants.ChunkMetadataStride);

            int solidPointCount = _maxActivePartitions * _maxPointsPerPartition;
            _solidPointsBuffer = new ComputeBuffer(
                solidPointCount,
                VoxelRenderConstants.PointDataStride,
                ComputeBufferType.Append);

            _visibleChunksBuffer = new ComputeBuffer(
                _maxActiveChunks,
                sizeof(uint),
                ComputeBufferType.Append);

            _visiblePartitionsBuffer = new ComputeBuffer(
                _maxActivePartitions,
                sizeof(uint),
                ComputeBufferType.Append);

            _indirectArgsBuffer = new ComputeBuffer(
                5,
                sizeof(uint),
                ComputeBufferType.IndirectArguments);

            // DX11: CopyCount destination must be Raw or IndirectArguments.
            _counterBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.Raw);

            long solidBytes = (long)solidPointCount * VoxelRenderConstants.PointDataStride;
            Debug.Log(
                $"VoxelWorldRenderer: Initialized buffers for {_maxActivePartitions} partitions, {_maxActiveChunks} chunks, {_maxPointsPerPartition} max points/partition ({solidBytes / (1024f * 1024f):F1} MiB solid buffer)");
        }

        private int ResolveDrawDistance()
        {
            int configured = VoxelEngineProvider.Current?.Settings?.Chunk?.DrawDistance ?? 2;
            return Mathf.Max(1, configured);
        }

        private int ResolveMaxPointsPerPartition(int activePartitions)
        {
            if (activePartitions <= 0)
                return VoxelRenderConstants.MaxPointsPerPartition;

            long bytesPerPartitionAtMax = (long)VoxelRenderConstants.MaxPointsPerPartition * VoxelRenderConstants.PointDataStride;
            long maxBytesAtCurrentDistance = bytesPerPartitionAtMax * activePartitions;
            if (maxBytesAtCurrentDistance <= SolidPointsBufferBudgetBytes)
                return VoxelRenderConstants.MaxPointsPerPartition;

            int clamped = Mathf.Max(1,
                (int)(SolidPointsBufferBudgetBytes / ((long)activePartitions * VoxelRenderConstants.PointDataStride)));

            Debug.LogWarning(
                $"VoxelWorldRenderer: Clamping max points per partition from {VoxelRenderConstants.MaxPointsPerPartition} to {clamped} " +
                $"to stay within {SolidPointsBufferBudgetBytes / (1024f * 1024f):F0} MiB solid buffer budget at draw distance capacity.");

            return clamped;
        }

        private void InitializeKernels()
        {
            if (meshBuilderCompute == null)
            {
                Debug.LogError("VoxelWorldRenderer: MeshBuilderCompute is null!");
                return;
            }

            _kernelRebuild = meshBuilderCompute.FindKernel("RebuildSolidPoints");
            _kernelCullChunks = meshBuilderCompute.FindKernel("CullChunks");
            _kernelCullPartitions = meshBuilderCompute.FindKernel("CullPartitions");
            _kernelBuildArgs = meshBuilderCompute.FindKernel("BuildIndirectArgs");
        }

        private void OnDestroy()
        {
            _voxelDataUploadBuffer?.Release();
            _partitionMetadataBuffer?.Release();
            _chunkMetadataBuffer?.Release();
            _solidPointsBuffer?.Release();
            _visibleChunksBuffer?.Release();
            _visiblePartitionsBuffer?.Release();
            _indirectArgsBuffer?.Release();
            _counterBuffer?.Release();

            if (_chunkManager != null)
                _chunkManager.OnGpuRebuildReady -= OnGpuRebuildReady;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
        }

        private void OnGpuRebuildReady()
        {
            // Fetch dirty partitions (max 16/frame)
            List<int3> dirtyPartitions = _chunkManager.FlushGpuDirtyPartitions();
            if (dirtyPartitions.Count == 0)
            {
                if (debugBootstrapDirtyPartition && _partitionIndexMap.Count == 0)
                {
                    _chunkManager.MarkPartitionDirty(int3.zero);
                    dirtyPartitions = _chunkManager.FlushGpuDirtyPartitions();
                }

                if (dirtyPartitions.Count == 0)
                    return;
            }

            // Current pipeline appends globally; reset once per rebuild batch for deterministic debug output.
            _solidPointsBuffer.SetCounterValue(0);

            // Upload voxel data for dirty partitions
            UploadVoxelData(dirtyPartitions);

            // Dispatch rebuild for each partition
            foreach (int3 partPos in dirtyPartitions)
            {
                int partIdx = GetOrCreatePartitionIndex(partPos);
                int3 partitionWorldOffset = new int3(
                    partPos.x * VoxelConstants.PartitionWidth,
                    partPos.y * VoxelConstants.PartitionHeight,
                    partPos.z * VoxelConstants.PartitionDepth);

                meshBuilderCompute.SetInt("_PartitionIndex", partIdx);
                meshBuilderCompute.SetInt("_MaxPointsPerPartition", _maxPointsPerPartition);
                meshBuilderCompute.SetInts("_PartitionWorldOffset", partitionWorldOffset.x, partitionWorldOffset.y, partitionWorldOffset.z);
                meshBuilderCompute.SetBuffer(_kernelRebuild, "_VoxelData", _voxelDataUploadBuffer);
                meshBuilderCompute.SetBuffer(_kernelRebuild, "_PointsOut", _solidPointsBuffer);
                meshBuilderCompute.SetBuffer(_kernelRebuild, "_Metadata", _partitionMetadataBuffer);

                // Dispatch 4×4×4 groups = 64 groups for 32^3 voxels with 8^3 threads
                meshBuilderCompute.Dispatch(_kernelRebuild, 4, 4, 4);
            }
        }

        private void UploadVoxelData(List<int3> dirtyPartitions)
        {
            // TODO: Implement actual voxel data upload from ChunkManager
            // For now: placeholder upload of zeros
            int voxelsPerPartition = 32 * 32 * 32;
            uint[] dummyData = new uint[voxelsPerPartition * dirtyPartitions.Count];

            // Fill with test data (solid voxel ID 1 for testing)
            for (int i = 0; i < dummyData.Length; i++)
            {
                dummyData[i] = (uint)((i % 32 < 16) ? 1 : 0); // Checkerboard pattern
            }

            _voxelDataUploadBuffer.SetData(dummyData);
        }

        private int GetOrCreatePartitionIndex(int3 partPos)
        {
            if (_partitionIndexMap.TryGetValue(partPos, out int idx))
                return idx;

            idx = _nextPartitionIndex++;
            _partitionIndexMap[partPos] = idx;
            return idx;
        }

        private void Draw(ScriptableRenderContext ctx, Camera cam)
        {
            if (meshBuilderCompute == null || solidMaterial == null) return;

            if (debugBypassCulling)
            {
                DrawWithoutCulling(ctx);
                return;
            }

            // Extract frustum planes
            GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanes);
            for (int i = 0; i < 6; i++)
            {
                _frustumPlanesData[i] = new Vector4(
                    _frustumPlanes[i].normal.x,
                    _frustumPlanes[i].normal.y,
                    _frustumPlanes[i].normal.z,
                    _frustumPlanes[i].distance);
            }

            meshBuilderCompute.SetVectorArray("_VoxelFrustumPlanes", _frustumPlanesData);

            // Stage 1: Cull chunks (coarse)
            _visibleChunksBuffer.SetCounterValue(0);
            meshBuilderCompute.SetInt("_ActiveChunkCount", _maxActiveChunks);
            meshBuilderCompute.SetBuffer(_kernelCullChunks, "_ChunkMetadata", _chunkMetadataBuffer);
            meshBuilderCompute.SetBuffer(_kernelCullChunks, "_VisibleChunks", _visibleChunksBuffer);
            int chunkGroups = Mathf.CeilToInt(_maxActiveChunks / 64f);
            meshBuilderCompute.Dispatch(_kernelCullChunks, chunkGroups, 1, 1);

            // Copy chunk count for debugging
            ComputeBuffer.CopyCount(_visibleChunksBuffer, _counterBuffer, 0);

            // Stage 2: Cull partitions (fine) - one group per visible chunk
            _visiblePartitionsBuffer.SetCounterValue(0);
            meshBuilderCompute.SetBuffer(_kernelCullPartitions, "_VisibleChunksIn", _visibleChunksBuffer);
            meshBuilderCompute.SetBuffer(_kernelCullPartitions, "_ChunkMetadata", _chunkMetadataBuffer);
            meshBuilderCompute.SetBuffer(_kernelCullPartitions, "_Metadata", _partitionMetadataBuffer);
            meshBuilderCompute.SetBuffer(_kernelCullPartitions, "_VisiblePartitions", _visiblePartitionsBuffer);
            // Dispatch worst-case (all chunks visible)
            meshBuilderCompute.Dispatch(_kernelCullPartitions, _maxActiveChunks, 1, 1);

            // Copy partition count
            ComputeBuffer.CopyCount(_visiblePartitionsBuffer, _counterBuffer, 4);

            // Stage 3: Build indirect args
            meshBuilderCompute.SetInt("_VisiblePartitionCount", _maxActivePartitions);
            meshBuilderCompute.SetBuffer(_kernelBuildArgs, "_VisiblePartitionsIn", _visiblePartitionsBuffer);
            meshBuilderCompute.SetBuffer(_kernelBuildArgs, "_Metadata", _partitionMetadataBuffer);
            meshBuilderCompute.SetBuffer(_kernelBuildArgs, "_IndirectArgs", _indirectArgsBuffer);
            meshBuilderCompute.Dispatch(_kernelBuildArgs, 1, 1, 1);

            // Stage 4: Draw indirect
            CommandBuffer cmd = CommandBufferPool.Get("VoxelSolidDraw");
            cmd.SetGlobalBuffer("_PointData", _solidPointsBuffer);
            cmd.SetGlobalBuffer("_VisiblePartitions", _visiblePartitionsBuffer);
            cmd.DrawProceduralIndirect(
                Matrix4x4.identity,
                solidMaterial,
                0,
                MeshTopology.Points,
                _indirectArgsBuffer,
                0);
            ctx.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void DrawWithoutCulling(ScriptableRenderContext ctx)
        {
            // Build indirect args from the append counter so we can validate mesh generation independent of culling/metadata.
            ComputeBuffer.CopyCount(_solidPointsBuffer, _counterBuffer, 0);
            _counterBuffer.GetData(_debugCounterReadback, 0, 0, 1);

            uint pointCount = _debugCounterReadback[0];
            _debugIndirectArgs[0] = pointCount * 6;
            _debugIndirectArgs[1] = pointCount > 0 ? 1u : 0u;
            _debugIndirectArgs[2] = 0;
            _debugIndirectArgs[3] = 0;
            _debugIndirectArgs[4] = 0;
            _indirectArgsBuffer.SetData(_debugIndirectArgs);

            if ((++_debugFrameCounter % 120) == 0)
                Debug.Log($"VoxelWorldRenderer(Debug): points={pointCount}, partitions={_partitionIndexMap.Count}");

            CommandBuffer cmd = CommandBufferPool.Get("VoxelSolidDrawDebug");
            cmd.SetGlobalBuffer("_PointData", _solidPointsBuffer);
            cmd.DrawProceduralIndirect(
                Matrix4x4.identity,
                solidMaterial,
                0,
                MeshTopology.Triangles,
                _indirectArgsBuffer,
                0);
            ctx.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

