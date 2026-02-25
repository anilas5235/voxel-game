using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            public float3 Position;
            private uint Quad;

            internal uint Extra;

            /*public ushort QuadIndex;
            public ushort padding;
            public ushort TextureIndex;
            private byte LightData;
            public byte AOData;*/
            public uint padding2;
            public uint padding3;

            public Vertex(float3 position, ushort quadIndex, ushort textureIndex, byte light, byte ao)
            {
                Position = position;
                /*QuadIndex = quadIndex;
                padding = 0;
                TextureIndex = textureIndex;
                LightData = 0;
                AOData = ao;*/
                Quad = 0;
                Extra = 0;
                padding2 = 0;
                padding3 = 0;

                SetQuadIndex(quadIndex);
                SetTextureIndex(textureIndex);
                SetLight(light);
                SetAO(ao);
            }

            public void SetQuadIndex(ushort quadIndex) => Quad |= quadIndex;

            public void SetTextureIndex(ushort textureIndex) => Extra |= textureIndex;

            public void SetLight(byte sunlight) => Extra |= ((uint)sunlight & 0b1111) << 16;

            public void SetAO(byte ao) => Extra |= ((uint)ao) << 24;
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            MeshFilter mf = GetComponent<MeshFilter>();

            mf.mesh = new Mesh();

            List<Vertex> vertexData = new();

            for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                Vertex v = new()
                {
                    Position = new float3(2 * x, y, 5),
                };
                v.SetQuadIndex((ushort)4);
                v.SetTextureIndex(1);
                v.SetLight(0b1111);

                v.SetAO((byte)((1 << x) | (1 << y)));
                vertexData.Add(v);
                
            }
            
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