using System.Collections.Generic;
using System.Linq;
using Runtime.Engine.Components;
using Runtime.Engine.Jobs.Chunk;
using Runtime.Engine.Jobs.Collider;
using Runtime.Engine.Jobs.Mesh;
using Runtime.Engine.Settings;
using Runtime.Engine.ThirdParty.Priority_Queue;
using Runtime.Engine.Utils.Extensions;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs {
    
    public class VoxelEngineScheduler {
        
        private readonly ChunkScheduler _chunkScheduler;
        private readonly MeshBuildScheduler _meshBuildScheduler;
        private readonly ColliderBuildScheduler _colliderBuildScheduler;

        private readonly ChunkManager _chunkManager;
        private readonly ChunkPool _chunkPool;

        private readonly SimpleFastPriorityQueue<int3, int> _viewQueue;
        private readonly SimpleFastPriorityQueue<int3, int> _dataQueue;
        private readonly SimpleFastPriorityQueue<int3, int> _colliderQueue;

        private readonly HashSet<int3> _viewSet;
        private readonly HashSet<int3> _dataSet;
        private readonly HashSet<int3> _colliderSet;

        private readonly VoxelEngineSettings _settings;

        internal VoxelEngineScheduler(
            VoxelEngineSettings settings, 
            MeshBuildScheduler meshBuildScheduler,
            ChunkScheduler chunkScheduler,
            ColliderBuildScheduler colliderBuildScheduler,
            ChunkManager chunkManager,
            ChunkPool chunkPool
        ) {
            _meshBuildScheduler = meshBuildScheduler;
            _chunkScheduler = chunkScheduler;
            _colliderBuildScheduler = colliderBuildScheduler;

            _chunkManager = chunkManager;
            _chunkPool = chunkPool;

            _viewQueue = new SimpleFastPriorityQueue<int3, int>();
            _dataQueue = new SimpleFastPriorityQueue<int3, int>();
            _colliderQueue = new SimpleFastPriorityQueue<int3, int>();

            _viewSet = new HashSet<int3>();
            _dataSet = new HashSet<int3>();
            _colliderSet = new HashSet<int3>();

            _settings = settings;
        }

        // Priority Updates for Reclaim
        // At max 2 Queues are updated in total (ViewReclaimQueue, DataReclaimQueue)
        internal void FocusUpdate(int3 focus) {
            _chunkManager.FocusUpdate(focus);
            _chunkPool.FocusUpdate(focus);
        }

        // TODO : This thing takes 4ms every frame need to make a reactive system and maybe try the fast queue
        // At max 3 Queues are updated in total (ViewQueue, DataQueue, ColliderQueue)
        internal void SchedulerUpdate(int3 focus) {
            var load = _settings.Chunk.LoadDistance;
            var draw = _settings.Chunk.DrawDistance;
            var update = _settings.Chunk.UpdateDistance;

            for (var x = -load; x <= load; x++) {
                for (var z = -load; z <= load; z++) {
                    for (var y = -load; y <= load; y++) {
                        var pos = focus + _settings.Chunk.ChunkSize.MemberMultiply(x, y, z);

                        if (
                            (x >= -draw && x <= draw) &&
                            (y >= -draw && y <= draw) &&
                            (z >= -draw && z <= draw)
                        ) {
                            if (_viewQueue.Contains(pos)) {
                                _viewQueue.UpdatePriority(pos, (pos - focus).SqrMagnitude());
                            } else if (ShouldScheduleForMeshing(pos) && CanGenerateMeshForChunk(pos)) {
                                _viewQueue.Enqueue(pos, (pos - focus).SqrMagnitude());
                            }
                        }
                        
                        if (
                            (x >= -update && x <= update) &&
                            (y >= -update && y <= update) &&
                            (z >= -update && z <= update)
                        ) {
                            if (_colliderQueue.Contains(pos)) {
                                _colliderQueue.UpdatePriority(pos, (pos - focus).SqrMagnitude());
                            } else if (ShouldScheduleForBaking(pos) && CanBakeColliderForChunk(pos)) {
                                _colliderQueue.Enqueue(pos, (pos - focus).SqrMagnitude());
                            }
                        }

                        if (_dataQueue.Contains(pos)) {
                            _dataQueue.UpdatePriority(pos, (pos - focus).SqrMagnitude());
                        } else if (ShouldScheduleForGenerating(pos)) {
                            _dataQueue.Enqueue(pos, (pos - focus).SqrMagnitude());
                        }
                    }
                }
            }
        }

        internal void JobUpdate() {
            if (_dataQueue.Count > 0 && _chunkScheduler.IsReady) {
                var count = math.min(_settings.Scheduler.StreamingBatchSize, _dataQueue.Count);
                
                for (var i = 0; i < count; i++) {
                    _dataSet.Add(_dataQueue.Dequeue());
                }
                
                _chunkScheduler.Start(_dataSet.ToList());
            }  
            
            if (_viewQueue.Count > 0 && _meshBuildScheduler.IsReady) {
                var count = math.min(_settings.Scheduler.MeshingBatchSize, _viewQueue.Count);
                
                for (var i = 0; i < count; i++) {
                    var chunk = _viewQueue.Dequeue();
                    
                    // The chunk may be removed from memory by the time we schedule,
                    // Should we check this only here ?
                    if (CanGenerateMeshForChunk(chunk)) _viewSet.Add(chunk);
                }

                _meshBuildScheduler.Start(_viewSet.ToList());
            }

            if (_colliderQueue.Count > 0 && _colliderBuildScheduler.IsReady) {
                var count = math.min(_settings.Scheduler.ColliderBatchSize, _colliderQueue.Count);

                for (var i = 0; i < count; i++) {
                    var position = _colliderQueue.Dequeue();

                    if (CanBakeColliderForChunk(position)) _colliderSet.Add(position);
                }
                
                _colliderBuildScheduler.Start(_colliderSet.ToList());
            }
        }

        internal void SchedulerLateUpdate() {
            if (_chunkScheduler.IsComplete && !_chunkScheduler.IsReady) {
                _chunkScheduler.Complete();
                _dataSet.Clear();
            }
            
            if (_meshBuildScheduler.IsComplete && !_meshBuildScheduler.IsReady) {
                _meshBuildScheduler.Complete();
                _viewSet.Clear();
            }

            if (_colliderBuildScheduler.IsComplete && !_colliderBuildScheduler.IsReady) {
                _colliderBuildScheduler.Complete();
                _colliderSet.Clear();
            }
        }

        internal void Dispose() {
            _chunkScheduler.Dispose();
            _meshBuildScheduler.Dispose();
            _colliderBuildScheduler.Dispose();
        }

        private bool ShouldScheduleForGenerating(int3 position) => !_chunkManager.IsChunkLoaded(position) && !_dataSet.Contains(position);
        private bool ShouldScheduleForMeshing(int3 position) => (!_chunkPool.IsActive(position) || _chunkManager.ShouldReMesh(position)) && !_viewSet.Contains(position);
        private bool ShouldScheduleForBaking(int3 position) => (!_chunkPool.IsCollidable(position) || _chunkManager.ShouldReCollide(position)) && !_colliderSet.Contains(position);

        /// <summary>
        /// Checks if the specified chunks and it's neighbours are generated
        /// </summary>
        /// <param name="position">Position of chunk to check</param>
        /// <returns>Is it ready to be meshed</returns>
        private bool CanGenerateMeshForChunk(int3 position) {
            var result = true;
            
            for (var x = -1; x <= 1; x++) {
                for (var z = -1; z <= 1; z++) {
                    for (var y = -1; y <= 1; y++) {
                        var pos = position + _settings.Chunk.ChunkSize.MemberMultiply(x, y, z);
                        result &= _chunkManager.IsChunkLoaded(pos);
                    }
                }
            }

            return result;
        }

        private bool CanBakeColliderForChunk(int3 position) => _chunkPool.IsActive(position);

        #region RuntimeStatsAPI

        public float DataAvgTiming => _chunkScheduler.AvgTime;
        public float MeshAvgTiming => _meshBuildScheduler.AvgTime;
        public float BakeAvgTiming => _colliderBuildScheduler.AvgTime;

        public int DataQueueCount => _dataQueue.Count;
        public int MeshQueueCount => _viewQueue.Count;
        public int BakeQueueCount => _colliderQueue.Count;

        #endregion

    }

}