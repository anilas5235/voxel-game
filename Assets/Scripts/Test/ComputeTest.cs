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
        private static readonly int IndexOffset = Shader.PropertyToID("_IndexOffset");

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
        [SerializeField] private Material _material;
        [SerializeField] private Material _Tmaterial;

        private const int PointCount = 6;
        private const int TPointCount = 6;
        private const int ThreadGroupX = 64; // muss mit [numthreads(64,1,1)] in CSExpand übereinstimmen

        private ComputeBuffer _pointBuffer;

        private ComputeBuffer _expandedBuffer;

        private List<ChunkDrawCall> _chunks = new();

        private void Start()
        {
            // ── Input-Buffer befüllen ──────────────────────────────────────────
            // stride = 28 bytes (3 floats + 4 uints)

            const int totalPoints = PointCount + TPointCount;
            Vertex[] points = new Vertex[totalPoints];
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

            for (int i = 0; i < TPointCount; i++)
            {
                points[i + PointCount] = new Vertex
                {
                    position = new Vector3(-1, 1, 1),
                    // quadIndex = 0 (lower 16 bits), texIndex = 1 (upper 16 bits)
                    packedX = (uint)(i),
                    packedY = 0xFFFFFFFF, // alle Licht-Nibbles auf max
                    packedZ = 0,
                    packedW = 0
                };
            }

            _pointBuffer = new ComputeBuffer(totalPoints, Marshal.SizeOf<Vertex>());
            _pointBuffer.SetData(points);

            // ── Output-Buffer anlegen ─────────────────────────────────────────
            // stride = 36 bytes (3+2 floats + 4 uints)
            _expandedBuffer = new ComputeBuffer(totalPoints * 4, Marshal.SizeOf<ExpandedVertex>());

            // ── Shader-Ressourcen setzen ──────────────────────────────────────
            int kernelIndex = _computeShader.FindKernel("CSExpand");

            _computeShader.SetBuffer(kernelIndex, PointBufferID, _pointBuffer);
            _computeShader.SetBuffer(kernelIndex, QuadBufferID, _importer._quadDataBuffer);
            _computeShader.SetBuffer(kernelIndex, ExpandedBufferID, _expandedBuffer);

            // ── Dispatch: ceil(PointCount / 64) Gruppen ──────────────────────
            const int groups = (totalPoints + ThreadGroupX - 1) / ThreadGroupX;
            _computeShader.Dispatch(kernelIndex, groups, 1, 1);

            // ── Args-Buffer für DrawProceduralIndirect ────────────────────────
            // Layout: { indexCountPerInstance, instanceCount, startIndex, baseVertex, startInstance }
            const int indexCount = PointCount * 6; // 2 Dreiecke × 3 Indices pro Quad
            ComputeBuffer argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(new uint[] { (uint)indexCount, 1u, 0u, 0u, 0u });

            // ── Args-T-Buffer für DrawProceduralIndirect ────────────────────────
            // Layout: { indexCountPerInstance, instanceCount, startIndex, baseVertex, startInstance }
            const uint indexTCount = TPointCount * 6; // 2 Dreiecke × 3 Indices pro Quad
            ComputeBuffer argsTBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            argsTBuffer.SetData(new uint[] { (uint)indexTCount, 1u, 0u, 0u, 0u });

            _chunks.Add(new ChunkDrawCall()
            {
                ExpandedBuffer = _expandedBuffer,
                ArgsBuffer = argsBuffer,
                SolidIndexCount = indexCount,
                ArgsTBuffer = argsTBuffer,
                Bounds = new Bounds(Vector3.zero, Vector3.one * 32f),
                Material = _material,
                TMaterial = _Tmaterial
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
                if (chunk.ArgsBuffer != null)
                {
                    var matProps = new MaterialPropertyBlock();
                    matProps.SetInt(IndexOffset, chunk.SolidIndexCount);
                    matProps.SetBuffer(ExpandedBufferID, chunk.ExpandedBuffer);
                    Graphics.DrawProceduralIndirect(
                        chunk.Material,
                        chunk.Bounds,
                        MeshTopology.Triangles,
                        chunk.ArgsBuffer,
                        0,
                        cam,
                        matProps
                    );
                }

                if (chunk.ArgsTBuffer != null)
                {
                    var matProps = new MaterialPropertyBlock();
                    matProps.SetInt(IndexOffset, chunk.SolidIndexCount);
                    matProps.SetBuffer(ExpandedBufferID, chunk.ExpandedBuffer);
                    Graphics.DrawProceduralIndirect(
                        chunk.TMaterial,
                        chunk.Bounds,
                        MeshTopology.Triangles,
                        chunk.ArgsTBuffer,
                        0,
                        cam,
                        matProps
                    );
                }
            }
        }

        private void OnDestroy()
        {
            _pointBuffer?.Release();
            _expandedBuffer?.Release();
            foreach (var chunk in _chunks)
            {
                chunk.ArgsBuffer?.Release();
                chunk.ArgsTBuffer?.Release();
            }
        }
    }

    public class ChunkDrawCall
    {
        public ComputeBuffer ExpandedBuffer;
        public ComputeBuffer ArgsBuffer;
        public int SolidIndexCount;
        public ComputeBuffer ArgsTBuffer;
        public Bounds Bounds;
        public Material Material;
        public Material TMaterial;

        public bool IsValid => ExpandedBuffer != null && Material != null && TMaterial != null;
    }
}