using System.Collections;
using System.Collections.Generic;
using ProceduralMeshes;
using ProceduralMeshes.Generators;
using ProceduralMeshes.Streams;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Voxels;
using Voxels.Chunk;

namespace Test
{
    public class SpawnChunk : MonoBehaviour
    {
        public GameObject chunkPrefab;
        public Vector2Int chunkPosition;
        
        [SerializeField, Range(1, 256)] private int resolution = 10;

        private MeshFilter meshFilter;

        private Coroutine coroutine;
        private Mesh theMesh;

        private void Start()
        {
            if (chunkPrefab)
            {
                Vector3 worldPosition = new Vector3(chunkPosition.x * 16, 0, chunkPosition.y * 16);
                var ch = Instantiate(chunkPrefab, worldPosition, Quaternion.identity);
                meshFilter = ch.GetComponent<MeshFilter>();
                theMesh = new Mesh
                {
                    name = "Procedural Mesh",
                };
                meshFilter.sharedMesh = theMesh;
                
                if (coroutine != null)
                {
                    return;
                }
                
                ChunkData chunkData = new ChunkData(VoxelWorld.Instance, new Vector2Int(chunkPosition.x, chunkPosition.y));
                chunkData.SetVoxel(Vector3Int.one, 1);

                coroutine = StartCoroutine(GenerateMesh(theMesh, chunkData));
            }
            else
            {
                Debug.LogError("Chunk Prefab is not assigned.");
            }
        }
        
        private IEnumerator GenerateMesh(Mesh mesh, ChunkData chunkData)
        {
            var startTime = Time.realtimeSinceStartupAsDouble;
            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];

            JobHandle jobHandel = MeshJob<ChunkGen, SingleStream>.Schedule(
                mesh, meshData, new ChunkGen()
                {
                    Resolution = resolution,
                    VoxelGrid = chunkData.voxels
                    
                }, default
            );
            var prepFinishTime = Time.realtimeSinceStartupAsDouble;
            yield return new WaitUntil(() => jobHandel.IsCompleted);
            var jobFinishTime = Time.realtimeSinceStartupAsDouble;
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontResetBoneBounds);
            coroutine = null;
            var finishTime = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"Prep Time: {(prepFinishTime - startTime) * 1000} ms, " +
                      $"Job Time: {(jobFinishTime - prepFinishTime) * 1000} ms, " +
                      $"Post Job Time: {(finishTime - jobFinishTime) * 1000} ms, " +
                      $"Time on Main Thread: {((prepFinishTime - startTime) + (finishTime - jobFinishTime)) * 1000} ms" +
                      $"Total Time: {(finishTime - startTime) * 1000} ms");
        }
        
    }
}