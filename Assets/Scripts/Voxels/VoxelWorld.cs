using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using ProceduralMeshes;
using Unity.Mathematics;
using UnityEngine;
using Utils;
using Voxels.Chunk;
using Voxels.Generation;

namespace Voxels
{
    public class VoxelWorld : Singleton<VoxelWorld>, IDisposable
    {
        public static readonly int3 ChunkSize = new(16, 256, 16);
        public static readonly int3 HalfChunkSize = ChunkSize / 2;
        public static readonly int VoxelsPerChunk = ChunkSize.Size();

        public int waterThreshold = 50;
        public float noiseScale = 0.03f;
        public GameObject chunkPrefab;

        public Transform playerTransform;
        public int viewDistance = 2;

        public WorldData WorldData { get; private set; }

        private int2 lastPlayerChunkPos = new (int.MinValue, int.MinValue);
        private HashSet<int2> chunksBeingGenerated = new ();

        protected override void Awake()
        {
            base.Awake();
            WorldData = new WorldData
            {
                ChunkData = new Dictionary<int2, ChunkData>(),
                Chunks = new Dictionary<int2, ChunkRenderer>(),
                ChunkSize = ChunkSize
            };
        }

        private void Update()
        {
            if (playerTransform == null) return;
            Vector3 playerPos = playerTransform.position;
            int2 playerChunkPos = new(
                Mathf.FloorToInt(playerPos.x / ChunkSize.x),
                Mathf.FloorToInt(playerPos.z / ChunkSize.z)
            );
            if ((playerChunkPos != lastPlayerChunkPos).AndReduce())
            {
                lastPlayerChunkPos = playerChunkPos;
                UpdateChunksAroundPlayer(playerChunkPos);
            }
        }

        private ChunkRenderer GetOrAddChunkRenderer(ChunkData data, int2 chunkPos)
        {
            if (WorldData.Chunks.TryGetValue(chunkPos, out ChunkRenderer chunkRenderer)) return chunkRenderer;
            GameObject chunkObject = Instantiate(chunkPrefab, data.WorldPosition.GetVector3(), Quaternion.identity);
            chunkObject.name = chunkPos.ToString();
            chunkRenderer = chunkObject.GetComponent<ChunkRenderer>();
            chunkRenderer.Initialize(data);
            WorldData.Chunks.Add(chunkPos, chunkRenderer);
            return chunkRenderer;
        }

        private void ClearWorld()
        {
            WorldData.ChunkData.Clear();
            foreach (ChunkRenderer chunk in WorldData.Chunks.Values) Destroy(chunk.gameObject);
            WorldData.Chunks.Clear();
        }

        public bool GetVoxelFromWoldVoxPos(int3 voxelWorldPos, out ushort voxelId)
        {
            voxelId = 0;
            if (IsNotInYRange(voxelWorldPos.y)) return false;
            ChunkData chunk = GetChunkFrom(voxelWorldPos);
            voxelId = chunk.GetVoxel(GetVoxPosFromWorldVoxPos(chunk, voxelWorldPos));
            return true;
        }

        private static bool IsNotInYRange(int y)
        {
            return y < 0 || y >= ChunkSize.y;
        }

        public void SetVoxelFromWorldVoxPos(int3 voxelWorldPos, ushort voxelId)
        {
            if (IsNotInYRange(voxelWorldPos.y)) return;
            ChunkData chunk = GetChunkFrom(voxelWorldPos);
            ChunkUtils.SetVoxel(chunk, GetVoxPosFromWorldVoxPos(chunk, voxelWorldPos), voxelId);
        }

        public static int3 GetVoxPosFromWorldVoxPos(ChunkData chunkData, int3 voxelWorldPos)
        {
            return voxelWorldPos - chunkData.WorldPosition;
        }

        public static int3 GetVoxPosFromWorldVoxPos(int2 chunkPos, int3 voxelWorldPos)
        {
            return voxelWorldPos - new int3(chunkPos.x * ChunkSize.x, 0, chunkPos.y * ChunkSize.z);
        }

        internal ChunkData GetChunkFrom(int3 voxelWorldPos)
        {
            int2 pos = GetChunkPosition(voxelWorldPos);
            WorldData.ChunkData.TryGetValue(pos, out ChunkData data);
            return data;
        }

        public static int2 GetChunkPosition(int3 voxelWorldPos)
        {
            return new Vector2(voxelWorldPos.x / (float)ChunkSize.x, voxelWorldPos.z / (float)ChunkSize.z).Int2();
        }

        public static int2 GetChunkPosition(Vector3 worldPos)
        {
            return GetChunkPosition(worldPos.Int3());
        }

        public ChunkData GetChunkFrom(int2 voxelWorldPos)
        {
            WorldData.ChunkData.TryGetValue(voxelWorldPos, out ChunkData data);
            return data;
        }

        private void UpdateChunksAroundPlayer(int2 centerChunk)
        {
            HashSet<int2> neededChunks = new();
            List<int2> newChunkPositions = new();
            for (int x = -viewDistance; x <= viewDistance; x++)
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                int2 chunkPos = new(centerChunk.x + x, centerChunk.y + z);
                neededChunks.Add(chunkPos);
                if (!WorldData.ChunkData.ContainsKey(chunkPos) && !chunksBeingGenerated.Contains(chunkPos))
                {
                    newChunkPositions.Add(chunkPos);
                    chunksBeingGenerated.Add(chunkPos);
                }
            }

            if (newChunkPositions.Count > 0)
            {
                StartCoroutine(GenerateChunkDataCoroutine(newChunkPositions));
            }

            // Unload chunks not needed
            List<int2> chunksToRemove = new();
            foreach (int2 chunkPos in WorldData.Chunks.Keys)
            {
                if (!neededChunks.Contains(chunkPos))
                {
                    chunksToRemove.Add(chunkPos);
                }
            }

            foreach (int2 chunkPos in chunksToRemove)
            {
                Destroy(WorldData.Chunks[chunkPos].gameObject);
                WorldData.Chunks.Remove(chunkPos);
                WorldData.ChunkData.Remove(chunkPos);
            }
        }

        private IEnumerator GenerateChunkDataCoroutine(List<int2> chunkPositions)
        {
            List<Thread> threads = new();
            Dictionary<int2, ChunkData> generatedData = new();
            foreach (int2 chunkPos in chunkPositions)
            {
                ChunkData data = new(chunkPos, ChunkSize);
                Thread thread = new(() =>
                {
                    ChunkData chunkData = WorldGeneration.GenerateVoxels(data, noiseScale, waterThreshold);
                    lock (generatedData)
                    {
                        generatedData.Add(chunkPos, chunkData);
                    }
                });
                thread.Start();
                threads.Add(thread);
            }

            foreach (Thread thread in threads)
            {
                yield return new WaitUntil(() => !thread.IsAlive);
            }

            foreach (KeyValuePair<int2, ChunkData> kvp in generatedData)
            {
                WorldData.ChunkData.Add(kvp.Key, kvp.Value);
                GetOrAddChunkRenderer(kvp.Value, kvp.Key);
                StartCoroutine(GenerateMesh(WorldData.Chunks[kvp.Key]));
                chunksBeingGenerated.Remove(kvp.Key);
            }
        }

        private IEnumerator GenerateMesh(ChunkRenderer chunkRenderer)
        {
            double startTime = Time.realtimeSinceStartupAsDouble;
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];

            Thread jobThread = new(() =>
            {
                ChunkMeshJob job = new(chunkRenderer, Instance);
                job.Execute(meshData);
            });
            jobThread.Start();
            double prepFinishTime = Time.realtimeSinceStartupAsDouble;
            yield return new WaitUntil(() => !jobThread.IsAlive);
            double jobFinishTime = Time.realtimeSinceStartupAsDouble;
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, chunkRenderer._mesh);
            chunkRenderer._mesh.RecalculateBounds();
            Debug.Log($"Mesh Bounds: {chunkRenderer._mesh.bounds}");
            double finishTime = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"Prep Time: {(prepFinishTime - startTime) * 1000} ms, " +
                      $"Job Time: {(jobFinishTime - prepFinishTime) * 1000} ms, " +
                      $"Post Job Time: {(finishTime - jobFinishTime) * 1000} ms, " +
                      $"Time on Main Thread: {((prepFinishTime - startTime) + (finishTime - jobFinishTime)) * 1000} ms" +
                      $"Total Time: {(finishTime - startTime) * 1000} ms");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Dispose();
        }

        public void Dispose()
        {
            WorldData.Dispose();
        }
    }

    public struct WorldData: IDisposable
    {
        public Dictionary<int2, ChunkData> ChunkData;
        public Dictionary<int2, ChunkRenderer> Chunks;
        public int3 ChunkSize;

        public void Dispose()
        {
            foreach (KeyValuePair<int2, ChunkData> data in ChunkData)
            {
                data.Value.Dispose();
            }
        }
    }
}