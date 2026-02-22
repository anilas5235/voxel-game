using System;
using Runtime.Engine.Data;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        #region Constants

        private const int VoxelCount4 = VoxelsPerPartition * 4;
        private const int VoxelCount6 = VoxelsPerPartition * 6;

        #endregion

        #region Structs

        [BurstCompile]
        internal struct PartitionJobResult
        {
            public int Index;
            public int3 PartitionPos;
            public Bounds MeshBounds;
            public Bounds ColliderBounds;
        }

        [BurstCompile]
        private struct PartitionJobData : IDisposable
        {
            public readonly Mesh.MeshData Mesh;
            public readonly Mesh.MeshData ColliderMesh;
            public readonly int2 ChunkPos;
            public readonly int3 PartitionPos;

            public MeshBuffer MeshBuffer;

            public NativeHashSet<int3> SeeThroughVoxels;
            public NativeHashSet<int3> CollisionVoxels;
            public NativeHashMap<int3, ushort> FoliageVoxels;
            public NativeHashMap<int3, ushort> TransparentVoxels;
            public NativeHashMap<int3, ushort> SolidVoxels;

            public PartitionLightData PartitionLightData;

            public ChunkVoxelData ChunkVoxelData;
            public int RenderVertexCount;
            public int CollisionVertexCount;

            public bool HasNoCollision => CollisionVoxels.IsEmpty;
            public bool HasNoFoliage => FoliageVoxels.IsEmpty;
            public bool HasNoTransparent => TransparentVoxels.IsEmpty;
            public bool HasNoSolid => SolidVoxels.IsEmpty;
            public bool HasNoVoxels => HasNoFoliage && HasNoTransparent && HasNoSolid;

            internal PartitionJobData(Mesh.MeshData mesh, Mesh.MeshData colliderMesh, int3 partitionPos,
                PartitionLightData partitionLightData, ChunkVoxelData chunkVoxelData)
            {
                Mesh = mesh;
                ColliderMesh = colliderMesh;
                PartitionPos = partitionPos;
                PartitionLightData = partitionLightData;
                ChunkVoxelData = chunkVoxelData;
                ChunkPos = partitionPos.xz;
                MeshBuffer = new MeshBuffer
                {
                    VertexBuffer = new NativeList<Vertex>(VoxelCount4, Allocator.Temp),
                    CVertexBuffer = new NativeList<CVertex>(VoxelCount4, Allocator.Temp),
                    SolidIndexBuffer = new NativeList<ushort>(VoxelCount6, Allocator.Temp),
                    TransparentIndexBuffer = new NativeList<ushort>(VoxelCount6, Allocator.Temp),
                    FoliageIndexBuffer = new NativeList<ushort>(VoxelCount6, Allocator.Temp),
                    CIndexBuffer = new NativeList<ushort>(VoxelCount6, Allocator.Temp)
                };

                SeeThroughVoxels = new NativeHashSet<int3>(VoxelsPerPartition, Allocator.Temp);
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
                SeeThroughVoxels.Dispose();
                CollisionVoxels.Dispose();
                TransparentVoxels.Dispose();
                FoliageVoxels.Dispose();
                SolidVoxels.Dispose();
            }
        }

        [BurstCompile]
        private struct AxisInfo
        {
            public int UAxis, VAxis, ULimit, VLimit;
        }

        [BurstCompile]
        private struct UVQuad
        {
            public float4 Uv1, Uv2, Uv3, Uv4;
        }

        [BurstCompile]
        private struct VQuad
        {
            public float3 V1, V2, V3, V4;

            public void OffsetAll(float3 offset)
            {
                V1 += offset;
                V2 += offset;
                V3 += offset;
                V4 += offset;
            }
        }

        private interface IMaskComparable<T>
        {
            bool CompareTo(T other);
        }

        [BurstCompile]
        private readonly struct CMask : IMaskComparable<CMask>
        {
            internal readonly sbyte Normal;

            public CMask(sbyte normal)
            {
                Normal = normal;
            }

            public bool CompareTo(CMask other)
            {
                return Normal == other.Normal;
            }
        }

        #endregion

        #region Helpers

        [BurstCompile]
        private ushort GetVoxel(ref PartitionJobData jobData, in int3 voxelPos)
        {
            int3 chunkLocalPos = voxelPos;
            chunkLocalPos += jobData.PartitionPos * VoxelsPerPartition;
            return ChunkAccessor.InChunkBounds(chunkLocalPos)
                ? jobData.ChunkVoxelData.GetVoxel(chunkLocalPos)
                : Accessor.GetVoxelInPartition(jobData.PartitionPos, voxelPos);
        }

        [BurstCompile]
        private MeshLayer GetMeshLayer(ushort voxelId, VoxelEngineRenderGenData renderGenData)
        {
            return renderGenData.GetMeshLayer(voxelId);
        }

        [BurstCompile]
        private void ClearColMaskRegion(NativeArray<CMask> normalMask, int n, int width, int height, int axis1Limit)
        {
            for (int l = 0; l < height; ++l)
            for (int k = 0; k < width; ++k)
                normalMask[n + k + l * axis1Limit] = default;
        }

        #endregion
    }
}