using System.Collections.Generic;
using UnityEngine;

namespace Voxels.Data
{
    public class MeshData
    {
        public List<Vector3> Vertices { get; } = new();
        public List<int> Triangles { get; } = new();
        public List<Vector2> UV { get; } = new();

        public List<Vector3> ColliderVertices { get; } = new();
        public List<int> ColliderTriangles { get; } = new();

        public MeshData WaterMeshData { get; }
        private bool _isMainMesh;

        public MeshData(bool isMainMesh = true)
        {
            this._isMainMesh = isMainMesh;
            if (isMainMesh)
            {
                WaterMeshData = new MeshData(false);
            }
        }

        public void AddVertex(Vector3 vertex, bool collision)
        {
            Vertices.Add(vertex);
            if (collision)
            {
                ColliderVertices.Add(vertex);
            }
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
    }
}