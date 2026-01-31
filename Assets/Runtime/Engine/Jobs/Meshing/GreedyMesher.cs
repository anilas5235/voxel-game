using Runtime.Engine.Data;
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
        /// <summary>
        /// Executes the meshing process: sweeps 3 axes, builds surface & collider quads, then foliage.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void GenerateMesh(PartitionJobData jobData)
        {
            int meshVertexCount = 0;
            int colliderVertexCount = 0;

            // Sweep along each principal axis (X, Y, Z)
            for (int mainAxis = 0; mainAxis < 3; mainAxis++)
            {
                // Define orthogonal axes for the 2D slice (U and V plane)
                int uAxis = (mainAxis + 1) % 3;
                int vAxis = (mainAxis + 2) % 3;

                // We only generate faces for slices starting inside the chunk (0…size-1)
                // so that negative-side faces are owned by the neighboring chunk.
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

                // Temporary mask buffer for the current slice (U x V)
                NativeArray<Mask> normalMask = new(axisInfo.ULimit * axisInfo.VLimit, Allocator.Temp);
                NativeArray<Mask> colliderMask = new(axisInfo.ULimit * axisInfo.VLimit, Allocator.Temp);

                for (pos[mainAxis] = 0; pos[mainAxis] < mainAxisLimit;)
                {
                    // Build both masks in a single pass to minimize voxel/def lookups
                    SliceInfo info = BuildMasks(jobData, pos, directionMask, axisInfo, normalMask, colliderMask);

                    // Move to the actual slice index we just built the mask for
                    ++pos[mainAxis];

                    if (info.HasSurface)
                    {
                        meshVertexCount = BuildSurfaceQuads(jobData, meshVertexCount, axisInfo, pos,
                            normalMask, directionMask);
                    }

                    if (info.HasCollider)
                    {
                        colliderVertexCount = BuildColliderQuads(jobData, colliderVertexCount, axisInfo, pos,
                            colliderMask, directionMask);
                    }
                }

                normalMask.Dispose();
                colliderMask.Dispose();
            }

            // Note: return value isn't used outside this method; keep local update for clarity.
            meshVertexCount = BuildFoliage(meshVertexCount, jobData);
        }

        /// <summary>
        /// Builds surface quads for one slice using greedy merging.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int BuildSurfaceQuads(PartitionJobData jobData, int vertexCount, AxisInfo axInfo, int3 pos,
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

                        vertexCount += CreateQuad(
                            jobData,
                            RenderGenData.GetRenderDef(current.VoxelId),
                            vertexCount,
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

            return vertexCount;
        }

        /// <summary>
        /// Builds foliage billboard quads from collected flora voxels.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int BuildFoliage(int vertexCount, PartitionJobData jobData)
        {
            // Build cross (billboard) quads for flora collected during the surface pass
            foreach (int3 voxelPos in jobData.FoliageVoxels)
            {
                int3 p = voxelPos;
                if (!ChunkAccessor.InChunkBounds(p)) continue;
                ushort voxelId = Accessor.GetVoxelInChunk(jobData.ChunkPos, p);
                VoxelRenderDef def = RenderGenData.GetRenderDef(voxelId);
                p -= jobData.YOffset;

                // Diagonal 1
                VQuad flora1 = new(
                    p,
                    p + new float3(0, 1, 0),
                    p + new float3(1, 0, 1),
                    p + new float3(1, 1, 1)
                );

                vertexCount += AddFloraQuad(jobData, def, vertexCount, in flora1, int4.zero);

                // Diagonal 2
                VQuad flora2 = new(
                    p + new float3(1, 0, 0),
                    p + new float3(1, 1, 0),
                    p + new float3(0, 0, 1),
                    p + new float3(0, 1, 1)
                );

                vertexCount += AddFloraQuad(jobData, def, vertexCount, in flora2, int4.zero);
            }

            return vertexCount;
        }

        /// <summary>
        /// Builds collider quads for one slice using greedy merging.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int BuildColliderQuads(PartitionJobData jobData, int vertexCount, AxisInfo axInfo, int3 pos,
            NativeArray<Mask> colliderMask,
            int3 directionMask)
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
                        Mask current = colliderMask[maskIndex];
                        pos[axInfo.UAxis] = u;
                        pos[axInfo.VAxis] = v;

                        int quadWidth = FindQuadWidth(colliderMask, maskIndex, current, u, axInfo.ULimit);
                        int quadHeight = FindQuadHeight(colliderMask, maskIndex, current, axInfo.ULimit,
                            axInfo.VLimit, quadWidth, v);

                        uDelta[axInfo.UAxis] = quadWidth;
                        vDelta[axInfo.VAxis] = quadHeight;

                        VQuad quadVerts = new(
                            pos,
                            pos + uDelta,
                            pos + vDelta,
                            pos + uDelta + vDelta
                        );

                        vertexCount += CreateColliderQuad(
                            jobData,
                            vertexCount,
                            current,
                            directionMask,
                            in quadVerts
                        );

                        ClearMaskRegion(colliderMask, maskIndex, quadWidth, quadHeight, axInfo.ULimit);
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

            return vertexCount;
        }
    }
}