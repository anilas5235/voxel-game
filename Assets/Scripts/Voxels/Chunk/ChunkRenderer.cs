using System.Linq;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using Utils;
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
            if (!showGizmos || !Application.isPlaying) return;

            Gizmos.color = Selection.activeGameObject == gameObject
                ? new Color(0, 1, 0, .4f)
                : new Color(1, 0, 1, .4f);

            Gizmos.DrawCube(transform.position + HalfChunkSize.GetVector3()
                , ChunkSize.GetVector3());
        }
#endif

        public void Initialize(ChunkData chunkData)
        {
            ChunkData = chunkData;
        }
    }
}