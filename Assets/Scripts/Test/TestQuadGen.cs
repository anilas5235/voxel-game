using System.Collections.Generic;
using UnityEngine;

namespace Test
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class TestQuadGen : MonoBehaviour
    {
        private MeshFilter meshFilter;
        private Mesh mesh;

        public List<Vector2Int> quadSizes = new()
        {
            new Vector2Int(1, 1),
            new Vector2Int(2, 1),
            new Vector2Int(1, 2),
            new Vector2Int(2, 2)
        };
        
        // Add UVs (normalized coordinates)
        private readonly Vector2[] UVs = {
            new(0, 0), // Bottom left
            new(1, 0), // Bottom right
            new(0, 1), // Top left
            new(1, 1) // Top right
        };
        
        public int selectedTex = 1;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            mesh = meshFilter.mesh;
        }

        private void Start()
        {
            GenerateMesh();
        }

        private void UpdateMesh()
        {
            mesh.Clear();

            List<Vector3> vertices = new();
            List<int> triangles = new();
            List<Vector3> uvs = new();

            float xOffset = 0;

            // Generate each quad based on the sizes in the list
            foreach (Vector2Int quadSize in quadSizes)
            {
                int width = quadSize.x;
                int height = quadSize.y;

                // Get the starting vertex index for this quad
                int vertexIndex = vertices.Count;

                // Add the 4 vertices for this quad
                vertices.Add(new Vector3(xOffset, 0, 0)); // Bottom left
                vertices.Add(new Vector3(xOffset + width, 0, 0)); // Bottom right
                vertices.Add(new Vector3(xOffset, height, 0)); // Top left
                vertices.Add(new Vector3(xOffset + width, height, 0)); // Top right

                // Add the 2 triangles (6 indices) that make up the quad
                triangles.Add(vertexIndex); // Bottom left
                triangles.Add(vertexIndex + 2); // Top left
                triangles.Add(vertexIndex + 3); // Top right

                triangles.Add(vertexIndex); // Bottom left
                triangles.Add(vertexIndex + 3); // Top right
                triangles.Add(vertexIndex + 1); // Bottom right
                
                foreach (Vector2 uv in UVs)
                {
                    Vector2 u = uv * quadSize;
                    uvs.Add(new Vector3(u.x,u.y,selectedTex));
                }

                // Move the x offset for the next quad
                xOffset += width + 0.5f; // Add a small gap between quads
            }

            // Assign the mesh data
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.SetUVs(0, uvs);

            // Recalculate mesh properties
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // Add a method to call UpdateMesh from the inspector or other scripts
        public void GenerateMesh()
        {
            UpdateMesh();
        }
    }
}