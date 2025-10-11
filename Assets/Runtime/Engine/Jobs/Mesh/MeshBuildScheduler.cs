using System.Collections.Generic;
using Runtime.Engine.Components;
using Runtime.Engine.Data;
using Runtime.Engine.Jobs.Core;
using Runtime.Engine.Settings;
using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.Voxels.Data;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Runtime.Engine.Jobs.Mesh
{
    public class MeshBuildScheduler : JobScheduler
    {
        private readonly ChunkManager _chunkManager;
        private readonly ChunkPool _chunkPool;
        private readonly VoxelRegistry _voxelRegistry;

        private int3 _chunkSize;
        private JobHandle _handle;

        private NativeList<int3> _jobs;
        private ChunkAccessor _chunkAccessor;
        private NativeParallelHashMap<int3, int> _results;
        private UnityEngine.Mesh.MeshDataArray _meshDataArray;
        private NativeArray<VertexAttributeDescriptor> _vertexParams;

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

            _vertexParams = new NativeArray<VertexAttributeDescriptor>(6, Allocator.Persistent);

            // Int interpolation cause issues
            _vertexParams[0] =
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
            _vertexParams[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
            _vertexParams[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4);
            _vertexParams[3] =
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3);
            _vertexParams[4] =
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2);
            _vertexParams[5] =
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4);

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

            MeshBuildJob job = new()
            {
                Accessor = _chunkAccessor,
                ChunkSize = _chunkSize,
                Jobs = _jobs,
                VertexParams = _vertexParams,
                MeshDataArray = _meshDataArray,
                VoxelGenData = _voxelRegistry.GetVoxelGenData(),
                Results = _results.AsParallelWriter()
            };

            _handle = job.Schedule(_jobs.Length, 1);
        }

        internal void Complete()
        {
            _handle.Complete();

            UnityEngine.Mesh[] meshes = new UnityEngine.Mesh[_jobs.Length];

            for (int index = 0; index < _jobs.Length; index++)
            {
                int3 position = _jobs[index];

                if (_chunkManager.ReMeshedChunk(position))
                {
                    meshes[_results[position]] = _chunkPool.Get(position).Mesh;
                }
                else
                {
                    meshes[_results[position]] = _chunkPool.Claim(position).Mesh;
                }
            }

            UnityEngine.Mesh.ApplyAndDisposeWritableMeshData(
                _meshDataArray,
                meshes,
                MeshUpdateFlags.DontRecalculateBounds
            );

            for (int index = 0; index < meshes.Length; index++)
            {
                meshes[index].RecalculateBounds();
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
            _results.Dispose();
            _jobs.Dispose();
        }
    }
}