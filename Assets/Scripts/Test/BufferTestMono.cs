using System.Collections.Generic;
using System.Runtime.InteropServices;
using Runtime.Engine.Jobs.Meshing;
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
                v.SetLight(15, Vertex.LightIndex.SunUL);

                v.SetAO((byte)((1 << x) | (1 << y)));
                vertexData.Add(v);
            }
            
            for (int x = 15; x > 0; x--)
            {
                Vertex v = new(new float3(15-x, 0, 0), 2 ,1);
                v.SetLight((byte)x, Vertex.LightIndex.SunUL);
                v.SetLight((byte)(x-1), Vertex.LightIndex.SunUR);
                v.SetLight((byte)(x-1), Vertex.LightIndex.SunDR);
                v.SetLight((byte)(x), Vertex.LightIndex.SunDL);
                vertexData.Add(v);
            }
            for (int x = 15; x > 0; x--)
            {
                Vertex v = new(new float3(15-x, 0, 1), 2 ,1);
                v.SetLight((byte)(x-1), Vertex.LightIndex.SunUL);
                v.SetLight((byte)(x-1), Vertex.LightIndex.SunUR);
                v.SetLight((byte)(x), Vertex.LightIndex.SunDR);
                v.SetLight((byte)(x), Vertex.LightIndex.SunDL);
                vertexData.Add(v);
            }

            for (ushort i = 0; i < 6; i++)
            {
                Vertex v = new(new float3(0, 0, 4), i ,1);
                v.SetLight((byte)15, Vertex.LightIndex.SunUL);
                v.SetLight((byte)0, Vertex.LightIndex.SunUR);
                v.SetLight((byte)15, Vertex.LightIndex.SunDR);
                v.SetLight((byte)0, Vertex.LightIndex.SunDL);
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