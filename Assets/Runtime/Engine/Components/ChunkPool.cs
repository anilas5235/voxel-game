using System;
using System.Collections.Generic;
using Runtime.Engine.Behaviour;
using Runtime.Engine.Settings;
using Runtime.Engine.ThirdParty.Priority_Queue;
using Runtime.Engine.Utils.Extensions;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Runtime.Engine.Components
{
    /// <summary>
    /// Pool für <see cref="ChunkBehaviour"/> Instanzen (Rendering + Collider). Verantwortlich für Aktivierung / Reclamation.
    /// Verwendet Prioritäts-Queue für Entfernung basierend auf Distanz zum Fokus.
    /// </summary>
    public class ChunkPool
    {
        private readonly ObjectPool<ChunkBehaviour> _pool;
        private readonly Dictionary<int3, ChunkBehaviour> _meshMap;
        private readonly HashSet<int3> _colliderSet;
        private readonly SimpleFastPriorityQueue<int3, int> _queue;

        private int3 _focus;
        private readonly int _chunkPoolSize;

        /// <summary>
        /// Erstellt einen neuen Pool basierend auf Draw-/Update-Distanzen aus Settings.
        /// </summary>
        internal ChunkPool(Transform transform, VoxelEngineSettings settings)
        {
            _chunkPoolSize = (settings.Chunk.DrawDistance + 2).SquareSize();

            _meshMap = new Dictionary<int3, ChunkBehaviour>(_chunkPoolSize);
            _colliderSet = new HashSet<int3>((settings.Chunk.UpdateDistance + 2).SquareSize());
            _queue = new SimpleFastPriorityQueue<int3, int>();

            _pool = new ObjectPool<ChunkBehaviour>( // pool size = x^2 + 1
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
        /// Prüft, ob ein Chunk aktiv gerendert wird.
        /// </summary>
        internal bool IsActive(int3 pos) => _meshMap.ContainsKey(pos);
        /// <summary>
        /// Prüft, ob ein Chunk bereits einen Collider hat.
        /// </summary>
        internal bool IsCollidable(int3 pos) => _colliderSet.Contains(pos);

        /// <summary>
        /// Aktualisiert Fokus und Prioritäten in Queue.
        /// </summary>
        internal void FocusUpdate(int3 focus)
        {
            _focus = focus;

            foreach (int3 position in _queue)
            {
                _queue.UpdatePriority(position, -(position - _focus).SqrMagnitude());
            }
        }

        /// <summary>
        /// Gibt existierende Instanz oder claimt eine neue aus dem Pool.
        /// </summary>
        internal ChunkBehaviour GetOrClaim(int3 position)
        {
            return IsActive(position) ? _meshMap[position] : Claim(position);
        }

        /// <summary>
        /// Claimt eine neue Instanz; löst ggf. Reclaim der ältesten Instanz aus.
        /// </summary>
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
            behaviour.name = $"Chunk({position.x},{position.y},{position.z})";

            _meshMap.Add(position, behaviour);
            _queue.Enqueue(position, -(position - _focus).SqrMagnitude());

            return behaviour;
        }

        /// <summary>
        /// Liefert aktive Mesh-Instanzen für gegebene Positionen.
        /// </summary>
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

        /// <summary>
        /// Callback, wenn Collider generiert wurde – markiert Chunk als kollidierbar.
        /// </summary>
        internal void ColliderBaked(int3 position)
        {
            _colliderSet.Add(position);
        }
    }
}