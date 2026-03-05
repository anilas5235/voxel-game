using System.Runtime.InteropServices;
using Runtime.Engine.VoxelConfig.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Test
{
    public class ComputeTest : MonoBehaviour
    {
        private static readonly int PointBufferID    = Shader.PropertyToID("_PointBuffer");
        private static readonly int QuadBufferID     = Shader.PropertyToID("_QuadBuffer");
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
        [SerializeField] private ComputeShader     _computeShader;
        [SerializeField] private Material          _material; // ExpandedBufferDraw

        private const int PointCount = 1;

        private ComputeBuffer _pointBuffer;
        private ComputeBuffer _expandedBuffer;

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
                    position = new Vector3(i, 0, 0),
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

            // ── Shader-Ressourcen setzen ──────────────────────────────────────
            int kernelIndex = _computeShader.FindKernel("CSExpand");

            _computeShader.SetBuffer(kernelIndex, PointBufferID, _pointBuffer);
            _computeShader.SetBuffer(kernelIndex, QuadBufferID, _importer._quadDataBuffer);
            _computeShader.SetBuffer(kernelIndex, ExpandedBufferID, _expandedBuffer);

            // ── Dispatch: numthreads(64,1,1), PointCount Threads → 1 Gruppe ──
            _computeShader.Dispatch(kernelIndex, 1, 1, 1);

            // ── Readback und Debug-Ausgabe der ersten 4 Vertices ─────────────
            ExpandedVertex[] result = new ExpandedVertex[PointCount * 4];
            _expandedBuffer.GetData(result);

            for (int i = 0; i < 4; i++)
            {
                ExpandedVertex v = result[i];
                Debug.Log($"[ComputeTest] Vertex {i}: pos={v.positionOS}  uv={v.uv}  " +
                          $"packedX=0x{v.packedX:X8} packedY=0x{v.packedY:X8}  packedZ=0x{v.packedZ:X8}  packedW=0x{v.packedW:X8}");
            }
        }

        private void Update()
        {
            if (_expandedBuffer == null || _material == null) return;

            // Bind the expanded buffer to the draw material
            _material.SetBuffer(ExpandedBufferID, _expandedBuffer);

            // PointCount quads × 6 indices each (two triangles per quad: 0,1,2  2,1,3)
            int indexCount = PointCount * 6;

            // Draw without a Mesh – the vertex shader reads SV_VertexID directly
            Graphics.DrawProcedural(
                _material,
                new Bounds(Vector3.zero, Vector3.one * 1000f),
                MeshTopology.Triangles,
                indexCount,
                instanceCount: 1
            );
        }

        private void OnDestroy()
        {
            _pointBuffer?.Release();
            _expandedBuffer?.Release();
        }
    }
}