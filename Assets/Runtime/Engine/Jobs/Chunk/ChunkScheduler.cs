using System.Collections.Generic;
using Runtime.Engine.Components;
using Runtime.Engine.Jobs.Core;
using Runtime.Engine.Noise;
using Runtime.Engine.Settings;
using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.Utils.Logger;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.Engine.Jobs.Chunk
{
    /// <summary>
    /// Scheduler für die Generierung von Chunk-Daten. Verpackt ChunkJob, verwaltet Job-Liste und Ergebnis-Map.
    /// Misst Laufzeiten und übergibt fertige Chunks an <see cref="ChunkManager"/>.
    /// </summary>
    public class ChunkScheduler : JobScheduler
    {
        private readonly int3 _chunkSize;
        private readonly ChunkManager _chunkStore;
        private readonly NoiseProfile _noiseProfile;
        private JobHandle _handle;
        private NativeList<int3> _jobs;
        private NativeParallelHashMap<int3, Data.Chunk> _results;
        private readonly GeneratorConfig _config;

        /// <summary>
        /// Erstellt einen produktiven ChunkScheduler mit eigener Ergebnis-Sammlung.
        /// </summary>
        public ChunkScheduler(
            VoxelEngineSettings settings,
            ChunkManager chunkStore,
            NoiseProfile noiseProfile,
            GeneratorConfig config
        )
        {
            _chunkSize = settings.Chunk.ChunkSize;
            _chunkStore = chunkStore;
            _noiseProfile = noiseProfile;
            _config = config;
            _jobs = new NativeList<int3>(Allocator.Persistent);
            _results = new NativeParallelHashMap<int3, Data.Chunk>(
                settings.Chunk.LoadDistance.SquareSize(),
                Allocator.Persistent
            );
        }

        internal bool IsReady = true; // True wenn bereit für neues Start

        /// <summary>
        /// Spezial-Konstruktor für extern bereitgestellte Ergebnis-Map (Testing/Debug).
        /// </summary>
        public ChunkScheduler(NativeParallelHashMap<int3, Data.Chunk> results)
        {
            _results = results;
        }

        /// <summary>
        /// Gibt an ob geplanter Job abgeschlossen ist.
        /// </summary>
        internal bool IsComplete => _handle.IsCompleted;

        /// <summary>
        /// Startet Burst Job für gegebene Liste von Chunk-Positionen.
        /// </summary>
        internal void Start(List<int3> jobs)
        {
            StartRecord();
            IsReady = false;
            foreach (int3 j in jobs) _jobs.Add(j);

            ChunkJob job = new()
            {
                Jobs = _jobs,
                ChunkSize = _chunkSize,
                NoiseProfile = _noiseProfile,
                Results = _results.AsParallelWriter(),
                RandomSeed = _config.GlobalSeed,
                Config = _config
            };

            _handle = job.Schedule(_jobs.Length, 1);
        }

        /// <summary>
        /// Wartet Job ab, überträgt Ergebnisse in ChunkStore und räumt temporäre Listen.
        /// </summary>
        internal void Complete()
        {
            double start = Time.realtimeSinceStartupAsDouble;
            _handle.Complete();
            _chunkStore.AddChunks(_results);
            double totalTime = (Time.realtimeSinceStartupAsDouble - start) * 1000;
            if (totalTime >= 1)
            {
                VoxelEngineLogger.Info<ChunkScheduler>(
                    $"Built {_jobs.Length} ChunkData, Collected Results in <color=red>{totalTime:0.000}</color>ms"
                );
            }
            _jobs.Clear();
            _results.Clear();
            IsReady = true;
            StopRecord();
        }

        /// <summary>
        /// Dispose aller Native Container; wartet vorher auf Abschluss.
        /// </summary>
        internal void Dispose()
        {
            _handle.Complete();

            _jobs.Dispose();
            _results.Dispose();
        }
    }
}