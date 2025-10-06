using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Voxels.Chunk
{
    public struct VoxelGrid : IDisposable
    {
        private int3 _size;
        private NativeArray<ushort> _voxels;

        public VoxelGrid(int3 size)
        {
            _size = size;
            _voxels = new NativeArray<ushort>(_size.x * _size.y * _size.z, Allocator.Persistent);
        }

        public ushort GetVoxel(int x, int y, int z)
        {
            return _voxels[GetIndex(x, y, z)];
        }

        public void SetVoxel(int x, int y, int z, ushort voxelId)
        {
            _voxels[GetIndex(x, y, z)] = voxelId;
        }

        public ushort GetVoxel(int3 pos)
        {
            return GetVoxel(pos.x, pos.y, pos.z);
        }

        public void SetVoxel(int3 pos, ushort voxelId)
        {
            SetVoxel(pos.x, pos.y, pos.z, voxelId);
        }

        private int GetIndex(int x, int y, int z)
        {
            return x +
                   y * _size.x +
                   z * _size.x * _size.z;
        }

        public void Dispose()
        {
            _voxels.Dispose();
        }
    }

    public struct ChunkData : IDisposable
    {
        public readonly int3 ChunkSize;
        public VoxelGrid voxels;
        public bool modified;

        public ChunkData(int2 chunkPosition, int3 chunkSize)
        {
            voxels = new VoxelGrid(chunkSize);
            ChunkSize = chunkSize;
            ChunkPosition = chunkPosition;
            WorldPosition = new int3(chunkPosition.x * chunkSize.x, 0, chunkPosition.y * chunkSize.z);
            modified = false;
        }

        public int3 WorldPosition { get; }

        public int2 ChunkPosition { get; }

        internal ushort GetVoxel(int3 voxelPosition)
        {
            return voxels.GetVoxel(voxelPosition.x, voxelPosition.y, voxelPosition.z);
        }

        internal void SetVoxel(int3 voxelPosition, ushort voxelId)
        {
            voxels.SetVoxel(voxelPosition.x, voxelPosition.y, voxelPosition.z, voxelId);
            modified = true;
        }

        public void Dispose()
        {
            voxels.Dispose();
        }
    }
}