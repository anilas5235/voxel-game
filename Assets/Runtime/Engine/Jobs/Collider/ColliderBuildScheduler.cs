using System.Collections.Generic;
using Runtime.Engine.Behaviour;
using Runtime.Engine.Components;
using Runtime.Engine.Jobs.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Collider {

    public class ColliderBuildScheduler : JobScheduler {

        private ChunkManager _chunkManager;
        private ChunkPool _chunkPool;
        
        private NativeList<int> _jobs;
        private Dictionary<int3, ChunkBehaviour> _meshes;

        private JobHandle _handle;

        public ColliderBuildScheduler(ChunkManager chunkManager, ChunkPool chunkPool) {
            _chunkManager = chunkManager;
            _chunkPool = chunkPool;
            
            _jobs = new NativeList<int>(Allocator.Persistent);
        }
        
        internal bool IsReady = true;
        
        internal bool IsComplete => _handle.IsCompleted;

        internal void Start(List<int3> jobs) {
            StartRecord();
            
            IsReady = false;

            _meshes = _chunkPool.GetActiveMeshes(jobs);

            foreach ((int3 _, ChunkBehaviour behaviour) in _meshes) {
                if (behaviour.Mesh.vertexCount > 0) { // Avoid colliders for empty meshes
                    _jobs.Add(behaviour.Mesh.GetInstanceID());
                }
            }

            ColliderBuildJob job = new ColliderBuildJob {
                MeshIDs = _jobs
            };
            
            _handle = job.Schedule(_jobs.Length, 1);
        }

        internal void Complete() {
            _handle.Complete();
            
            foreach ((int3 position, ChunkBehaviour behaviour) in _meshes) {
                _chunkPool.ColliderBaked(position);
                _chunkManager.ReCollideChunk(position);
                
                if (behaviour.Mesh.vertexCount <= 0 ) continue;
                behaviour.Collider.sharedMesh = behaviour.Mesh;
            }
            
            _jobs.Clear();
            _meshes = null;
            
            IsReady = true;
            
            StopRecord();
        }

        internal void Dispose() {
            _handle.Complete();

            _jobs.Dispose();
        }
        
    }

}