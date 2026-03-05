using System.Collections.Generic;
using System.Runtime.InteropServices;
using Runtime.Engine.VoxelConfig.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Test
{
    public class ComputeTest : MonoBehaviour
    {
        private static readonly int PointBufferID = Shader.PropertyToID("_PointBuffer");
        private static readonly int QuadBufferID = Shader.PropertyToID("_QuadBuffer");
        private static readonly int ExpandedBufferID = Shader.PropertyToID("_ExpandedBuffer");

        // ──────────────────────────────────────────────────────────────────────
        // Muss mit dem Vertex-Struct in VoxelExpand.compute übereinstimmen
        // stride = 28 bytes  (float3 Position + uint4 PackedData)
        // ──────────────────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Vertex
        {
            public Vector3 position; // 12 bytes
            public uint packedX; //  4 bytes  low16 = quadIndex, high16 = texIndex
            public uint packedY; //  4 bytes  light nibbles
            public uint packedZ; //  4 bytes  ao | depthFade | glow
            public uint packedW; //  4 bytes  unused
        }

        // ──────────────────────────────────────────────────────────────────────
        // Muss mit ExpandedVertex in VoxelExpand.compute übereinstimmen
        // stride = 36 bytes  (float3 positionOS + float2 uv + uint4 packed)
        // ──────────────────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ExpandedVertex
        {
            public Vector3 positionOS; // 12 bytes
            public Vector2 uv; //  8 bytes
            public uint packedX; //  4 bytes
            public uint packedY; //  4 bytes
            public uint packedZ; //  4 bytes
            public uint packedW; //  4 bytes
        }

        [SerializeField] private VoxelDataImporter _importer;
        [SerializeField] private ComputeShader _computeShader;
        [SerializeField] private Material _material; // ExpandedBufferDraw

        private const int PointCount = 6;
        private const int ThreadGroupX = 64; // muss mit [numthreads(64,1,1)] in CSExpand übereinstimmen

        private ComputeBuffer _pointBuffer;

        private ComputeBuffer _expandedBuffer;

        // DrawProceduralIndirect args: { indexCountPerInstance, instanceCount, startIndex, baseVertex, startInstance }
        private ComputeBuffer _argsBuffer;

        private List<ChunkDrawCall> _chunks = new();

        private void Start()
        {
            // ── Input-Buffer befüllen ──────────────────────────────────────────
            // stride = 28 bytes (3 floats + 4 uints)
            _pointBuffer = new ComputeBuffer(PointCount, Marshal.SizeOf<Vertex>());

            Vertex[] points = new Vertex[PointCount];
            for (int i = 0; i < PointCount; i++)
            {
                points[i] = new Vertex
                {
                    position = new Vector3(1, 1, 1),
                    // quadIndex = 0 (lower 16 bits), texIndex = 1 (upper 16 bits)
                    packedX = (uint)(i),
                    packedY = 0xFFFFFFFF, // alle Licht-Nibbles auf max
                    packedZ = 0,
                    packedW = 0
                };
            }

            _pointBuffer.SetData(points);

            // ── Output-Buffer anlegen ─────────────────────────────────────────
            // stride = 36 bytes (3+2 floats + 4 uints)
            _expandedBuffer = new ComputeBuffer(PointCount * 4, Marshal.SizeOf<ExpandedVertex>());

            // ── Args-Buffer für DrawProceduralIndirect ────────────────────────
            // Layout: { indexCountPerInstance, instanceCount, startIndex, baseVertex, startInstance }
            int indexCount = PointCount * 6; // 2 Dreiecke × 3 Indices pro Quad
            _argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            _argsBuffer.SetData(new uint[] { (uint)indexCount, 1u, 0u, 0u, 0u });

            // ── Shader-Ressourcen setzen ──────────────────────────────────────
            int kernelIndex = _computeShader.FindKernel("CSExpand");

            _computeShader.SetBuffer(kernelIndex, PointBufferID, _pointBuffer);
            _computeShader.SetBuffer(kernelIndex, QuadBufferID, _importer._quadDataBuffer);
            _computeShader.SetBuffer(kernelIndex, ExpandedBufferID, _expandedBuffer);

            // ── Dispatch: ceil(PointCount / 64) Gruppen ──────────────────────
            int groups = (PointCount + ThreadGroupX - 1) / ThreadGroupX;
            _computeShader.Dispatch(kernelIndex, groups, 1, 1);

            // ── Readback und Debug-Ausgabe der ersten 4 Vertices ─────────────
            ExpandedVertex[] result = new ExpandedVertex[PointCount * 4];
            _expandedBuffer.GetData(result);

            for (int i = 0; i < 4; i++)
            {
                ExpandedVertex v = result[i];
                Debug.Log($"[ComputeTest] Vertex {i}: pos={v.positionOS}  uv={v.uv}  " +
                          $"packedX=0x{v.packedX:X8} packedY=0x{v.packedY:X8}  packedZ=0x{v.packedZ:X8}  packedW=0x{v.packedW:X8}");
            }

            _chunks.Add(new ChunkDrawCall()
            {
                ExpandedBuffer = _expandedBuffer,
                ArgsBuffer = _argsBuffer,
                Bounds = new Bounds(Vector3.zero, Vector3.one * 1000f),
                Material = _material
            });
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += DrawChunks;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= DrawChunks;
        }

        private void DrawChunks(ScriptableRenderContext ctx, Camera cam)
        {
            foreach (var chunk in _chunks)
            {
                if (!chunk.IsValid) continue;
                Debug.Log($"[ComputeTest] Drawing chunk with Bounds={chunk.Bounds} using {chunk.ExpandedBuffer.count} expanded vertices");
                chunk.Material.SetBuffer(ExpandedBufferID, chunk.ExpandedBuffer);
                Graphics.DrawProceduralIndirect(
                    chunk.Material,
                    chunk.Bounds,
                    MeshTopology.Triangles,
                    chunk.ArgsBuffer
                );
            }
        }

        private void OnDestroy()
        {
            _pointBuffer?.Release();
            _expandedBuffer?.Release();
            _argsBuffer?.Release();
        }
    }

    public class ChunkDrawCall
    {
        public ComputeBuffer ExpandedBuffer;
        public ComputeBuffer ArgsBuffer;
        public Bounds Bounds;
        public Material Material; // eigene Material-Instanz (per chunk)

        public bool IsValid => ExpandedBuffer != null && ArgsBuffer != null && Material;
    }
}