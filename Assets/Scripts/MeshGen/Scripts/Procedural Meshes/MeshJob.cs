using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Voxels;
using Voxels.Chunk;
using Voxels.MeshGeneration;

namespace ProceduralMeshes
{
    public struct MeshJob<G, S> : IJob
        where G : struct, IMeshGenerator
        where S : struct, IMeshStreams
    {
        G generator;

        [WriteOnly] S streams;

        public void Execute() => generator.Execute(streams);

        public static JobHandle Schedule(
            Mesh mesh, Mesh.MeshData meshData, G generator, JobHandle dependency
        )
        {
            var job = new MeshJob<G, S>
            {
                generator = generator
            };
            job.streams.Setup(
                meshData,
                mesh.bounds = job.generator.Bounds,
                job.generator.VertexCount,
                job.generator.IndexCount
            );
            return job.Schedule(dependency);
        }
    }

    public class ChunkMeshJob
    {
        [ReadOnly] private readonly ChunkRenderer _meshRenderer;
        [ReadOnly] private readonly VoxelWorld _world;

        public ChunkMeshJob(ChunkRenderer meshRenderer, VoxelWorld world)
        {
            _meshRenderer = meshRenderer;
            _world = world;
        }

        public void Execute(Mesh.MeshData meshData)
        {
            GreedyMesher greedyMesher = new(_meshRenderer.ChunkData, meshData, VoxelWorld.ChunkSize);
            greedyMesher.GenerateVisibleFaces();
            greedyMesher.AddAllFacesToMeshData();
            greedyMesher.WriteMeshData();
        }
    }
}