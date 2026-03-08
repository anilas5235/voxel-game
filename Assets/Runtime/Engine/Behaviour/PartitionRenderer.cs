using System.Collections;
using System.Runtime.InteropServices;
using Runtime.Engine.Jobs.Meshing;
using Runtime.Engine.Settings;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Behaviour
{
    public class PartitionRenderer : MonoBehaviour
    {
        private const int ThreadGroupX = 64;

        private static readonly int IndexOffsetID = Shader.PropertyToID("_IndexOffset");
        private static readonly int PointBufferID = Shader.PropertyToID("_PointBuffer");
        private static readonly int QuadBufferID = Shader.PropertyToID("_QuadBuffer");
        private static readonly int ExpandedBufferID = Shader.PropertyToID("_ExpandedBuffer");
        private static readonly int PartitionWorldPosID = Shader.PropertyToID("_PartitionWorldPos");

        [SerializeField] private Material[] materials = new Material[3]; // 0: solid, 1: transparent, 2: foliage
        [SerializeField] private ComputeShader expandComputeShader;

        private PartitionMeshGPUData _gpuData;
        private readonly PartitionDrawCall[] _drawCalls = new PartitionDrawCall[3];
        private ComputeBuffer _expandedBuffer;
        [SerializeField] private bool validForDraw;

        private VoxelDataImporter _importer;

        private void Awake()
        {
            _importer = VoxelDataImporter.Instance;

            for (int i = 0; i < _drawCalls.Length; i++)
            {
                _drawCalls[i] = new PartitionDrawCall
                {
                    ArgsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments),
                    Material = materials[i],
                    MaterialPropertyBlock = new MaterialPropertyBlock(),
                };
            }

            GlobalPartitionRenderer.Instance.RegisterRenderer(this);
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
            if (!validForDraw) return;
            foreach (PartitionDrawCall drawCall in _drawCalls)
            {
                Graphics.DrawProceduralIndirect(
                    drawCall.Material,
                    drawCall.Bounds,
                    MeshTopology.Triangles,
                    drawCall.ArgsBuffer,
                    0,
                    cam,
                    drawCall.MaterialPropertyBlock,
                    ShadowCastingMode.Off,
                    false
                );
            }
        }

        public void Init(RendererSettings settings)
        {
        }

        public void Clear()
        {
            validForDraw = false;
            _expandedBuffer?.Release();
            _expandedBuffer = null;
            _gpuData.Vertices.Dispose();
        }

        public void DrawForPass(int pass, Camera cam)
        {
            if (!validForDraw) return;
            PartitionDrawCall drawCall = _drawCalls[pass];
            Graphics.DrawProceduralIndirect(
                drawCall.Material,
                drawCall.Bounds,
                MeshTopology.Triangles,
                drawCall.ArgsBuffer,
                0,
                cam,
                drawCall.MaterialPropertyBlock,
                ShadowCastingMode.Off,
                false
            );
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

            Bounds bounds = _gpuData.Bounds;
            bounds.center += transform.position;
            foreach (PartitionDrawCall drawCall in _drawCalls)
            {
                drawCall.Bounds = bounds;
            }

            StartCoroutine(RunComputeShader());
        }

        private void OnDestroy()
        {
            Clear();
            foreach (PartitionDrawCall drawCall in _drawCalls) drawCall.ArgsBuffer?.Release();
            GlobalPartitionRenderer.Instance?.UnregisterRenderer(this);
        }

        private IEnumerator RunComputeShader()
        {
            ComputeBuffer pointBuffer = new(_gpuData.TotalVertexCount, Marshal.SizeOf<Vertex>(),
                ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);
            UnsafeList<Vertex> vertices = _gpuData.Vertices;
            int vertexCount = _gpuData.TotalVertexCount;

            NativeArray<Vertex> mapped = pointBuffer.BeginWrite<Vertex>(0, vertexCount);
            for (int i = 0; i < vertexCount; i++) mapped[i] = vertices[i];
            pointBuffer.EndWrite<Vertex>(vertexCount);

            // ── Output-Buffer anlegen ─────────────────────────────────────────
            ComputeBuffer newExpandedBuffer = new(_gpuData.TotalVertexCount * 4, Marshal.SizeOf<ExpandedVertex>());

            yield return new WaitForNextFrameUnit();

            // ── Shader-Ressourcen setzen ──────────────────────────────────────
            int kernelIndex = expandComputeShader.FindKernel("CSExpand");

            expandComputeShader.SetBuffer(kernelIndex, PointBufferID, pointBuffer);
            expandComputeShader.SetBuffer(kernelIndex, QuadBufferID, _importer.QuadDataBuffer);
            expandComputeShader.SetBuffer(kernelIndex, ExpandedBufferID, newExpandedBuffer);
            expandComputeShader.SetVector(PartitionWorldPosID, transform.position);

            // ── Dispatch ──────────────────────────────────────────────────────
            int groups = (_gpuData.TotalVertexCount + ThreadGroupX - 1) / ThreadGroupX;
            expandComputeShader.Dispatch(kernelIndex, groups, 1, 1);

            // ── ArgsBuffer ────────────────────────────────────────────────────
            _drawCalls[0].ArgsBuffer.SetData(new[] { (uint)_gpuData.SolidVertexCount * 6u, 1u, 0u, 0u, 0u });
            _drawCalls[0].MaterialPropertyBlock.SetInt(IndexOffsetID, 0);
            _drawCalls[1].ArgsBuffer.SetData(new[] { (uint)_gpuData.TransparentVertexCount * 6u, 1u, 0u, 0u, 0u });
            _drawCalls[1].MaterialPropertyBlock.SetInt(IndexOffsetID, _gpuData.SolidVertexCount * 6);
            _drawCalls[2].ArgsBuffer.SetData(new[] { (uint)_gpuData.FoliageVertexCount * 6u, 1u, 0u, 0u, 0u });
            _drawCalls[2].MaterialPropertyBlock.SetInt(IndexOffsetID,
                (_gpuData.SolidVertexCount + _gpuData.TransparentVertexCount) * 6);

            pointBuffer.Release();
            _expandedBuffer?.Release();
            _expandedBuffer = newExpandedBuffer;
            validForDraw = true;
            foreach (PartitionDrawCall drawCall in _drawCalls)
            {
                drawCall.MaterialPropertyBlock.SetBuffer(ExpandedBufferID, _expandedBuffer);
            }
        }

        private class PartitionDrawCall
        {
            public ComputeBuffer ArgsBuffer;
            public Bounds Bounds;
            public Material Material;
            public MaterialPropertyBlock MaterialPropertyBlock;
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