using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public const int ChunkHeight = 128; // Height of the chunk, can be adjusted as needed
        public const int HalfChunkHeight = ChunkHeight / 2;
        public const int VoxelsPerChunk = ChunkSize * ChunkSize * ChunkHeight;

        public int waterThreshold = 50;
        public float noiseScale = 0.03f;
        public GameObject chunkPrefab;

        private readonly List<Vector2Int> _chunksToLoad = new();
        private readonly List<Vector2Int> _chunksToUnload = new();

        // Thread-safe queue for completed chunk data
        private readonly ConcurrentQueue<(ChunkData, Vector2Int)> _completedChunks = new();

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

        public void LoadChunk(Vector2Int chunkPosition)
        {
            if (WorldData.Chunks.ContainsKey(chunkPosition) || _chunksToLoad.Contains(chunkPosition)) return;
            _chunksToUnload.Remove(chunkPosition);
            _chunksToLoad.Add(chunkPosition);
        }

        public void LoadChunk(List<Vector2Int> chunkPositions)
        {
            foreach (Vector2Int pos in chunkPositions) LoadChunk(pos);
        }

        public void UnloadChunk(Vector2Int chunkPosition)
        {
            if (!WorldData.Chunks.ContainsKey(chunkPosition) || _chunksToUnload.Contains(chunkPosition)) return;
            _chunksToLoad.Remove(chunkPosition);
            _chunksToUnload.Add(chunkPosition);
        }

        public void UnloadChunk(List<Vector2Int> chunkPositions)
        {
            foreach (Vector2Int pos in chunkPositions) UnloadChunk(pos);
        }

        private void FixedUpdate()
        {
            LoadChunkStep();
            UnloadChunkStep();
            ProcessCompletedChunks();
        }

        private void ProcessCompletedChunks()
        {
            if (_completedChunks.TryDequeue(out (ChunkData, Vector2Int) item))
            {
                (ChunkData data, Vector2Int chunkPos) = item;
                WorldData.ChunkData[chunkPos] = data;
                AddChunkRenderer(data, chunkPos);
            }
        }

        private void UnloadChunkStep()
        {
            if (_chunksToUnload.Count == 0) return;
            Vector2Int chunkPos = _chunksToUnload.First();
            _chunksToUnload.Remove(chunkPos);
            if (!WorldData.Chunks.TryGetValue(chunkPos, out ChunkRenderer chunkRenderer)) return;

            Destroy(chunkRenderer.gameObject);
            WorldData.Chunks.Remove(chunkPos);
        }

        private void LoadChunkStep()
        {
            if (_chunksToLoad.Count == 0) return;
            Vector2Int chunkPos = _chunksToLoad.First();
            _chunksToLoad.Remove(chunkPos);
            if (WorldData.Chunks.ContainsKey(chunkPos)) return;

            WorldData.ChunkData.TryGetValue(chunkPos, out ChunkData data);
            if (data == null)
            {
                // Run voxel generation asynchronously
                Task.Run(() =>
                {
                    data = new ChunkData(this, chunkPos);
                    WorldData.ChunkData.Add(chunkPos, null);
                    WorldGeneration.GenerateVoxels(data, noiseScale, waterThreshold);
                    _completedChunks.Enqueue((data, chunkPos));
                });
            }
            else
            {
                AddChunkRenderer(data, chunkPos);
            }
        }

        private void AddChunkRenderer(ChunkData data, Vector2Int chunkPos)
        {
            if (WorldData.Chunks.ContainsKey(chunkPos)) return;
            GameObject chunkObject = Instantiate(chunkPrefab, data.WorldPosition, Quaternion.identity);
            chunkObject.name = chunkPos.ToString();
            ChunkRenderer chunkRenderer = chunkObject.GetComponent<ChunkRenderer>();
            WorldData.Chunks.Add(chunkPos, chunkRenderer);
            chunkRenderer.Initialize(data);
            for (int x = -1; x < 2; x++)
            for (int z = -1; z < 2; z++)
            {
                Vector2Int neighborPos = chunkPos + new Vector2Int(x, z);
                if (!WorldData.ChunkData.TryGetValue(neighborPos, out ChunkData neighborChunk)) continue;
                if(neighborChunk != null) neighborChunk.dirty = true;
            }
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
    }

    public struct WorldData
    {
        public Dictionary<Vector2Int, ChunkData> ChunkData;
        public Dictionary<Vector2Int, ChunkRenderer> Chunks;
        public int ChunkSize;
        public int ChunkHeight;
    }
}