using Runtime.Engine.Data;
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
        private bool BuildMasks(ref PartitionJobData jobData, int3 posItr, int3 directionMask, AxisInfo axInfo,
            out NativeArray<Mask> normalMask, bool posNormal)
        {
            normalMask = new NativeArray<Mask>(axInfo.ULimit * axInfo.VLimit, Allocator.Temp);

            int n = 0;
            bool hasSurface = false;

            for (posItr[axInfo.VAxis] = 0; posItr[axInfo.VAxis] < axInfo.VLimit; ++posItr[axInfo.VAxis])
            {
                for (posItr[axInfo.UAxis] = 0; posItr[axInfo.UAxis] < axInfo.ULimit; ++posItr[axInfo.UAxis])
                {
                    int3 currentCoord = posItr + jobData.YOffset;

                    if (!jobData.SolidVoxels.ContainsKey(currentCoord))
                    {
                        normalMask[n] = default;
                        n++;
                        continue;
                    }

                    int3 neighborCoord = currentCoord + directionMask;

                    ushort currentVoxel = jobData.SolidVoxels[currentCoord];
                    ushort neighborVoxel = Accessor.GetVoxelInChunk(jobData.ChunkPos, neighborCoord);

                    VoxelRenderDef neighborDef = RenderGenData.GetRenderDef(neighborVoxel);
                    VoxelRenderDef currentDef = RenderGenData.GetRenderDef(currentVoxel);

                    MeshLayer currentLayer = currentDef.MeshLayer;
                    MeshLayer neighborLayer = neighborDef.MeshLayer;

                    if (!currentDef.AlwaysRenderAllFaces && currentLayer == neighborLayer)
                    {
                        normalMask[n] = default;
                    }
                    else
                    {
                        int4 ao = ComputeAOMask(neighborCoord, jobData.ChunkPos, axInfo);
                        normalMask[n] = new Mask(currentVoxel, currentLayer, posNormal ? (sbyte)1 : (sbyte)-1, ao);
                        hasSurface = true;
                    }
                    n++;
                }
            }

            return hasSurface;
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
        private sbyte ComputeTopVoxelOfType(int3 coord, ushort currentVoxelId, ref PartitionJobData jobData)
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