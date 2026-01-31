using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        #region Mask Helpers

        /// <summary>
        /// Builds surface & collider masks for the current slice.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private SliceInfo BuildMasks(PartitionJobData jobData, int3 chunkItr, int3 directionMask,
            AxisInfo axInfo, NativeArray<Mask> normalMask, NativeArray<Mask> colliderMask)
        {
            int n = 0;
            bool hasSurface = false;
            bool hasCollider = false;

            for (chunkItr[axInfo.VAxis] = 0; chunkItr[axInfo.VAxis] < axInfo.VLimit; ++chunkItr[axInfo.VAxis])
            {
                for (chunkItr[axInfo.UAxis] = 0; chunkItr[axInfo.UAxis] < axInfo.ULimit; ++chunkItr[axInfo.UAxis])
                {
                    int3 currentCoord = chunkItr + jobData.YOffset;
                    int3 neighborCoord = currentCoord + directionMask;

                    ushort currentVoxel = Accessor.GetVoxelInChunk(jobData.ChunkPos, currentCoord);
                    ushort neighborVoxel = Accessor.GetVoxelInChunk(jobData.ChunkPos, neighborCoord);

                    VoxelRenderDef currentDef = RenderGenData.GetRenderDef(currentVoxel);
                    VoxelRenderDef neighborDef = RenderGenData.GetRenderDef(neighborVoxel);

                    MeshLayer currentLayer = currentDef.MeshLayer;
                    MeshLayer neighborLayer = neighborDef.MeshLayer;

                    // Flora: collect for separate foliage pass, still emit a backface to keep AO continuity
                    if (currentDef.VoxelType == VoxelType.Flora)
                    {
                        jobData.FoliageVoxels.Add(currentCoord);
                        if (neighborDef.VoxelType == VoxelType.Flora)
                        {
                            normalMask[n] = default;
                        }
                        else
                        {
                            int4 floraAo = ComputeAOMask(currentCoord, jobData.ChunkPos, axInfo);
                            sbyte neighborTopOpen = ComputeTopVoxelOfType(neighborCoord, neighborVoxel, jobData);
                            normalMask[n] = new Mask(neighborVoxel, neighborLayer, -1, floraAo, neighborTopOpen);
                            hasSurface = true;
                        }
                    }
                    else if (ShouldSkipFace(currentDef, neighborDef))
                    {
                        normalMask[n] = default;
                    }
                    else
                    {
                        bool currentOwns = IsCurrentOwner(currentLayer, neighborDef);
                        if (currentOwns)
                        {
                            int4 ao = ComputeAOMask(neighborCoord, jobData.ChunkPos, axInfo);
                            sbyte topOpen = ComputeTopVoxelOfType(currentCoord, currentVoxel, jobData);
                            normalMask[n] = new Mask(currentVoxel, currentLayer, 1, ao, topOpen);
                        }
                        else
                        {
                            int4 ao = ComputeAOMask(currentCoord, jobData.ChunkPos, axInfo);
                            sbyte topOpen = ComputeTopVoxelOfType(neighborCoord, neighborVoxel, jobData);
                            normalMask[n] = new Mask(neighborVoxel, neighborLayer, -1, ao, topOpen);
                        }

                        hasSurface = true;
                    }

                    bool currentCollidable = currentDef.Collision;
                    bool compareCollidable = neighborDef.Collision;
                    if (currentCollidable ^ compareCollidable)
                    {
                        sbyte normal = currentCollidable ? (sbyte)1 : (sbyte)-1;
                        colliderMask[n] = new Mask(1, MeshLayer.Solid, normal, new int4(0, 0, 0, 0), 0);
                        hasCollider = true;
                    }
                    else
                    {
                        colliderMask[n] = default;
                    }

                    n++;
                }
            }

            return new SliceInfo { HasSurface = hasSurface, HasCollider = hasCollider };
        }

        [BurstCompile]
        private bool ShouldSkipFace(VoxelRenderDef currentDef, VoxelRenderDef neighborDef)
        {
            return !currentDef.AlwaysRenderAllFaces &&
                   currentDef.MeshLayer == neighborDef.MeshLayer &&
                   neighborDef.VoxelType != VoxelType.Flora;
        }

        [BurstCompile]
        private bool IsCurrentOwner(MeshLayer currentLayer, VoxelRenderDef neighborDef)
        {
            return currentLayer < neighborDef.MeshLayer || neighborDef.VoxelType == VoxelType.Flora;
        }

        [BurstCompile]
        private sbyte ComputeTopVoxelOfType(int3 coord, ushort currentVoxelId, PartitionJobData jobData)
        {
            ushort aboveId = Accessor.GetVoxelInChunk(jobData.ChunkPos, coord + YOne);
            return (sbyte)(aboveId != currentVoxelId ? 1 : 0);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int FindQuadWidth(NativeArray<Mask> normalMask, int n, Mask currentMask, int start, int max)
        {
            int width;
            for (width = 1; start + width < max && normalMask[n + width].CompareTo(currentMask); width++)
            {
            }

            return width;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int FindQuadHeight(NativeArray<Mask> normalMask, int n, Mask currentMask, int axis1Limit,
            int axis2Limit, int width, int j)
        {
            int height;
            bool done = false;
            for (height = 1; j + height < axis2Limit; height++)
            {
                for (int k = 0; k < width; ++k)
                {
                    if (normalMask[n + k + height * axis1Limit].CompareTo(currentMask)) continue;
                    done = true;
                    break;
                }

                if (done) break;
            }

            return height;
        }

        #endregion
    }
}
