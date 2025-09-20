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
        internal Mesh _mesh;
        internal MeshCollider _meshCollider;
        private MeshFilter _meshFilter;
        internal ChunkData ChunkData { get; set; }

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
    }
}