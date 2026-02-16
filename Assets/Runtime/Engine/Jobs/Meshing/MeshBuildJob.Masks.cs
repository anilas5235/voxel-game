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
        private bool BuildMasks(ref PartitionJobData jobData, ref NativeHashMap<int3, ushort> sortedVoxels, int3 posItr,
            int3 dirMask, AxisInfo axInfo, ref NativeArray<Mask> posNormalMask, ref NativeArray<Mask> negNormalMask)
        {
            int n = 0;
            bool hasSurface = false;

            for (posItr[axInfo.VAxis] = 0; posItr[axInfo.VAxis] < axInfo.VLimit; ++posItr[axInfo.VAxis])
            {
                for (posItr[axInfo.UAxis] = 0; posItr[axInfo.UAxis] < axInfo.ULimit; ++posItr[axInfo.UAxis])
                {
                    posNormalMask[n] = default;
                    negNormalMask[n] = default;
                    

                    if (!sortedVoxels.ContainsKey(posItr))
                    {
                        n++;
                        continue;
                    }

                    hasSurface |= TryAddMask(jobData, dirMask, axInfo, posItr, n, posNormalMask, true, ref sortedVoxels);
                    hasSurface |= TryAddMask(jobData, dirMask, axInfo, posItr, n, negNormalMask, false, ref sortedVoxels);
                    n++;
                }
            }

            return hasSurface;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private bool BuildColliderMasks(ref PartitionJobData jobData, int3 posItr, int3 dirMask, AxisInfo axInfo,
            ref NativeArray<CMask> posNormalMask, ref NativeArray<CMask> negNormalMask)
        {
            int n = 0;
            bool hasCollision = false;

            for (posItr[axInfo.VAxis] = 0; posItr[axInfo.VAxis] < axInfo.VLimit; ++posItr[axInfo.VAxis])
            {
                for (posItr[axInfo.UAxis] = 0; posItr[axInfo.UAxis] < axInfo.ULimit; ++posItr[axInfo.UAxis])
                {
                    posNormalMask[n] = default;
                    negNormalMask[n] = default;

                    if (!jobData.CollisionVoxels.Contains(posItr))
                    {
                        n++;
                        continue;
                    }

                    hasCollision |= TryAddCollisionMask(jobData, dirMask, posItr, n, posNormalMask, true);
                    hasCollision |= TryAddCollisionMask(jobData, dirMask, posItr, n, negNormalMask, false);
                    n++;
                }
            }

            return hasCollision;
        }

        private bool TryAddCollisionMask(PartitionJobData jobData, int3 dirMask, int3 pos, int n,
            NativeArray<CMask> cMask, bool posNormal)
        {
            int3 neighborCoord = pos + dirMask * (posNormal ? 1 : -1);

            ushort neighborVoxel = Accessor.GetVoxelInPartition(jobData.PartitionPos, neighborCoord);

            VoxelRenderDef neighborDef = RenderGenData.GetRenderDef(neighborVoxel);

            if (neighborDef.Collision)
            {
                cMask[n] = default;
                return false;
            }

            cMask[n] = new CMask(posNormal ? (sbyte)1 : (sbyte)-1);
            return true;
        }

        private bool TryAddMask(PartitionJobData jobData, int3 dirMask, AxisInfo axInfo, int3 pos, int n,
            NativeArray<Mask> nMask, bool posNormal, ref NativeHashMap<int3, ushort> sortedVoxels)
        {
            int3 neighborCoord = pos + dirMask * (posNormal ? 1 : -1);

            ushort currentVoxel = sortedVoxels[pos];
            ushort neighborVoxel = Accessor.GetVoxelInPartition(jobData.PartitionPos, neighborCoord);

            VoxelRenderDef neighborDef = RenderGenData.GetRenderDef(neighborVoxel);
            VoxelRenderDef currentDef = RenderGenData.GetRenderDef(currentVoxel);

            MeshLayer currentLayer = currentDef.MeshLayer;
            MeshLayer neighborLayer = neighborDef.MeshLayer;

            if (ShouldSkipFace(currentDef, neighborDef))
            {
                return false;
            }

            sbyte top = ComputeTopVoxelOfType(pos, currentVoxel, ref jobData);
            int4 ao = ComputeAOMask(neighborCoord, jobData.PartitionPos, axInfo);
            byte sunlight = ComputeSunlight(ref jobData, neighborCoord);
            nMask[n] = new Mask(currentVoxel, currentLayer, posNormal ? (sbyte)1 : (sbyte)-1, ao, sunlight,top);
            return true;
        }

        private static byte ComputeSunlight(ref PartitionJobData jobData, in int3 neighborCoord)
        {
            return jobData.LightDataMap.TryGetValue(neighborCoord, out LightData lightData) ? lightData.Sunlight : (byte)0;
        }

        [BurstCompile]
        private bool ShouldSkipFace(VoxelRenderDef currentDef, VoxelRenderDef neighborDef)
        {
            return (!currentDef.AlwaysRenderAllFaces && currentDef.MeshLayer == neighborDef.MeshLayer)
                   || currentDef.MeshLayer > neighborDef.MeshLayer;
        }

        [BurstCompile]
        private sbyte ComputeTopVoxelOfType(int3 coord, ushort currentVoxelId, ref PartitionJobData jobData)
        {
            ushort aboveId = Accessor.GetVoxelInPartition(jobData.PartitionPos, coord + YOne);
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
        private int FindColQuadWidth(NativeArray<CMask> cMasks, int n, CMask currentMask, int start, int max)
        {
            int width;
            for (width = 1; start + width < max && cMasks[n + width].CompareTo(currentMask); width++)
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int FindColQuadHeight(NativeArray<CMask> cMasks, int n, CMask currentMask, int axis1Limit,
            int axis2Limit, int width, int j)
        {
            int height;
            bool done = false;
            for (height = 1; j + height < axis2Limit; height++)
            {
                for (int k = 0; k < width; ++k)
                {
                    if (cMasks[n + k + height * axis1Limit].CompareTo(currentMask)) continue;
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