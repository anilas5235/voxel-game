using System;
using System.Collections.Generic;
using Runtime.Engine.Data;
using Runtime.Engine.Settings;
using Runtime.Engine.ThirdParty.Priority_Queue;
using Runtime.Engine.Utils;
using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.Utils.Logger;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.Engine.Components
{
    /// <summary>
    /// Manages chunk data in memory. Maintains a limited pool of loaded chunks prioritized by focus position.
    /// Tracks remesh / recollider flags and provides voxel access.
    /// </summary>
    public class ChunkManager
    {
        private readonly Dictionary<int3, Chunk> _chunks;
        private readonly SimpleFastPriorityQueue<int3, int> _queue;
        private NativeParallelHashMap<int3, Chunk> _accessorMap;

        private readonly HashSet<int3> _reMeshChunks;
        private readonly HashSet<int3> _reCollideChunks;

        private int3 _focus;
        private readonly int3 _chunkSize;
        private readonly int _chunkStoreSize;

        /// <summary>
        /// Creates a new manager with capacities from <paramref name="settings"/>.
        /// </summary>
        internal ChunkManager(VoxelEngineSettings settings)
        {
            _chunkSize = settings.Chunk.ChunkSize;
            _chunkStoreSize = (settings.Chunk.LoadDistance + 2).SquareSize();

            _reMeshChunks = new HashSet<int3>();
            _reCollideChunks = new HashSet<int3>();

            _chunks = new Dictionary<int3, Chunk>(_chunkStoreSize);
            _queue = new SimpleFastPriorityQueue<int3, int>();

            _accessorMap = new NativeParallelHashMap<int3, Chunk>(
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
            int3 chunkPos = VoxelUtils.GetChunkCoords(position);
            int3 blockPos = VoxelUtils.GetVoxelIndex(position);
            if (blockPos.y < 0 || blockPos.y >= _chunkSize.y) return 0;
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
            int3 chunkPos = VoxelUtils.GetChunkCoords(position);
            int3 blockPos = VoxelUtils.GetVoxelIndex(position);
            if (!_chunks.TryGetValue(chunkPos, out Chunk chunk))
            {
                VoxelEngineLogger.Warn<ChunkManager>($"Chunk : {chunkPos} not loaded");
                return false;
            }
            if (_reMeshChunks.Contains(chunkPos))
            {
                VoxelEngineLogger.Warn<ChunkManager>($"Chunk : {chunkPos} is pending remesh, cannot set voxel now");
                return false;
            }
            bool result = chunk.SetVoxel(blockPos, voxelId);
            _chunks[chunkPos] = chunk;
            if (remesh && result) ReMeshChunks(position.Int3());
            return result;
        }

        /// <summary>
        /// Number of currently loaded chunks.
        /// </summary>
        public int ChunkCount() => _chunks.Count;

        /// <summary>
        /// Checks if a chunk is loaded.
        /// </summary>
        public bool IsChunkLoaded(int3 position) => _chunks.ContainsKey(position);

        #endregion

        /// <summary>
        /// Whether a chunk is flagged for remeshing.
        /// </summary>
        internal bool ShouldReMesh(int3 position) => _reMeshChunks.Contains(position);
        /// <summary>
        /// Whether a chunk needs collider rebuild.
        /// </summary>
        internal bool ShouldReCollide(int3 position) => _reCollideChunks.Contains(position);

        /// <summary>
        /// Disposes native resources and all chunk data.
        /// </summary>
        internal void Dispose()
        {
            _accessorMap.Dispose();
            foreach ((int3 _, Chunk chunk) in _chunks) chunk.Dispose();
        }

        /// <summary>
        /// Updates focus (player) and priorities in eviction queue.
        /// </summary>
        internal void FocusUpdate(int3 focus)
        {
            _focus = focus;
            foreach (int3 position in _queue)
                _queue.UpdatePriority(position, -(position - focus).SqrMagnitude());
        }

        /// <summary>
        /// Adds newly generated chunks, evicts oldest if capacity exceeded.
        /// </summary>
        internal void AddChunks(NativeParallelHashMap<int3, Chunk> chunks)
        {
            foreach (KeyValue<int3, Chunk> pair in chunks)
            {
                int3 position = pair.Key;
                Chunk chunk = pair.Value;
                if (_chunks.ContainsKey(chunk.Position)) throw new InvalidOperationException($"Chunk {position} already exists");
                if (_queue.Count >= _chunkStoreSize) RemoveChunkData(_queue.Dequeue());
                _chunks.Add(position, chunk);
                _queue.Enqueue(position, -(position - _focus).SqrMagnitude());
            }
        }

        /// <summary>
        /// Removes the chunk data (eviction). Persistence hook could be added here.
        /// </summary>
        private void RemoveChunkData(int3 position)
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
                    int3 pos = position + _chunkSize.MemberMultiply(x, 0, z);
                    if (!_chunks.TryGetValue(pos, out Chunk chunk))
                        throw new InvalidOperationException($"Chunk {pos} has not been generated");
                    if (!_accessorMap.ContainsKey(pos)) _accessorMap.Add(pos, chunk);
                }
            }
            return new ChunkAccessor(_accessorMap.AsReadOnly(), _chunkSize);
        }

        /// <summary>
        /// Callback after chunk remesh completes: remove flag and flag for collider rebuild.
        /// </summary>
        internal void ReMeshedChunk(int3 position)
        {
            if (!_reMeshChunks.Contains(position)) return;
            _reMeshChunks.Remove(position);
            VoxelEngineLogger.Info<ChunkManager>($"Chunk : {position} has been remeshed;{Time.realtimeSinceStartupAsDouble}");
            _reCollideChunks.Add(position);
        }

        /// <summary>
        /// Callback after collider bake completes: remove flag.
        /// </summary>
        internal void ReCollidedChunk(int3 position)
        {
            if (!_reCollideChunks.Contains(position)) return;
            _reCollideChunks.Remove(position);
        }

        /// <summary>
        /// Flags all neighbor chunks for remesh based on block modification.
        /// </summary>
        private void ReMeshChunks(int3 blockPosition)
        {
            foreach (int3 dir in VoxelUtils.Directions)
                _reMeshChunks.Add(VoxelUtils.GetChunkCoords(blockPosition + dir));
            VoxelEngineLogger.Info<ChunkManager>($"ReMeshChunks called at {blockPosition}, total {_reMeshChunks.Count} chunks to remesh;{Time.realtimeSinceStartupAsDouble}");
        }
    }
}