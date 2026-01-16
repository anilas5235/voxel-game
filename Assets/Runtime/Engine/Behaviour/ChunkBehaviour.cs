using System.Collections.Generic;
using Runtime.Engine.Settings;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Behaviour
{
    /// <summary>
    /// MonoBehaviour representation of a chunk with a dedicated render mesh and collider mesh.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class ChunkBehaviour : MonoBehaviour
    {
        [SerializeField] private ChunkPartition[] chunkPartitions;
        public ChunkPartition[] ChunkPartitions => chunkPartitions;
        
        /// <summary>
        /// Initializes renderer-specific options (e.g. shadow casting) from settings.
        /// </summary>
        public void Init(RendererSettings settings)
        {
            for (int pId = 0; pId < chunkPartitions.Length; pId++)
            {
                ChunkPartition partition = chunkPartitions[pId];
                partition.Init(settings,pId);
            }
        }

        public void ClearData()
        {
            foreach (var partition in chunkPartitions)
            {
                partition.Mesh.Clear();
                partition.ColliderMesh.Clear();
                partition.Collider.sharedMesh = null;
            }
        }

        public IEnumerable<KeyValuePair<int3, ChunkPartition>> GetMap(int2 position)
        {
            foreach (ChunkPartition partition in chunkPartitions)
            {
                int3 chunkPos = new(position.x, partition.PartitionId * 16, position.y);
                yield return new KeyValuePair<int3, ChunkPartition>(chunkPos, partition);
            }
        }
    }
 

    public class ChunkPartition : MonoBehaviour
    {
        private MeshRenderer _renderer;
        [SerializeField] private MeshCollider _Collider;
        /// <summary>
        /// Mesh used for visual rendering.
        /// </summary>
        public Mesh Mesh { get; private set; }
        /// <summary>
        /// Dedicated mesh for collider (not shared with render mesh).
        /// </summary>
        public Mesh ColliderMesh { get; private set; }
        /// <summary>
        /// Access to the underlying MeshCollider.
        /// </summary>
        public MeshCollider Collider => _Collider;
        
        public short PartitionId{get; private set;}

        private void Awake()
        {
            Mesh = GetComponent<MeshFilter>().mesh;
            _renderer = GetComponent<MeshRenderer>();
            ColliderMesh = new Mesh { name = "ChunkCollider" };
        }
        
        public void Init(RendererSettings settings, int pId)
        {
            PartitionId = (short)pId;
            transform.localPosition = new Vector3(0, 16 * pId, 0);
            if (!settings.CastShadows) _renderer.shadowCastingMode = ShadowCastingMode.Off;
        } 
    }
}