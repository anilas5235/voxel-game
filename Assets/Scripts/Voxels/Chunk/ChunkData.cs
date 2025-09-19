using UnityEngine;
using static Voxels.VoxelWorld;

namespace Voxels.Chunk
{
    public class ChunkData
    {
        private readonly int[] voxels;
        public bool modified;

        public ChunkData(VoxelWorld world, Vector2Int chunkPosition)
        {
            voxels = new int[VoxelsPerChunk];
            World = world;
            ChunkPosition = chunkPosition;
            WorldPosition = new Vector3Int(chunkPosition.x * ChunkSize, 0, chunkPosition.y * ChunkSize);
            modified = false;
        }

        public VoxelWorld World { get; }
        public Vector3Int WorldPosition { get; }

        public Vector2Int ChunkPosition { get; }

        internal int GetVoxel(Vector3Int voxelPosition)
        {
            return voxels[GetIndex(voxelPosition)];
        }

        internal void SetVoxel(Vector3Int voxelPosition, int voxelId)
        {
            voxels[GetIndex(voxelPosition)] = voxelId;
            modified = true;
        }

        private static int GetIndex(Vector3Int voxelPosition)
        {
            return voxelPosition.x +
                   voxelPosition.y * ChunkSize +
                   voxelPosition.z * ChunkSize * ChunkHeight;
        }
    }
}