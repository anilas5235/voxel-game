using System;
using System.Collections.Generic;
using Runtime.Engine.Data;
using Runtime.Engine.Jobs;
using Runtime.Engine.Settings;
using Runtime.Engine.ThirdParty.Priority_Queue;
using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.Utils.Logger;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static Runtime.Engine.Utils.Extensions.VectorConstants;
using static Runtime.Engine.Utils.VoxelConstants;
using static Runtime.Engine.Utils.VoxelUtils;

namespace Runtime.Engine.Components
{
    /// <summary>
    /// Manages chunk data in memory. Maintains a limited pool of loaded chunks prioritized by focus position.
    /// Tracks remesh / recollider flags and provides voxel access.
    /// </summary>
    internal class ChunkManager
    {
        private readonly Dictionary<int2, Chunk> _chunks;
        private readonly SimpleFastPriorityQueue<int2, int> _queue;
        private NativeParallelHashMap<int2, Chunk> _accessorMap;

        private readonly HashSet<int3> _reMeshPartitions;
        private readonly HashSet<int3> _reCollidePartitions;

        private int3 _focus;
        private readonly int _chunkStoreSize;
        
        internal Action OnChunkRemeshRequested;
        /// <summary>
        /// Creates a new manager with capacities from <paramref name="settings"/>.
        /// </summary>
        internal ChunkManager(VoxelEngineSettings settings)
        {
            _chunkStoreSize = (settings.Chunk.LoadDistance + 2).SquareSize();

            _reMeshPartitions = new HashSet<int3>();
            _reCollidePartitions = new HashSet<int3>();

            _chunks = new Dictionary<int2, Chunk>(_chunkStoreSize);
            _queue = new SimpleFastPriorityQueue<int2, int>();

            _accessorMap = new NativeParallelHashMap<int2, Chunk>(
                settings.Scheduler.MeshingBatchSize * 27,
                Allocator.Persistent
            );
        }

        #region API

        /// <summary>
        /// Reads a voxel at world position. Returns 0 if chunk or Y out of range.
        /// </summary>
        internal ushort GetVoxel(Vector3Int position)
        {
            int2 chunkPos = GetChunkCoords(position);
            int3 blockPos = GetLocalVoxelCoords(position);
            if (blockPos.y < 0 || blockPos.y >= ChunkSize.y) return 0;
            if (_chunks.TryGetValue(chunkPos, out Chunk chunk)) return chunk.GetVoxel(blockPos);
            VoxelEngineLogger.Warn<ChunkManager>($"Chunk : {chunkPos} not loaded");
            return 0;
        }

        /// <summary>
        /// Sets a voxel at world position. Optionally triggers remesh for affected neighbor chunks.
        /// </summary>
        /// <param name="voxelId">Voxel ID/type.</param>
        /// <param name="position">World position.</param>
        /// <param name="remesh">Whether to flag for remeshing.</param>
        /// <returns>True if voxel actually changed.</returns>
        internal bool SetVoxel(ushort voxelId, Vector3Int position, bool remesh = true)
        {
            int2 chunkPos = GetChunkCoords(position);
            int3 blockPos = GetLocalVoxelCoords(position);
            if (!_chunks.TryGetValue(chunkPos, out Chunk chunk))
            {
                VoxelEngineLogger.Warn<ChunkManager>($"Chunk : {chunkPos} not loaded");
                return false;
            }

            int3 pCoords = GetPartitionCoords(position);

            if (_reMeshPartitions.Contains(pCoords))
            {
                return false;
            }

            bool result = chunk.SetVoxel(blockPos, voxelId);
            _chunks[chunkPos] = chunk;
            if (remesh && result) ReMeshPartitions(position.Int3());
            return result;
        }

        /// <summary>
        /// Number of currently loaded chunks.
        /// </summary>
        public int ChunkCount() => _chunks.Count;

        /// <summary>
        /// Checks if a chunk is loaded.
        /// </summary>
        public bool IsChunkLoaded(int2 position) => _chunks.ContainsKey(position);

        #endregion

        /// <summary>
        /// Whether a chunk is flagged for remeshing.
        /// </summary>
        internal bool ShouldReMesh(int3 position) => _reMeshPartitions.Contains(position);

        /// <summary>
        /// Whether a chunk needs collider rebuild.
        /// </summary>
        internal bool ShouldReCollide(int3 position) => _reCollidePartitions.Contains(position);

        /// <summary>
        /// Disposes native resources and all chunk data.
        /// </summary>
        internal void Dispose()
        {
            _accessorMap.Dispose();
            foreach ((int2 _, Chunk chunk) in _chunks) chunk.Dispose();
        }

        /// <summary>
        /// Updates focus (player) and priorities in eviction queue.
        /// </summary>
        internal void FocusUpdate(int3 focus)
        {
            _queue.UpdateAllPriorities(pos => -PriorityUtil.DistPriority(ref pos, ref focus));
        }

        /// <summary>
        /// Adds newly generated chunks, evicts oldest if capacity exceeded.
        /// </summary>
        internal void AddChunks(NativeParallelHashMap<int2, Chunk> chunks)
        {
            foreach (KeyValue<int2, Chunk> pair in chunks)
            {
                int2 position = pair.Key;
                Chunk chunk = pair.Value;
                if (_chunks.ContainsKey(chunk.Position))
                    throw new InvalidOperationException($"Chunk {position} already exists");
                if (_queue.Count >= _chunkStoreSize) RemoveChunkData(_queue.Dequeue());
                _chunks.Add(position, chunk);
                _queue.Enqueue(position, -(position - _focus.xz).SqrMagnitude());
            }
        }

        /// <summary>
        /// Removes the chunk data (eviction). Persistence hook could be added here.
        /// </summary>
        private void RemoveChunkData(int2 position)
        {
            _chunks.Remove(position);
        }

        /// <summary>
        /// Builds a <see cref="ChunkAccessor"/> that includes a 3x3 neighborhood for given positions.
        /// </summary>
        internal ChunkAccessor GetAccessor(List<int3> positions)
        {
            _accessorMap.Clear();
            foreach (int3 position in positions)
            {
                for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    int2 pos = position.xz + new int2(x, z);
                    if (!_chunks.TryGetValue(pos, out Chunk chunk))
                        throw new InvalidOperationException($"Chunk {pos} has not been generated");
                    if (!_accessorMap.ContainsKey(pos)) _accessorMap.Add(pos, chunk);
                }
            }

            return new ChunkAccessor(_accessorMap.AsReadOnly());
        }

        /// <summary>
        /// Callback after chunk remesh completes: remove flag and flag for collider rebuild.
        /// </summary>
        internal void ReMeshedPartition(int3 position)
        {
            if (!_reMeshPartitions.Contains(position)) return;
            _reMeshPartitions.Remove(position);
            _reCollidePartitions.Add(position);
        }

        /// <summary>
        /// Callback after collider bake completes: remove flag.
        /// </summary>
        internal void ReCollidedPartition(int3 position)
        {
            if (!_reCollidePartitions.Contains(position)) return;
            _reCollidePartitions.Remove(position);
        }

        /// <summary>
        /// Flags all neighbor partitions (including own) for remeshing.
        /// </summary>
        /// <param name="blockPosition">World block position that changed.</param>
        private void ReMeshPartitions(int3 blockPosition)
        {
            int3 pCoords = GetPartitionCoords(blockPosition);
            _reMeshPartitions.Add(pCoords);
            int3 localPos = GetPartitionLocalVoxelCoords(pCoords, blockPosition);
            switch (localPos.x % PartitionWidth)
            {
                case 0:
                    _reMeshPartitions.Add(pCoords + Int3Left);
                    break;
                case 15:
                    _reMeshPartitions.Add(pCoords + Int3Right);
                    break;
            }

            switch (localPos.z % PartitionDepth)
            {
                case 0:
                    _reMeshPartitions.Add(pCoords + Int3Backward);
                    break;
                case 15:
                    _reMeshPartitions.Add(pCoords + Int3Forward);
                    break;
            }

            switch (localPos.y % PartitionHeight)
            {
                case 0:
                    _reMeshPartitions.Add(pCoords + Int3Down);
                    break;
                case 15:
                    _reMeshPartitions.Add(pCoords + Int3Up);
                    break;
            }
            
            OnChunkRemeshRequested?.Invoke();
        }
    }
}