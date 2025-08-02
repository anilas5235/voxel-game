using UnityEngine;

namespace Voxels.Data
{
    public class ChunkData
    {
        public const int ChunkSize = 16;
        public const int HalfChunkSize = ChunkSize / 2;
        public const int ChunkHeight = 256; // Height of the chunk, can be adjusted as needed
        public const int HalfChunkHeight = ChunkHeight / 2;
        public const int VoxelsPerChunk = ChunkSize * ChunkSize * ChunkHeight;
        
        public readonly VoxelType[] voxelData;
        private readonly VoxelWorld _world;
        public Vector3Int WorldPosition { get; private set; }
        public bool modified;

        public ChunkData(VoxelWorld world, Vector3Int worldPosition)
        {
            voxelData = new VoxelType[VoxelsPerChunk];
            _world = world;
            WorldPosition = worldPosition;
            modified = false;
        }
    }
}
