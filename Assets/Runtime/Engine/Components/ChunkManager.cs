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

        internal ChunkManager(VoxelEngineSettings settings)
        {
            _chunkSize = settings.Chunk.ChunkSize;
            _chunkStoreSize = (settings.Chunk.LoadDistance + 2).CubedSize();

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

        internal ushort GetVoxel(Vector3Int position)
        {
            int3 chunkPos = VoxelUtils.GetChunkCoords(position);
            int3 blockPos = VoxelUtils.GetVoxelIndex(position);

            if (_chunks.TryGetValue(chunkPos, out Chunk chunk)) return chunk.GetVoxel(blockPos);
            
            VoxelEngineLogger.Warn<ChunkManager>($"Chunk : {chunkPos} not loaded");
            return 0;
        }

        /// <summary>
        /// Set a voxelId at a position
        /// </summary>
        /// <param name="voxelId">Block VoxelType</param>
        /// <param name="position">World Position</param>
        /// <param name="remesh">Regenerate Mesh and Collider ?</param>
        /// <returns>Operation Success</returns>
        internal bool SetVoxel(ushort voxelId, Vector3Int position, bool remesh = true)
        {
            int3 chunkPos = VoxelUtils.GetChunkCoords(position);
            int3 blockPos = VoxelUtils.GetVoxelIndex(position);

            if (!_chunks.TryGetValue(chunkPos, out Chunk chunk))
            {
                VoxelEngineLogger.Warn<ChunkManager>($"Chunk : {chunkPos} not loaded");
                return false;
            }

            bool result = chunk.SetVoxel(blockPos, voxelId);

            _chunks[chunkPos] = chunk;

            if (remesh && result) ReMeshChunks(position.Int3());

            return result;
        }

        public int ChunkCount() => _chunks.Count;

        public bool IsChunkLoaded(int3 position) => _chunks.ContainsKey(position);

        #endregion

        internal bool ShouldReMesh(int3 position) => _reMeshChunks.Contains(position);
        internal bool ShouldReCollide(int3 position) => _reCollideChunks.Contains(position);

        internal void RemoveChunk(int3 position) => _chunks.Remove(position);

        internal void Dispose()
        {
            _accessorMap.Dispose();

            foreach ((int3 _, Chunk chunk) in _chunks)
            {
                chunk.Dispose();
            }
        }

        internal void FocusUpdate(int3 focus)
        {
            _focus = focus;

            foreach (int3 position in _queue)
            {
                _queue.UpdatePriority(position, -(position - focus).SqrMagnitude());
            }
        }

        internal void AddChunks(NativeParallelHashMap<int3, Chunk> chunks)
        {
            foreach (KeyValue<int3, Chunk> pair in chunks)
            {
                int3 position = pair.Key;
                Chunk chunk = pair.Value;

                if (_chunks.ContainsKey(chunk.Position))
                {
                    throw new InvalidOperationException($"Chunk {position} already exists");
                }

                if (_queue.Count >= _chunkStoreSize)
                {
                    _chunks.Remove(_queue.Dequeue());
                    // if dirty save chunk
                }

                _chunks.Add(position, chunk);
                _queue.Enqueue(position, -(position - _focus).SqrMagnitude());
            }
        }

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
                    {
                        // Anytime this exception is thrown, mesh building completely stops
                        throw new InvalidOperationException($"Chunk {pos} has not been generated");
                    }

                    if (!_accessorMap.ContainsKey(pos)) _accessorMap.Add(pos, chunk);
                }
            }

            return new ChunkAccessor(_accessorMap.AsReadOnly(), _chunkSize);
        }

        internal bool ReMeshedChunk(int3 position)
        {
            if (!_reMeshChunks.Contains(position)) return false;

            _reMeshChunks.Remove(position);
            _reCollideChunks.Add(position);

            return true;
        }

        internal bool ReCollideChunk(int3 position)
        {
            if (!_reCollideChunks.Contains(position)) return false;

            _reCollideChunks.Remove(position);

            return true;
        }

        private void ReMeshChunks(int3 blockPosition)
        {
            foreach (int3 dir in VoxelUtils.Directions)
            {
                _reMeshChunks.Add(VoxelUtils.GetChunkCoords(blockPosition + dir));
            }
        }
    }
}