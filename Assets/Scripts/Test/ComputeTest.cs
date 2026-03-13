using System;
using System.Runtime.InteropServices;
using Runtime.Engine.Jobs.Meshing;
using Runtime.Engine.Utils.Collections;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static Runtime.Engine.Utils.VoxelConstants;
using static Runtime.Engine.Utils.VoxelRenderConstants;
using static UnityEngine.GraphicsBuffer;

namespace Test
{
    public class ComputeTest : MonoBehaviour
    {
        private static readonly int VoxelRenderDefNameID = Shader.PropertyToID("_VoxelRenderDefs");
        private static readonly int VoxelDataNameID = Shader.PropertyToID("_RawVoxels");
        private static readonly int MetadataNameID = Shader.PropertyToID("_Metadata");
        private static readonly int PointsOutNameID = Shader.PropertyToID("_PointsOut");
        private static readonly int PartitionIndexNameID = Shader.PropertyToID("_PartitionIndex");
        
        private static readonly int PointsCopyOutNameID = Shader.PropertyToID("_PointsCopyOut");
        private static readonly int PointsInNameID = Shader.PropertyToID("_PointsIn");
        private static readonly int PointCountNameID = Shader.PropertyToID("_PointCount");
        private static readonly int PointOffsetNameID = Shader.PropertyToID("_PointOffset");
        
        private static readonly int PointDataNameID = Shader.PropertyToID("_PointData");

        struct PartitionMetadata
        {
            int3 partitionPos; // World partition coordinates
            uint pointCount; // Actual points generated
            float3 boundsMin; // AABB min for frustum culling
            float3 boundsMax; // AABB max
        };

        struct ChunkMetadata
        {
            int2 chunkPos; // World chunk XZ coordinates
            float3 boundsMin; // AABB min for coarse culling
            float3 boundsMax; // AABB max
            uint partitionMask; // Bitmask: which of 8 partitions are active (bit Y)
        };

        [SerializeField] private ComputeShader pointBuilder;
        [SerializeField] private Material material;


        private GraphicsBuffer _voxelRenderDefBuffer;
        private ComputeBuffer _voxelData;
        private ComputeBuffer _metadata;
        private GraphicsBuffer _pointsOut;

        private ComputeBuffer _argBuffer;
        private MaterialPropertyBlock _materialPropertyBlock;

        private GraphicsBuffer _bigVertexBuffer;

        private int _buildPointsKernel;
        private int _copyPointsKernel;

        private UnsafeIntervalList<ushort> _voxels;
        
        private bool _dataInitialized = false;


        private void Awake()
        {
            _voxelRenderDefBuffer = VoxelDataImporter.Instance.VoxelRegistry.VoxelRenderDefBuffer;
            _buildPointsKernel = pointBuilder.FindKernel("RebuildPoints");
            _copyPointsKernel = pointBuilder.FindKernel("CopyPoints");
            _voxels = new UnsafeIntervalList<ushort>(10, Allocator.Domain);
            _voxels.AddInterval(0,VoxelsPerPartition);
            for (int j = 0; j < 32; j++)
            {
                _voxels.Set(j,1);
            }
            var intervalData = new uint2[_voxels.CompressedLength];
            int i = 0;
            foreach (UnsafeIntervalList<ushort>.Node n in _voxels.Internal)
            {
                intervalData[i++] = new uint2(n.Value, (uint) n.Count);
            }
            
            if(_voxels.Length != VoxelsPerPartition) throw new Exception("Voxel data length mismatch!");
            _voxelData = new ComputeBuffer(_voxels.CompressedLength, Marshal.SizeOf<uint2>());
            _voxelData.SetData(intervalData);
            Debug.Log("Voxel data uploaded to GPU.: " +String.Join(", ", intervalData));
            _metadata = new ComputeBuffer(1, Marshal.SizeOf<PartitionMetadata>());
            _pointsOut = new GraphicsBuffer(Target.Append, MaxPointsPerPartition,
                Marshal.SizeOf<Vertex>());

            _bigVertexBuffer =
                new GraphicsBuffer(Target.Structured, 1000000, Marshal.SizeOf<Vertex>());

            _argBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            _argBuffer.SetData(new uint[]
                { 0u, 1u, 0u, 0u, 0u }); // vertex count, instance count, start vertex, start instance
            _materialPropertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
        }

        private void OnDestroy()
        {
            _voxels.Dispose();
            _voxelData.Dispose();
            _metadata.Dispose();
            _pointsOut.Dispose();
            _argBuffer.Dispose();
            _bigVertexBuffer.Dispose();
            _materialPropertyBlock.Clear();
        }

        private void Start()
        {
            pointBuilder.SetBuffer(_buildPointsKernel,VoxelRenderDefNameID, _voxelRenderDefBuffer);
            pointBuilder.SetBuffer(_buildPointsKernel, VoxelDataNameID, _voxelData);
            pointBuilder.SetBuffer(_buildPointsKernel, MetadataNameID, _metadata);
            pointBuilder.SetBuffer(_buildPointsKernel, PointsOutNameID, _pointsOut);
            pointBuilder.SetInt(PartitionIndexNameID, 0);
            pointBuilder.Dispatch(_buildPointsKernel, 4, 4, 4);

            //copy counter value to arg buffer for indirect draw
            CopyCount(_pointsOut, _argBuffer, 0);
            uint[] argData = new uint[5];
            _argBuffer.GetData(argData);
            int realPointCount = (int)argData[0];
            argData[0] *= 6u;
            _argBuffer.SetData(argData);
            Debug.Log($"Indirect args: {string.Join(", ", argData)}");
            Debug.Log($"Real point count: {realPointCount}");

            _pointsOut.SetCounterValue(0);

            pointBuilder.SetBuffer(_copyPointsKernel, PointsCopyOutNameID, _bigVertexBuffer);
            pointBuilder.SetBuffer(_copyPointsKernel, PointsInNameID, _pointsOut);
            pointBuilder.SetInt(PointCountNameID, realPointCount);
            pointBuilder.SetInt(PointOffsetNameID, 0);
            pointBuilder.Dispatch(_copyPointsKernel, Mathf.CeilToInt(realPointCount / 64f), 1, 1);
            
            _dataInitialized = true;    
        }

        private void Draw(ScriptableRenderContext context, Camera cam)
        {
            if (!_dataInitialized)  return;
            _materialPropertyBlock.SetBuffer(PointDataNameID, _bigVertexBuffer);
            Graphics.DrawProceduralIndirect(
                material,
                new Bounds(Vector3.zero, Vector3.one * 100),
                MeshTopology.Triangles,
                _argBuffer,
                0,
                cam,
                _materialPropertyBlock,
                ShadowCastingMode.Off,
                false,
                gameObject.layer
            );
        }
    }
}