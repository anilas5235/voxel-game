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
        [BurstCompile]
        private readonly struct Mask
        {
            public readonly ushort VoxelId;

            internal readonly byte MeshIndex;
            internal readonly sbyte Normal;
            internal readonly int4 AO;

            public Mask(ushort voxelId, byte meshIndex, sbyte normal, int4 ao)
            {
                MeshIndex = meshIndex;
                VoxelId = voxelId;
                Normal = normal;
                AO = ao;
            }
        }

        [BurstCompile]
        private static bool CompareMask(Mask m1, Mask m2)
        {
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
                int axis1 = (direction + 1) % 3;
                int axis2 = (direction + 2) % 3;

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
                    ComputeNormalMask(accessor, chunkPos, chunkItr, directionMask, axis1, axis2, axis1Limit, axis2Limit, renderGenData, normalMask);
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
                                int width = FindQuadWidth(normalMask, n, currentMask, axis1Limit, axis1, axis2, i, axis1Limit);
                                int height = FindQuadHeight(normalMask, n, currentMask, axis1Limit, axis2Limit, width, j);
                                deltaAxis1[axis1] = width;
                                deltaAxis2[axis2] = height;
                                vertexCount += CreateAndAddQuad(mesh, renderGenData, currentMask, directionMask, width, height, chunkItr, deltaAxis1, deltaAxis2, vertexCount);
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
        private static void ComputeNormalMask(ChunkAccessor accessor, int3 chunkPos, int3 chunkItr, int3 directionMask, int axis1, int axis2, int axis1Limit, int axis2Limit, VoxelEngineRenderGenData renderGenData, NativeArray<Mask> normalMask)
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
                    // Always draw faces between different voxels, even if both are transparent
                    if (currentMeshIndex == compareMeshIndex && currentMeshIndex != 1)
                    {
                        normalMask[n++] = default;
                    }
                    else if (currentMeshIndex < compareMeshIndex)
                    {
                        normalMask[n++] = new Mask(currentVoxel, currentMeshIndex, 1,
                            ComputeAOMask(accessor, renderGenData, chunkPos, chunkItr + directionMask, axis1, axis2));
                    }
                    else
                    {
                        normalMask[n++] = new Mask(compareVoxel, compareMeshIndex, -1,
                            ComputeAOMask(accessor, renderGenData, chunkPos, chunkItr, axis1, axis2));
                    }
                }
            }
        }

        [BurstCompile]
        private static int FindQuadWidth(NativeArray<Mask> normalMask, int n, Mask currentMask, int axis1Limit, int axis1, int axis2, int i, int axis1LimitMax)
        {
            int width;
            for (width = 1; i + width < axis1LimitMax && CompareMask(normalMask[n + width], currentMask); width++)
            {
            }
            return width;
        }

        [BurstCompile]
        private static int FindQuadHeight(NativeArray<Mask> normalMask, int n, Mask currentMask, int axis1Limit, int axis2Limit, int width, int j)
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
        private static int CreateAndAddQuad(MeshBuffer mesh, VoxelEngineRenderGenData renderGenData, Mask currentMask, int3 directionMask, int width, int height, int3 chunkItr, int3 deltaAxis1, int3 deltaAxis2, int vertexCount)
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
            return mask.MeshIndex switch
            {
                0 => CreateQuadMesh0(mesh, info, vertexCount, mask, directionMask, width, height, v1, v2, v3, v4),
                1 => CreateQuadMesh1(mesh, info, vertexCount, mask, directionMask, width, height, v1, v2, v3, v4),
                _ => 0
            };
        }

        [BurstCompile]
        private static int CreateQuadMesh0(
            MeshBuffer mesh, VoxelRenderDef info, int vertexCount, Mask mask, int3 directionMask,
            int width, int height, float3 v1, float3 v2, float3 v3, float3 v4
        )
        {
            int3 normal = directionMask * mask.Normal;

            // Main UV
            float3 uv1, uv2, uv3, uv4;
            int uvz = info.GetTextureId(normal.ToDirection());

            if (normal.x is 1 or -1)
            {
                uv1 = new float3(0, 0, uvz);
                uv2 = new float3(0, width, uvz);
                uv3 = new float3(height, 0, uvz);
                uv4 = new float3(height, width, uvz);
            }
            else
            {
                uv1 = new float3(0, 0, uvz);
                uv2 = new float3(width, 0, uvz);
                uv3 = new float3(0, height, uvz);
                uv4 = new float3(width, height, uvz);
            }

            // 1 Bottom Left
            Vertex vertex1 = new()
            {
                Position = v1,
                Normal = normal,
                UV0 = uv1,
                UV1 = new float2(0, 0),
                UV2 = mask.AO
            };

            // 2 Top Left
            Vertex vertex2 = new()
            {
                Position = v2,
                Normal = normal,
                UV0 = uv2,
                UV1 = new float2(0, 1),
                UV2 = mask.AO
            };

            // 3 Bottom Right
            Vertex vertex3 = new()
            {
                Position = v3,
                Normal = normal,
                UV0 = uv3,
                UV1 = new float2(1, 0),
                UV2 = mask.AO
            };

            // 4 Top Right
            Vertex vertex4 = new()
            {
                Position = v4,
                Normal = normal,
                UV0 = uv4,
                UV1 = new float2(1, 1),
                UV2 = mask.AO
            };

            mesh.VertexBuffer.Add(vertex1);
            mesh.VertexBuffer.Add(vertex2);
            mesh.VertexBuffer.Add(vertex3);
            mesh.VertexBuffer.Add(vertex4);

            NativeList<int> indexBuffer = mesh.IndexBuffer0;

            if (mask.AO[0] + mask.AO[3] > mask.AO[1] + mask.AO[2])
            {
                // + -
                indexBuffer.Add(vertexCount); // 0 0
                indexBuffer.Add(vertexCount + 2 - mask.Normal); // 1 3
                indexBuffer.Add(vertexCount + 2 + mask.Normal); // 3 1

                indexBuffer.Add(vertexCount + 3); // 3 3
                indexBuffer.Add(vertexCount + 1 + mask.Normal); // 2 0
                indexBuffer.Add(vertexCount + 1 - mask.Normal); // 0 2
            }
            else
            {
                // + -
                indexBuffer.Add(vertexCount + 1); // 1 1
                indexBuffer.Add(vertexCount + 1 + mask.Normal); // 2 0
                indexBuffer.Add(vertexCount + 1 - mask.Normal); // 0 2

                indexBuffer.Add(vertexCount + 2); // 2 2
                indexBuffer.Add(vertexCount + 2 - mask.Normal); // 1 3
                indexBuffer.Add(vertexCount + 2 + mask.Normal); // 3 1
            }

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

            // Main UV
            float3 uv1, uv2, uv3, uv4;
            if (normal.x is 1 or -1)
            {
                uv1 = new float3(0, 0, uvz);
                uv2 = new float3(0, width, uvz);
                uv3 = new float3(height, 0, uvz);
                uv4 = new float3(height, width, uvz);
            }
            else
            {
                uv1 = new float3(0, 0, uvz);
                uv2 = new float3(width, 0, uvz);
                uv3 = new float3(0, height, uvz);
                uv4 = new float3(width, height, uvz);
            }

            switch (info.VoxelType)
            {
                case VoxelType.Full:
                    break;
                case VoxelType.Liquid:
                    if (normal.y == 1)
                    {
                        v1.y -= 0.25f;
                        v2.y -= 0.25f;
                        v3.y -= 0.25f;
                        v4.y -= 0.25f;
                    }
                    break;
                case VoxelType.Flora:
                    if(normal.y != 1) break;
                    // Draw two crossed quads for grass
                    float3 center = (v1 + v4) * 0.5f;
                    float3 size = new(width, height, width); // Use width for X/Z, height for Y
                    float3 offsetX = new(size.x * 0.5f, 0, 0);
                    float3 offsetZ = new(0, 0, size.z * 0.5f);
                    float3 offsetY = new(0, size.y, 0);

                    // First quad (XZ diagonal)
                    AddFloraQuad(mesh, info, vertexCount, center - offsetX, center + offsetX + offsetY, center - offsetX + offsetY, center + offsetX);
                    // Second quad (ZX diagonal)
                    AddFloraQuad(mesh, info, vertexCount + 4, center - offsetZ, center + offsetZ + offsetY, center - offsetZ + offsetY, center + offsetZ);
                    return 8;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // 1 Bottom Left
            Vertex vertex1 = new()
            {
                Position = v1,
                Normal = normal,
                UV0 = uv1,
                UV1 = new float2(0, 0),
                UV2 = info.OverrideColor
            };

            // 2 Top Left
            Vertex vertex2 = new()
            {
                Position = v2,
                Normal = normal,
                UV0 = uv2,
                UV1 = new float2(0, 1),
                UV2 = info.OverrideColor
            };

            // 3 Bottom Right
            Vertex vertex3 = new()
            {
                Position = v3,
                Normal = normal,
                UV0 = uv3,
                UV1 = new float2(1, 0),
                UV2 = info.OverrideColor
            };

            // 4 Top Right
            Vertex vertex4 = new()
            {
                Position = v4,
                Normal = normal,
                UV0 = uv4,
                UV1 = new float2(1, 1),
                UV2 = info.OverrideColor
            };

            mesh.VertexBuffer.Add(vertex1);
            mesh.VertexBuffer.Add(vertex2);
            mesh.VertexBuffer.Add(vertex3);
            mesh.VertexBuffer.Add(vertex4);

            NativeList<int> indexBuffer = mesh.IndexBuffer1;

            if (mask.AO[0] + mask.AO[3] > mask.AO[1] + mask.AO[2])
            {
                // + -
                indexBuffer.Add(vertexCount); // 0 0
                indexBuffer.Add(vertexCount + 2 - mask.Normal); // 1 3
                indexBuffer.Add(vertexCount + 2 + mask.Normal); // 3 1
                indexBuffer.Add(vertexCount + 3); // 3 3
                indexBuffer.Add(vertexCount + 1 + mask.Normal); // 2 0
                indexBuffer.Add(vertexCount + 1 - mask.Normal); // 0 2
            }
            else
            {
                // + -
                indexBuffer.Add(vertexCount + 1); // 1 1
                indexBuffer.Add(vertexCount + 1 + mask.Normal); // 2 0
                indexBuffer.Add(vertexCount + 1 - mask.Normal); // 0 2
                indexBuffer.Add(vertexCount + 2); // 2 2
                indexBuffer.Add(vertexCount + 2 - mask.Normal); // 1 3
                indexBuffer.Add(vertexCount + 2 + mask.Normal); // 3 1
            }

            return 4;
        }

        private static void AddFloraQuad(MeshBuffer mesh, VoxelRenderDef info, int vertexCount, float3 v1, float3 v2, float3 v3, float3 v4)
        {
            int uvz = info.TexUp;
            Vertex vertex1 = new()
            {
                Position = v1,
                Normal = new float3(0, 1, 0),
                UV0 = new float3(0, 0, uvz),
                UV1 = new float2(0, 0),
                UV2 = info.OverrideColor
            };
            Vertex vertex2 = new()
            {
                Position = v2,
                Normal = new float3(0, 1, 0),
                UV0 = new float3(1, 1, uvz),
                UV1 = new float2(0, 1),
                UV2 = info.OverrideColor
            };
            Vertex vertex3 = new()
            {
                Position = v3,
                Normal = new float3(0, 1, 0),
                UV0 = new float3(0, 1, uvz),
                UV1 = new float2(1, 0),
                UV2 = info.OverrideColor
            };
            Vertex vertex4 = new()
            {
                Position = v4,
                Normal = new float3(0, 1, 0),
                UV0 = new float3(1, 0, uvz),
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

