using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Voxels.VoxelWorld;

namespace Voxels.Chunk
{
    public struct VoxelGrid : System.IDisposable
    {
        private int width;
        private int height;
        private int depth;
        private NativeArray<int> voxels;

        public VoxelGrid(int width, int height, int depth, Allocator allocator = Allocator.Persistent)
        {
            this.width = width;
            this.height = height;
            this.depth = depth;
            voxels = new NativeArray<int>(width * height * depth, allocator);
        }

        public readonly int GetVoxel(int x, int y, int z)
        {
            return voxels[GetIndex(x, y, z)];
        }

        public void SetVoxel(int x, int y, int z, int voxelId)
        {
            voxels[GetIndex(x, y, z)] = voxelId;
        }

        public int GetVoxel(int3 pos)
        {
            return GetVoxel(pos.x, pos.y, pos.z);
        }

        public void SetVoxel(int3 pos, int voxelId)
        {
            SetVoxel(pos.x, pos.y, pos.z, voxelId);
        }

        public void Dispose()
        {
            if (voxels.IsCreated)
                voxels.Dispose();
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
        
        ~ChunkData()
        {
            voxels.Dispose();
        }

        public VoxelWorld World { get; }
        public Vector3Int WorldPosition { get; }

        public Vector2Int ChunkPosition { get; }

        internal int GetVoxel(Vector3Int voxelPosition)
        {
            return voxels.GetVoxel(voxelPosition.x, voxelPosition.y, voxelPosition.z);
        }

        internal void SetVoxel(Vector3Int voxelPosition, int voxelId)
        {
            voxels.SetVoxel(voxelPosition.x, voxelPosition.y, voxelPosition.z, voxelId);
            modified = true;
        }
    }
}