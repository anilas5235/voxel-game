using System;
using Runtime.Engine.Utils.Collections;
using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Data
{
    [BurstCompile]
    public struct Chunk : IDisposable
    {
        public int3 Position { get; }
        public bool Dirty { get; private set; }

        private readonly int3 _chunkSize;
        private UnsafeIntervalList _data;

        public Chunk(int3 position, int3 chunkSize)
        {
            Dirty = false;
            Position = position;
            _chunkSize = chunkSize;
            _data = new UnsafeIntervalList(128, Allocator.Persistent);
        }

        public void AddVoxels(ushort voxelId, int count)
        {
            _data.AddInterval(voxelId, count);
        }

        public bool SetVoxel(int x, int y, int z, ushort block)
        {
            bool result = _data.Set(_chunkSize.Flatten(x, y, z), block);
            if (result) Dirty = true;
            return result;
        }

        public bool SetVoxel(int3 pos, ushort voxelId)
        {
            bool result = _data.Set(_chunkSize.Flatten(pos), voxelId);
            if (result) Dirty = true;
            return result;
        }

        public ushort GetVoxel(int x, int y, int z)
        {
            return _data.Get(_chunkSize.Flatten(x, y, z));
        }

        public ushort GetVoxel(int3 pos)
        {
            return _data.Get(_chunkSize.Flatten(pos.x, pos.y, pos.z));
        }

        public void Dispose()
        {
            _data.Dispose();
        }

        public override string ToString()
        {
            return $"Pos : {Position}, Dirty : {Dirty}, Data : {_data.ToString()}";
        }
    }
}