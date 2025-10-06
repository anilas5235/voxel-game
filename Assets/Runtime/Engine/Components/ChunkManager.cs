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
        private Dictionary<int3, Chunk> _chunks;
        private SimpleFastPriorityQueue<int3, int> _queue;
        private NativeParallelHashMap<int3, Chunk> _accessorMap;

        private HashSet<int3> _reMeshChunks;
        private HashSet<int3> _reCollideChunks;

        private int3 _focus;
        private int3 _chunkSize;
        private int _chunkStoreSize;

        internal ChunkManager(VoxelEngineSettings settings)
        {
            _chunkSize = settings.Chunk.ChunkSize;
            _chunkStoreSize = (settings.Chunk.LoadDistance + 2).CubedSize();

            _reMeshChunks = new HashSet<int3>();
            _reCollideChunks = new HashSet<int3>();

            _chunks = new Dictionary<int3, Chunk>(capacity: _chunkStoreSize);
            _queue = new SimpleFastPriorityQueue<int3, int>();

            _accessorMap = new NativeParallelHashMap<int3, Chunk>(
                capacity: settings.Scheduler.MeshingBatchSize * 27,
                allocator: Allocator.Persistent
            );
        }

        #region API

        public Block GetBlock(Vector3Int position)
        {
            int3 chunkPos = VoxelUtils.GetChunkCoords(Position: position);
            int3 blockPos = VoxelUtils.GetBlockIndex(Position: position);

            if (!_chunks.TryGetValue(key: chunkPos, value: out Chunk chunk))
            {
                VoxelEngineLogger.Warn<ChunkManager>(message: $"Chunk : {chunkPos} not loaded");
                return Block.ERROR;
            }

            return (Block)chunk.GetBlock(pos: blockPos);
        }

        /// <summary>
        /// Set a block at a position
        /// </summary>
        /// <param name="block">Block Type</param>
        /// <param name="position">World Position</param>
        /// <param name="remesh">Regenerate Mesh and Collider ?</param>
        /// <returns>Operation Success</returns>
        public bool SetBlock(Block block, Vector3Int position, bool remesh = true)
        {
            int3 chunkPos = VoxelUtils.GetChunkCoords(Position: position);
            int3 blockPos = VoxelUtils.GetBlockIndex(Position: position);

            if (!_chunks.TryGetValue(chunkPos, out Chunk chunk))
            {
                VoxelEngineLogger.Warn<ChunkManager>(message: $"Chunk : {chunkPos} not loaded");
                return false;
            }

            bool result = chunk.SetBlock(pos: blockPos, block: VoxelUtils.GetBlockId(block: block));

            _chunks[key: chunkPos] = chunk;

            if (remesh && result) ReMeshChunks(blockPosition: position.Int3());

            return result;
        }

        public int ChunkCount() => _chunks.Count;

        public bool IsChunkLoaded(int3 position) => _chunks.ContainsKey(key: position);

        #endregion

        internal bool ShouldReMesh(int3 position) => _reMeshChunks.Contains(item: position);
        internal bool ShouldReCollide(int3 position) => _reCollideChunks.Contains(item: position);
        
        internal void RemoveChunk(int3 position) => _chunks.Remove(key: position);

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
                _queue.UpdatePriority(item: position, priority: -(position - focus).SqrMagnitude());
            }
        }

        internal void AddChunks(NativeParallelHashMap<int3, Chunk> chunks)
        {
            foreach (KeyValue<int3, Chunk> pair in chunks)
            {
                int3 position = pair.Key;
                Chunk chunk = pair.Value;

                if (_chunks.ContainsKey(key: chunk.Position))
                {
                    throw new InvalidOperationException(message: $"Chunk {position} already exists");
                }

                if (_queue.Count >= _chunkStoreSize)
                {
                    _chunks.Remove(key: _queue.Dequeue());
                    // if dirty save chunk
                }

                _chunks.Add(key: position, value: chunk);
                _queue.Enqueue(item: position, priority: -(position - _focus).SqrMagnitude());
            }
        }

        internal ChunkAccessor GetAccessor(List<int3> positions)
        {
            _accessorMap.Clear();

            foreach (int3 position in positions)
            {
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            int3 pos = position + _chunkSize.MemberMultiply(x: x, y: y, z: z);

                            if (!_chunks.ContainsKey(key: pos))
                            {
                                // Anytime this exception is thrown, mesh building completely stops
                                throw new InvalidOperationException(message: $"Chunk {pos} has not been generated");
                            }

                            if (!_accessorMap.ContainsKey(key: pos))
                                _accessorMap.Add(key: pos, item: _chunks[key: pos]);
                        }
                    }
                }
            }

            return new ChunkAccessor(chunks: _accessorMap.AsReadOnly(), chunkSize: _chunkSize);
        }

        internal bool ReMeshedChunk(int3 position)
        {
            if (!_reMeshChunks.Contains(item: position)) return false;

            _reMeshChunks.Remove(item: position);
            _reCollideChunks.Add(item: position);

            return true;
        }

        internal bool ReCollideChunk(int3 position)
        {
            if (!_reCollideChunks.Contains(item: position)) return false;

            _reCollideChunks.Remove(item: position);

            return true;
        }

        private void ReMeshChunks(int3 blockPosition)
        {
            foreach (int3 dir in VoxelUtils.Directions)
            {
                _reMeshChunks.Add(item: VoxelUtils.GetChunkCoords(Position: blockPosition + dir));
            }
        }
    }
}