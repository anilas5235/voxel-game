using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Meshing
{
    /// <summary>
    /// Burst-optimized greedy mesher for voxel chunks. Merges contiguous faces into maximal rectangles
    /// to reduce vertex/index count. Produces render and collider data plus foliage (billboard) quads.
    /// </summary>
    internal partial struct MeshBuildJob
    {
        private void MeshSolids(ref PartitionJobData jobData)
        {
            if (jobData.HasNoSolid) return;

            SliceMeshBuild(ref jobData, ref jobData.SolidVoxels);
        }

        private void MeshTransparent(ref PartitionJobData jobData)
        {
            if (jobData.HasNoTransparent) return;

            SliceMeshBuild(ref jobData, ref jobData.TransparentVoxels);
        }

        private void MeshCollision(ref PartitionJobData jobData)
        {
            if (jobData.HasNoCollision) return;
            // Sweep along each principal axis (X, Y, Z)
            NativeHashMap<int3, ushort> map = default;
            SliceMeshBuild(ref jobData, ref map, true);
        }

        private void SliceMeshBuild(ref PartitionJobData jobData, ref NativeHashMap<int3, ushort> sortedVoxels,
            bool collider = false)
        {
            // Sweep along each principal axis (X, Y, Z)
            for (int mainAxis = 0; mainAxis < 3; mainAxis++)
            {
                // Define orthogonal axes for the 2D slice (U and V plane)
                int uAxis = (mainAxis + 1) % 3;
                int vAxis = (mainAxis + 2) % 3;

                int mainAxisLimit = PartitionSize[mainAxis];

                AxisInfo axisInfo = new()
                {
                    UAxis = uAxis,
                    VAxis = vAxis,
                    ULimit = PartitionSize[uAxis],
                    VLimit = PartitionSize[vAxis]
                };

                int3 pos = int3.zero;

                int3 directionMask = int3.zero;
                directionMask[mainAxis] = 1;

                if (collider)
                {
                    BuildColliderSlice(ref jobData, pos, mainAxis, mainAxisLimit, directionMask, axisInfo);
                }
                else
                {
                    BuildRenderSlice(ref jobData, ref sortedVoxels, pos, mainAxis, mainAxisLimit, directionMask,
                        axisInfo);
                }
            }
        }

        private void BuildRenderSlice(ref PartitionJobData jobData, ref NativeHashMap<int3, ushort> sortedVoxels,
            int3 pos, int mainAxis, int mainAxisLimit, int3 directionMask, AxisInfo axisInfo)
        {
            // Temporary mask buffer for the current slice (U x V)
            int uvGridSize = axisInfo.ULimit * axisInfo.VLimit;
            NativeArray<Mask> posNormalMask = new(uvGridSize, Allocator.Temp);
            NativeArray<Mask> negNormalMask = new(uvGridSize, Allocator.Temp);

            for (pos[mainAxis] = 0; pos[mainAxis] < mainAxisLimit;)
            {
                bool info = BuildMasks(ref jobData, ref sortedVoxels, pos, directionMask, axisInfo,
                    ref posNormalMask, ref negNormalMask);

                // Move to the actual slice index we just built the mask for
                ++pos[mainAxis];

                if (!info) continue;

                BuildSurfaceQuads(ref jobData, axisInfo, pos, posNormalMask, directionMask);
                BuildSurfaceQuads(ref jobData, axisInfo, pos - directionMask, negNormalMask, -directionMask);
            }

            posNormalMask.Dispose();
            negNormalMask.Dispose();
        }

        private void BuildColliderSlice(ref PartitionJobData jobData, int3 pos, int mainAxis, int mainAxisLimit,
            int3 directionMask, AxisInfo axisInfo)
        {
            // Temporary mask buffer for the current slice (U x V)
            int uvGridSize = axisInfo.ULimit * axisInfo.VLimit;
            NativeArray<CMask> posColMask = new(uvGridSize, Allocator.Temp);
            NativeArray<CMask> negColMask = new(uvGridSize, Allocator.Temp);

            for (pos[mainAxis] = 0; pos[mainAxis] < mainAxisLimit;)
            {
                bool info = BuildColliderMasks(ref jobData, pos, directionMask, axisInfo, ref posColMask,
                    ref negColMask);

                // Move to the actual slice index we just built the mask for
                ++pos[mainAxis];

                if (!info) continue;

                BuildColliderQuads(ref jobData, axisInfo, pos, posColMask, directionMask);
                BuildColliderQuads(ref jobData, axisInfo, pos - directionMask, negColMask, -directionMask);
            }

            posColMask.Dispose();
            negColMask.Dispose();
        }

        /// <summary>
        /// Builds surface quads for one slice using greedy merging.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void BuildSurfaceQuads(ref PartitionJobData jobData, AxisInfo axInfo, int3 pos,
            NativeArray<Mask> normalMask, int3 directionMask)
        {
            int3 uDelta = int3.zero;
            int3 vDelta = int3.zero;

            int maskIndex = 0;
            for (int v = 0; v < axInfo.VLimit; v++)
            {
                for (int u = 0; u < axInfo.ULimit;)
                {
                    if (normalMask[maskIndex].Normal != 0)
                    {
                        // Found a face; grow the maximal rectangle (width x height)
                        Mask current = normalMask[maskIndex];
                        pos[axInfo.UAxis] = u;
                        pos[axInfo.VAxis] = v;

                        int quadWidth = FindQuadWidth(normalMask, maskIndex, current, u, axInfo.ULimit);
                        int quadHeight = FindQuadHeight(normalMask, maskIndex, current, axInfo.ULimit,
                            axInfo.VLimit, quadWidth, v);

                        uDelta[axInfo.UAxis] = quadWidth;
                        vDelta[axInfo.VAxis] = quadHeight;

                        VQuad quadVerts = new(
                            pos,
                            pos + uDelta,
                            pos + vDelta,
                            pos + uDelta + vDelta
                        );

                        CreateQuad(
                            ref jobData,
                            RenderGenData.GetRenderDef(current.VoxelId),
                            current,
                            directionMask,
                            new int2(quadWidth, quadHeight),
                            in quadVerts
                        );

                        ClearMaskRegion(normalMask, maskIndex, quadWidth, quadHeight, axInfo.ULimit);
                        uDelta = int3.zero;
                        vDelta = int3.zero;

                        // Jump horizontally by the consumed width
                        u += quadWidth;
                        maskIndex += quadWidth;
                    }
                    else
                    {
                        // No face here; advance to next cell
                        u++;
                        maskIndex++;
                    }
                }
            }
        }

        /// <summary>
        /// Builds foliage billboard quads from collected flora voxels.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void MeshFoliage(ref PartitionJobData jobData)
        {
            if (jobData.HasNoFoliage) return;

            // Build cross (billboard) quads for flora collected during the surface pass
            foreach (KVPair<int3, ushort> foliageVoxel in jobData.FoliageVoxels)
            {
                int3 pos = foliageVoxel.Key;
                VoxelRenderDef def = RenderGenData.GetRenderDef(foliageVoxel.Value);

                // Diagonal 1
                VQuad flora1 = new(
                    pos,
                    pos + new float3(0, 1, 0),
                    pos + new float3(1, 0, 1),
                    pos + new float3(1, 1, 1)
                );

                AddFloraQuad(ref jobData, def, in flora1, int4.zero);

                // Diagonal 2
                VQuad flora2 = new(
                    pos + new float3(1, 0, 0),
                    pos + new float3(1, 1, 0),
                    pos + new float3(0, 0, 1),
                    pos + new float3(0, 1, 1)
                );

                AddFloraQuad(ref jobData, def, in flora2, int4.zero);
            }
        }

        /// <summary>
        /// Builds collider quads for one slice using greedy merging.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void BuildColliderQuads(ref PartitionJobData jobData, AxisInfo axInfo, int3 pos,
            NativeArray<CMask> colliderMask, int3 directionMask)
        {
            int3 uDelta = int3.zero;
            int3 vDelta = int3.zero;

            // Greedy merge over the collider mask
            int maskIndex = 0;
            for (int v = 0; v < axInfo.VLimit; v++)
            {
                for (int u = 0; u < axInfo.ULimit;)
                {
                    if (colliderMask[maskIndex].Normal != 0)
                    {
                        CMask current = colliderMask[maskIndex];
                        pos[axInfo.UAxis] = u;
                        pos[axInfo.VAxis] = v;

                        int quadWidth = FindColQuadWidth(colliderMask, maskIndex, current, u, axInfo.ULimit);
                        int quadHeight = FindColQuadHeight(colliderMask, maskIndex, current, axInfo.ULimit,
                            axInfo.VLimit,
                            quadWidth, v);

                        uDelta[axInfo.UAxis] = quadWidth;
                        vDelta[axInfo.VAxis] = quadHeight;

                        VQuad quadVerts = new(
                            pos,
                            pos + uDelta,
                            pos + vDelta,
                            pos + uDelta + vDelta
                        );

                        CreateColliderQuad(
                            ref jobData,
                            current,
                            directionMask,
                            in quadVerts
                        );

                        ClearColMaskRegion(colliderMask, maskIndex, quadWidth, quadHeight, axInfo.ULimit);
                        uDelta = int3.zero;
                        vDelta = int3.zero;

                        u += quadWidth;
                        maskIndex += quadWidth;
                    }
                    else
                    {
                        u++;
                        maskIndex++;
                    }
                }
            }
        }
    }
}