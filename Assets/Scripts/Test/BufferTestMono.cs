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
            public byte LightDataAndAO;
            public byte padding1;
            public uint padding2;
            public uint padding3;
        }


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            MeshFilter mf = GetComponent<MeshFilter>();

            mf.mesh = new Mesh();

            List<Vertex> vertexData = new();

            for (int i = 0; i < 6; i++)
            {
                vertexData.Add(new Vertex
                {
                    Position = float3.zero,
                    QuadIndex = (ushort)i,
                    TextureIndex = 3,
                    LightDataAndAO = 0b0000_1111,
                });
            }
            
            for (int i = 0; i < 6; i++)
            {
                vertexData.Add(new Vertex
                {
                    Position = new float3(0,1,0),
                    QuadIndex = (ushort)i,
                    TextureIndex = 3,
                    LightDataAndAO = 0b1111_0000,
                });
            }
            
            for (int i = 0; i < 6; i++)
            {
                vertexData.Add(new Vertex
                {
                    Position = new float3(0,2,0),
                    QuadIndex = (ushort)i,
                    TextureIndex = 3,
                    LightDataAndAO = 0b1111,
                });
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

            mf.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
        }
    }
}