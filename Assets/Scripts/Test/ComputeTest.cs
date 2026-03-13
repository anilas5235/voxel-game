using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Runtime.Engine.Jobs.Meshing;
using Runtime.Engine.Utils.Collections;
using Runtime.Engine.Utils.Extensions;
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
        private static readonly int VoxelRenderDefCountNameID = Shader.PropertyToID("_VoxelRenderDefsCount");
        private static readonly int VoxelDataNameID = Shader.PropertyToID("_RawVoxels");
        private static readonly int VoxelCompressedCountNameID = Shader.PropertyToID("_RawVoxelsCompressedCount");
        private static readonly int MetadataNameID = Shader.PropertyToID("_Metadata");
        private static readonly int SolidPointsOutNameID = Shader.PropertyToID("_SolidPointsOut");
        private static readonly int TransparentPointsOutNameID = Shader.PropertyToID("_TransparentPointsOut");
        private static readonly int FoliagePointsOutNameID = Shader.PropertyToID("_FoliagePointsOut");
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
        [SerializeField] private Material solidMaterial;
        [SerializeField] private Material transparentMaterial;
        [SerializeField] private Material foliageMaterial;


        private GraphicsBuffer _voxelRenderDefBuffer;
        private GraphicsBuffer _voxelData;
        private GraphicsBuffer _metadata;

        private GraphicsBuffer _solidPointsOut;
        private GraphicsBuffer _transparentPointsOut;
        private GraphicsBuffer _foliagePointsOut;

        private GraphicsBuffer _solidArgBuffer;
        private GraphicsBuffer _transparentArgBuffer;
        private GraphicsBuffer _foliageArgBuffer;
        private MaterialPropertyBlock _materialPropertyBlock;

        private GraphicsBuffer _bigSolidVertexBuffer;
        private GraphicsBuffer _bigTransparentVertexBuffer;
        private GraphicsBuffer _bigFoliageVertexBuffer;

        private GraphicsBuffer _readBackCountBuffer;

        private int _buildPointsKernel;
        private int _copyPointsKernel;

        private UnsafeIntervalList<ushort> _voxels;

        private bool _dataInitialized = false;

        private MaterialPass[] _drawCalls = new MaterialPass[3];

        private void Awake()
        {
            _voxelRenderDefBuffer = VoxelDataImporter.Instance.VoxelRegistry.VoxelRenderDefBuffer;
            _buildPointsKernel = pointBuilder.FindKernel("RebuildPoints");
            _copyPointsKernel = pointBuilder.FindKernel("CopyPoints");
            _voxels = new UnsafeIntervalList<ushort>(10, Allocator.Domain);
            _voxels.AddInterval(0, VoxelsPerPartition);
            for (int j = 0; j < 64; j++)
            {
                int index = PartitionSize.Flatten(new int3((j % 16) * 2, 0, 2 * (int)math.floor(j / 16.0f)));
                _voxels.Set(index, (ushort)j);
            }

            uint2[] intervalData = new uint2[_voxels.CompressedLength];
            int i = 0;
            foreach (UnsafeIntervalList<ushort>.Node n in _voxels.Internal)
            {
                intervalData[i++] = new uint2(n.Value, (uint)n.Count);
            }

            if (_voxels.Length != VoxelsPerPartition) throw new Exception("Voxel data length mismatch!");
            _voxelData = new GraphicsBuffer(Target.Structured, _voxels.CompressedLength, Marshal.SizeOf<uint2>());
            _voxelData.SetData(intervalData);
            Debug.Log("Voxel data uploaded to GPU.: " + string.Join(", ", intervalData));
            _metadata = new GraphicsBuffer(Target.Structured, 1, Marshal.SizeOf<PartitionMetadata>());

            _solidPointsOut = new GraphicsBuffer(Target.Append, MaxPointsPerPartition, Marshal.SizeOf<Vertex>());
            _transparentPointsOut = new GraphicsBuffer(Target.Append, MaxPointsPerPartition, Marshal.SizeOf<Vertex>());
            _foliagePointsOut = new GraphicsBuffer(Target.Append, MaxPointsPerPartition / 3, Marshal.SizeOf<Vertex>());

            _bigSolidVertexBuffer = new GraphicsBuffer(Target.Structured, 1000000, Marshal.SizeOf<Vertex>());
            _bigTransparentVertexBuffer = new GraphicsBuffer(Target.Structured, 1000000, Marshal.SizeOf<Vertex>());
            _bigFoliageVertexBuffer = new GraphicsBuffer(Target.Structured, 1000000, Marshal.SizeOf<Vertex>());

            uint[] defaultArgs = { 0u, 1u, 0u, 0u, 0u };
            _solidArgBuffer = new GraphicsBuffer(Target.IndirectArguments, 5, sizeof(uint));
            _solidArgBuffer.SetData(defaultArgs);
            _transparentArgBuffer = new GraphicsBuffer(Target.IndirectArguments, 5, sizeof(uint));
            _transparentArgBuffer.SetData(defaultArgs);
            _foliageArgBuffer = new GraphicsBuffer(Target.IndirectArguments, 5, sizeof(uint));
            _foliageArgBuffer.SetData(defaultArgs);

            _materialPropertyBlock = new MaterialPropertyBlock();

            _readBackCountBuffer = new GraphicsBuffer(Target.Structured, 1, sizeof(uint));

            _drawCalls[0] = new MaterialPass(solidMaterial);
            _drawCalls[1] = new MaterialPass(transparentMaterial);
            _drawCalls[2] = new MaterialPass(foliageMaterial);
            _drawCalls[0].AddDrawCall(new DrawCall
            {
                VertexBuffer = _bigSolidVertexBuffer,
                ArgsBuffer = _solidArgBuffer
            });
            _drawCalls[1].AddDrawCall(new DrawCall
            {
                VertexBuffer = _bigTransparentVertexBuffer,
                ArgsBuffer = _transparentArgBuffer,
            });
            _drawCalls[2].AddDrawCall(new DrawCall
            {
                VertexBuffer = _bigFoliageVertexBuffer,
                ArgsBuffer = _foliageArgBuffer,
            });
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
            _solidPointsOut.Dispose();
            _transparentPointsOut.Dispose();
            _foliagePointsOut.Dispose();
            _solidArgBuffer.Dispose();
            _bigSolidVertexBuffer.Dispose();
            _materialPropertyBlock.Clear();
        }

        private void Start()
        {
            var start = Time.realtimeSinceStartupAsDouble;
            pointBuilder.SetBuffer(_buildPointsKernel, VoxelRenderDefNameID, _voxelRenderDefBuffer);
            pointBuilder.SetInt(VoxelRenderDefCountNameID, _voxelRenderDefBuffer.count);
            pointBuilder.SetBuffer(_buildPointsKernel, VoxelDataNameID, _voxelData);
            pointBuilder.SetInt(VoxelCompressedCountNameID, _voxelData.count);

            pointBuilder.SetBuffer(_buildPointsKernel, MetadataNameID, _metadata);
            pointBuilder.SetBuffer(_buildPointsKernel, SolidPointsOutNameID, _solidPointsOut);
            pointBuilder.SetBuffer(_buildPointsKernel, TransparentPointsOutNameID, _transparentPointsOut);
            pointBuilder.SetBuffer(_buildPointsKernel, FoliagePointsOutNameID, _foliagePointsOut);

            pointBuilder.SetInt(PartitionIndexNameID, 0);

            pointBuilder.Dispatch(_buildPointsKernel, 4, 4, 4);

            ArgsAndCopy(_solidPointsOut, _bigSolidVertexBuffer, _solidArgBuffer, out int solidCount);
            _drawCalls[0].DrawCalls[0].VertexCount = solidCount;
            ArgsAndCopy(_transparentPointsOut, _bigTransparentVertexBuffer, _transparentArgBuffer,
                out int transparentCount);
            _drawCalls[1].DrawCalls[0].VertexCount = transparentCount;
            ArgsAndCopy(_foliagePointsOut, _bigFoliageVertexBuffer, _foliageArgBuffer, out int foliageCount);
            _drawCalls[2].DrawCalls[0].VertexCount = foliageCount;

            _dataInitialized = true;

            Debug.Log($"Point building and copy took {(Time.realtimeSinceStartupAsDouble - start) * 1000.0} ms");
        }

        private bool ArgsAndCopy(GraphicsBuffer source, GraphicsBuffer destination, GraphicsBuffer argBuffer,
            out int count)
        {
            CopyCount(source, argBuffer, 0);
            uint[] argData = new uint[5];
            argBuffer.GetData(argData);
            count = (int)argData[0];
            if (count < 1) return false;
            argData[0] *= 6u;
            argBuffer.SetData(argData);
            //Debug.Log($"Indirect args: {string.Join(", ", argData)}");
            //Debug.Log($"Real point count: {count}");

            source.SetCounterValue(0);

            CopyJob(source, destination, count);
            return true;
        }

        private void CopyJob(GraphicsBuffer source, GraphicsBuffer destination, int count, int offset = 0)
        {
            pointBuilder.SetBuffer(_copyPointsKernel, PointsCopyOutNameID, destination);
            pointBuilder.SetBuffer(_copyPointsKernel, PointsInNameID, source);
            pointBuilder.SetInt(PointCountNameID, count);
            pointBuilder.SetInt(PointOffsetNameID, offset);
            pointBuilder.Dispatch(_copyPointsKernel, Mathf.CeilToInt(count / 64f), 1, 1);
        }

        private void Draw(ScriptableRenderContext context, Camera cam)
        {
            if (!_dataInitialized) return;
            foreach (MaterialPass pass in _drawCalls)
            {
                pass.Draw(cam);
            }
        }

        private class MaterialPass
        {
            public readonly Material Material;
            private MaterialPropertyBlock PropertyBlock = new();
            public readonly List<DrawCall> DrawCalls = new();

            public MaterialPass(Material material)
            {
                Material = material;
            }

            public void AddDrawCall(DrawCall drawCall)
            {
                DrawCalls.Add(drawCall);
            }

            public void Draw(Camera cam)
            {
                foreach (DrawCall drawCall in DrawCalls)
                {
                    if (!drawCall.CanDraw) continue;
                    PropertyBlock.SetBuffer(PointDataNameID, drawCall.VertexBuffer);
                    Graphics.DrawProceduralIndirect(
                        Material,
                        new Bounds(Vector3.zero, Vector3.one * 100),
                        MeshTopology.Triangles,
                        drawCall.ArgsBuffer,
                        0,
                        cam,
                        PropertyBlock,
                        ShadowCastingMode.Off,
                        false
                    );
                }
            }
        }

        private class DrawCall
        {
            public bool CanDraw => VertexCount > 0;
            public int VertexCount;
            public GraphicsBuffer VertexBuffer;
            public GraphicsBuffer ArgsBuffer;
        }
    }
}