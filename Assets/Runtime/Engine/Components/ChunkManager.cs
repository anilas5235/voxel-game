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
    /// Verwalter für Chunk-Daten im Speicher. Hält einen begrenzten Pool geladener Chunks bereit, priorisiert anhand Fokusposition.
    /// Verwaltet Remesh / ReCollider Flags und stellt Zugriff auf Voxel bereit.
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
        /// Erstellt einen neuen ChunkManager mit Kapazitäten aus <paramref name="settings"/>.
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
        /// Liest einen Voxel an Weltposition. Gibt 0 zurück wenn Chunk oder Y außerhalb.
        /// </summary>
        internal ushort GetVoxel(Vector3Int position)
        {
            int3 chunkPos = VoxelUtils.GetChunkCoords(position);
            int3 blockPos = VoxelUtils.GetVoxelIndex(position);

            if (blockPos.y < 0 || blockPos.y >= _chunkSize.y)
            {
                return 0;
            }

            if (_chunks.TryGetValue(chunkPos, out Chunk chunk)) return chunk.GetVoxel(blockPos);

            VoxelEngineLogger.Warn<ChunkManager>($"Chunk : {chunkPos} not loaded");
            return 0;
        }

        /// <summary>
        /// Setzt einen Voxel an Weltposition. Optional Remesh für betroffene Nachbar-Chunks.
        /// </summary>
        /// <param name="voxelId">Voxel ID / Typ.</param>
        /// <param name="position">Weltposition.</param>
        /// <param name="remesh">Soll Remeshing angestoßen werden.</param>
        /// <returns>True bei Änderung.</returns>
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
        /// Anzahl aktuell geladener Chunks.
        /// </summary>
        public int ChunkCount() => _chunks.Count;

        /// <summary>
        /// Prüft, ob ein Chunk geladen ist.
        /// </summary>
        public bool IsChunkLoaded(int3 position) => _chunks.ContainsKey(position);

        #endregion

        /// <summary>
        /// Ob ein Chunk für Remeshing markiert ist.
        /// </summary>
        internal bool ShouldReMesh(int3 position) => _reMeshChunks.Contains(position);
        /// <summary>
        /// Ob ein Chunk für Collider-Neugenerierung markiert ist.
        /// </summary>
        internal bool ShouldReCollide(int3 position) => _reCollideChunks.Contains(position);

        /// <summary>
        /// Dispose verwalteter Native Ressourcen und Chunks.
        /// </summary>
        internal void Dispose()
        {
            _accessorMap.Dispose();

            foreach ((int3 _, Chunk chunk) in _chunks)
            {
                chunk.Dispose();
            }
        }

        /// <summary>
        /// Aktualisiert Fokus (Spieler) und Prioritäten der Eviction Queue.
        /// </summary>
        internal void FocusUpdate(int3 focus)
        {
            _focus = focus;

            foreach (int3 position in _queue)
            {
                _queue.UpdatePriority(position, -(position - focus).SqrMagnitude());
            }
        }

        /// <summary>
        /// Fügt neu generierte Chunks hinzu, entfernt ggf. älteste bei Kapazitätsüberschreitung.
        /// </summary>
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
                    RemoveChunkData(_queue.Dequeue());
                }

                _chunks.Add(position, chunk);
                _queue.Enqueue(position, -(position - _focus).SqrMagnitude());
            }
        }

        /// <summary>
        /// Entfernt die Datendarstellung eines Chunks aus dem Speicher (Eviction).
        /// </summary>
        private void RemoveChunkData(int3 position)
        {
            _chunks.Remove(position);
            //TODO: at this point a chunk is evicted from store/memory, if persistence is needed it should be saved here
        }

        /// <summary>
        /// Erstellt einen <see cref="ChunkAccessor"/> für die angegebenen Chunk Positionen (inkl. Nachbarschaft 3x3).
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
                    {
                        // Anytime this exception is thrown, mesh building completely stops
                        throw new InvalidOperationException($"Chunk {pos} has not been generated");
                    }

                    if (!_accessorMap.ContainsKey(pos)) _accessorMap.Add(pos, chunk);
                }
            }

            return new ChunkAccessor(_accessorMap.AsReadOnly(), _chunkSize);
        }

        /// <summary>
        /// Callback nach Remeshing eines Chunks – entfernt Flag und markiert für Collider.
        /// </summary>
        internal void ReMeshedChunk(int3 position)
        {
            if (!_reMeshChunks.Contains(position)) return;

            _reMeshChunks.Remove(position);
            VoxelEngineLogger.Info<ChunkManager>(
                $"Chunk : {position} has been remeshed;{Time.realtimeSinceStartupAsDouble}");
            _reCollideChunks.Add(position);
        }

        /// <summary>
        /// Callback nach Collider Bake eines Chunks – entfernt Flag.
        /// </summary>
        internal void ReCollidedChunk(int3 position)
        {
            if (!_reCollideChunks.Contains(position)) return;

            _reCollideChunks.Remove(position);
        }

        /// <summary>
        /// Markiert alle benachbarten Chunks für Remeshing basierend auf Blockänderung.
        /// </summary>
        private void ReMeshChunks(int3 blockPosition)
        {
            foreach (int3 dir in VoxelUtils.Directions)
            {
                _reMeshChunks.Add(VoxelUtils.GetChunkCoords(blockPosition + dir));
            }

            VoxelEngineLogger.Info<ChunkManager>(
                $"ReMeshChunks called at {blockPosition}, total {_reMeshChunks.Count} chunks to remesh;{Time.realtimeSinceStartupAsDouble}");
        }
    }
}