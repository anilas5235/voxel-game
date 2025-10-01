using System.Collections;
using System.Collections.Generic;
using System.Threading;
using ProceduralMeshes;
using UnityEngine;
using Utils;
using Voxels.Chunk;
using Voxels.Generation;

namespace Voxels
{
    public class VoxelWorld : Singleton<VoxelWorld>
    {
        public const int ChunkSize = 16;
        public const int HalfChunkSize = ChunkSize / 2;
        public const int ChunkHeight = 256;
        public const int HalfChunkHeight = ChunkHeight / 2;
        public const int VoxelsPerChunk = ChunkSize * ChunkSize * ChunkHeight;

        public int waterThreshold = 50;
        public float noiseScale = 0.03f;
        public GameObject chunkPrefab;

        public Transform playerTransform;
        public int viewDistance = 2;

        public WorldData WorldData { get; private set; }

        private Vector2Int lastPlayerChunkPos = new Vector2Int(int.MinValue, int.MinValue);
        private HashSet<Vector2Int> chunksBeingGenerated = new HashSet<Vector2Int>();

        protected override void Awake()
        {
            base.Awake();
            WorldData = new WorldData
            {
                ChunkData = new Dictionary<Vector2Int, ChunkData>(),
                Chunks = new Dictionary<Vector2Int, ChunkRenderer>(),
                ChunkSize = ChunkSize,
                ChunkHeight = ChunkHeight
            };
        }

        private void Update()
        {
            if (playerTransform == null) return;
            Vector3 playerPos = playerTransform.position;
            Vector2Int playerChunkPos = new(
                Mathf.FloorToInt(playerPos.x / (float)ChunkSize),
                Mathf.FloorToInt(playerPos.z / (float)ChunkSize)
            );
            if (playerChunkPos != lastPlayerChunkPos)
            {
                lastPlayerChunkPos = playerChunkPos;
                UpdateChunksAroundPlayer(playerChunkPos);
            }
        }

        private ChunkRenderer GetOrAddChunkRenderer(ChunkData data, Vector2Int chunkPos)
        {
            if (WorldData.Chunks.TryGetValue(chunkPos, out ChunkRenderer chunkRenderer)) return chunkRenderer;
            GameObject chunkObject = Instantiate(chunkPrefab, data.WorldPosition, Quaternion.identity);
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

        public bool GetVoxelFromWoldVoxPos(Vector3Int voxelWorldPos, out ushort voxelId)
        {
            voxelId = 0;
            if (IsNotInYRange(voxelWorldPos.y)) return false;
            ChunkData chunk = GetChunkFrom(voxelWorldPos);
            if (chunk == null) return false;
            voxelId = chunk.GetVoxel(GetVoxPosFromWorldVoxPos(chunk, voxelWorldPos));
            return true;
        }

        private static bool IsNotInYRange(int y)
        {
            return y is < 0 or >= ChunkHeight;
        }

        public void SetVoxelFromWorldVoxPos(Vector3Int voxelWorldPos, ushort voxelId)
        {
            if (IsNotInYRange(voxelWorldPos.y)) return;
            ChunkData chunk = GetChunkFrom(voxelWorldPos);
            ChunkUtils.SetVoxel(chunk, GetVoxPosFromWorldVoxPos(chunk, voxelWorldPos), voxelId);
        }

        public static Vector3Int GetVoxPosFromWorldVoxPos(ChunkData chunkData, Vector3Int voxelWorldPos)
        {
            return voxelWorldPos - chunkData.WorldPosition;
        }

        public static Vector3Int GetVoxPosFromWorldVoxPos(Vector2Int chunkPos, Vector3Int voxelWorldPos)
        {
            return voxelWorldPos - new Vector3Int(chunkPos.x * ChunkSize, 0, chunkPos.y * ChunkSize);
        }

        internal ChunkData GetChunkFrom(Vector3Int voxelWorldPos)
        {
            Vector2Int pos = GetChunkPosition(voxelWorldPos);
            WorldData.ChunkData.TryGetValue(pos, out ChunkData data);
            return data;
        }

        public static Vector2Int GetChunkPosition(Vector3Int voxelWorldPos)
        {
            return Vector2Int.FloorToInt(
                new Vector2(
                    voxelWorldPos.x / (float)ChunkSize,
                    voxelWorldPos.z / (float)ChunkSize
                )
            );
        }

        public static Vector2Int GetChunkPosition(Vector3 worldPos)
        {
            return GetChunkPosition(Vector3Int.FloorToInt(worldPos));
        }

        public ChunkData GetChunkFrom(Vector2Int voxelWorldPos)
        {
            WorldData.ChunkData.TryGetValue(voxelWorldPos, out ChunkData data);
            return data;
        }

        private void UpdateChunksAroundPlayer(Vector2Int centerChunk)
        {
            HashSet<Vector2Int> neededChunks = new HashSet<Vector2Int>();
            List<Vector2Int> newChunkPositions = new List<Vector2Int>();
            for (int x = -viewDistance; x <= viewDistance; x++)
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                Vector2Int chunkPos = new(centerChunk.x + x, centerChunk.y + z);
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
            var chunksToRemove = new List<Vector2Int>();
            foreach (var chunkPos in WorldData.Chunks.Keys)
            {
                if (!neededChunks.Contains(chunkPos))
                {
                    chunksToRemove.Add(chunkPos);
                }
            }
            foreach (var chunkPos in chunksToRemove)
            {
                Destroy(WorldData.Chunks[chunkPos].gameObject);
                WorldData.Chunks.Remove(chunkPos);
                WorldData.ChunkData.Remove(chunkPos);
            }
        }

        private IEnumerator GenerateChunkDataCoroutine(List<Vector2Int> chunkPositions)
        {
            List<Thread> threads = new List<Thread>();
            Dictionary<Vector2Int, ChunkData> generatedData = new Dictionary<Vector2Int, ChunkData>();
            foreach (var chunkPos in chunkPositions)
            {
                ChunkData data = new(this, chunkPos);
                Thread thread = new Thread(() =>
                {
                    var chunkData = WorldGeneration.GenerateVoxels(data, noiseScale, waterThreshold);
                    lock (generatedData)
                    {
                        generatedData.Add(chunkPos, chunkData);
                    }
                });
                thread.Start();
                threads.Add(thread);
            }
            foreach (var thread in threads)
            {
                yield return new WaitUntil(() => !thread.IsAlive);
            }
            foreach (var kvp in generatedData)
            {
                WorldData.ChunkData.Add(kvp.Key, kvp.Value);
                GetOrAddChunkRenderer(kvp.Value, kvp.Key);
                StartCoroutine(GenerateMesh(WorldData.Chunks[kvp.Key]));
                chunksBeingGenerated.Remove(kvp.Key);
            }
        }

        private IEnumerator GenerateMesh(ChunkRenderer chunkRenderer)
        {
            var startTime = Time.realtimeSinceStartupAsDouble;
            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];

            var jobThread = new Thread(() =>
            {
                var job = new ChunkMeshJob(chunkRenderer, Instance);
                job.Execute(meshData);
            });
            jobThread.Start();
            var prepFinishTime = Time.realtimeSinceStartupAsDouble;
            yield return new WaitUntil(() => !jobThread.IsAlive);
            var jobFinishTime = Time.realtimeSinceStartupAsDouble;
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, chunkRenderer._mesh);
            chunkRenderer._mesh.RecalculateBounds();
            Debug.Log($"Mesh Bounds: {chunkRenderer._mesh.bounds}");
            var finishTime = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"Prep Time: {(prepFinishTime - startTime) * 1000} ms, " +
                      $"Job Time: {(jobFinishTime - prepFinishTime) * 1000} ms, " +
                      $"Post Job Time: {(finishTime - jobFinishTime) * 1000} ms, " +
                      $"Time on Main Thread: {((prepFinishTime - startTime) + (finishTime - jobFinishTime)) * 1000} ms" +
                      $"Total Time: {(finishTime - startTime) * 1000} ms");
        }
    }

    public struct WorldData
    {
        public Dictionary<Vector2Int, ChunkData> ChunkData;
        public Dictionary<Vector2Int, ChunkRenderer> Chunks;
        public int ChunkSize;
        public int ChunkHeight;
    }
}