using System.Collections;
using System.Collections.Generic;
using System.Threading;
using ProceduralMeshes;
using UnityEngine;
using UnityEngine.Rendering;
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

        public WorldData WorldData { get; private set; }

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

        private void Start()
        {
            GenerateWorld();
        }

        public void GenerateWorld()
        {
            ClearWorld();
            for (int x = -1; x < 2; x++)
            for (int z = -1; z < 2; z++)
            {
                Vector2Int chunkPos = new(x, z);
                ChunkData data = new(this, chunkPos);
                WorldData.ChunkData.Add(chunkPos, WorldGeneration.GenerateVoxels(data, noiseScale, waterThreshold));
                var chunkRenderer = GetOrAddChunkRenderer(data, chunkPos);
                StartCoroutine(GenerateMesh(chunkRenderer));
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

        public int GetVoxelFromWoldVoxPos(Vector3Int voxelWorldPos)
        {
            if (IsNotInYRange(voxelWorldPos.y)) return -1;
            ChunkData chunk = GetChunkFrom(voxelWorldPos);
            if (chunk == null) return -1;
            return ChunkUtils.GetVoxel(chunk, GetVoxPosFromWorldVoxPos(chunk, voxelWorldPos));
        }

        private static bool IsNotInYRange(int y)
        {
            return y is < 0 or >= ChunkHeight;
        }

        public void SetVoxelFromWorldVoxPos(Vector3Int voxelWorldPos, int voxelId)
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