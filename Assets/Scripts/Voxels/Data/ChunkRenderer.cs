using System.Linq;
using UnityEditor;
using UnityEngine;
using static Voxels.Data.VoxelWorld;


namespace Voxels.Data
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter), typeof(MeshCollider))]
    public class ChunkRenderer : MonoBehaviour
    {
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;
        private Mesh mesh;

        public bool showGizmos;
        public ChunkData ChunkData { get; private set; }

        public bool Modified
        {
            get => ChunkData.modified;
            set => ChunkData.modified = value;
        }

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshCollider = GetComponent<MeshCollider>();

            mesh = meshFilter.mesh;
        }

        public void Initialize(ChunkData chunkData)
        {
            ChunkData = chunkData;
        }

        private void RenderMesh(MeshData meshData)
        {
            if (meshData == null) return;

            mesh.Clear();

            mesh.subMeshCount = 2;
            mesh.vertices = meshData.Vertices.Concat(meshData.WaterMeshData.Vertices).ToArray();

            mesh.SetTriangles(meshData.Triangles.ToArray(), 0);
            mesh.SetTriangles(meshData.WaterMeshData.Triangles.Select(val => val + meshData.Vertices.Count).ToArray(),
                1);

            mesh.uv = meshData.UV.Concat(meshData.WaterMeshData.UV).ToArray();
            mesh.RecalculateNormals();

            meshCollider.sharedMesh = null;
            Mesh colliderMesh = new()
            {
                vertices = meshData.ColliderVertices.ToArray(),
                triangles = meshData.ColliderTriangles.ToArray()
            };
            colliderMesh.RecalculateNormals();
            meshCollider.sharedMesh = colliderMesh;
        }

        public void UpdateChunk()
        {
            RenderMesh(Chunk.GetChunkMeshData(ChunkData));
        }

        public void UpdateChunk(MeshData meshData)
        {
            RenderMesh(meshData);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showGizmos || !Application.isPlaying || ChunkData == null) return;

            Gizmos.color = Selection.activeGameObject == gameObject
                ? new Color(0, 1, 0, .4f)
                : new Color(1, 0, 1, .4f);

            Gizmos.DrawCube(transform.position +
                            new Vector3(HalfChunkSize, HalfChunkHeight, HalfChunkSize)
                , new Vector3(ChunkSize, ChunkHeight, ChunkSize));
        }
#endif
    }
}