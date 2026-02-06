using Runtime.Engine.Settings;
using UnityEngine;

namespace Runtime.Engine.Behaviour
{
    /// <summary>
    /// MonoBehaviour representation of a chunk with a dedicated render mesh and collider mesh.
    /// </summary>
    public class ChunkBehaviour : MonoBehaviour
    {
        [SerializeField] private ChunkPartition[] chunkPartitions;

        [SerializeField] private Material solidMaterial;
        [SerializeField] private Material transparentMaterial;

        /// <summary>
        /// Initializes renderer-specific options (e.g. shadow casting) from settings.
        /// </summary>
        public void Init(RendererSettings settings)
        {
            for (int pId = 0; pId < chunkPartitions.Length; pId++)
            {
                ChunkPartition partition = chunkPartitions[pId];
                partition.Init(settings, pId);
            }
        }

        public void ClearData()
        {
            foreach (ChunkPartition partition in chunkPartitions)
            {
                partition.Clear();
            }
        }

        private void FixedUpdate()
        {
            UpdatePartitionsRenderStatus();
        }

        private void UpdatePartitionsRenderStatus()
        {
            foreach (ChunkPartition partition in chunkPartitions)
            {
                partition.UpdateRenderStatus();
            }
        }

        public ChunkPartition GetPartition(int y) => chunkPartitions[y];
    }
}