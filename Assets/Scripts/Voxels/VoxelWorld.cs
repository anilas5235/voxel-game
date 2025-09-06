using System.Collections.Generic;
using UnityEngine;
using Utils;
using Voxels.Chunk;
using Voxels.Data;

namespace Voxels
{
    public class VoxelWorld : Singleton<VoxelWorld>
    {
        public const int ChunkSize = 16;
        public const int HalfChunkSize = ChunkSize / 2;
        public const int ChunkHeight = 128; // Height of the chunk, can be adjusted as needed
        public const int HalfChunkHeight = ChunkHeight / 2;
        public const int VoxelsPerChunk = ChunkSize * ChunkSize * ChunkHeight;

        public int mapSizeInChunks = 8;
        public int waterThreshold = 50;
        public float noiseScale = 0.03f;
        public GameObject chunkPrefab;

        private readonly Dictionary<Vector2Int, ChunkData> _chunkData = new();
        private readonly Dictionary<Vector2Int, ChunkRenderer> _chunks = new();

        public void GenerateWorld()
        {
            ClearWorld();

            for (int x = 0; x < mapSizeInChunks; x++)
            for (int z = 0; z < mapSizeInChunks; z++)
            {
                Vector2Int chunkPos = new(x, z);
                ChunkData data = new(this, chunkPos);
                GenerateVoxels(data);
                _chunkData.Add(chunkPos, data);
            }

            foreach (ChunkData data in _chunkData.Values)
            {
                GameObject chunkObject = Instantiate(chunkPrefab, data.WorldPosition, Quaternion.identity);
                Vector2Int chunkPos = data.ChunkPosition;
                chunkObject.name = chunkPos.ToString();
                ChunkRenderer chunkRenderer = chunkObject.GetComponent<ChunkRenderer>();
                _chunks.Add(chunkPos, chunkRenderer);
                chunkRenderer.Initialize(data);
            }
        }

        private void ClearWorld()
        {
            _chunkData.Clear();
            foreach (ChunkRenderer chunk in _chunks.Values) Destroy(chunk.gameObject);

            _chunks.Clear();
        }

        private void GenerateVoxels(ChunkData data)
        {
            int dirt = VoxelRegistry.GetId("std:Dirt");
            int grass = VoxelRegistry.GetId("std:Grass");
            int water = VoxelRegistry.GetId("std:Water");
            for (int x = 0; x < ChunkSize; x++)
            for (int z = 0; z < ChunkSize; z++)
            {
                float noiseValue = Mathf.PerlinNoise((data.WorldPosition.x + x) * noiseScale,
                    (data.WorldPosition.z + z) * noiseScale);
                int groundPosition = Mathf.RoundToInt(noiseValue * ChunkHeight);
                for (int y = 0; y < ChunkHeight; y++)
                {
                    int voxelId = dirt;
                    if (y > groundPosition)
                        voxelId = y < waterThreshold ? water : 0;
                    else if (y == groundPosition) voxelId = grass;

                    ChunkUtils.SetVoxel(data, new Vector3Int(x, y, z), voxelId);
                }
            }
        }

        public int GetVoxelFromWoldVoxPos(Vector3Int voxelWorldPos)
        {
            if(IsNotInYRange(voxelWorldPos.y)) return -1;
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
            if(IsNotInYRange(voxelWorldPos.y)) return;
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
            _chunkData.TryGetValue(pos, out ChunkData data);
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
    }
}