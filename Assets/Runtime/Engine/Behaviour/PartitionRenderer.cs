using System.Collections;
using System.Runtime.InteropServices;
using Runtime.Engine.Jobs.Meshing;
using Runtime.Engine.Settings;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Behaviour
{
    public class PartitionRenderer : MonoBehaviour
    {
        private const int ThreadGroupX = 64;

        private static readonly int IndexOffsetID = Shader.PropertyToID("_IndexOffset");
        private static readonly int VertexBufferID = Shader.PropertyToID("_VertexBuffer");
        private static readonly int PointBufferID = Shader.PropertyToID("_PointBuffer");
        private static readonly int QuadBufferID = Shader.PropertyToID("_QuadBuffer");
        private static readonly int ExpandedBufferID = Shader.PropertyToID("_ExpandedBuffer");

        [SerializeField] private Material[] materials = new Material[3]; // 0: solid, 1: transparent, 2: foliage
        [SerializeField] private ComputeShader expandComputeShader;

        private PartitionMeshGPUData _gpuData;
        private readonly PartitionDrawCall[] _drawCalls = new PartitionDrawCall[3];
        private ComputeBuffer _expandedBuffer;
        private bool _validForDraw;

        private VoxelDataImporter _importer;

        private void Awake()
        {
            _importer = VoxelDataImporter.Instance;

            for (int i = 0; i < _drawCalls.Length; i++)
            {
                _drawCalls[i] = new PartitionDrawCall
                {
                    ArgsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments),
                    VertexOffset = 0,
                    Material = materials[i],
                };
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
        }

        private void Draw(ScriptableRenderContext ctx, Camera cam)
        {
            if (!_validForDraw) return;
            foreach (PartitionDrawCall drawCall in _drawCalls)
            {
                if (drawCall.ArgsBuffer == null) continue;

                MaterialPropertyBlock matProps = new();
                matProps.SetInt(IndexOffsetID, drawCall.VertexOffset);
                matProps.SetBuffer(VertexBufferID, _expandedBuffer);

                Graphics.DrawProceduralIndirect(
                    drawCall.Material,
                    drawCall.Bounds,
                    MeshTopology.Triangles,
                    drawCall.ArgsBuffer,
                    0,
                    cam,
                    matProps
                );
            }
        }

        public void Init(RendererSettings settings)
        {
        }

        public void Clear()
        {
            _expandedBuffer?.Release();
            _expandedBuffer = null;
            _validForDraw = false;
            _gpuData.Vertices.Dispose();
        }

        public void MeshUpdate(ref PartitionMeshGPUData data)
        {
            _gpuData.Vertices.Dispose();
            _gpuData = data;

            if (_gpuData.TotalVertexCount == 0)
            {
                Clear();
                return;
            }

            foreach (PartitionDrawCall drawCall in _drawCalls)
            {
                drawCall.Bounds = _gpuData.Bounds;
            }

            StartCoroutine(RunComputeShader());
        }

        private void OnDestroy()
        {
            _expandedBuffer?.Release();
            foreach (PartitionDrawCall drawCall in _drawCalls)
            {
                drawCall.ArgsBuffer?.Release();
            }
        }

        private IEnumerator RunComputeShader()
        {
            ComputeBuffer pointBuffer = new(_gpuData.TotalVertexCount, Marshal.SizeOf<Vertex>());
            UnsafeList<Vertex> vertices = _gpuData.Vertices;
            int vertexCount = _gpuData.TotalVertexCount;

            NativeArray<Vertex> mapped = pointBuffer.BeginWrite<Vertex>(0, vertexCount);
            for (int i = 0; i < vertexCount; i++) mapped[i] = vertices[i];
            pointBuffer.EndWrite<Vertex>(vertexCount);

            // ── Output-Buffer anlegen ─────────────────────────────────────────
            ComputeBuffer oldExpandedBuffer = _expandedBuffer;
            _expandedBuffer = new ComputeBuffer(_gpuData.TotalVertexCount * 4, Marshal.SizeOf<ExpandedVertex>());

            // ── Shader-Ressourcen setzen ──────────────────────────────────────
            int kernelIndex = expandComputeShader.FindKernel("CSExpand");

            expandComputeShader.SetBuffer(kernelIndex, PointBufferID, pointBuffer);
            expandComputeShader.SetBuffer(kernelIndex, QuadBufferID, _importer.QuadDataBuffer);
            expandComputeShader.SetBuffer(kernelIndex, ExpandedBufferID, _expandedBuffer);

            // ── Dispatch ──────────────────────────────────────────────────────
            int groups = (_gpuData.TotalVertexCount + ThreadGroupX - 1) / ThreadGroupX;
            expandComputeShader.Dispatch(kernelIndex, groups, 1, 1);

            // ── ArgsBuffer ────────────────────────────────────────────────────
            _drawCalls[0].ArgsBuffer.SetData(new[] { (uint)_gpuData.SolidVertexCount * 6, 1u, 0u, 0u, 0u });
            _drawCalls[1].ArgsBuffer.SetData(new[] { (uint)_gpuData.TransparentVertexCount * 6, 1u, 0u, 0u, 0u });
            _drawCalls[2].ArgsBuffer.SetData(new[] { (uint)_gpuData.FoliageVertexCount * 6, 1u, 0u, 0u, 0u });

            pointBuffer.Release();
            _gpuData.Vertices.Dispose();
            oldExpandedBuffer?.Release();
            _validForDraw = true;
            yield return null;
        }

        private class PartitionDrawCall
        {
            public ComputeBuffer ArgsBuffer;
            public int VertexOffset;
            public Bounds Bounds;
            public Material Material;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ExpandedVertex
        {
            public float3 positionOS; // 12 bytes
            public float2 uv; //  8 bytes
            public uint packedX; //  4 bytes
            public uint packedY; //  4 bytes
            public uint packedZ; //  4 bytes
            public uint packedW; //  4 bytes
        }
    }

    public struct PartitionMeshGPUData
    {
        public UnsafeList<Vertex> Vertices;
        public readonly int SolidVertexCount;
        public readonly int TransparentVertexCount;
        public readonly int FoliageVertexCount;
        public readonly int TotalVertexCount;
        public readonly Bounds Bounds;

        public PartitionMeshGPUData(UnsafeList<Vertex> vertices, int solidVertexCount, int transparentVertexCount,
            int foliageVertexCount, Bounds bounds)
        {
            Vertices = vertices;
            SolidVertexCount = solidVertexCount;
            TransparentVertexCount = transparentVertexCount;
            FoliageVertexCount = foliageVertexCount;
            Bounds = bounds;
            TotalVertexCount = solidVertexCount + transparentVertexCount + foliageVertexCount;
        }
    }
}