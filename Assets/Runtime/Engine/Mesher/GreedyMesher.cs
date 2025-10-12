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
            ChunkAccessor accessor, int3 pos, int3 size, VoxelEngineRenderGenData renderGenData
        )
        {
            MeshBuffer mesh = new()
            {
                VertexBuffer = new NativeList<Vertex>(Allocator.Temp),
                IndexBuffer0 = new NativeList<int>(Allocator.Temp),
                IndexBuffer1 = new NativeList<int>(Allocator.Temp),
                IndexBuffer2 = new NativeList<int>(Allocator.Temp),
                IndexBuffer3 = new NativeList<int>(Allocator.Temp)
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

                // Optimize Allocation
                NativeArray<Mask> normalMask = new(axis1Limit * axis2Limit, Allocator.Temp);

                for (chunkItr[direction] = -1; chunkItr[direction] < mainAxisLimit;)
                {
                    int n = 0;

                    // Compute the mask
                    for (chunkItr[axis2] = 0; chunkItr[axis2] < axis2Limit; ++chunkItr[axis2])
                    {
                        for (chunkItr[axis1] = 0; chunkItr[axis1] < axis1Limit; ++chunkItr[axis1])
                        {
                            ushort currentVoxel = accessor.GetVoxelInChunk(pos, chunkItr);
                            ushort compareVoxel = accessor.GetVoxelInChunk(pos, chunkItr + directionMask);

                            byte currentMeshIndex = GetMeshIndex(currentVoxel, renderGenData);
                            byte compareMeshIndex = GetMeshIndex(compareVoxel, renderGenData);

                            if (currentMeshIndex == compareMeshIndex)
                            {
                                normalMask[n++] =
                                    default; // Air with Air or Water with Water or Solid with Solid, no face in this case
                            }
                            else if (currentMeshIndex < compareMeshIndex)
                            {
                                normalMask[n++] = new Mask(currentVoxel, currentMeshIndex, 1,
                                    ComputeAOMask(accessor, renderGenData, pos, chunkItr + directionMask, axis1,
                                        axis2));
                            }
                            else
                            {
                                normalMask[n++] = new Mask(compareVoxel, compareMeshIndex, -1,
                                    ComputeAOMask(accessor, renderGenData, pos, chunkItr, axis1, axis2));
                            }
                        }
                    }

                    ++chunkItr[direction];
                    n = 0;

                    for (int j = 0; j < axis2Limit; j++)
                    {
                        for (int i = 0; i < axis1Limit;)
                        {
                            if (normalMask[n].Normal != 0)
                            {
                                // Create Quad
                                Mask currentMask = normalMask[n];
                                chunkItr[axis1] = i;
                                chunkItr[axis2] = j;

                                // Compute the width of this quad and store it in w                        
                                // This is done by searching along the current axis until mask[n + w] is false
                                int width;

                                for (width = 1;
                                     i + width < axis1Limit && CompareMask(normalMask[n + width], currentMask);
                                     width++)
                                {
                                }

                                // Compute the height of this quad and store it in h                        
                                // This is done by checking if every block next to this row (range 0 to w) is also part of the mask.
                                // For example, if w is 5 we currently have a quad of dimensions 1 x 5. To reduce triangle count,
                                // greedy meshing will attempt to expand this quad out to CHUNK_SIZE x 5, but will stop if it reaches a hole in the mask

                                int height;
                                bool done = false;

                                for (height = 1; j + height < axis2Limit; height++)
                                {
                                    // Check each block next to this quad
                                    for (int k = 0; k < width; ++k)
                                    {
                                        if (CompareMask(normalMask[n + k + height * axis1Limit], currentMask)) continue;

                                        done = true;

                                        break; // If there's a hole in the mask, exit
                                    }

                                    if (done) break;
                                }

                                // set delta's
                                deltaAxis1[axis1] = width;
                                deltaAxis2[axis2] = height;

                                // create quad
                                vertexCount += CreateQuad(
                                    mesh, vertexCount, currentMask, directionMask,
                                    width, height,
                                    chunkItr,
                                    chunkItr + deltaAxis1,
                                    chunkItr + deltaAxis2,
                                    chunkItr + deltaAxis1 + deltaAxis2,
                                    renderGenData
                                );

                                // reset delta's
                                deltaAxis1 = int3.zero;
                                deltaAxis2 = int3.zero;

                                // Clear this part of the mask, so we don't add duplicate faces
                                for (int l = 0; l < height; ++l)
                                for (int k = 0; k < width; ++k)
                                    normalMask[n + k + l * axis1Limit] = default;

                                // update loop vars
                                i += width;
                                n += width;
                            }
                            else
                            {
                                // nothing to do
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
        private static int CreateQuad(
            MeshBuffer mesh, int vertexCount, Mask mask, int3 directionMask,
            int width, int height, int3 v1, int3 v2, int3 v3, int3 v4,
            VoxelEngineRenderGenData renderGenData
        )
        {
            return mask.MeshIndex switch
            {
                0 => CreateQuadMesh0(mesh, vertexCount, mask, directionMask, width, height, v1, v2, v3, v4,
                    renderGenData),
                1 => CreateQuadMesh1(mesh, renderGenData, vertexCount, mask, directionMask, width, height, v1, v2, v3,
                    v4),
                _ => 0
            };
        }

        [BurstCompile]
        private static int CreateQuadMesh0(
            MeshBuffer mesh, int vertexCount, Mask mask, int3 directionMask,
            int width, int height, float3 v1, float3 v2, float3 v3, float3 v4,
            VoxelEngineRenderGenData voxelEngineRenderGenData
        )
        {
            int3 normal = directionMask * mask.Normal;

            // Main UV
            float3 uv1, uv2, uv3, uv4;
            int uvz = voxelEngineRenderGenData.GetTextureId(mask.VoxelId, normal.ToDirection());

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
            MeshBuffer mesh, VoxelEngineRenderGenData renderGenData, int vertexCount, Mask mask, int3 directionMask,
            int width, int height, float3 v1, float3 v2, float3 v3, float3 v4
        )
        {
            VoxelRenderDef info = renderGenData.GetRenderDef(mask.VoxelId);
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
                    break;
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

            if ((normal != new int3(0, 1, 0)).AndReduce()) return 4;

            normal *= -1;

            // 1 Bottom Left
            Vertex vertex5 = new()
            {
                Position = v1,
                Normal = normal,
                UV0 = uv1,
                UV1 = new float2(0, 0),
                UV2 = mask.AO
            };

            // 2 Top Left
            Vertex vertex6 = new()
            {
                Position = v2,
                Normal = normal,
                UV0 = uv2,
                UV1 = new float2(0, 1),
                UV2 = mask.AO
            };

            // 3 Bottom Right
            Vertex vertex7 = new()
            {
                Position = v3,
                Normal = normal,
                UV0 = uv3,
                UV1 = new float2(1, 0),
                UV2 = mask.AO
            };

            // 4 Top Right
            Vertex vertex8 = new()
            {
                Position = v4,
                Normal = normal,
                UV0 = uv4,
                UV1 = new float2(1, 1),
                UV2 = mask.AO
            };

            mesh.VertexBuffer.Add(vertex5);
            mesh.VertexBuffer.Add(vertex6);
            mesh.VertexBuffer.Add(vertex7);
            mesh.VertexBuffer.Add(vertex8);

            vertexCount += 4;

            if (mask.AO[0] + mask.AO[3] > mask.AO[1] + mask.AO[2])
            {
                // + -
                indexBuffer.Add(vertexCount + 2 + mask.Normal); // 3 1
                indexBuffer.Add(vertexCount + 2 - mask.Normal); // 1 3
                indexBuffer.Add(vertexCount); // 0 0

                indexBuffer.Add(vertexCount + 1 - mask.Normal); // 0 2
                indexBuffer.Add(vertexCount + 1 + mask.Normal); // 2 0
                indexBuffer.Add(vertexCount + 3); // 3 3
            }
            else
            {
                // + -
                indexBuffer.Add(vertexCount + 1 - mask.Normal); // 0 2
                indexBuffer.Add(vertexCount + 1 + mask.Normal); // 2 0
                indexBuffer.Add(vertexCount + 1); // 1 1

                indexBuffer.Add(vertexCount + 2 + mask.Normal); // 3 1
                indexBuffer.Add(vertexCount + 2 - mask.Normal); // 1 3
                indexBuffer.Add(vertexCount + 2); // 2 2
            }

            return 8;
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