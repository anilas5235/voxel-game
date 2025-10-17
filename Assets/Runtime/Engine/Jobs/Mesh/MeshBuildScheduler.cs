using System.Collections.Generic;
using Runtime.Engine.Components;
using Runtime.Engine.Data;
using Runtime.Engine.Jobs.Core;
using Runtime.Engine.Settings;
using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.Utils.Logger;
using Runtime.Engine.Voxels.Data;
using Runtime.Engine.Behaviour;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Jobs.Mesh
{
    public class MeshBuildScheduler : JobScheduler
    {
        private readonly ChunkManager _chunkManager;
        private readonly ChunkPool _chunkPool;
        private readonly VoxelRegistry _voxelRegistry;

        private readonly int3 _chunkSize;
        private JobHandle _handle;

        private NativeList<int3> _jobs;
        private ChunkAccessor _chunkAccessor;
        private NativeParallelHashMap<int3, int> _results;
        private UnityEngine.Mesh.MeshDataArray _meshDataArray;
        private UnityEngine.Mesh.MeshDataArray _colliderMeshDataArray;
        private NativeArray<VertexAttributeDescriptor> _vertexParams;
        private NativeArray<VertexAttributeDescriptor> _colliderVertexParams;

        public MeshBuildScheduler(
            VoxelEngineSettings settings,
            ChunkManager chunkManager,
            ChunkPool chunkPool,
            VoxelRegistry voxelRegistry
        )
        {
            _chunkManager = chunkManager;
            _chunkPool = chunkPool;
            _voxelRegistry = voxelRegistry;

            _chunkSize = settings.Chunk.ChunkSize;

            _vertexParams = new NativeArray<VertexAttributeDescriptor>(5, Allocator.Persistent)
            {
                // Int interpolation cause issues
                [0] = new VertexAttributeDescriptor(VertexAttribute.Position),
                [1] = new VertexAttributeDescriptor(VertexAttribute.Normal),
                [2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0),
                [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2),
                [4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4)
            };

            // Collider uses only Position and Normal from CVertex
            _colliderVertexParams = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Persistent)
            {
                [0] = new VertexAttributeDescriptor(VertexAttribute.Position),
                [1] = new VertexAttributeDescriptor(VertexAttribute.Normal)
            };

            _results = new NativeParallelHashMap<int3, int>(settings.Chunk.DrawDistance.CubedSize(),
                Allocator.Persistent);
            _jobs = new NativeList<int3>(Allocator.Persistent);
        }

        internal bool IsReady = true;
        internal bool IsComplete => _handle.IsCompleted;

        internal void Start(List<int3> jobs)
        {
            StartRecord();

            IsReady = false;

            _chunkAccessor = _chunkManager.GetAccessor(jobs);

            foreach (int3 j in jobs)
            {
                _jobs.Add(j);
            }

            _meshDataArray = UnityEngine.Mesh.AllocateWritableMeshData(_jobs.Length);
            _colliderMeshDataArray = UnityEngine.Mesh.AllocateWritableMeshData(_jobs.Length);

            MeshBuildJob job = new()
            {
                Accessor = _chunkAccessor,
                ChunkSize = _chunkSize,
                Jobs = _jobs,
                VertexParams = _vertexParams,
                ColliderVertexParams = _colliderVertexParams,
                MeshDataArray = _meshDataArray,
                ColliderMeshDataArray = _colliderMeshDataArray,
                VoxelEngineRenderGenData = _voxelRegistry.GetVoxelGenData(),
                Results = _results.AsParallelWriter()
            };

            _handle = job.Schedule(_jobs.Length, 1);
        }

        internal void Complete()
        {
            double start = Time.realtimeSinceStartupAsDouble;
            _handle.Complete();

            UnityEngine.Mesh[] meshes = new UnityEngine.Mesh[_jobs.Length];
            UnityEngine.Mesh[] colliderMeshes = new UnityEngine.Mesh[_jobs.Length];

            for (int index = 0; index < _jobs.Length; index++)
            {
                int3 position = _jobs[index];
                ChunkBehaviour cb;
                if (_chunkManager.ReMeshedChunk(position))
                {
                    cb = _chunkPool.Get(position);
                }
                else
                {
                    cb = _chunkPool.Claim(position);
                }

                meshes[_results[position]] = cb.Mesh;
                colliderMeshes[_results[position]] = cb.ColliderMesh;
            }

            UnityEngine.Mesh.ApplyAndDisposeWritableMeshData(
                _meshDataArray,
                meshes,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                MeshUpdateFlags.DontResetBoneBounds
            );

            UnityEngine.Mesh.ApplyAndDisposeWritableMeshData(
                _colliderMeshDataArray,
                colliderMeshes,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                MeshUpdateFlags.DontResetBoneBounds
            );

            foreach (UnityEngine.Mesh m in meshes)
            {
                m.RecalculateBounds();
            }
            foreach (UnityEngine.Mesh cm in colliderMeshes)
            {
                cm.RecalculateBounds();
            }

            double totalTime = (Time.realtimeSinceStartupAsDouble - start) * 1000;
            if (totalTime >= 0.8)
            {
                VoxelEngineLogger.Info<MeshBuildScheduler>(
                    $"Built {_jobs.Length} meshes, Collected Results in <color=red>{totalTime:0.000}</color>ms"
                );
            }

            _results.Clear();
            _jobs.Clear();

            IsReady = true;
            StopRecord();
        }

        internal void Dispose()
        {
            _handle.Complete();

            _vertexParams.Dispose();
            _colliderVertexParams.Dispose();
            _results.Dispose();
            _jobs.Dispose();
        }
    }
}