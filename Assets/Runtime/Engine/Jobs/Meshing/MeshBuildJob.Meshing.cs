using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.UI;
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
            NativeHashMap<int3, ushort> sortedVoxels = jobData.SolidVoxels;
            BuildFaces(ref jobData, ref sortedVoxels, SubMeshType.Solid);
        }

        private void MeshTransparent(ref PartitionJobData jobData)
        {
            if (jobData.HasNoTransparent) return;
            NativeHashMap<int3, ushort> sortedVoxels = jobData.TransparentVoxels;
            BuildFaces(ref jobData, ref sortedVoxels, SubMeshType.Transparent);
        }

        private void MeshCollision(ref PartitionJobData jobData)
        {
            if (jobData.HasNoCollision) return;
            // Sweep along each principal axis (X, Y, Z)
            NativeHashMap<int3, ushort> map = default;
            ColliderSliceMeshBuild(ref jobData, ref map);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void BuildFaces(ref PartitionJobData jobData, ref NativeHashMap<int3, ushort> sortedVoxels,
            SubMeshType subMeshType)
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

                Direction dir = directionMask.ToDirection();
                Direction negDir = dir.GetOpposite();

                for (pos[mainAxis] = 0; pos[mainAxis] < mainAxisLimit; ++pos[mainAxis])
                for (pos[axisInfo.VAxis] = 0; pos[axisInfo.VAxis] < axisInfo.VLimit; ++pos[axisInfo.VAxis])
                for (pos[axisInfo.UAxis] = 0; pos[axisInfo.UAxis] < axisInfo.ULimit; ++pos[axisInfo.UAxis])
                {
                    if (!sortedVoxels.ContainsKey(pos))
                    {
                        continue;
                    }

                    BuildFace(ref jobData, sortedVoxels, subMeshType, pos, dir);
                    BuildFace(ref jobData, sortedVoxels, subMeshType, pos, negDir);
                }
            }
        }

        private void BuildFace(ref PartitionJobData jobData, NativeHashMap<int3, ushort> sortedVoxels,
            SubMeshType subMeshType, int3 pos, Direction direction)
        {
            int3 neighborCoord = pos + direction.ToInt3();

            ushort currentVoxel = sortedVoxels[pos];
            ushort neighborVoxel = GetVoxel(ref jobData, neighborCoord);

            VoxelRenderDef neighborDef = RenderGenData.GetRenderDef(neighborVoxel);
            VoxelRenderDef currentDef = RenderGenData.GetRenderDef(currentVoxel);

            MeshLayer currentLayer = currentDef.MeshLayer;
            MeshLayer neighborLayer = neighborDef.MeshLayer;

            if (ShouldSkipFace(currentDef, neighborDef))
            {
                return;
            }

            sbyte top = ComputeTopVoxelOfType(pos, currentVoxel, ref jobData);
            ComputeAO(neighborCoord, ref jobData, direction, out byte ao);
            byte sunlight = ComputeSunlight(ref jobData, neighborCoord);

            int quadIndex = direction switch
            {
                Direction.Right => 0,
                Direction.Left => 1,
                Direction.Up => 2,
                Direction.Down => 3,
                Direction.Forward => 4,
                Direction.Backward => 5,
                _ => -1
            };

            var vertex = new Vertex
            (
                pos,
                (ushort)quadIndex,
                (ushort)currentDef.GetTextureId(direction),
                sunlight,
                ao
            );

            AddVertex(ref jobData, subMeshType, ref vertex);
        }

        private void ColliderSliceMeshBuild(ref PartitionJobData jobData,
            ref NativeHashMap<int3, ushort> sortedVoxels)
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

                BuildColliderSlice(ref jobData, pos, mainAxis, mainAxisLimit, directionMask, axisInfo);
            }
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
                byte sunLight = ComputeSunlight(ref jobData, pos);
                ComputeAO(pos, ref jobData, Direction.Forward, out byte ao);

                Vertex vertex = new(
                    pos,
                    6,
                    (ushort)def.GetTextureId(Direction.Forward),
                    sunLight,
                    ao
                );
                AddVertex(ref jobData, SubMeshType.Foliage, ref vertex);

                vertex = new Vertex
                (
                    pos,
                    7,
                    (ushort)def.GetTextureId(Direction.Forward),
                    sunLight,
                    ao
                );
                AddVertex(ref jobData, SubMeshType.Foliage, ref vertex);
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

                        VQuad quadVerts = new()
                        {
                            V1 = pos,
                            V2 = pos + uDelta,
                            V3 = pos + vDelta,
                            V4 = pos + uDelta + vDelta
                        };

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