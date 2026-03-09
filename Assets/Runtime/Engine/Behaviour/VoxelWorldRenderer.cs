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
        private ComputeBuffer _counterBuffer; // uint[4] for append counters

        // State
        private int _maxActivePartitions;
        private int _maxActiveChunks;
        private ChunkManager _chunkManager;
        private Plane[] _frustumPlanes = new Plane[6];
        private Vector4[] _frustumPlanesData = new Vector4[6];
        private Dictionary<int3, int> _partitionIndexMap = new();
        private int _nextPartitionIndex;

        private void Awake()
        {
            // Get ChunkManager reference from VoxelWorld
            _chunkManager = VoxelWorld.Instance?.ChunkManager;
            if (_chunkManager == null)
            {
                Debug.LogError("VoxelWorldRenderer: ChunkManager not found!");
                enabled = false;
                return;
            }

            // Calculate max capacities based on draw distance
            int drawDist = 8; // TODO: Get from settings
            _maxActiveChunks = VoxelRenderConstants.MaxActiveChunks(drawDist);
            _maxActivePartitions = VoxelRenderConstants.MaxActivePartitions(drawDist);

            InitializeBuffers();
            InitializeKernels();

            // Hook GPU rebuild trigger
            _chunkManager.OnGpuRebuildReady += OnGpuRebuildReady;
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

            _solidPointsBuffer = new ComputeBuffer(
                _maxActivePartitions * VoxelRenderConstants.MaxPointsPerPartition,
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

            _counterBuffer = new ComputeBuffer(4, sizeof(uint));

            Debug.Log(
                $"VoxelWorldRenderer: Initialized buffers for {_maxActivePartitions} partitions, {_maxActiveChunks} chunks");
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
            if (dirtyPartitions.Count == 0) return;

            // Upload voxel data for dirty partitions
            UploadVoxelData(dirtyPartitions);

            // Dispatch rebuild for each partition
            foreach (int3 partPos in dirtyPartitions)
            {
                int partIdx = GetOrCreatePartitionIndex(partPos);

                meshBuilderCompute.SetInt("_PartitionIndex", partIdx);
                meshBuilderCompute.SetInt("_MaxPointsPerPartition", VoxelRenderConstants.MaxPointsPerPartition);
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

            meshBuilderCompute.SetVectorArray("_FrustumPlanes", _frustumPlanesData);

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
                MeshTopology.Triangles,
                _indirectArgsBuffer,
                0);
            ctx.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}