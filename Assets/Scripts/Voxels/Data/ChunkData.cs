using UnityEngine;

namespace Voxels.Data
{
    public class ChunkData
    {
        public const int ChunkSize = 16;
        public const int HalfChunkSize = ChunkSize / 2;
        public const int ChunkHeight = 256; // Height of the chunk, can be adjusted as needed
        public const int HalfChunkHeight = ChunkHeight / 2;
        private const int VoxelsPerChunk = ChunkSize * ChunkSize * ChunkHeight;
        
        private readonly VoxelType[] _chunkData;
        private readonly VoxelWorld _world;
        public Vector3Int WorldPosition { get; private set; }
        public bool modified;

        public ChunkData(VoxelWorld world, Vector3Int worldPosition)
        {
            _chunkData = new VoxelType[VoxelsPerChunk];
            _world = world;
            WorldPosition = worldPosition;
            modified = false;
        }
        public VoxelType this[int x, int y, int z]
        {
            get => _chunkData[GetIndex(x, y, z)];
            set => _chunkData[GetIndex(x, y, z)] = value;
        }

        private static int GetIndex(int x, int y, int z)
        {
            return x + y * ChunkSize + z * ChunkSize * ChunkHeight;
        }
    }
}
