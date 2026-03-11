using System.Runtime.InteropServices;
using Runtime.Engine.Jobs.Meshing;
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

        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private Material material;


        private ComputeBuffer _VoxelData;
        private ComputeBuffer _Metadata;
        private GraphicsBuffer _PointsOut;

        private ComputeBuffer _ArgBuffer;
        private MaterialPropertyBlock _MaterialPropertyBlock;


        private GraphicsBuffer _BigVertexBuffer;


        private void Awake()
        {
            _VoxelData = new ComputeBuffer(VoxelsPerPartition, sizeof(uint));
            _Metadata = new ComputeBuffer(1, Marshal.SizeOf<PartitionMetadata>());
            _PointsOut = new GraphicsBuffer(Target.Append, MaxPointsPerPartition,
                Marshal.SizeOf<Vertex>());

            _BigVertexBuffer =
                new GraphicsBuffer(Target.Structured, 1000000, Marshal.SizeOf<Vertex>());

            _ArgBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            _ArgBuffer.SetData(new uint[]
                { 0u, 1u, 0u, 0u, 0u }); // vertex count, instance count, start vertex, start instance
            _MaterialPropertyBlock = new MaterialPropertyBlock();
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
            _VoxelData.Dispose();
            _Metadata.Dispose();
            _PointsOut.Dispose();
            _ArgBuffer.Dispose();
            _BigVertexBuffer.Dispose();
        }

        private void Start()
        {
            uint[] dummyData = new uint[VoxelsPerPartition];

            for (int i = 0; i < VoxelsPerPartition; i++)
            {
                dummyData[i] = (uint)(i % 40);
            }

            _VoxelData.SetData(dummyData);

            int kernel = computeShader.FindKernel("RebuildSolidPoints");
            computeShader.SetBuffer(kernel, "_VoxelData", _VoxelData);
            computeShader.SetBuffer(kernel, "_Metadata", _Metadata);
            computeShader.SetBuffer(kernel, "_PointsOut", _PointsOut);
            computeShader.SetInt("_PartitionIndex", 0);
            computeShader.Dispatch(kernel, 4, 4, 4);

            //copy counter value to arg buffer for indirect draw
            CopyCount(_PointsOut, _ArgBuffer, 0);
            uint[] argData = new uint[5];
            _ArgBuffer.GetData(argData);
            int realPointCount = (int)argData[0];
            argData[0] *= 6u;
            _ArgBuffer.SetData(argData);
            Debug.Log($"Indirect args: {string.Join(", ", argData)}");
            Debug.Log($"Real point count: {realPointCount}");

            _PointsOut.SetCounterValue(0);

            kernel = computeShader.FindKernel("CopyPoints");
            computeShader.SetBuffer(kernel, "_PointsCopyOut", _BigVertexBuffer);
            computeShader.SetBuffer(kernel, "_PointsIn", _PointsOut);
            computeShader.SetInt("_PointCount", realPointCount);
            computeShader.SetInt("_PointOffset", 0);
            computeShader.Dispatch(kernel, Mathf.CeilToInt(realPointCount / 64f), 1, 1);
            Vertex[] Bigpoints = new Vertex[100];
            _BigVertexBuffer.GetData(Bigpoints);
            for (int i = 0; i < 10; i++)
            {
                Vertex vertex = Bigpoints[i];
                Debug.Log(
                    $"Copied Vertex: {vertex.Position}, TextureIndex: {vertex.GetTextureIndex()}, QuadIndex: {vertex.GetQuadIndex()}");
            }
        }

        private void Draw(ScriptableRenderContext context, Camera cam)
        {
            _MaterialPropertyBlock.SetBuffer("_PointData", _BigVertexBuffer);
            Graphics.DrawProceduralIndirect(
                material,
                new Bounds(Vector3.zero, Vector3.one * 100),
                MeshTopology.Triangles,
                _ArgBuffer,
                0,
                cam,
                _MaterialPropertyBlock,
                ShadowCastingMode.Off,
                false,
                gameObject.layer
            );
        }
    }
}