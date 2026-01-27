using System.Collections.Generic;
using Runtime.Engine.Mesher;
using Runtime.Engine.Settings;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Behaviour
{
    /// <summary>
    /// MonoBehaviour representation of a chunk with a dedicated render mesh and collider mesh.
    /// </summary>
    public class ChunkBehaviour : MonoBehaviour
    {
        [SerializeField] private ChunkPartition[] chunkPartitions;

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

        public ChunkPartition GetPartition(int y) => chunkPartitions[y];

        public void CombineMeshes()
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
            List<CombineInstance> combineInstances = new();
            int totalVertexCount = 0;

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter.gameObject == gameObject) continue;

                Mesh mesh = meshFilter.sharedMesh;
                if (!mesh || !mesh.isReadable || mesh.vertexCount < 2) continue;

                totalVertexCount += mesh.vertexCount;
                combineInstances.Add(new CombineInstance
                {
                    mesh = mesh,
                    transform = transform.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix,
                    subMeshIndex = 0,
                });

                meshFilter.gameObject.SetActive(false);
            }

            if (combineInstances.Count == 0) return;

            Mesh combinedMesh = new();
            var vertexParams = new NativeArray<VertexAttributeDescriptor>(5, Allocator.Persistent)
            {
                // Int interpolation cause issues
                [0] = new VertexAttributeDescriptor(VertexAttribute.Position),
                [1] = new VertexAttributeDescriptor(VertexAttribute.Normal),
                [2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
                [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
                [4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4)
            };
            combinedMesh.SetVertexBufferParams(totalVertexCount, vertexParams);
            
            CombineInstance[] instances = combineInstances.ToArray();
            combinedMesh.CombineMeshes(instances, true, false);
            combinedMesh.RecalculateBounds();
            gameObject.GetComponent<MeshFilter>().sharedMesh = combinedMesh;
        }
    }

    [CustomEditor(typeof(ChunkBehaviour))]

    public class ChunkBehaviourCustomEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ChunkBehaviour chunkBehaviour = (ChunkBehaviour)target;
            if (GUILayout.Button("Combine Meshes"))
            {
                chunkBehaviour.CombineMeshes();
            }
        }
        
    }
}