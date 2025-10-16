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

        // Helper container for face UVs
        private struct UVQuad
        {
            public float3 Uv1, Uv2, Uv3, Uv4;
        }

        [BurstCompile]
        private static MeshLayer ToLayer(byte meshIndex) => (MeshLayer)meshIndex;

        [BurstCompile]
        private static UVQuad ComputeFaceUVs(int3 normal, int width, int height, int uvz)
        {
            UVQuad uv;
            float uMin = UVEdgeInset;
            float vMin = UVEdgeInset;
            float uMaxW = math.max(UVEdgeInset, width - UVEdgeInset);
            float vMaxH = math.max(UVEdgeInset, height - UVEdgeInset);

            if (normal.x is 1 or -1)
            {
                uv.Uv1 = new float3(vMin, uMin, uvz);      // (0,0)
                uv.Uv2 = new float3(vMin, uMaxW, uvz);     // (0,width)
                uv.Uv3 = new float3(vMaxH, uMin, uvz);     // (height,0)
                uv.Uv4 = new float3(vMaxH, uMaxW, uvz);    // (height,width)
            }
            else
            {
                uv.Uv1 = new float3(uMin, vMin, uvz);      // (0,0)
                uv.Uv2 = new float3(uMaxW, vMin, uvz);     // (width,0)
                uv.Uv3 = new float3(uMin, vMaxH, uvz);     // (0,height)
                uv.Uv4 = new float3(uMaxW, vMaxH, uvz);    // (width,height)
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
        private static void LowerLiquidTopFaceIfNeeded(VoxelType voxelType, int3 normal, ref float3 v1, ref float3 v2, ref float3 v3, ref float3 v4)
        {
            if (voxelType == VoxelType.Liquid && normal.y == 1)
            {
                v1.y -= 0.25f;
                v2.y -= 0.25f;
                v3.y -= 0.25f;
                v4.y -= 0.25f;
            }
        }

        [BurstCompile]
        private static int RenderFloraCross(MeshBuffer mesh, VoxelRenderDef info, int vertexCount, float3 v1, float3 v2, float3 v3, float3 v4, int3 normal)
        {
            if (normal.y != 1) return 0;
            // First quad (XZ diagonal)
            AddFloraQuad(mesh, info, vertexCount, v1 - new float3(0, 1, 0), v1, v4 - new float3(0, 1, 0), v4);
            vertexCount += 4;
            // Second quad (ZX diagonal)
            AddFloraQuad(mesh, info, vertexCount, v3 - new float3(0, 1, 0), v3, v2 - new float3(0, 1, 0), v2);
            return 8;
        }

        [BurstCompile]
        private readonly struct Mask
        {
            public readonly ushort VoxelId;

            internal readonly byte MeshIndex;
            internal readonly sbyte Normal;
            internal readonly int4 AO;
            // Prevent greedy-merging for certain voxel faces (e.g., Flora)
            internal readonly byte NoGreedy;
            public Mask(ushort voxelId, byte meshIndex, sbyte normal, int4 ao, byte noGreedy)
            {
                MeshIndex = meshIndex;
                VoxelId = voxelId;
                Normal = normal;
                AO = ao;
                NoGreedy = noGreedy;
            }
        }

        [BurstCompile]
        private static bool CompareMask(Mask m1, Mask m2)
        {
            // Do not merge if either side explicitly disables greedy merging
            if (m1.NoGreedy == 1 || m2.NoGreedy == 1) return false;
            return
                m1.MeshIndex == m2.MeshIndex &&
                m1.VoxelId == m2.VoxelId &&
                m1.Normal == m2.Normal &&
                m1.AO[0] == m2.AO[0] &&
                m1.AO[1] == m2.AO[1] &&
                m1.AO[2] == m2.AO[2] &&
                m1.AO[3] == m2.AO[3];
        }

        [BurstCompile]
        private static byte GetMeshIndex(ushort block, VoxelEngineRenderGenData renderGenData)
        {
            return renderGenData.GetMeshIndex(block);
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
                                vertexCount += CreateAndAddQuad(mesh, renderGenData, currentMask, directionMask, width,
                                    height, chunkItr, deltaAxis1, deltaAxis2, vertexCount);
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
                    byte currentMeshIndex = GetMeshIndex(currentVoxel, renderGenData);
                    byte compareMeshIndex = GetMeshIndex(compareVoxel, renderGenData);
                    if (currentMeshIndex == compareMeshIndex)
                    {
                        normalMask[n++] = default;
                    }
                    else
                    {
                        // Disable greedy merge if either side is Flora
                        bool eitherIsFlora = renderGenData.GetRenderDef(currentVoxel).VoxelType == VoxelType.Flora ||
                                             renderGenData.GetRenderDef(compareVoxel).VoxelType == VoxelType.Flora;
                        byte noGreedy = eitherIsFlora ? (byte)1 : (byte)0;

                        if (currentMeshIndex < compareMeshIndex)
                        {
                            var ao = ComputeAOMask(accessor, renderGenData, chunkPos, chunkItr + directionMask, axis1, axis2);
                            normalMask[n++] = new Mask(currentVoxel, currentMeshIndex, 1, ao, noGreedy);
                        }
                        else
                        {
                            var ao = ComputeAOMask(accessor, renderGenData, chunkPos, chunkItr, axis1, axis2);
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
        private static int CreateAndAddQuad(MeshBuffer mesh, VoxelEngineRenderGenData renderGenData, Mask currentMask,
            int3 directionMask, int width, int height, int3 chunkItr, int3 deltaAxis1, int3 deltaAxis2, int vertexCount)
        {
            return CreateQuad(
                mesh, renderGenData.GetRenderDef(currentMask.VoxelId),
                vertexCount, currentMask, directionMask, width, height,
                chunkItr,
                chunkItr + deltaAxis1,
                chunkItr + deltaAxis2,
                chunkItr + deltaAxis1 + deltaAxis2
            );
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
            int width, int height, int3 v1, int3 v2, int3 v3, int3 v4
        )
        {
            switch (ToLayer(mask.MeshIndex))
            {
                case MeshLayer.Solid:
                    return CreateQuadMesh0(mesh, info, vertexCount, mask, directionMask, width, height, v1, v2, v3, v4);
                case MeshLayer.Transparent:
                    return CreateQuadMesh1(mesh, info, vertexCount, mask, directionMask, width, height, v1, v2, v3, v4);
                default:
                    return 0;
            }
        }

        [BurstCompile]
        private static int CreateQuadMesh0(
            MeshBuffer mesh, VoxelRenderDef info, int vertexCount, Mask mask, int3 directionMask,
            int width, int height, float3 v1, float3 v2, float3 v3, float3 v4
        )
        {
            int3 normal = directionMask * mask.Normal;

            int uvz = info.GetTextureId(normal.ToDirection());
            UVQuad uv = ComputeFaceUVs(normal, width, height, uvz);

            // 1 Bottom Left
            Vertex vertex1 = new()
            {
                Position = v1,
                Normal = normal,
                UV0 = uv.Uv1,
                UV1 = new float2(0, 0),
                UV2 = mask.AO
            };

            // 2 Top Left
            Vertex vertex2 = new()
            {
                Position = v2,
                Normal = normal,
                UV0 = uv.Uv2,
                UV1 = new float2(0, 1),
                UV2 = mask.AO
            };

            // 3 Bottom Right
            Vertex vertex3 = new()
            {
                Position = v3,
                Normal = normal,
                UV0 = uv.Uv3,
                UV1 = new float2(1, 0),
                UV2 = mask.AO
            };

            // 4 Top Right
            Vertex vertex4 = new()
            {
                Position = v4,
                Normal = normal,
                UV0 = uv.Uv4,
                UV1 = new float2(1, 1),
                UV2 = mask.AO
            };

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
            int width, int height, float3 v1, float3 v2, float3 v3, float3 v4
        )
        {
            int3 normal = directionMask * mask.Normal;
            int uvz = info.GetTextureId(normal.ToDirection());

            // Flora uses crossed quads, early out
            if (info.VoxelType == VoxelType.Flora)
            {
                return RenderFloraCross(mesh, info, vertexCount, v1, v2, v3, v4, normal);
            }

            // Liquids: lower the top face a bit
            LowerLiquidTopFaceIfNeeded(info.VoxelType, normal, ref v1, ref v2, ref v3, ref v4);

            UVQuad uv = ComputeFaceUVs(normal, width, height, uvz);

            // 1 Bottom Left
            Vertex vertex1 = new()
            {
                Position = v1,
                Normal = normal,
                UV0 = uv.Uv1,
                UV1 = new float2(0, 0),
                UV2 = info.OverrideColor
            };

            // 2 Top Left
            Vertex vertex2 = new()
            {
                Position = v2,
                Normal = normal,
                UV0 = uv.Uv2,
                UV1 = new float2(0, 1),
                UV2 = info.OverrideColor
            };

            // 3 Bottom Right
            Vertex vertex3 = new()
            {
                Position = v3,
                Normal = normal,
                UV0 = uv.Uv3,
                UV1 = new float2(1, 0),
                UV2 = info.OverrideColor
            };

            // 4 Top Right
            Vertex vertex4 = new()
            {
                Position = v4,
                Normal = normal,
                UV0 = uv.Uv4,
                UV1 = new float2(1, 1),
                UV2 = info.OverrideColor
            };

            mesh.VertexBuffer.Add(vertex1);
            mesh.VertexBuffer.Add(vertex2);
            mesh.VertexBuffer.Add(vertex3);
            mesh.VertexBuffer.Add(vertex4);

            AddQuadIndices(mesh.IndexBuffer1, vertexCount, mask.Normal, mask.AO);
            return 4;
        }

        private static void AddFloraQuad(MeshBuffer mesh, VoxelRenderDef info, int vertexCount, float3 v1, float3 v2,
            float3 v3, float3 v4)
        {
            int uvz = info.TexUp;
            float u0 = UVEdgeInset;
            float v0 = UVEdgeInset;
            float u1 = 1f - UVEdgeInset;
            float v1U = 1f - UVEdgeInset;
            Vertex vertex1 = new()
            {
                Position = v1,
                Normal = new float3(0, 1, 0),
                UV0 = new float3(u0, v0, uvz),
                UV1 = new float2(0, 0),
                UV2 = info.OverrideColor
            };
            Vertex vertex2 = new()
            {
                Position = v2,
                Normal = new float3(0, 1, 0),
                UV0 = new float3(u0, v1U, uvz),
                UV1 = new float2(0, 1),
                UV2 = info.OverrideColor
            };
            Vertex vertex3 = new()
            {
                Position = v3,
                Normal = new float3(0, 1, 0),
                UV0 = new float3(u1, v0, uvz),
                UV1 = new float2(1, 0),
                UV2 = info.OverrideColor
            };
            Vertex vertex4 = new()
            {
                Position = v4,
                Normal = new float3(0, 1, 0),
                UV0 = new float3(u1, v1U, uvz),
                UV1 = new float2(1, 1),
                UV2 = info.OverrideColor
            };
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

            int lo = GetMeshIndex(accessor.GetVoxelInChunk(pos, l), renderGenData) == 0 ? 1 : 0;
            int ro = GetMeshIndex(accessor.GetVoxelInChunk(pos, r), renderGenData) == 0 ? 1 : 0;
            int bo = GetMeshIndex(accessor.GetVoxelInChunk(pos, b), renderGenData) == 0 ? 1 : 0;
            int to = GetMeshIndex(accessor.GetVoxelInChunk(pos, T), renderGenData) == 0 ? 1 : 0;

            int lbco = GetMeshIndex(accessor.GetVoxelInChunk(pos, lbc), renderGenData) == 0 ? 1 : 0;
            int rbco = GetMeshIndex(accessor.GetVoxelInChunk(pos, rbc), renderGenData) == 0 ? 1 : 0;
            int ltco = GetMeshIndex(accessor.GetVoxelInChunk(pos, ltc), renderGenData) == 0 ? 1 : 0;
            int rtco = GetMeshIndex(accessor.GetVoxelInChunk(pos, rtc), renderGenData) == 0 ? 1 : 0;

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
