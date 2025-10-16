using System;
using Runtime.Engine.Data;
using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.Voxels.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Mesher
{
    [GenerateTestsForBurstCompatibility]
    public static class GreedyMesher
    {
        // Small UV inset to reduce atlas bleeding at tile borders (in tile UV units)
        private const float UVEdgeInset = 0.005f;

        [BurstCompile]
        private struct UVQuad
        {
            public float3 Uv1, Uv2, Uv3, Uv4;

            public UVQuad(float3 uv1, float3 uv2, float3 uv3, float3 uv4)
            {
                Uv1 = uv1;
                Uv2 = uv2;
                Uv3 = uv3;
                Uv4 = uv4;
            }
        }

        [BurstCompile]
        private struct VQuad
        {
            public float3 V1, V2, V3, V4;

            public VQuad(float3 v1, float3 v2, float3 v3, float3 v4)
            {
                V1 = v1;
                V2 = v2;
                V3 = v3;
                V4 = v4;
            }

            public void OffsetAll(float3 offset)
            {
                V1 += offset;
                V2 += offset;
                V3 += offset;
                V4 += offset;
            }
        }


        [BurstCompile]
        private static UVQuad ComputeFaceUVs(int3 normal, int width, int height, int uvz)
        {
            UVQuad uv;
            float uMaxW = math.max(UVEdgeInset, width - UVEdgeInset);
            float vMaxH = math.max(UVEdgeInset, height - UVEdgeInset);

            if (normal.x is 1 or -1)
            {
                uv.Uv1 = new float3(UVEdgeInset, UVEdgeInset, uvz); // (0,0)
                uv.Uv2 = new float3(UVEdgeInset, uMaxW, uvz); // (0,width)
                uv.Uv3 = new float3(vMaxH, UVEdgeInset, uvz); // (height,0)
                uv.Uv4 = new float3(vMaxH, uMaxW, uvz); // (height,width)
            }
            else
            {
                uv.Uv1 = new float3(UVEdgeInset, UVEdgeInset, uvz); // (0,0)
                uv.Uv2 = new float3(uMaxW, UVEdgeInset, uvz); // (width,0)
                uv.Uv3 = new float3(UVEdgeInset, vMaxH, uvz); // (0,height)
                uv.Uv4 = new float3(uMaxW, vMaxH, uvz); // (width,height)
            }

            return uv;
        }

        [BurstCompile]
        private static void AddQuadIndices(NativeList<int> indexBuffer, int baseVertexIndex, sbyte normalSign, int4 ao)
        {
            // Choose diagonal based on AO to minimize artifacts
            if (ao[0] + ao[3] > ao[1] + ao[2])
            {
                indexBuffer.Add(baseVertexIndex);
                indexBuffer.Add(baseVertexIndex + 2 - normalSign);
                indexBuffer.Add(baseVertexIndex + 2 + normalSign);

                indexBuffer.Add(baseVertexIndex + 3);
                indexBuffer.Add(baseVertexIndex + 1 + normalSign);
                indexBuffer.Add(baseVertexIndex + 1 - normalSign);
            }
            else
            {
                indexBuffer.Add(baseVertexIndex + 1);
                indexBuffer.Add(baseVertexIndex + 1 + normalSign);
                indexBuffer.Add(baseVertexIndex + 1 - normalSign);

                indexBuffer.Add(baseVertexIndex + 2);
                indexBuffer.Add(baseVertexIndex + 2 - normalSign);
                indexBuffer.Add(baseVertexIndex + 2 + normalSign);
            }
        }

        [BurstCompile]
        private static int RenderFloraCross(MeshBuffer mesh, VoxelRenderDef info, int vertexCount, VQuad verts)
        {
            // First quad (XZ diagonal)
            AddFloraQuad(mesh, info, vertexCount,
                new VQuad(verts.V1 - new float3(0, 1, 0), verts.V1, verts.V4 - new float3(0, 1, 0), verts.V4));
            vertexCount += 4;
            // Second quad (ZX diagonal)
            AddFloraQuad(mesh, info, vertexCount,
                new VQuad(verts.V3 - new float3(0, 1, 0), verts.V3, verts.V2 - new float3(0, 1, 0), verts.V2));
            return 8;
        }

        [BurstCompile]
        private readonly struct Mask
        {
            public readonly ushort VoxelId;

            internal readonly MeshLayer MeshLayer;
            internal readonly sbyte Normal;

            internal readonly int4 AO;

            // Prevent greedy-merging for certain voxel faces (e.g., Flora)
            internal readonly bool NoGreedy;

            public Mask(ushort voxelId, MeshLayer meshLayer, sbyte normal, int4 ao, bool noGreedy)
            {
                MeshLayer = meshLayer;
                VoxelId = voxelId;
                Normal = normal;
                AO = ao;
                NoGreedy = noGreedy;
            }
        }

        [BurstCompile]
        private static bool CompareMask(Mask m1, Mask m2)
        {
            if (m1.NoGreedy || m2.NoGreedy) return false;
            return
                m1.MeshLayer == m2.MeshLayer &&
                m1.VoxelId == m2.VoxelId &&
                m1.Normal == m2.Normal &&
                m1.AO[0] == m2.AO[0] &&
                m1.AO[1] == m2.AO[1] &&
                m1.AO[2] == m2.AO[2] &&
                m1.AO[3] == m2.AO[3];
        }

        [BurstCompile]
        private static MeshLayer GetMeshLayer(ushort voxelId, VoxelEngineRenderGenData renderGenData)
        {
            return renderGenData.GetMeshLayer(voxelId);
        }

        [BurstCompile]
        internal static MeshBuffer GenerateMesh(
            ChunkAccessor accessor, int3 chunkPos, int3 size, VoxelEngineRenderGenData renderGenData
        )
        {
            MeshBuffer mesh = new()
            {
                VertexBuffer = new NativeList<Vertex>(Allocator.Temp),
                IndexBuffer0 = new NativeList<int>(Allocator.Temp),
                IndexBuffer1 = new NativeList<int>(Allocator.Temp),
            };

            int vertexCount = 0;

            for (int direction = 0; direction < 3; direction++)
            {
                int axis1 = (direction + 1) % 3; // U axis
                int axis2 = (direction + 2) % 3; // V axis

                int mainAxisLimit = size[direction];
                int axis1Limit = size[axis1];
                int axis2Limit = size[axis2];

                int3 deltaAxis1 = int3.zero;
                int3 deltaAxis2 = int3.zero;

                int3 chunkItr = int3.zero;
                int3 directionMask = int3.zero;
                directionMask[direction] = 1;

                NativeArray<Mask> normalMask = new(axis1Limit * axis2Limit, Allocator.Temp);

                for (chunkItr[direction] = -1; chunkItr[direction] < mainAxisLimit;)
                {
                    // Build mask for current slice
                    BuildFaceMask(accessor, chunkPos, chunkItr, directionMask, axis1, axis2, axis1Limit, axis2Limit,
                        renderGenData, normalMask);
                    ++chunkItr[direction];
                    int n = 0;
                    for (int j = 0; j < axis2Limit; j++)
                    {
                        for (int i = 0; i < axis1Limit;)
                        {
                            if (normalMask[n].Normal != 0)
                            {
                                Mask currentMask = normalMask[n];
                                chunkItr[axis1] = i;
                                chunkItr[axis2] = j;
                                int width = FindQuadWidth(normalMask, n, currentMask, i, axis1Limit);
                                int height = FindQuadHeight(normalMask, n, currentMask, axis1Limit, axis2Limit, width,
                                    j);
                                deltaAxis1[axis1] = width;
                                deltaAxis2[axis2] = height;
                                vertexCount += CreateQuad(
                                    mesh, renderGenData.GetRenderDef(currentMask.VoxelId),
                                    vertexCount, currentMask, directionMask, width, height, new VQuad()
                                    {
                                        V1 = chunkItr,
                                        V2 = chunkItr + deltaAxis1,
                                        V3 = chunkItr + deltaAxis2,
                                        V4 = chunkItr + deltaAxis1 + deltaAxis2
                                    }
                                );
                                ClearMaskRegion(normalMask, n, width, height, axis1Limit);
                                deltaAxis1 = int3.zero;
                                deltaAxis2 = int3.zero;
                                i += width;
                                n += width;
                            }
                            else
                            {
                                i++;
                                n++;
                            }
                        }
                    }
                }

                normalMask.Dispose();
            }

            return mesh;
        }

        [BurstCompile]
        private static void BuildFaceMask(ChunkAccessor accessor, int3 chunkPos, int3 chunkItr, int3 directionMask,
            int axis1, int axis2, int axis1Limit, int axis2Limit, VoxelEngineRenderGenData renderGenData,
            NativeArray<Mask> normalMask)
        {
            int n = 0;
            for (chunkItr[axis2] = 0; chunkItr[axis2] < axis2Limit; ++chunkItr[axis2])
            {
                for (chunkItr[axis1] = 0; chunkItr[axis1] < axis1Limit; ++chunkItr[axis1])
                {
                    ushort currentVoxel = accessor.GetVoxelInChunk(chunkPos, chunkItr);
                    ushort compareVoxel = accessor.GetVoxelInChunk(chunkPos, chunkItr + directionMask);
                    VoxelRenderDef currentDef = renderGenData.GetRenderDef(currentVoxel);
                    VoxelRenderDef compareDef = renderGenData.GetRenderDef(compareVoxel);
                    MeshLayer currentMeshIndex = currentDef.MeshLayer;
                    MeshLayer compareMeshIndex = compareDef.MeshLayer;
                    if (currentMeshIndex == compareMeshIndex)
                    {
                        normalMask[n++] = default;
                    }
                    else
                    {
                        bool noGreedy = currentDef.VoxelType == VoxelType.Flora ||
                                        compareDef.VoxelType == VoxelType.Flora;

                        if (currentMeshIndex < compareMeshIndex)
                        {
                            int4 ao = ComputeAOMask(accessor, renderGenData, chunkPos, chunkItr + directionMask, axis1,
                                axis2);
                            normalMask[n++] = new Mask(currentVoxel, currentMeshIndex, 1, ao, noGreedy);
                        }
                        else
                        {
                            int4 ao = ComputeAOMask(accessor, renderGenData, chunkPos, chunkItr, axis1, axis2);
                            normalMask[n++] = new Mask(compareVoxel, compareMeshIndex, -1, ao, noGreedy);
                        }
                    }
                }
            }
        }

        [BurstCompile]
        private static int FindQuadWidth(NativeArray<Mask> normalMask, int n, Mask currentMask, int start, int max)
        {
            int width;
            for (width = 1; start + width < max && CompareMask(normalMask[n + width], currentMask); width++)
            {
            }

            return width;
        }

        [BurstCompile]
        private static int FindQuadHeight(NativeArray<Mask> normalMask, int n, Mask currentMask, int axis1Limit,
            int axis2Limit, int width, int j)
        {
            int height;
            bool done = false;
            for (height = 1; j + height < axis2Limit; height++)
            {
                for (int k = 0; k < width; ++k)
                {
                    if (CompareMask(normalMask[n + k + height * axis1Limit], currentMask)) continue;
                    done = true;
                    break;
                }

                if (done) break;
            }

            return height;
        }

        [BurstCompile]
        private static void ClearMaskRegion(NativeArray<Mask> normalMask, int n, int width, int height, int axis1Limit)
        {
            for (int l = 0; l < height; ++l)
            for (int k = 0; k < width; ++k)
                normalMask[n + k + l * axis1Limit] = default;
        }

        [BurstCompile]
        private static int CreateQuad(
            MeshBuffer mesh, VoxelRenderDef info, int vertexCount, Mask mask, int3 directionMask,
            int width, int height, VQuad verts
        )
        {
            switch (mask.MeshLayer)
            {
                case MeshLayer.Solid:
                    return CreateQuadMesh0(mesh, info, vertexCount, mask, directionMask, width, height, verts);
                case MeshLayer.Transparent:
                    return CreateQuadMesh1(mesh, info, vertexCount, mask, directionMask, width, height, verts);
                default:
                    return 0;
            }
        }

        [BurstCompile]
        private static int CreateQuadMesh0(
            MeshBuffer mesh, VoxelRenderDef info, int vertexCount, Mask mask, int3 directionMask,
            int width, int height, VQuad verts
        )
        {
            int3 normal = directionMask * mask.Normal;

            int uvz = info.GetTextureId(normal.ToDirection());
            UVQuad uv = ComputeFaceUVs(normal, width, height, uvz);

            // 1 Bottom Left
            Vertex vertex1 = new(verts.V1, normal, uv.Uv1, new float2(0, 0), mask.AO);

            // 2 Top Left
            Vertex vertex2 = new(verts.V2, normal, uv.Uv2, new float2(0, 1), mask.AO);

            // 3 Bottom Right
            Vertex vertex3 = new(verts.V3, normal, uv.Uv3, new float2(1, 0), mask.AO);

            // 4 Top Right
            Vertex vertex4 = new(verts.V4, normal, uv.Uv4, new float2(1, 1), mask.AO);

            mesh.VertexBuffer.Add(vertex1);
            mesh.VertexBuffer.Add(vertex2);
            mesh.VertexBuffer.Add(vertex3);
            mesh.VertexBuffer.Add(vertex4);

            AddQuadIndices(mesh.IndexBuffer0, vertexCount, mask.Normal, mask.AO);
            return 4;
        }

        [BurstCompile]
        private static int CreateQuadMesh1(
            MeshBuffer mesh, VoxelRenderDef info, int vertexCount, Mask mask, int3 directionMask,
            int width, int height, VQuad verts
        )
        {
            int3 normal = directionMask * mask.Normal;

            switch (info.VoxelType)
            {
                case VoxelType.Full:
                    break;
                case VoxelType.Liquid:
                    if (info.VoxelType != VoxelType.Liquid || normal.y != 1) break;
                    verts.OffsetAll(new float3(0, -0.25f, 0));
                    break;
                case VoxelType.Flora:
                    return normal.y != 1
                        ? 0
                        : RenderFloraCross(mesh, info, vertexCount, verts); // Only render flora on top faces
                default:
                    throw new ArgumentOutOfRangeException();
            }

            int uvz = info.GetTextureId(normal.ToDirection());
            UVQuad uv = ComputeFaceUVs(normal, width, height, uvz);
            float3 fNormal = normal;

            // 1 Bottom Left
            Vertex vertex1 = new(verts.V1, fNormal, uv.Uv1, new float2(0, 0), info.OverrideColor);

            // 2 Top Left
            Vertex vertex2 = new(verts.V2, fNormal, uv.Uv2, new float2(0, 1), info.OverrideColor);

            // 3 Bottom Right
            Vertex vertex3 = new(verts.V3, fNormal, uv.Uv3, new float2(1, 0), info.OverrideColor);

            // 4 Top Right
            Vertex vertex4 = new(verts.V4, fNormal, uv.Uv4, new float2(1, 1), info.OverrideColor);

            mesh.VertexBuffer.Add(vertex1);
            mesh.VertexBuffer.Add(vertex2);
            mesh.VertexBuffer.Add(vertex3);
            mesh.VertexBuffer.Add(vertex4);

            AddQuadIndices(mesh.IndexBuffer1, vertexCount, mask.Normal, mask.AO);
            return 4;
        }

        private static void AddFloraQuad(MeshBuffer mesh, VoxelRenderDef info, int vertexCount, VQuad verts)
        {
            int uvz = info.TexUp;
            UVQuad uv = ComputeFaceUVs(new int3(1, 1, 0), 1, 1, uvz);
            float3 normal = new(0, 1, 0);

            Vertex vertex1 = new(verts.V1, normal, uv.Uv1, new float2(0, 0), info.OverrideColor);

            Vertex vertex2 = new(verts.V2, normal, uv.Uv2, new float2(0, 1), info.OverrideColor);

            Vertex vertex3 = new(verts.V3, normal, uv.Uv3, new float2(1, 0), info.OverrideColor);

            Vertex vertex4 = new(verts.V4, normal, uv.Uv4, new float2(1, 1), info.OverrideColor);

            mesh.VertexBuffer.Add(vertex1);
            mesh.VertexBuffer.Add(vertex2);
            mesh.VertexBuffer.Add(vertex3);
            mesh.VertexBuffer.Add(vertex4);
            NativeList<int> indexBuffer = mesh.IndexBuffer1;
            indexBuffer.Add(vertexCount);
            indexBuffer.Add(vertexCount + 1);
            indexBuffer.Add(vertexCount + 2);
            indexBuffer.Add(vertexCount + 2);
            indexBuffer.Add(vertexCount + 1);
            indexBuffer.Add(vertexCount + 3);
        }

        [BurstCompile]
        private static int4 ComputeAOMask(ChunkAccessor accessor, VoxelEngineRenderGenData renderGenData, int3 pos,
            int3 coord, int axis1, int axis2)
        {
            int3 l = coord;
            int3 r = coord;
            int3 b = coord;
            int3 T = coord;

            int3 lbc = coord;
            int3 rbc = coord;
            int3 ltc = coord;
            int3 rtc = coord;

            l[axis2] -= 1;
            r[axis2] += 1;
            b[axis1] -= 1;
            T[axis1] += 1;

            lbc[axis1] -= 1;
            lbc[axis2] -= 1;
            rbc[axis1] -= 1;
            rbc[axis2] += 1;
            ltc[axis1] += 1;
            ltc[axis2] -= 1;
            rtc[axis1] += 1;
            rtc[axis2] += 1;

            int lo = GetMeshLayer(accessor.GetVoxelInChunk(pos, l), renderGenData) == 0 ? 1 : 0;
            int ro = GetMeshLayer(accessor.GetVoxelInChunk(pos, r), renderGenData) == 0 ? 1 : 0;
            int bo = GetMeshLayer(accessor.GetVoxelInChunk(pos, b), renderGenData) == 0 ? 1 : 0;
            int to = GetMeshLayer(accessor.GetVoxelInChunk(pos, T), renderGenData) == 0 ? 1 : 0;

            int lbco = GetMeshLayer(accessor.GetVoxelInChunk(pos, lbc), renderGenData) == 0 ? 1 : 0;
            int rbco = GetMeshLayer(accessor.GetVoxelInChunk(pos, rbc), renderGenData) == 0 ? 1 : 0;
            int ltco = GetMeshLayer(accessor.GetVoxelInChunk(pos, ltc), renderGenData) == 0 ? 1 : 0;
            int rtco = GetMeshLayer(accessor.GetVoxelInChunk(pos, rtc), renderGenData) == 0 ? 1 : 0;

            return new int4(
                ComputeAO(lo, bo, lbco),
                ComputeAO(lo, to, ltco),
                ComputeAO(ro, bo, rbco),
                ComputeAO(ro, to, rtco)
            );
        }

        [BurstCompile]
        private static int ComputeAO(int s1, int s2, int c)
        {
            if (s1 == 1 && s2 == 1)
            {
                return 0;
            }

            return 3 - (s1 + s2 + c);
        }
    }
}