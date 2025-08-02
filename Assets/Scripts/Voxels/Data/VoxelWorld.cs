using System.Collections.Generic;
using UnityEngine;

namespace Voxels.Data
{
    public class VoxelWorld : MonoBehaviour
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

        private readonly Dictionary<Vector3Int, ChunkData> _chunkData = new();
        private readonly Dictionary<Vector3Int, ChunkRenderer> _chunks = new();

        public void GenerateWorld()
        {
            ClearWorld();

            for (int x = 0; x < mapSizeInChunks; x++)
            {
                for (int z = 0; z < mapSizeInChunks; z++)
                {
                    ChunkData data = new(this, new Vector3Int(x * ChunkSize, 0, z * ChunkSize));
                    GenerateVoxels(data);
                    _chunkData.Add(data.WorldPosition, data);
                }
            }

            foreach (ChunkData data in _chunkData.Values)
            {
                MeshData meshData = Chunk.GetChunkMeshData(data);
                GameObject chunkObject = Instantiate(chunkPrefab, data.WorldPosition, Quaternion.identity);
                ChunkRenderer chunkRenderer = chunkObject.GetComponent<ChunkRenderer>();
                _chunks.Add(data.WorldPosition, chunkRenderer);
                chunkRenderer.Initialize(data);
                chunkRenderer.UpdateChunk(meshData);
            }
        }

        private void ClearWorld()
        {
            _chunkData.Clear();
            foreach (ChunkRenderer chunk in _chunks.Values)
            {
                Destroy(chunk.gameObject);
            }

            _chunks.Clear();
        }

        private void GenerateVoxels(ChunkData data)
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    float noiseValue = Mathf.PerlinNoise((data.WorldPosition.x + x) * noiseScale,
                        (data.WorldPosition.z + z) * noiseScale);
                    int groundPosition = Mathf.RoundToInt(noiseValue * ChunkHeight);
                    for (int y = 0; y < ChunkHeight; y++)
                    {
                        VoxelType voxelType = VoxelType.Dirt;
                        if (y > groundPosition)
                        {
                            voxelType = y < waterThreshold ? VoxelType.Water : VoxelType.Air;
                        }
                        else if (y == groundPosition)
                        {
                            voxelType = VoxelType.GrassDirt;
                        }

                        Chunk.SetVoxel(data, new Vector3Int(x, y, z), voxelType);
                    }
                }
            }
        }

        internal VoxelType GetVoxelFromOtherChunk(Vector3Int voxelWorldPos)
        {
            Vector3Int pos = Chunk.GetChunkPosition(voxelWorldPos);

            _chunkData.TryGetValue(pos, out ChunkData containerChunk);

            if (containerChunk == null) return VoxelType.Nothing;
            
            Vector3Int voxelInChunk = Chunk.GetVoxelPosition(voxelWorldPos, containerChunk);
            return Chunk.GetVoxel(containerChunk, voxelInChunk);
        }
    }
}