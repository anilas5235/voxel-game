using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProceduralMeshes.Streams
{
    public struct SingleStream : IMeshStreams
    {
        [NativeDisableContainerSafetyRestriction]
        private NativeArray<Vertex> _vertices;

        [NativeDisableContainerSafetyRestriction]
        private NativeArray<TriangleUInt16> _triangles;

        public void Setup(
            Mesh.MeshData meshData, Bounds bounds, int vertexCount, int indexCount
        )
        {
            NativeArray<VertexAttributeDescriptor> descriptor = new(
                4, Allocator.Temp, NativeArrayOptions.UninitializedMemory
            )
            {
                [0] = new VertexAttributeDescriptor(VertexAttribute.Position, dimension: 3),
                [1] = new VertexAttributeDescriptor(VertexAttribute.Normal, dimension: 3),
                [2] = new VertexAttributeDescriptor(VertexAttribute.Tangent, dimension: 4),
                [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 4)
            };
            meshData.SetVertexBufferParams(vertexCount, descriptor);
            descriptor.Dispose();

            meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt16);

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(
                0, new SubMeshDescriptor(0, indexCount)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                },
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices
            );

            _vertices = meshData.GetVertexData<Vertex>();
            _triangles = meshData.GetIndexData<ushort>().Reinterpret<TriangleUInt16>(2);
        }

        public void SetVertex(int index, Vertex vertex) => _vertices[index] = vertex; 

        public void SetTriangle(int index, int3 triangle) => _triangles[index] = triangle;
    }
}