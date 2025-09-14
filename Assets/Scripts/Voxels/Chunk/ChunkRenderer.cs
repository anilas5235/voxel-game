using System.Linq;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using Voxels.MeshGeneration;
using static Voxels.VoxelWorld;


namespace Voxels.Chunk
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter), typeof(MeshCollider))]
    public class ChunkRenderer : MonoBehaviour
    {
        public bool showGizmos;
        private Mesh _mesh;
        private MeshCollider _meshCollider;
        private MeshFilter _meshFilter;
        private ChunkData ChunkData { get; set; }

        public bool Modified => ChunkData.modified;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();

            _mesh = _meshFilter.mesh;
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

        public void Initialize(ChunkData chunkData)
        {
            ChunkData = chunkData;
        }

        private void RenderMesh(MeshData meshData)
        {
            if (meshData == null) return;

            _mesh.Clear();

            _mesh.subMeshCount = 2;
            _mesh.vertices = meshData.Vertices.Concat(meshData.WaterMeshData.Vertices).ToArray();

            _mesh.SetTriangles(meshData.Triangles.ToArray(), 0);
            _mesh.SetTriangles(meshData.WaterMeshData.Triangles.Select(val => val + meshData.Vertices.Count).ToArray(),
                1);

            _mesh.SetUVs(0, meshData.UV.Concat(meshData.WaterMeshData.UV).ToArray());
            _mesh.RecalculateNormals();

            _meshCollider.sharedMesh = null;
            Mesh colliderMesh = new()
            {
                vertices = meshData.ColliderVertices.ToArray(),
                triangles = meshData.ColliderTriangles.ToArray()
            };
            colliderMesh.RecalculateNormals();
            _meshCollider.sharedMesh = colliderMesh;
        }

        public void UpdateChunk()
        {
            if (!ChunkData.dirty) return;
            MeshData meshData = GreedyMesher.Run(ChunkData);
            RenderMesh(meshData);
            ChunkData.dirty = false;
        }
    }
}