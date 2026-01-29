using System.Collections.Generic;
using Runtime.Engine.Mesher;
using Runtime.Engine.Settings;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Behaviour
{
    /// <summary>
    /// MonoBehaviour representation of a chunk with a dedicated render mesh and collider mesh.
    /// </summary>
    public class ChunkBehaviour : MonoBehaviour
    {
        private static readonly Mesh.MeshData EmptyMeshData = new();

        private static readonly NativeArray<VertexAttributeDescriptor> VertexParams = new(5, Allocator.Persistent)
        {
            [0] = new VertexAttributeDescriptor(VertexAttribute.Position),
            [1] = new VertexAttributeDescriptor(VertexAttribute.Normal),
            [2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
            [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            [4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4)
        };

        [SerializeField] private ChunkPartition[] chunkPartitions;
        private readonly Mesh.MeshData[] _meshData = new Mesh.MeshData[PartitionHeight];

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

        public void CombineMeshesManual()
        {
            List<(int, Mesh.MeshData)> meshDataList = new();
            int totalVertices = 0;
            int totalSIndices = 0;
            int totalTIndices = 0;

            for (int i = 0; i < _meshData.Length; i++)
            {
                Mesh.MeshData data = _meshData[i];
                if (EmptyMeshData.Equals(data) || data.vertexCount < 2) continue;
                meshDataList.Add((i, data));
                totalVertices += data.vertexCount;
                totalSIndices += data.GetSubMesh(0).indexCount;
                totalTIndices += data.GetSubMesh(1).indexCount;
            }

            // 2. Allocate NativeArrays for the new mesh data
            NativeArray<Vertex> allVertices = new(totalVertices, Allocator.Temp);
            NativeArray<int> allSIndices = new(totalSIndices, Allocator.Temp);
            NativeArray<int> allTIndices = new(totalTIndices, Allocator.Temp);

            int vertexOffset = 0;
            int indexSOffset = 0;
            int indexTOffset = 0;

            foreach ((int, Mesh.MeshData) data in meshDataList)
            {
                Matrix4x4 localToTarget = Matrix4x4.Translate(new Vector3(0, data.Item1 * PartitionHeight, 0));

                // Get source data
                NativeArray<Vertex> sourceVertices = data.Item2.GetVertexData<Vertex>();
                NativeArray<int> sourceIndices = data.Item2.GetIndexData<int>();
                SubMeshDescriptor subMesh0 = data.Item2.GetSubMesh(0);
                SubMeshDescriptor subMesh1 = data.Item2.GetSubMesh(1);

                // 3. Copy and Transform Vertices
                for (int i = 0; i < sourceVertices.Length; i++)
                {
                    Vertex v = sourceVertices[i];
                    // Manually apply the matrix to Position and Normal
                    v.Position = localToTarget.MultiplyPoint3x4(v.Position);

                    // UVs (Custom Attributes) are copied exactly as they are
                    allVertices[vertexOffset + i] = v;
                }

                // 4. Copy and Offset Indices
                for (int i = 0; i < subMesh0.indexCount; i++)
                {
                    int originalIndex = sourceIndices[subMesh0.indexStart + i];
                    allSIndices[indexSOffset + i] = originalIndex + vertexOffset;
                }

                for (int i = 0; i < subMesh1.indexCount; i++)
                {
                    int originalIndex = sourceIndices[subMesh1.indexStart + i];
                    allTIndices[indexTOffset + i] = originalIndex + vertexOffset;
                }

                vertexOffset += sourceVertices.Length;
                indexSOffset += subMesh0.indexCount;
                indexTOffset += subMesh1.indexCount;

                sourceVertices.Dispose();
                sourceIndices.Dispose();
            }

            // 5. Apply to the final mesh
            Mesh combinedMesh = new();
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData combinedMeshData = meshDataArray[0];
            combinedMeshData.SetVertexBufferParams(totalVertices, VertexParams);
            combinedMeshData.GetVertexData<Vertex>().CopyFrom(allVertices);
            combinedMeshData.SetIndexBufferParams(totalSIndices + totalTIndices, IndexFormat.UInt32);
            NativeArray<int> combinedIndexBuffer = combinedMeshData.GetIndexData<int>();
            if (totalSIndices > 0)
                NativeArray<int>.Copy(allSIndices, 0, combinedIndexBuffer, 0, totalSIndices);
            if (totalTIndices > 0)
                NativeArray<int>.Copy(allTIndices, 0, combinedIndexBuffer, totalSIndices, totalTIndices);
            combinedMeshData.subMeshCount = 2;
            SubMeshDescriptor descriptor0 = new(0, totalSIndices);
            SubMeshDescriptor descriptor1 = new(totalSIndices, totalTIndices);
            combinedMeshData.SetSubMesh(0, descriptor0, MeshFlags);
            combinedMeshData.SetSubMesh(1, descriptor1, MeshFlags);
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, combinedMesh, MeshFlags);
            combinedMesh.RecalculateBounds();
            
            GetComponent<MeshFilter>().sharedMesh = combinedMesh;
            GetComponent<MeshRenderer>().enabled = true;

            allVertices.Dispose();
            allSIndices.Dispose();
            allTIndices.Dispose();
        }

        public void SetMeshData(int index, Mesh.MeshData meshData)
        {
            _meshData[index] = meshData;
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
                chunkBehaviour.CombineMeshesManual();
            }
        }
    }
}