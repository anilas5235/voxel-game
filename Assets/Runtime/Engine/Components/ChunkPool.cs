using System;
using System.Collections.Generic;
using System.Linq;
using Runtime.Engine.Behaviour;
using Runtime.Engine.Settings;
using Runtime.Engine.ThirdParty.Priority_Queue;
using Runtime.Engine.Utils.Extensions;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using static Runtime.Engine.Jobs.PriorityUtil;
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
        private readonly SimpleFastPriorityQueue<int2, int> _chunkQueue;
        private readonly SimpleFastPriorityQueue<int3, int> _partitionQueue;
        private readonly SimpleFastPriorityQueue<int3, int> _colliderQueue;

        private int3 _focus;
        private readonly int _chunkPoolSize;
        private readonly int _partitionPoolSize;
        private readonly int _colliderPoolSize;

        /// <summary>
        /// Constructs a pool based on draw/update distances.
        /// </summary>
        internal ChunkPool(Transform transform, VoxelEngineSettings settings)
        {
            _chunkPoolSize = (settings.Chunk.DrawDistance + 2).SquareSize();
            _chunkMap = new Dictionary<int2, ChunkBehaviour>(_chunkPoolSize);

            _partitionPoolSize = settings.Chunk.DrawDistance.SquareSize() * 16;
            _meshMap = new Dictionary<int3, ChunkPartition>(_partitionPoolSize);

            _colliderPoolSize = settings.Chunk.UpdateDistance.SquareSize() * 16;
            _colliderSet = new HashSet<int3>(_colliderPoolSize);

            _chunkQueue = new SimpleFastPriorityQueue<int2, int>();
            _partitionQueue = new SimpleFastPriorityQueue<int3, int>();
            _colliderQueue = new SimpleFastPriorityQueue<int3, int>();

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
            _chunkQueue.UpdateAllPriorities(PriorityCalc);
            _partitionQueue.UpdateAllPriorities(PriorityCalc);
            _colliderQueue.UpdateAllPriorities(PriorityCalc);
        }


        private int PriorityCalc(int2 position) => -DistPriority(ref position, ref _focus);
        private int PriorityCalc(int3 position) => -DistPriority(ref position, ref _focus);

        /// <summary>
        /// Returns whether a chunk is active (rendered).
        /// </summary>
        internal bool IsChunkActive(int2 pos) => _chunkMap.ContainsKey(pos);

        /// <summary>
        /// Returns existing instance or claims a new one.
        /// </summary>
        internal ChunkBehaviour GetOrClaimChunk(int2 position) =>
            IsChunkActive(position) ? _chunkMap[position] : ClaimChunk(position);

        /// <summary>
        /// Claims a new instance; evicts oldest if capacity reached.
        /// </summary>
        internal ChunkBehaviour ClaimChunk(int2 position)
        {
            if (_chunkMap.ContainsKey(position))
                throw new InvalidOperationException($"Chunk ({position}) already active");
            if (_chunkQueue.Count >= _chunkPoolSize)
            {
                int2 reclaim = _chunkQueue.Dequeue();
                ChunkBehaviour reclaimBehaviour = _chunkMap[reclaim];
                reclaimBehaviour.ClearData();
                _pool.Release(reclaimBehaviour);
                _chunkMap.Remove(reclaim);
                Debug.Log($"Reclaimed Chunk");
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
            _chunkMap.Add(position, behaviour);
            _chunkQueue.Enqueue(position, PriorityCalc(position));
            return behaviour;
        }

        internal int GetPartitionPrioThreshold() => _partitionQueue.Count < _partitionPoolSize
            ? int.MinValue
            : PriorityCalc(_partitionQueue.First);

        internal ChunkPartition GetOrClaimPartition(int3 position) =>
            IsPartitionActive(position) ? _meshMap[position] : ClaimPartition(position);

        internal ChunkPartition ClaimPartition(int3 position)
        {
            if (_meshMap.ContainsKey(position))
                throw new InvalidOperationException($"Partition ({position}) already active");

            ChunkBehaviour behaviour = GetOrClaimChunk(position.xz);
            ChunkPartition partition = behaviour.GetPartition(position.y);

            partition.Clear();
            _meshMap.Add(position, partition);
            _partitionQueue.Enqueue(position, PriorityCalc(position));

            if (_partitionQueue.Count <= _partitionPoolSize)
            {
                Debug.Log(
                    $"Claimed Partition() No Reclaim Needed Queue:{_partitionPoolSize}");
                return partition;
            }

            int3 reclaim = _partitionQueue.Dequeue();
            ChunkPartition reclaimPartition = _meshMap[reclaim];
            reclaimPartition.Clear();
            _meshMap.Remove(reclaim);
            _colliderSet.Remove(reclaim);
            Debug.Log($"Reclaimed Partition()");
            return partition;
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