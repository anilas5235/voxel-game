using System;
using System.Collections.Generic;
using System.Linq;
using Runtime.Engine.Components;
using Runtime.Engine.Jobs.Chunk;
using Runtime.Engine.Jobs.Collider;
using Runtime.Engine.Jobs.MeshHandling;
using Runtime.Engine.Settings;
using Runtime.Engine.ThirdParty.Priority_Queue;
using Runtime.Engine.Utils.Extensions;
using Unity.Mathematics;
using static Runtime.Engine.Jobs.PriorityUtil;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs
{
    /// <summary>
    /// Zentraler Scheduler der Daten-, Mesh- und Collider-Jobs als Round-Robin State Machine orchestriert.
    /// Verwaltet separate Prioritäts-Queues und wählt Batches nach Konfiguration (<see cref="SchedulerSettings"/>).
    /// </summary>
    public class VoxelEngineScheduler
    {
        private enum JobType
        {
            Data,
            Mesh,
            Collider
        }

        private enum SchedulerSteps
        {
            UpdateQueues,
            JobUpdate,
            CollectResults,
        }

        private readonly ChunkScheduler _chunkScheduler;
        private readonly MeshBuildScheduler _meshBuildScheduler;
        private readonly ColliderBuildScheduler _colliderBuildScheduler;

        private readonly ChunkManager _chunkManager;
        private readonly ChunkPool _chunkPool;

        private readonly SimpleFastPriorityQueue<int3, int> _meshQueue;
        private readonly SimpleFastPriorityQueue<int2, int> _dataQueue;
        private readonly SimpleFastPriorityQueue<int3, int> _colliderQueue;

        private readonly HashSet<int3> _viewSet;
        private readonly HashSet<int2> _dataSet;
        private readonly HashSet<int3> _colliderSet;

        private readonly VoxelEngineSettings _settings;

        private SchedulerSteps _currentStep;
        private readonly JobType[] _currentJobTypes = { JobType.Data, JobType.Mesh, JobType.Collider };

        /// <summary>
        /// Erstellt neuen Scheduler und initialisiert alle Queues / Sets.
        /// </summary>
        internal VoxelEngineScheduler(
            VoxelEngineSettings settings,
            MeshBuildScheduler meshBuildScheduler,
            ChunkScheduler chunkScheduler,
            ColliderBuildScheduler colliderBuildScheduler,
            ChunkManager chunkManager,
            ChunkPool chunkPool
        )
        {
            _meshBuildScheduler = meshBuildScheduler;
            _chunkScheduler = chunkScheduler;
            _colliderBuildScheduler = colliderBuildScheduler;

            _chunkManager = chunkManager;
            _chunkPool = chunkPool;

            _meshQueue = new SimpleFastPriorityQueue<int3, int>();
            _dataQueue = new SimpleFastPriorityQueue<int2, int>();
            _colliderQueue = new SimpleFastPriorityQueue<int3, int>();

            _viewSet = new HashSet<int3>();
            _dataSet = new HashSet<int2>();
            _colliderSet = new HashSet<int3>();

            _settings = settings;
        }

        /// <summary>
        /// Ausführung eines Scheduler-Ticks. Führt abhängig vom aktuellen Schritt Queue-Updates, Job-Vergaben oder Result-Sammlung aus.
        /// </summary>
        /// <param name="focus">Fokusposition (z.B. Spieler Chunk Root).</param>
        internal void ScheduleUpdate(int3 focus)
        {
            switch (_currentStep)
            {
                case SchedulerSteps.UpdateQueues:
                    UpdatedQueues(focus, _currentJobTypes[(int)_currentStep]);
                    _currentJobTypes[(int)_currentStep] = NextJobType(_currentJobTypes[(int)_currentStep]);
                    _currentStep = SchedulerSteps.JobUpdate;
                    break;
                case SchedulerSteps.JobUpdate:
                    JobUpdate(_currentJobTypes[(int)_currentStep]);
                    _currentJobTypes[(int)_currentStep] = NextJobType(_currentJobTypes[(int)_currentStep]);
                    _currentStep = SchedulerSteps.CollectResults;
                    break;
                case SchedulerSteps.CollectResults:
                    SchedulerCollectResults(_currentJobTypes[(int)_currentStep]);
                    _currentJobTypes[(int)_currentStep] = NextJobType(_currentJobTypes[(int)_currentStep]);
                    _currentStep = SchedulerSteps.UpdateQueues;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Aktualisiert Prioritäten aller Queues und delegiert Fokus an Manager/Pool.
        /// </summary>
        internal void FocusUpdate(int3 focus)
        {
            UpdateQueuePriorities(focus);
            _chunkManager.FocusUpdate(focus);
            _chunkPool.FocusUpdate(focus);
        }

        private void UpdateQueuePriorities(int3 focus)
        {
            _dataQueue.UpdateAllPriorities(pos => DistPriority(ref pos, ref focus));
            _meshQueue.UpdateAllPriorities(pos => DistPriority(ref pos, ref focus));
            _colliderQueue.UpdateAllPriorities(pos => DistPriority(ref pos, ref focus));
        }

        private void UpdatedQueues(int3 focus, JobType jobType)
        {
            switch (jobType)
            {
                case JobType.Data:
                    EnqueueDataChunks(focus, _settings.Chunk.LoadDistance);
                    break;
                case JobType.Mesh:
                    EnqueueMeshChunks(focus, _settings.Chunk.DrawDistance);
                    break;
                case JobType.Collider:
                    EnqueueColliderChunks(focus, _settings.Chunk.UpdateDistance);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(jobType), jobType, null);
            }
        }

        private void EnqueueMeshChunks(int3 focus, int draw)
        {
            int prioThreshold = _chunkPool.GetPartitionPrioThreshold();
            for (int x = -draw; x <= draw; x++)
            for (int z = -draw; z <= draw; z++)
            {
                if (!CanGenerateMeshForChunk(focus + new int3(x, 0, z))) continue;
                for (int y = 0; y < PartitionsPerChunk; y++)
                {
                    int3 pos = new(x + focus.x, y, z + focus.z);
                    if (_meshQueue.Contains(pos) || !ShouldScheduleForMeshing(pos)) continue;

                    if (-DistPriority(ref pos, ref focus) <= prioThreshold) continue;
                    
                    _meshQueue.Enqueue(pos, DistPriority(ref pos, ref focus));
                }
            }
        }

        private void EnqueueColliderChunks(int3 focus, int update)
        {
            for (int x = -update; x <= update; x++)
            for (int z = -update; z <= update; z++)
            for (int y = 0; y < PartitionsPerChunk; y++)
            {
                int3 pos = new(x + focus.x, y, z + focus.z);
                if (!_colliderQueue.Contains(pos) && ShouldScheduleForBaking(pos) &&
                    CanBakeColliderForChunk(pos))
                {
                    _colliderQueue.Enqueue(pos, DistPriority(ref pos, ref focus));
                }
            }
        }

        private void EnqueueDataChunks(int3 focus, int load)
        {
            for (int x = -load; x <= load; x++)
            for (int z = -load; z <= load; z++)
            {
                int2 pos = focus.xz + new int2(x, z);
                if (!_dataQueue.Contains(pos) && ShouldScheduleForGenerating(pos))
                {
                    _dataQueue.Enqueue(pos, DistPriority(ref pos, ref focus));
                }
            }
        }

        private void JobUpdate(JobType jobType)
        {
            switch (jobType)
            {
                case JobType.Data:
                    if (_dataQueue.Count > 0 && _chunkScheduler.IsReady)
                    {
                        int count = math.min(_settings.Scheduler.StreamingBatchSize, _dataQueue.Count);

                        for (int i = 0; i < count; i++)
                        {
                            _dataSet.Add(_dataQueue.Dequeue());
                        }

                        _chunkScheduler.Start(_dataSet.ToList());
                    }

                    break;
                case JobType.Mesh:
                    if (_meshQueue.Count > 0 && _meshBuildScheduler.IsReady)
                    {
                        int count = math.min(_settings.Scheduler.MeshingBatchSize, _meshQueue.Count);

                        for (int i = 0; i < count; i++)
                        {
                            int3 chunk = _meshQueue.Dequeue();

                            // The chunk may be removed from memory by the time we schedule,
                            // Should we check this only here ?
                            if (CanGenerateMeshForChunk(chunk)) _viewSet.Add(chunk);
                        }

                        _meshBuildScheduler.Start(_viewSet.ToList());
                    }

                    break;
                case JobType.Collider:
                    if (_colliderQueue.Count > 0 && _colliderBuildScheduler.IsReady)
                    {
                        int count = math.min(_settings.Scheduler.ColliderBatchSize, _colliderQueue.Count);

                        for (int i = 0; i < count; i++)
                        {
                            int3 position = _colliderQueue.Dequeue();

                            if (CanBakeColliderForChunk(position)) _colliderSet.Add(position);
                        }

                        _colliderBuildScheduler.Start(_colliderSet.ToList());
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(jobType), jobType, null);
            }
        }

        private void SchedulerCollectResults(JobType jobType)
        {
            switch (jobType)
            {
                case JobType.Data:
                    if (_chunkScheduler.IsComplete && !_chunkScheduler.IsReady)
                    {
                        _chunkScheduler.Complete();
                        _dataSet.Clear();
                    }

                    break;
                case JobType.Mesh:

                    if (_meshBuildScheduler.IsComplete && !_meshBuildScheduler.IsReady)
                    {
                        _meshBuildScheduler.Complete();
                        _viewSet.Clear();
                    }

                    break;
                case JobType.Collider:

                    if (_colliderBuildScheduler.IsComplete && !_colliderBuildScheduler.IsReady)
                    {
                        _colliderBuildScheduler.Complete();
                        _colliderSet.Clear();
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(jobType), jobType, null);
            }
        }

        /// <summary>
        /// Räumt Sub-Scheduler Ressourcen auf.
        /// </summary>
        internal void Dispose()
        {
            _chunkScheduler.Dispose();
            _meshBuildScheduler.Dispose();
            _colliderBuildScheduler.Dispose();
        }

        private bool ShouldScheduleForGenerating(int2 position) =>
            !_chunkManager.IsChunkLoaded(position) && !_dataSet.Contains(position);

        private bool ShouldScheduleForMeshing(int3 position) =>
            (!_chunkPool.IsPartitionActive(position) || _chunkManager.ShouldReMesh(position)) &&
            !_viewSet.Contains(position);

        private bool ShouldScheduleForBaking(int3 position) =>
            (!_chunkPool.IsCollidable(position) || _chunkManager.ShouldReCollide(position)) &&
            !_colliderSet.Contains(position);

        /// <summary>
        /// Checks if the specified chunks and it's neighbors are generated.
        /// </summary>
        /// <param name="position">Position of chunk to check</param>
        /// <returns>Is it ready to be meshed</returns>
        private bool CanGenerateMeshForChunk(int3 position)
        {
            bool result = true;

            for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                int2 pos = position.xz + new int2(x, z);
                result &= _chunkManager.IsChunkLoaded(pos);
            }

            return result;
        }

        private bool CanBakeColliderForChunk(int3 position) => _chunkPool.IsPartitionActive(position);

        #region RuntimeStatsAPI

        /// <summary>
        /// Durchschnittliche Laufzeit von Datenjobs.
        /// </summary>
        public float DataAvgTiming => _chunkScheduler.AvgTime;

        /// <summary>
        /// Durchschnittliche Laufzeit von Meshjobs.
        /// </summary>
        public float MeshAvgTiming => _meshBuildScheduler.AvgTime;

        /// <summary>
        /// Durchschnittliche Laufzeit von Colliderjobs.
        /// </summary>
        public float BakeAvgTiming => _colliderBuildScheduler.AvgTime;

        /// <summary>
        /// Anzahl Chunks in Daten-Queue.
        /// </summary>
        public int DataQueueCount => _dataQueue.Count;

        /// <summary>
        /// Anzahl Chunks in Mesh-Queue.
        /// </summary>
        public int MeshQueueCount => _meshQueue.Count;

        /// <summary>
        /// Anzahl Chunks in Collider-Queue.
        /// </summary>
        public int BakeQueueCount => _colliderQueue.Count;

        #endregion

        private static JobType NextJobType(JobType currentJobType)
        {
            return currentJobType switch
            {
                JobType.Data => JobType.Mesh,
                JobType.Mesh => JobType.Collider,
                JobType.Collider => JobType.Data,
                _ => throw new ArgumentOutOfRangeException(nameof(currentJobType), currentJobType, null)
            };
        }
    }
}