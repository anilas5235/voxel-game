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
    internal struct GreedyMesher : IDisposable
    {
        private static readonly int3 YOne = new(0, 1, 0);

        // Small UV inset to reduce atlas bleeding at tile borders (in tile UV units)
        private const float UVEdgeInset = 0.005f;

        // Amount to lower liquid surface when exposed at the top
        private const float LiquidSurfaceLowering = 0.2f;

        private readonly ChunkAccessor _accessor;
        private readonly int3 _chunkPos;
        private readonly int3 _size;
        private readonly VoxelEngineRenderGenData _renderGenData;

        private readonly MeshBuffer _meshBuffer;
        private NativeHashMap<int3, VoxelRenderDef> _foliageVoxels;

        internal GreedyMesher(
            ChunkAccessor accessor, int3 chunkPos, int3 size, VoxelEngineRenderGenData renderGenData
        )
        {
            _accessor = accessor;
            _chunkPos = chunkPos;
            _size = size;
            _renderGenData = renderGenData;
            _meshBuffer = new MeshBuffer
            {
                VertexBuffer = new NativeList<Vertex>(Allocator.Temp),
                IndexBuffer0 = new NativeList<int>(Allocator.Temp),
                IndexBuffer1 = new NativeList<int>(Allocator.Temp),
                CVertexBuffer = new NativeList<CVertex>(Allocator.Temp),
                CIndexBuffer = new NativeList<int>(Allocator.Temp)
            };
            _foliageVoxels = new NativeHashMap<int3, VoxelRenderDef>(40, Allocator.Temp);
        }

        [BurstCompile]
        internal MeshBuffer GenerateMesh()
        {
            int vertexCount = 0;

            for (int direction = 0; direction < 3; direction++)
            {
                int axis1 = (direction + 1) % 3; // U axis
                int axis2 = (direction + 2) % 3; // V axis

                int mainAxisLimit = _size[direction];
                int axis1Limit = _size[axis1];
                int axis2Limit = _size[axis2];

                int3 deltaAxis1 = int3.zero;
                int3 deltaAxis2 = int3.zero;

                int3 chunkItr = int3.zero;
                int3 directionMask = int3.zero;
                directionMask[direction] = 1;

                NativeArray<Mask> normalMask = new(axis1Limit * axis2Limit, Allocator.Temp);

                for (chunkItr[direction] = -1; chunkItr[direction] < mainAxisLimit;)
                {
                    // Build mask for current slice
                    BuildFaceMask(chunkItr, directionMask, axis1, axis2, axis1Limit, axis2Limit, normalMask);
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
                                     _renderGenData.GetRenderDef(currentMask.VoxelId),
                                    vertexCount, currentMask, directionMask, new int2(width, height), new VQuad(
                                        chunkItr,
                                        chunkItr + deltaAxis1,
                                        chunkItr + deltaAxis2,
                                        chunkItr + deltaAxis1 + deltaAxis2
                                    )
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

            foreach (KVPair<int3, VoxelRenderDef> entry in _foliageVoxels)
            {
                int3 pos = entry.Key;
                VoxelRenderDef def = entry.Value;
                vertexCount += AddFloraQuad(def, vertexCount,
                    new VQuad(
                        pos,
                        pos + new float3(0, 1, 0),
                        pos + new float3(1, 0, 1),
                        pos + new float3(1, 1, 1)
                    ), int4.zero
                );
                vertexCount += AddFloraQuad(def, vertexCount,
                    new VQuad(
                        pos + new float3(1, 0, 0),
                        pos + new float3(1, 1, 0),
                        pos + new float3(0, 0, 1),
                        pos + new float3(0, 1, 1)
                    ), int4.zero
                );
            }

            // Collider pass: build a minimal collider mesh using greedy quads over collidable boundaries
            int cVertexCount = 0;
            for (int direction = 0; direction < 3; direction++)
            {
                int axis1 = (direction + 1) % 3; // U axis
                int axis2 = (direction + 2) % 3; // V axis

                int mainAxisLimit = _size[direction];
                int axis1Limit = _size[axis1];
                int axis2Limit = _size[axis2];

                int3 deltaAxis1 = int3.zero;
                int3 deltaAxis2 = int3.zero;

                int3 chunkItr = int3.zero;
                int3 directionMask = int3.zero;
                directionMask[direction] = 1;

                NativeArray<Mask> colliderMask = new(axis1Limit * axis2Limit, Allocator.Temp);

                for (chunkItr[direction] = -1; chunkItr[direction] < mainAxisLimit;)
                {
                    // Build collider mask for current slice
                    BuildColliderMask(_accessor, _chunkPos, chunkItr, directionMask, axis1, axis2, axis1Limit,
                        axis2Limit, _renderGenData, colliderMask);

                    ++chunkItr[direction];
                    int n = 0;
                    for (int j = 0; j < axis2Limit; j++)
                    {
                        for (int i = 0; i < axis1Limit;)
                        {
                            if (colliderMask[n].Normal != 0)
                            {
                                Mask currentMask = colliderMask[n];
                                chunkItr[axis1] = i;
                                chunkItr[axis2] = j;
                                int width = FindQuadWidth(colliderMask, n, currentMask, i, axis1Limit);
                                int height = FindQuadHeight(colliderMask, n, currentMask, axis1Limit, axis2Limit, width,
                                    j);
                                deltaAxis1[axis1] = width;
                                deltaAxis2[axis2] = height;

                                cVertexCount += CreateColliderQuad(
                                    _meshBuffer, cVertexCount, currentMask, directionMask, new VQuad(
                                        chunkItr,
                                        chunkItr + deltaAxis1,
                                        chunkItr + deltaAxis2,
                                        chunkItr + deltaAxis1 + deltaAxis2
                                    )
                                );

                                ClearMaskRegion(colliderMask, n, width, height, axis1Limit);
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

                colliderMask.Dispose();
            }

            return _meshBuffer;
        }

        #region Mask Helpers

        [BurstCompile]
        private void BuildFaceMask(int3 chunkItr, int3 directionMask, int axis1, int axis2, int axis1Limit,
            int axis2Limit, NativeArray<Mask> normalMask)
        {
            int n = 0;
            for (chunkItr[axis2] = 0; chunkItr[axis2] < axis2Limit; ++chunkItr[axis2])
            {
                for (chunkItr[axis1] = 0; chunkItr[axis1] < axis1Limit; ++chunkItr[axis1])
                {
                    int3 neighborCoord = chunkItr + directionMask;

                    ushort currentVoxel = _accessor.GetVoxelInChunk(_chunkPos, chunkItr);
                    ushort neighborVoxel = _accessor.GetVoxelInChunk(_chunkPos, neighborCoord);

                    VoxelRenderDef currentDef = _renderGenData.GetRenderDef(currentVoxel);
                    VoxelRenderDef neighborDef = _renderGenData.GetRenderDef(neighborVoxel);

                    MeshLayer currentLayer = currentDef.MeshLayer;
                    MeshLayer neighborLayer = neighborDef.MeshLayer;

                    // Flora: collect for separate foliage pass, still emit a backface to keep AO continuity
                    if (currentDef.VoxelType == VoxelType.Flora)
                    {
                        _foliageVoxels.TryAdd(chunkItr, currentDef);
                        int4 floraAo = ComputeAOMask(chunkItr, axis1, axis2);
                        // top open for the neighbor (owning backface)
                        sbyte neighborTopOpen = ComputeTopOpen(neighborCoord + YOne, neighborDef.VoxelType);
                        normalMask[n] = new Mask(neighborVoxel, neighborLayer, -1, floraAo, neighborTopOpen);
                        n++;
                        continue;
                    }

                    // Same layer and not forced -> no face between them
                    if (ShouldSkipFace(currentDef, neighborDef))
                    {
                        normalMask[n] = default;
                        n++;
                        continue;
                    }

                    bool currentOwns = IsCurrentOwner(currentLayer, neighborDef);
                    if (currentOwns)
                    {
                        int4 ao = ComputeAOMask(neighborCoord, axis1, axis2);
                        sbyte topOpen = ComputeTopOpen(chunkItr + YOne, currentDef.VoxelType);
                        normalMask[n] = new Mask(currentVoxel, currentLayer, 1, ao, topOpen);
                    }
                    else
                    {
                        int4 ao = ComputeAOMask(chunkItr, axis1, axis2);
                        sbyte topOpen = ComputeTopOpen(neighborCoord + YOne, neighborDef.VoxelType);
                        normalMask[n] = new Mask(neighborVoxel, neighborLayer, -1, ao, topOpen);
                    }

                    n++;
                }
            }
        }

        [BurstCompile]
        private static bool ShouldSkipFace(VoxelRenderDef currentDef, VoxelRenderDef neighborDef)
        {
            return !currentDef.AlwaysRenderAllFaces &&
                   currentDef.MeshLayer == neighborDef.MeshLayer &&
                   neighborDef.VoxelType != VoxelType.Flora;
        }

        [BurstCompile]
        private static bool IsCurrentOwner(MeshLayer currentLayer, VoxelRenderDef neighborDef)
        {
            return currentLayer < neighborDef.MeshLayer || neighborDef.VoxelType == VoxelType.Flora;
        }

        [BurstCompile]
        private sbyte ComputeTopOpen(int3 aboveCoord, VoxelType ownerType)
        {
            if (ownerType != VoxelType.Liquid) return 0;
            ushort aboveId = _accessor.GetVoxelInChunk(_chunkPos, aboveCoord);
            VoxelRenderDef aboveDef = _renderGenData.GetRenderDef(aboveId);
            return (sbyte)(aboveDef.VoxelType != VoxelType.Liquid ? 1 : 0);
        }

        [BurstCompile]
        private static void BuildColliderMask(ChunkAccessor accessor, int3 chunkPos, int3 chunkItr, int3 directionMask,
            int axis1, int axis2, int axis1Limit, int axis2Limit, VoxelEngineRenderGenData renderGenData,
            NativeArray<Mask> colliderMask)
        {
            int n = 0;
            for (chunkItr[axis2] = 0; chunkItr[axis2] < axis2Limit; ++chunkItr[axis2])
            {
                for (chunkItr[axis1] = 0; chunkItr[axis1] < axis1Limit; ++chunkItr[axis1])
                {
                    ushort currentVoxel = accessor.GetVoxelInChunk(chunkPos, chunkItr);
                    ushort compareVoxel = accessor.GetVoxelInChunk(chunkPos, chunkItr + directionMask);
                    bool currentCollidable = renderGenData.GetRenderDef(currentVoxel).Collision;
                    bool compareCollidable = renderGenData.GetRenderDef(compareVoxel).Collision;

                    // Only when there is a boundary between collidable and non-collidable we emit a face
                    if (currentCollidable ^ compareCollidable)
                    {
                        sbyte normal = currentCollidable ? (sbyte)1 : (sbyte)-1;
                        colliderMask[n++] = new Mask(1, MeshLayer.Solid, normal, new int4(0, 0, 0, 0), 0);
                    }
                    else
                    {
                        colliderMask[n++] = default;
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

        #endregion

        #region Quad Creation

        [BurstCompile]
        private int CreateQuad(
            VoxelRenderDef info, int vertexCount, Mask mask, int3 directionMask, int2 size, VQuad verts
        )
        {
            switch (mask.MeshLayer)
            {
                case MeshLayer.Solid:
                    return CreateQuadMesh0(info, vertexCount, mask, directionMask, size, verts);
                case MeshLayer.Transparent:
                    return CreateQuadMesh1(info, vertexCount, mask, directionMask, size, verts);
                default:
                    return 0;
            }
        }

        [BurstCompile]
        private static int CreateColliderQuad(
            MeshBuffer mesh, int cVertexCount, Mask mask, int3 directionMask,
            VQuad verts
        )
        {
            int3 normal = directionMask * mask.Normal;

            AddColliderVertices(mesh, verts, normal);

            // Use AO zeros for a deterministic diagonal, reuse existing helper for correct winding
            AddQuadIndices(mesh.CIndexBuffer, cVertexCount, mask.Normal, int4.zero);
            return 4;
        }

        [BurstCompile]
        private int CreateQuadMesh0(
            VoxelRenderDef info, int vertexCount, Mask mask, int3 directionMask,
            int2 size, VQuad verts
        )
        {
            int3 normal = directionMask * mask.Normal;

            int texIndex = info.GetTextureId(normal.ToDirection());
            UVQuad uv = ComputeFaceUVs(normal, size);

            AddVertices(_meshBuffer, verts, normal, uv, new float4(texIndex, 0, 0, 0), mask.AO);

            AddQuadIndices(_meshBuffer.IndexBuffer0, vertexCount, mask.Normal, mask.AO);
            return 4;
        }

        [BurstCompile]
        private int CreateQuadMesh1(
            VoxelRenderDef info, int vertexCount, Mask mask, int3 directionMask,
            int2 size, VQuad verts
        )
        {
            int3 normal = directionMask * mask.Normal;

            switch (info.VoxelType)
            {
                case VoxelType.Full:
                    break;
                case VoxelType.Liquid:
                    // Lower the visible liquid surface
                    if (normal.y == 1)
                    {
                        verts.OffsetAll(new float3(0, -LiquidSurfaceLowering, 0));
                    }
                    // For vertical faces, if the top is exposed (no liquid above), lower only the top edge
                    else if (normal.y == 0 && mask.TopOpen == 1)
                    {
                        float topY = math.max(math.max(verts.V1.y, verts.V2.y), math.max(verts.V3.y, verts.V4.y));
                        const float eps = 1e-4f;
                        if (math.abs(verts.V1.y - topY) < eps) verts.V1.y -= LiquidSurfaceLowering;
                        if (math.abs(verts.V2.y - topY) < eps) verts.V2.y -= LiquidSurfaceLowering;
                        if (math.abs(verts.V3.y - topY) < eps) verts.V3.y -= LiquidSurfaceLowering;
                        if (math.abs(verts.V4.y - topY) < eps) verts.V4.y -= LiquidSurfaceLowering;
                    }

                    break;
                case VoxelType.Flora:
                    float3 xOne = new(1, 0, 0);
                    return normal.x switch
                    {
                        1 => AddFloraQuad(info, vertexCount,
                            new VQuad(verts.V1, verts.V2, verts.V3 - xOne, verts.V4 - xOne), mask.AO),
                        -1 => AddFloraQuad(info, vertexCount,
                            new VQuad(verts.V1, verts.V2, verts.V3 + xOne, verts.V4 + xOne), mask.AO),
                        _ => 0
                    };

                default:
                    throw new ArgumentOutOfRangeException();
            }

            int texIndex = info.GetTextureId(normal.ToDirection());
            UVQuad uv = ComputeFaceUVs(normal, size);

            AddVertices(_meshBuffer, verts, normal, uv, new float4(texIndex, info.DepthFadeDistance, 0, 0), mask.AO);

            AddQuadIndices(_meshBuffer.IndexBuffer1, vertexCount, mask.Normal, mask.AO);
            return 4;
        }

        [BurstCompile]
        private static UVQuad ComputeFaceUVs(int3 normal, int2 size)
        {
            UVQuad uv;
            float uMaxW = math.max(UVEdgeInset, size.x - UVEdgeInset);
            float vMaxH = math.max(UVEdgeInset, size.y - UVEdgeInset);

            if (normal.x is 1 or -1)
            {
                uv.Uv1 = new float4(UVEdgeInset, UVEdgeInset, 0, 0); // (0,0)
                uv.Uv2 = new float4(UVEdgeInset, uMaxW, 0, 1); // (0,width)
                uv.Uv3 = new float4(vMaxH, UVEdgeInset, 1, 0); // (height,0)
                uv.Uv4 = new float4(vMaxH, uMaxW, 1, 1); // (height,width)
            }
            else
            {
                uv.Uv1 = new float4(UVEdgeInset, UVEdgeInset, 0, 0); // (0,0)
                uv.Uv2 = new float4(uMaxW, UVEdgeInset, 0, 1); // (width,0)
                uv.Uv3 = new float4(UVEdgeInset, vMaxH, 1, 0); // (0,height)
                uv.Uv4 = new float4(uMaxW, vMaxH, 1, 1); // (width,height)
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
        private static void AddVertices(MeshBuffer mesh, VQuad verts, float3 normal, UVQuad uv0, float4 uv1, float4 uv2)
        {
            // 1 Bottom Left
            Vertex vertex1 = new(verts.V1, normal, uv0.Uv1, uv1, uv2);

            // 2 Top Left
            Vertex vertex2 = new(verts.V2, normal, uv0.Uv2, uv1, uv2);

            // 3 Bottom Right
            Vertex vertex3 = new(verts.V3, normal, uv0.Uv3, uv1, uv2);

            // 4 Top Right
            Vertex vertex4 = new(verts.V4, normal, uv0.Uv4, uv1, uv2);

            mesh.VertexBuffer.Add(vertex1);
            mesh.VertexBuffer.Add(vertex2);
            mesh.VertexBuffer.Add(vertex3);
            mesh.VertexBuffer.Add(vertex4);
        }

        [BurstCompile]
        private static void AddColliderVertices(MeshBuffer mesh, VQuad verts, float3 normal)
        {
            // 1 Bottom Left
            CVertex vertex1 = new(verts.V1, normal);
            // 2 Top Left
            CVertex vertex2 = new(verts.V2, normal);
            // 3 Bottom Right
            CVertex vertex3 = new(verts.V3, normal);
            // 4 Top Right
            CVertex vertex4 = new(verts.V4, normal);

            mesh.CVertexBuffer.Add(vertex1);
            mesh.CVertexBuffer.Add(vertex2);
            mesh.CVertexBuffer.Add(vertex3);
            mesh.CVertexBuffer.Add(vertex4);
        }

        [BurstCompile]
        private int AddFloraQuad(VoxelRenderDef info, int vertexCount, VQuad verts, float4 ao)
        {
            int texIndex = info.TexUp;
            UVQuad uv = ComputeFaceUVs(new int3(1, 1, 0), new int2(1, 1));
            float3 normal = new(0, 1, 0);

            AddVertices(_meshBuffer, verts, normal, uv, new float4(texIndex, -1, 0, 0), ao);

            NativeList<int> indexBuffer = _meshBuffer.IndexBuffer1;
            indexBuffer.Add(vertexCount);
            indexBuffer.Add(vertexCount + 1);
            indexBuffer.Add(vertexCount + 2);
            indexBuffer.Add(vertexCount + 2);
            indexBuffer.Add(vertexCount + 1);
            indexBuffer.Add(vertexCount + 3);

            return 4;
        }

        #endregion

        #region AO Calculation

        [BurstCompile]
        private int4 ComputeAOMask(int3 coord, int axis1, int axis2)
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

            int lo = GetMeshLayer(_accessor.GetVoxelInChunk(_chunkPos, l), _renderGenData) == 0 ? 1 : 0;
            int ro = GetMeshLayer(_accessor.GetVoxelInChunk(_chunkPos, r), _renderGenData) == 0 ? 1 : 0;
            int bo = GetMeshLayer(_accessor.GetVoxelInChunk(_chunkPos, b), _renderGenData) == 0 ? 1 : 0;
            int to = GetMeshLayer(_accessor.GetVoxelInChunk(_chunkPos, T), _renderGenData) == 0 ? 1 : 0;

            int lbco = GetMeshLayer(_accessor.GetVoxelInChunk(_chunkPos, lbc), _renderGenData) == 0 ? 1 : 0;
            int rbco = GetMeshLayer(_accessor.GetVoxelInChunk(_chunkPos, rbc), _renderGenData) == 0 ? 1 : 0;
            int ltco = GetMeshLayer(_accessor.GetVoxelInChunk(_chunkPos, ltc), _renderGenData) == 0 ? 1 : 0;
            int rtco = GetMeshLayer(_accessor.GetVoxelInChunk(_chunkPos, rtc), _renderGenData) == 0 ? 1 : 0;

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

        #endregion

        #region Structs

        [BurstCompile]
        private struct UVQuad
        {
            public float4 Uv1, Uv2, Uv3, Uv4;
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
        private readonly struct Mask
        {
            public readonly ushort VoxelId;

            internal readonly MeshLayer MeshLayer;
            internal readonly sbyte Normal;
            internal readonly sbyte TopOpen; // 1 if the owning voxel's top is exposed (no liquid above)

            internal readonly int4 AO;


            public Mask(ushort voxelId, MeshLayer meshLayer, sbyte normal, int4 ao, sbyte topOpen)
            {
                MeshLayer = meshLayer;
                VoxelId = voxelId;
                Normal = normal;
                AO = ao;
                TopOpen = topOpen;
            }

            public bool CompareTo(Mask other)
            {
                return
                    MeshLayer == other.MeshLayer &&
                    VoxelId == other.VoxelId &&
                    Normal == other.Normal &&
                    TopOpen == other.TopOpen &&
                    AO[0] == other.AO[0] &&
                    AO[1] == other.AO[1] &&
                    AO[2] == other.AO[2] &&
                    AO[3] == other.AO[3];
            }
        }

        #endregion

        #region Helpers

        [BurstCompile]
        private static bool CompareMask(Mask m1, Mask m2)
        {
            return m1.CompareTo(m2);
        }

        [BurstCompile]
        private static MeshLayer GetMeshLayer(ushort voxelId, VoxelEngineRenderGenData renderGenData)
        {
            return renderGenData.GetMeshLayer(voxelId);
        }

        [BurstCompile]
        private static void ClearMaskRegion(NativeArray<Mask> normalMask, int n, int width, int height, int axis1Limit)
        {
            for (int l = 0; l < height; ++l)
            for (int k = 0; k < width; ++k)
                normalMask[n + k + l * axis1Limit] = default;
        }

        #endregion

        public void Dispose()
        {
            _foliageVoxels.Dispose();
        }
    }
}