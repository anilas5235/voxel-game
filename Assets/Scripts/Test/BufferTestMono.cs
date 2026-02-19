using System.Collections.Generic;
using Unity.Collections;
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
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float16, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float16, 4)
        };

        public struct Vertex
        {
            public half4 Position; // xyz = position, w = 0 (unused could hold extra data)
            public half4 Normal; // xyz = normal, w = 0 (unused could hold extra data)
            public half4 UV0; // xy = Sized UV for texture atlas, zw = normalized UV for shader sampling
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
                    Position = (half4)new float4(0.0f, 0.0f, 0.0f, 0.0f),
                    Normal = (half4)new float4(0.0f, 1.0f, 0.0f, 0.0f),
                    UV0 = (half4)new float4(0f, 0f, 0.0f, 0.0f),
                    UV1 = (half4)new float4(1.0f, 0f, 0.0f, 1.0f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1.0f)
                },
                new Vertex
                {
                    Position = (half4)new float4(1.0f, 0.0f, 0.0f, 0.0f),
                    Normal = (half4)new float4(1.0f, 0f, 0.0f, 0.0f),
                    UV0 = (half4)new float4(1f, 0f, 0.0f, 0.0f),
                    UV1 = (half4)new float4(1.0f, 0f, 0.0f, 1.0f),
                    AO = (half4)new float4(1.0f, 1.0f, 1.0f, 1.0f)
                }
            };

            mf.mesh.SetVertexBufferParams(vertexData.Count, VertexParams);

            mf.mesh.SetIndexBufferParams(vertexData.Count, IndexFormat.UInt16);

            mf.mesh.SetVertexBufferData(vertexData, 0, 0, vertexData.Count, 0, MeshUpdateFlags.DontRecalculateBounds);

            ushort[] indices = new ushort[vertexData.Count];
            for (ushort i = 0; i < vertexData.Count; i++)
            {
                indices[i] = i;
            }

            mf.mesh.SetIndices(indices, MeshTopology.Points, 0);

            mf.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10.0f);
        }
    }
}