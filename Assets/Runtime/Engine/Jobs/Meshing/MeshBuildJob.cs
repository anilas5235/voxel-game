using System;
using Runtime.Engine.Data;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Meshing
{
    /// <summary>
    /// Burst-compiled parallel job that generates render and collider mesh data for a list of chunk positions
    /// using the greedy mesher and writes the results into provided <see cref="UnityEngine.Mesh.MeshDataArray"/>
    /// instances while recording the position-to-index mapping.
    /// </summary>
    [BurstCompile]
    internal partial struct MeshBuildJob : IJobParallelFor
    {
        private const int VoxelCount4 = VoxelsPerPartition * 4;
        private const int VoxelCount6 = VoxelsPerPartition * 6;

        [ReadOnly] public NativeArray<VertexAttributeDescriptor> VertexParams;
        [ReadOnly] public NativeArray<VertexAttributeDescriptor> ColliderVertexParams;
        [ReadOnly] public ChunkAccessor Accessor;
        [ReadOnly] public NativeList<int3> Jobs;
        [WriteOnly] public NativeParallelHashMap<int3, int>.ParallelWriter Results;
        [ReadOnly] public VoxelEngineRenderGenData RenderGenData;
        public Mesh.MeshDataArray MeshDataArray;
        public Mesh.MeshDataArray ColliderMeshDataArray;

        private struct PartitionJobData : IDisposable
        {
            public readonly Mesh.MeshData Mesh;
            public readonly Mesh.MeshData ColliderMesh;
            public readonly int2 ChunkPos;
            public readonly int3 PartitionPos;
            public readonly int3 YOffset;

            public MeshBuffer MeshBuffer;

            public NativeHashSet<int3> AirVoxels;
            public NativeHashSet<int3> CollisionVoxels;
            public NativeHashMap<int3, ushort> FoliageVoxels;
            public NativeHashMap<int3, ushort> TransparentVoxels;
            public NativeHashMap<int3, ushort> SolidVoxels;

            public int RenderVertexCount;
            public int CollisionVertexCount;

            public bool HasNoCollision => CollisionVoxels.IsEmpty;
            public bool HasNoFoliage => FoliageVoxels.IsEmpty;
            public bool HasNoTransparent => TransparentVoxels.IsEmpty;
            public bool HasNoSolid => SolidVoxels.IsEmpty;

            internal PartitionJobData(Mesh.MeshData mesh, Mesh.MeshData colliderMesh, int3 partitionPos)
            {
                Mesh = mesh;
                ColliderMesh = colliderMesh;
                PartitionPos = partitionPos;
                ChunkPos = partitionPos.xz;
                YOffset = new int3(0, partitionPos.y * PartitionHeight, 0);
                MeshBuffer = new MeshBuffer
                {
                    VertexBuffer = new NativeList<Vertex>(VoxelCount4, Allocator.Temp),
                    CVertexBuffer = new NativeList<CVertex>(VoxelCount4, Allocator.Temp),
                    SolidIndexBuffer = new NativeList<int>(VoxelCount6, Allocator.Temp),
                    TransparentIndexBuffer = new NativeList<int>(VoxelCount6, Allocator.Temp),
                    FoliageIndexBuffer = new NativeList<int>(VoxelCount6, Allocator.Temp),
                    CIndexBuffer = new NativeList<int>(VoxelCount6, Allocator.Temp)
                };

                AirVoxels = new NativeHashSet<int3>(VoxelsPerPartition, Allocator.Temp);
                CollisionVoxels = new NativeHashSet<int3>(VoxelsPerPartition, Allocator.Temp);
                FoliageVoxels = new NativeHashMap<int3, ushort>(VoxelsPerPartition, Allocator.Temp);
                TransparentVoxels = new NativeHashMap<int3, ushort>(VoxelsPerPartition, Allocator.Temp);
                SolidVoxels = new NativeHashMap<int3, ushort>(VoxelsPerPartition, Allocator.Temp);

                RenderVertexCount = 0;
                CollisionVertexCount = 0;
            }

            public void Dispose()
            {
                MeshBuffer.Dispose();
                AirVoxels.Dispose();
                CollisionVoxels.Dispose();
                TransparentVoxels.Dispose();
                FoliageVoxels.Dispose();
                SolidVoxels.Dispose();
            }
        }

        /// <summary>
        /// Executes mesh generation for the given job index and fills mesh and collider submesh data
        /// for the corresponding chunk position.
        /// </summary>
        /// <param name="index">Index of the chunk position to process within the <see cref="Jobs"/> list.</param>
        public void Execute(int index)
        {
            PartitionJobData jobData = new(MeshDataArray[index], ColliderMeshDataArray[index], Jobs[index]);

            SortVoxels(ref jobData);

            ConstructSolid(ref jobData);

            ConstructTransparent(ref jobData);

            ConstructFoliage(ref jobData);

            //ConstructCollision(ref jobData);

            FillRenderMeshData(in jobData);

            //FillColliderMeshData(in jobData);

            Results.TryAdd(jobData.PartitionPos, index);

            jobData.Dispose();
        }

        private void ConstructCollision(ref PartitionJobData jobData)
        {
        }

        private void SortVoxels(ref PartitionJobData jobData)
        {
            for (int y = 0; y < PartitionHeight; y++)
            {
                for (int z = 0; z < PartitionDepth; z++)
                {
                    for (int x = 0; x < PartitionWidth; x++)
                    {
                        int3 localPos = new(x, y + jobData.YOffset.y, z);
                        ushort voxelId = Accessor.GetVoxelInChunk(jobData.ChunkPos, localPos);
                        VoxelRenderDef renderDef = RenderGenData.GetRenderDef(voxelId);

                        if (renderDef.Collision) jobData.CollisionVoxels.Add(localPos);

                        if (renderDef.IsAir) jobData.AirVoxels.Add(localPos);
                        else if (renderDef.IsFoliage) jobData.FoliageVoxels.Add(localPos, voxelId);
                        else if (renderDef.IsTransparent) jobData.TransparentVoxels.Add(localPos, voxelId);
                        else jobData.SolidVoxels.Add(localPos, voxelId);
                    }
                }
            }
        }

        private void FillColliderMeshData(in PartitionJobData jobData)
        {
            MeshBuffer meshBuffer = jobData.MeshBuffer;
            Mesh.MeshData colliderMesh = jobData.ColliderMesh;

            int cVertexCount = meshBuffer.CVertexBuffer.Length;
            colliderMesh.SetVertexBufferParams(cVertexCount, ColliderVertexParams);
            colliderMesh.GetVertexData<CVertex>().CopyFrom(meshBuffer.CVertexBuffer.AsArray());

            int cIndexCount = meshBuffer.CIndexBuffer.Length;
            colliderMesh.SetIndexBufferParams(cIndexCount, IndexFormat.UInt32);
            NativeArray<int> cIndexBuffer = colliderMesh.GetIndexData<int>();
            if (cIndexCount > 0)
                NativeArray<int>.Copy(meshBuffer.CIndexBuffer.AsArray(), 0, cIndexBuffer, 0, cIndexCount);

            colliderMesh.subMeshCount = 1;
            SubMeshDescriptor cDesc = new(0, cIndexCount);
            colliderMesh.SetSubMesh(0, cDesc, MeshFlags);
        }

        private void FillRenderMeshData(in PartitionJobData jobData)
        {
            MeshBuffer meshBuffer = jobData.MeshBuffer;
            Mesh.MeshData mesh = jobData.Mesh;

            int vertexCount = meshBuffer.VertexBuffer.Length;
            mesh.SetVertexBufferParams(vertexCount, VertexParams);
            mesh.GetVertexData<Vertex>().CopyFrom(meshBuffer.VertexBuffer.AsArray());

            int solidIndexes = meshBuffer.SolidIndexBuffer.Length;
            int transparentIndexes = meshBuffer.TransparentIndexBuffer.Length;
            int foliageIndexes = meshBuffer.FoliageIndexBuffer.Length;
            
            mesh.SetIndexBufferParams(solidIndexes + transparentIndexes + foliageIndexes, IndexFormat.UInt32);
            NativeArray<int> indexBuffer = mesh.GetIndexData<int>();
            NativeArray<int>.Copy(meshBuffer.SolidIndexBuffer.AsArray(), 0, indexBuffer, 0, solidIndexes);
            if (transparentIndexes > 1)
            {
                NativeArray<int>.Copy(meshBuffer.TransparentIndexBuffer.AsArray(), 0, indexBuffer, solidIndexes,
                    transparentIndexes);
            }
            
            if(foliageIndexes > 1)
            {
                NativeArray<int>.Copy(meshBuffer.FoliageIndexBuffer.AsArray(), 0, indexBuffer, solidIndexes + transparentIndexes,
                    foliageIndexes);
            }

            mesh.subMeshCount = 3;
            SubMeshDescriptor descriptor0 = new(0, solidIndexes);
            SubMeshDescriptor descriptor1 = new(solidIndexes, transparentIndexes);
            SubMeshDescriptor descriptor2 = new(solidIndexes + transparentIndexes, foliageIndexes);

            mesh.SetSubMesh(0, descriptor0, MeshFlags);
            mesh.SetSubMesh(1, descriptor1, MeshFlags);
            mesh.SetSubMesh(2, descriptor2, MeshFlags);
        }
    }
}