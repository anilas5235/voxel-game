using System.Collections.Generic;
using System.Runtime.InteropServices;
using Runtime.Engine.Utils.Extensions;
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
            new VertexAttributeDescriptor(VertexAttribute.Normal),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UInt8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float16, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float16, 4)
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vertex
        {
            public float3 Position; // xyz = position, w = 0 (unused could hold extra data)
            public float3 Normal; // xyz = normal, w = 0 (unused could hold extra data)
            public uint UV0; // xy = Sized UV for texture atlas, zw = normalized UV for shader sampling
            public half4 UV1; // x = texture ID, y = depth fade factor, z = unused, w = sunlight level
            public half4 AO; // xyzw = AO values 
        }


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            MeshFilter mf = GetComponent<MeshFilter>();

            mf.mesh = new Mesh();

            List<Vertex> vertexData = new()
            {
                new Vertex
                {
                    Position = new float3(1f, 0.0f, 1.0f),
                    Normal = new float3(1.0f, 0.0f, 0.0f),
                    UV0 = uint.MaxValue,
                    UV1 = (half4)new float4(0.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1f)
                },
                new Vertex
                {
                    Position = new float3(0.0f, 0.0f, 0.0f),
                    Normal = new float3(-1.0f, 0.0f, 0.0f),
                    UV1 = (half4)new float4(0.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(0.0f, 1.0f, 1.0f, 1f)
                },
                new Vertex
                {
                    Position = new float3(0.0f, 1.0f, 0.0f),
                    Normal = new float3(0.0f, 1.0f, 0.0f),
                    UV1 = (half4)new float4(2.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1f)
                },
                new Vertex
                {
                    Position = new float3(0.0f, 0.0f, 1.0f),
                    Normal = new float3(0.0f, -1.0f, 0.0f),
                    UV1 = (half4)new float4(2.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1f)
                },
                new Vertex
                {
                    Position = new float3(0.0f, 0.0f, 1.0f),
                    Normal = new float3(0.0f, 0.0f, 1.0f),
                    UV1 = (half4)new float4(4.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1f)
                },
                new Vertex
                {
                    Position = new float3(1.0f, 0.0f, 0.0f),
                    Normal = new float3(0.0f, 0.0f, -1.0f),
                    UV1 = (half4)new float4(4.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1f)
                },

                new Vertex
                {
                    Position = new float3(2f, 0.0f, 0.0f),
                    Normal = new float3(1.0f, 0.0f, 0.0f),
                    UV1 = (half4)new float4(0.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1f)
                },
                new Vertex
                {
                    Position = new float3(2.0f, 0.0f, 0.0f),
                    Normal = new float3(-1.0f, 0.0f, 0.0f),
                    UV1 = (half4)new float4(0.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(0.0f, 1.0f, 1.0f, 1f)
                },
                new Vertex
                {
                    Position = new float3(2.0f, 0.0f, 0.0f),
                    Normal = new float3(0.0f, 1.0f, 0.0f),
                    UV1 = (half4)new float4(2.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1f)
                },
                new Vertex
                {
                    Position = new float3(2.0f, 0.0f, 0.0f),
                    Normal = new float3(0.0f, -1.0f, 0.0f),
                    UV1 = (half4)new float4(2.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1f)
                },
                new Vertex
                {
                    Position = new float3(2.0f, 0.0f, 0.0f),
                    Normal = new float3(0.0f, 0.0f, 1.0f),
                    UV1 = (half4)new float4(4.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1f)
                },
                new Vertex
                {
                    Position = new float3(2.0f, 0.0f, 0.0f),
                    Normal = new float3(0.0f, 0.0f, -1.0f),
                    UV1 = (half4)new float4(4.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1f)
                },
                new Vertex
                {
                    Position = new float3(5.0f, 0.0f, 0.0f),
                    Normal = new float3(0.0f, 1.0f, -1.0f).Normalized(),
                    UV1 = (half4)new float4(8.0f, 0f, 0.0f, 15f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1f)
                }
            };

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