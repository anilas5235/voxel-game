using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ProceduralMeshes;
using static Voxels.VoxelWorld; // For Vertex

namespace Voxels.MeshGeneration
{
    public class MeshData
    {
        private bool _isMainMesh;

        public MeshData(bool isMainMesh = true)
        {
            _isMainMesh = isMainMesh;
            if (isMainMesh) WaterMeshData = new MeshData(false);
        }

        public List<Vertex> Vertices { get; } = new();
        public List<int> Triangles { get; } = new();
        public List<Vertex> ColliderVertices { get; } = new();
        public List<int> ColliderTriangles { get; } = new();

        public MeshData WaterMeshData { get; set; }

        public void AddVertex(Vector3 position, Vector3 normal, Vector4 tangent, Vector4 uv, bool collision)
        {
            var vertex = new Vertex
            {
                Position = position,
                Normal = normal,
                Tangent = tangent,
                UV0 = uv
            };
            Vertices.Add(vertex);
            if (collision) ColliderVertices.Add(vertex);
        }

        public void AddQuadTriangles(bool collision)
        {
            // Add two triangles for a quad
            Triangles.Add(Vertices.Count - 4);
            Triangles.Add(Vertices.Count - 3);
            Triangles.Add(Vertices.Count - 2);

            Triangles.Add(Vertices.Count - 4);
            Triangles.Add(Vertices.Count - 2);
            Triangles.Add(Vertices.Count - 1);

            if (!collision) return;
            // Add the same triangles to the collider mesh
            ColliderTriangles.Add(ColliderVertices.Count - 4);
            ColliderTriangles.Add(ColliderVertices.Count - 3);
            ColliderTriangles.Add(ColliderVertices.Count - 2);

            ColliderTriangles.Add(ColliderVertices.Count - 4);
            ColliderTriangles.Add(ColliderVertices.Count - 2);
            ColliderTriangles.Add(ColliderVertices.Count - 1);
        }

        public void WriteTo(Mesh.MeshData meshData)
        {
            if (!_isMainMesh) return;
            meshData.subMeshCount = 2;

            // Use fixed chunk bounds to avoid culling issues
            const int ChunkSize = 16; // from VoxelWorld
            const int ChunkHeight = 256; // from VoxelWorld
            Bounds bounds = new Bounds(
                new Vector3(ChunkSize / 2f, ChunkHeight / 2f, ChunkSize / 2f),
                new Vector3(ChunkSize, ChunkHeight, ChunkSize)
            );

            meshData.SetVertexBufferParams(
                Vertices.Count + WaterMeshData.Vertices.Count,
                new[]
                {
                    new VertexAttributeDescriptor(VertexAttribute.Position, dimension: 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, dimension: 3),
                    new VertexAttributeDescriptor(VertexAttribute.Tangent, dimension: 4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 4)
                }
            );

            var vertexData = meshData.GetVertexData<Vertex>();
            for (int i = 0; i < Vertices.Count; i++)
                vertexData[i] = Vertices[i];
            for (int i = 0; i < WaterMeshData.Vertices.Count; i++)
                vertexData[i + Vertices.Count] = WaterMeshData.Vertices[i];

            meshData.SetIndexBufferParams(Triangles.Count + WaterMeshData.Triangles.Count, IndexFormat.UInt32);
            var indexData = meshData.GetIndexData<int>();
            for (int i = 0; i < Triangles.Count; i++)
                indexData[i] = Triangles[i];
            for (int i = 0; i < WaterMeshData.Triangles.Count; i++)
                indexData[i + Triangles.Count] = WaterMeshData.Triangles[i] + Vertices.Count;

            meshData.SetSubMesh(
                0,
                new SubMeshDescriptor(0, Triangles.Count)
                {
                    vertexCount = Vertices.Count + WaterMeshData.Vertices.Count,
                    bounds = bounds
                },
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices
            );

            meshData.SetSubMesh(
                1,
                new SubMeshDescriptor(Triangles.Count, WaterMeshData.Triangles.Count)
                {
                    vertexCount = Vertices.Count + WaterMeshData.Vertices.Count,
                    bounds = bounds
                },
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices
            );
        }


        // Helper to calculate normal from direction
        public static Vector3 GetNormalForDirection(Direction direction)
        {
            return direction.GetVector();
        }

        // Helper to calculate tangent for a face
        public static Vector4 GetTangentForDirection(Direction direction)
        {
            // Tangent is a vector along the U axis of the face, with w = 1
            switch (direction)
            {
                case Direction.Forward:
                case Direction.Backward:
                    return new Vector4(1, 0, 0, 1); // X axis
                case Direction.Left:
                case Direction.Right:
                    return new Vector4(0, 0, -1, 1); // -Z axis
                case Direction.Up:
                case Direction.Down:
                    return new Vector4(1, 0, 0, 1); // X axis
                default:
                    return new Vector4(1, 0, 0, 1);
            }
        }
    }
}