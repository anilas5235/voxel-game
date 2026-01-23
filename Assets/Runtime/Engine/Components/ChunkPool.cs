using System;
using System.Collections.Generic;
using Runtime.Engine.Behaviour;
using Runtime.Engine.Settings;
using Runtime.Engine.ThirdParty.Priority_Queue;
using Runtime.Engine.Utils.Extensions;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Pool;
using static Runtime.Engine.Utils.VoxelConstants;
using Object = UnityEngine.Object;

namespace Runtime.Engine.Components
{
    /// <summary>
    /// Pool for <see cref="ChunkBehaviour"/> instances (render + collider). Handles activation & reclamation.
    /// Uses a priority queue ordered by distance to focus for eviction.
    /// </summary>
    public class ChunkPool
    {
        private readonly ObjectPool<ChunkBehaviour> _pool;
        private readonly Dictionary<int3, ChunkPartition> _meshMap;
        private readonly Dictionary<int2, ChunkBehaviour> _chunkMap;
        private readonly HashSet<int3> _colliderSet;
        private readonly SimpleFastPriorityQueue<int2, int> _queue;

        private int3 _focus;
        private readonly int _chunkPoolSize;

        /// <summary>
        /// Constructs a pool based on draw/update distances.
        /// </summary>
        internal ChunkPool(Transform transform, VoxelEngineSettings settings)
        {
            _chunkPoolSize = (settings.Chunk.DrawDistance + 2).SquareSize();
            _meshMap = new Dictionary<int3, ChunkPartition>(_chunkPoolSize * 16);
            _chunkMap = new Dictionary<int2, ChunkBehaviour>(_chunkPoolSize);
            _colliderSet = new HashSet<int3>((settings.Chunk.UpdateDistance + 2).SquareSize() * 8);
            _queue = new SimpleFastPriorityQueue<int2, int>();
            _pool = new ObjectPool<ChunkBehaviour>(
                () =>
                {
                    GameObject go = Object.Instantiate(settings.Chunk.ChunkPrefab, transform);
                    go.SetActive(false);
                    ChunkBehaviour chunkBehaviour = go.GetComponent<ChunkBehaviour>();
                    chunkBehaviour.Init(settings.Renderer);
                    return chunkBehaviour;
                },
                chunkBehaviour => chunkBehaviour.gameObject.SetActive(true),
                chunkBehaviour => chunkBehaviour.gameObject.SetActive(false),
                null, false, _chunkPoolSize, _chunkPoolSize
            );
        }

        /// <summary>
        /// Returns whether a chunk is active (rendered).
        /// </summary>
        internal bool IsChunkActive(int2 pos) => _chunkMap.ContainsKey(pos);

        internal bool IsPartitionActive(int3 pos) => _meshMap.ContainsKey(pos);

        /// <summary>
        /// Returns whether a chunk already has a baked collider.
        /// </summary>
        internal bool IsCollidable(int3 pos) => _colliderSet.Contains(pos);

        /// <summary>
        /// Updates focus and queue priorities.
        /// </summary>
        internal void FocusUpdate(int3 focus)
        {
            _focus = focus;
            foreach (int2 position in _queue)
            {
                _queue.UpdatePriority(position, PriorityCalc(position));
            }
        }

        private int PriorityCalc(int2 position) => -(position - _focus.xz).SqrMagnitude();

        /// <summary>
        /// Returns existing instance or claims a new one.
        /// </summary>
        internal ChunkBehaviour GetOrClaim(int2 position) =>
            IsChunkActive(position) ? _chunkMap[position] : Claim(position);

        /// <summary>
        /// Claims a new instance; evicts oldest if capacity reached.
        /// </summary>
        internal ChunkBehaviour Claim(int2 position)
        {
            if (_chunkMap.ContainsKey(position))
                throw new InvalidOperationException($"Chunk ({position}) already active");
            if (_queue.Count >= _chunkPoolSize)
            {
                int2 reclaim = _queue.Dequeue();
                ChunkBehaviour reclaimBehaviour = _chunkMap[reclaim];
                reclaimBehaviour.ClearData();
                _pool.Release(reclaimBehaviour);
                _chunkMap.Remove(reclaim);
                for (int pId = 0; pId < PartitionsPerChunk; pId++)
                {
                    int3 partitionPos = new(reclaim.x, pId, reclaim.y);
                    _meshMap.Remove(partitionPos);
                    _colliderSet.Remove(partitionPos);
                }
            }

            ChunkBehaviour behaviour = _pool.Get();
            behaviour.transform.position = new float3(position.x * ChunkWidth, 0, position.y * ChunkDepth);
            behaviour.name = $"Chunk({position.x},{position.y})";
            _meshMap.AddRange(behaviour.GetMap(position));
            _chunkMap.Add(position, behaviour);
            _queue.Enqueue(position, PriorityCalc(position));
            return behaviour;
        }

        /// <summary>
        /// Returns active mesh behaviours for supplied positions.
        /// </summary>
        internal Dictionary<int3, ChunkPartition> GetActiveMeshes(List<int3> positions)
        {
            Dictionary<int3, ChunkPartition> map = new();
            for (int i = 0; i < positions.Count; i++)
            {
                int3 position = positions[i];
                if (_meshMap.TryGetValue(position, out ChunkPartition partition)) map.Add(position, partition);
            }

            return map;
        }

        /// <summary>
        /// Callback after collider bake: mark chunk collidable.
        /// </summary>
        internal void ColliderBaked(int3 position) => _colliderSet.Add(position);
    }
}