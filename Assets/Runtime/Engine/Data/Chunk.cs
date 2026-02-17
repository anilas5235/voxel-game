using System;
using Runtime.Engine.Utils.Collections;
using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using static Runtime.Engine.Utils.VoxelConstants;

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
        public int2 Position { get; }

        public ChunkVoxelData VoxelData;

        public ChunkLightData LightData;

        /// <summary>
        /// Constructs a new chunk with position and size; initializes data structure.
        /// </summary>
        public Chunk(int2 position)
        {
            Position = position;
            VoxelData = default;
            LightData = default;
        }

        public void Dispose()
        {
            VoxelData.Dispose();
            LightData.Dispose();
        }
    }

    [BurstCompile]
    public struct ChunkLightData : IDisposable
    {
        private UnsafeList<UnsafeIntervalList<byte>> _data;

        public ChunkLightData(int initCapacity)
        {
            _data = new UnsafeList<UnsafeIntervalList<byte>>(PartitionsPerChunk, Allocator.Persistent);
            for (int i = 0; i < PartitionsPerChunk; i++)
            {
                UnsafeIntervalList<byte> partitionData = new(initCapacity, Allocator.Persistent);
                partitionData.AddInterval(0, VoxelsPerPartition);
                _data.Add(partitionData);
            }
        }

        /// <summary>
        /// Adds a run of identical light values (during initialization / generation).
        /// </summary>
        public void AddLight(int partitionIndex,byte lightValue, int count)
        {
            _data[partitionIndex].AddInterval(lightValue, count);
        }

        /// <summary>
        /// Sets light value by int3 position. Marks dirty on change.
        /// </summary>
        public bool SetLight(int partitionIndex,int3 pos, byte lightValue)
        {
            bool result = _data[partitionIndex].Set(GetIndex(pos), lightValue);
            return result;
        }

        /// <summary>
        /// Reads light value at int3 position.
        /// </summary>
        public byte GetLight(int partitionIndex,int3 pos) => _data[partitionIndex].Get(GetIndex(pos));

        /// <summary>
        /// Disposes native resources.
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < _data.Length; i++) _data[i].Dispose();
            _data.Dispose();
        }

        private static int GetIndex(in int3 pos) => ChunkSize.Flatten(pos);
    }

    [BurstCompile]
    public struct ChunkVoxelData : IDisposable
    {
        private UnsafeIntervalList<ushort> _data;

        public ChunkVoxelData(int initCapacity)
        {
            _data = new UnsafeIntervalList<ushort>(initCapacity, Allocator.Persistent);
        }

        /// <summary>
        /// Adds a run of identical voxels (during initialization / generation).
        /// </summary>
        public void AddVoxels(ushort voxelId, int count)
        {
            _data.AddInterval(voxelId, count);
        }

        /// <summary>
        /// Sets voxel by int3 position. Marks dirty on change.
        /// </summary>
        public bool SetVoxel(int3 pos, ushort voxelId)
        {
            bool result = _data.Set(GetIndex(pos), voxelId);
            return result;
        }

        /// <summary>
        /// Reads voxel at int3 position.
        /// </summary>
        public ushort GetVoxel(int3 pos) => _data.Get(GetIndex(pos));

        /// <summary>
        /// Disposes native resources.
        /// </summary>
        public void Dispose() => _data.Dispose();

        private static int GetIndex(in int3 pos) => ChunkSize.Flatten(pos);
    }
}