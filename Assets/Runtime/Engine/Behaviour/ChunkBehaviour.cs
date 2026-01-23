using System.Collections.Generic;
using Runtime.Engine.Settings;
using Unity.Mathematics;
using UnityEngine;
using static Runtime.Engine.Utils.VoxelConstants;

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
            foreach (ChunkPartition partition in chunkPartitions)
            {
                partition.Mesh.Clear();
                partition.ColliderMesh.Clear();
                partition.Collider.sharedMesh = null;
                partition.UpdateRenderStatus();
            }
        }

        public IEnumerable<KeyValuePair<int3, ChunkPartition>> GetMap(int2 position)
        {
            foreach (ChunkPartition partition in chunkPartitions)
            {
                int3 chunkPos = new(position.x, partition.PartitionId, position.y);
                yield return new KeyValuePair<int3, ChunkPartition>(chunkPos, partition);
            }
        }

        public void UpdatePartitionsRenderStatus()
        {
            foreach (ChunkPartition partition in chunkPartitions)
            {
                partition.UpdateRenderStatus();
            }
        }
    }
}