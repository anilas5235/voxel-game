using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Voxels.VoxelWorld;

namespace Voxels.Chunk
{
    public class VoxelGrid
    {
        private int width;
        private int height;
        private int depth;
        private ushort[] voxels;

        public VoxelGrid(int width, int height, int depth)
        {
            this.width = width;
            this.height = height;
            this.depth = depth;
            voxels = new ushort[width * height * depth];
        }

        public ushort GetVoxel(int x, int y, int z)
        {
            return voxels[GetIndex(x, y, z)];
        }

        public void SetVoxel(int x, int y, int z, ushort voxelId)
        {
            voxels[GetIndex(x, y, z)] = voxelId;
        }

        public ushort GetVoxel(int3 pos)
        {
            return GetVoxel(pos.x, pos.y, pos.z);
        }

        public void SetVoxel(int3 pos, ushort voxelId)
        {
            SetVoxel(pos.x, pos.y, pos.z, voxelId);
        }
       
        private static int GetIndex(int x, int y, int z)
        {
            return x +
                   y * ChunkSize +
                   z * ChunkSize * ChunkHeight;
        }
    }

    public class ChunkData
    {
        public VoxelGrid voxels;
        public bool modified;

        public ChunkData(VoxelWorld world, Vector2Int chunkPosition)
        {
            voxels = new VoxelGrid(ChunkSize, ChunkHeight, ChunkSize);
            World = world;
            ChunkPosition = chunkPosition;
            WorldPosition = new Vector3Int(chunkPosition.x * ChunkSize, 0, chunkPosition.y * ChunkSize);
            modified = false;
        }

        public VoxelWorld World { get; }
        public Vector3Int WorldPosition { get; }

        public Vector2Int ChunkPosition { get; }

        internal ushort GetVoxel(Vector3Int voxelPosition)
        {
            return voxels.GetVoxel(voxelPosition.x, voxelPosition.y, voxelPosition.z);
        }

        internal void SetVoxel(Vector3Int voxelPosition, ushort voxelId)
        {
            voxels.SetVoxel(voxelPosition.x, voxelPosition.y, voxelPosition.z, voxelId);
            modified = true;
        }
    }
}