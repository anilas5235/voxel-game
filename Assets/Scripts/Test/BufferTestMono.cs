using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Test
{
    [RequireComponent(typeof(MeshFilter))]
    public class BufferTestMono : MonoBehaviour
    {
        private static readonly VertexAttributeDescriptor[] VertexParams = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UInt32, 4),
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vertex
        {
            public float3 Position; // xyz = position, w = 0 (unused could hold extra data)
            public ushort QuadIndex;
            public ushort padding;
            public ushort TextureIndex;
            private byte LightDataAndAO;
            public byte padding1;
            public uint padding2;
            public uint padding3;

            public void SetLight(byte sunlight)
            {
                LightDataAndAO = (byte)(LightDataAndAO | (sunlight & 0b1111));
            }

            public void SetAO(byte ao)
            {
                LightDataAndAO = (byte)(LightDataAndAO | (ao << 4));
            }
        }


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            MeshFilter mf = GetComponent<MeshFilter>();

            mf.mesh = new Mesh();

            List<Vertex> vertexData = new();

            for (int i = 0; i < 6; i++)
            {
                var v = new Vertex
                {
                    Position = float3.zero,
                    QuadIndex = (ushort)i,
                    TextureIndex = 3,
                };
                v.SetLight(15);
                vertexData.Add(v);
            }

            for (int i = 0; i < 16; i++)
            {
                var v = new Vertex
                {
                    Position = new float3(1 + i, 0, 0),
                    QuadIndex = 0,
                    TextureIndex = 3,
                };
                v.SetLight((byte)i);
                vertexData.Add(v);
            }

            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    var v = new Vertex
                    {
                        Position = new float3(2 * i, 1, 0),
                        QuadIndex = (ushort)j,
                        TextureIndex = 0,
                    };
                    v.SetLight(15);
                    v.SetAO((byte)i);
                    vertexData.Add(v);
                }
            }

            /*for (int x = -100; x < 100; x++)
            for (int y = -100; y < 100; y++)
            {
                vertexData.Add(new Vertex
                {
                    Position = new float3(x, y, 0f),
                    Normal = new float3(0, 0f, -1.0f),
                    UV0 = (half4)float4.zero,
                    UV1 = (half4)new float4(0f, 0f, 0.0f, 1.0f),
                    AO = (half4)new float4(0f, 0f, 0f, 0f)
                });
            }*/

            mf.mesh.SetVertexBufferParams(vertexData.Count, VertexParams);

            mf.mesh.SetIndexBufferParams(vertexData.Count, IndexFormat.UInt16);

            mf.mesh.SetVertexBufferData(vertexData, 0, 0, vertexData.Count, 0,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices
                                                      | MeshUpdateFlags.DontResetBoneBounds);

            ushort[] indices = new ushort[vertexData.Count];
            for (ushort i = 0; i < vertexData.Count; i++)
            {
                indices[i] = i;
            }

            mf.mesh.SetIndices(indices, MeshTopology.Points, 0);

            mf.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        }
    }
}