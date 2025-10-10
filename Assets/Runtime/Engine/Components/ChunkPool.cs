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

namespace Runtime.Engine.Components
{
    /// <summary>
    /// Chunks are created on demand
    /// </summary>
    public class ChunkPool
    {
        private readonly ObjectPool<ChunkBehaviour> _pool;
        private readonly Dictionary<int3, ChunkBehaviour> _meshMap;
        private readonly HashSet<int3> _colliderSet;
        private readonly SimpleFastPriorityQueue<int3, int> _queue;

        private int3 _focus;
        private readonly int _chunkPoolSize;

        internal ChunkPool(Transform transform, VoxelEngineSettings settings)
        {
            _chunkPoolSize = (settings.Chunk.DrawDistance + 2).CubedSize();

            _meshMap = new Dictionary<int3, ChunkBehaviour>(_chunkPoolSize);
            _colliderSet = new HashSet<int3>((settings.Chunk.UpdateDistance + 2).CubedSize());
            _queue = new SimpleFastPriorityQueue<int3, int>();

            _pool = new ObjectPool<ChunkBehaviour>( // pool size = x^2 + 1
                () =>
                {
                    GameObject go = UnityEngine.Object.Instantiate(settings.Chunk.ChunkPrefab, transform);

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

        internal bool IsActive(int3 pos) => _meshMap.ContainsKey(pos);
        internal bool IsCollidable(int3 pos) => _colliderSet.Contains(pos);

        internal void FocusUpdate(int3 focus)
        {
            _focus = focus;

            foreach (int3 position in _queue)
            {
                _queue.UpdatePriority(position, -(position - _focus).SqrMagnitude());
            }
        }

        internal ChunkBehaviour Claim(int3 position)
        {
            if (_meshMap.ContainsKey(position))
            {
                throw new InvalidOperationException($"Chunk ({position}) already active");
            }

            // Reclaim
            if (_queue.Count >= _chunkPoolSize)
            {
                int3 reclaim = _queue.Dequeue();
                ChunkBehaviour reclaimBehaviour = _meshMap[reclaim];

                reclaimBehaviour.Collider.sharedMesh = null;

                _pool.Release(reclaimBehaviour);
                _meshMap.Remove(reclaim);
                _colliderSet.Remove(reclaim);
            }

            // Claim
            ChunkBehaviour behaviour = _pool.Get();

            behaviour.transform.position = position.GetVector3();
            behaviour.name = $"Chunk({position.x},{position.z})";

            _meshMap.Add(position, behaviour);
            _queue.Enqueue(position, -(position - _focus).SqrMagnitude());

            return behaviour;
        }

        internal Dictionary<int3, ChunkBehaviour> GetActiveMeshes(List<int3> positions)
        {
            Dictionary<int3, ChunkBehaviour> map = new();

            for (int i = 0; i < positions.Count; i++)
            {
                int3 position = positions[i];

                if (IsActive(position)) map.Add(position, _meshMap[position]);
            }

            return map;
        }

        internal void ColliderBaked(int3 position)
        {
            _colliderSet.Add(position);
        }

        internal ChunkBehaviour Get(int3 position)
        {
            if (!_meshMap.TryGetValue(position, out ChunkBehaviour chunk))
            {
                throw new InvalidOperationException($"Chunk ({position}) isn't active");
            }

            return chunk;
        }
    }
}