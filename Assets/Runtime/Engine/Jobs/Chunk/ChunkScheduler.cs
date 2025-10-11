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

namespace Runtime.Engine.Jobs.Chunk {

    public class ChunkScheduler : JobScheduler {
        private int3 _chunkSize;
        private ChunkManager _chunkStore;
        private NoiseProfile _noiseProfile;

        private JobHandle _handle;
        
        // can be native arrays
        private NativeList<int3> _jobs;
        private NativeParallelHashMap<int3, Data.Chunk> _results;

        public ChunkScheduler(
            VoxelEngineSettings settings,
            ChunkManager chunkStore,
            NoiseProfile noiseProfile
        ) {
            _chunkSize = settings.Chunk.ChunkSize;
            _chunkStore = chunkStore;
            _noiseProfile = noiseProfile;

            _jobs = new NativeList<int3>(Allocator.Persistent);
            _results = new NativeParallelHashMap<int3, Data.Chunk>(
                settings.Chunk.LoadDistance.CubedSize(), 
                Allocator.Persistent
            );
        }

        internal bool IsReady = true;
        internal bool IsComplete => _handle.IsCompleted;

        internal void Start(List<int3> jobs) {
            StartRecord();

            IsReady = false;
            
            foreach (int3 j in jobs) {
                _jobs.Add(j);
            }
            
            ChunkJob job = new()
            {
                Jobs = _jobs,
                ChunkSize = _chunkSize,
                NoiseProfile = _noiseProfile,
                Results = _results.AsParallelWriter(),
            };
            
            _handle = job.Schedule(_jobs.Length, 1);
        }

        internal void Complete() {
            double start = Time.realtimeSinceStartupAsDouble;
            _handle.Complete();
            
            _chunkStore.AddChunks(_results);
            
            double totalTime = (Time.realtimeSinceStartupAsDouble - start) * 1000;
            
            if (totalTime >= 0.8)
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
        
        internal void Dispose() {
            _handle.Complete();
            
            _jobs.Dispose();
            _results.Dispose();
        }

    }

}