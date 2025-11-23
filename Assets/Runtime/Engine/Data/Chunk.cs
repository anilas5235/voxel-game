using System;
using Runtime.Engine.Utils.Collections;
using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Data
{
    /// <summary>
    /// In-memory representation of a chunk with compressed voxel data (run-length style).
    /// Tracks a dirty flag for mesh rebuild decisions and provides simple read/write API.
    /// </summary>
    [BurstCompile]
    public struct Chunk : IDisposable
    {
        /// <summary>
        /// World-space position (chunk origin coordinate).
        /// </summary>
        public int3 Position { get; }
        /// <summary>
        /// Flag indicating modifications since last mesh build.
        /// </summary>
        public bool Dirty { get; private set; }

        private readonly int3 _chunkSize;
        private UnsafeIntervalList _data;

        /// <summary>
        /// Constructs a new chunk with position and size; initializes data structure.
        /// </summary>
        public Chunk(int3 position, int3 chunkSize)
        {
            Dirty = false;
            Position = position;
            _chunkSize = chunkSize;
            _data = new UnsafeIntervalList(128, Allocator.Persistent);
        }

        /// <summary>
        /// Adds a run of identical voxels (during initialization / generation).
        /// </summary>
        public void AddVoxels(ushort voxelId, int count)
        {
            _data.AddInterval(voxelId, count);
        }

        /// <summary>
        /// Sets voxel by individual coordinates. Marks dirty on change.
        /// </summary>
        public bool SetVoxel(int x, int y, int z, ushort block)
        {
            bool result = _data.Set(_chunkSize.Flatten(x, y, z), block);
            if (result) Dirty = true;
            return result;
        }

        /// <summary>
        /// Sets voxel by int3 position. Marks dirty on change.
        /// </summary>
        public bool SetVoxel(int3 pos, ushort voxelId)
        {
            bool result = _data.Set(_chunkSize.Flatten(pos), voxelId);
            if (result) Dirty = true;
            return result;
        }

        /// <summary>
        /// Reads voxel at coordinates.
        /// </summary>
        public ushort GetVoxel(int x, int y, int z)
        {
            return _data.Get(_chunkSize.Flatten(x, y, z));
        }

        /// <summary>
        /// Reads voxel at int3 position.
        /// </summary>
        public ushort GetVoxel(int3 pos)
        {
            return GetVoxel(pos.x, pos.y, pos.z);
        }

        /// <summary>
        /// Disposes native resources.
        /// </summary>
        public void Dispose()
        {
            _data.Dispose();
        }

        /// <summary>
        /// Debug string including dirty status and compressed data statistics.
        /// </summary>
        public override string ToString()
        {
            return $"Pos : {Position}, Dirty : {Dirty}, Data : {_data.ToString()}";
        }
    }
}