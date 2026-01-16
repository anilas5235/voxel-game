using Runtime.Engine.Data;
using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Mesher
{
    /// <summary>
    /// Burst-optimized greedy mesher for voxel chunks. Merges contiguous faces into maximal rectangles
    /// to reduce vertex/index count. Produces render and collider data plus foliage (billboard) quads.
    /// </summary>
    [GenerateTestsForBurstCompatibility]
    internal struct GreedyMesher
    {
        private static readonly int3 YOne = new(0, 1, 0);
        private const float UVEdgeInset = 0.005f;
        private const float LiquidSurfaceLowering = 0.2f;

        private readonly ChunkAccessor _accessor;
        private readonly int3 _partitionPos;
        private readonly int2 _chunkPos;
        private readonly int3 _partitionSize;
        private readonly int3 _yOffset;
        private readonly VoxelEngineRenderGenData _renderGenData;
        private readonly MeshBuffer _mesh;
        private NativeHashMap<int3, byte> _foliageVoxels;
        private int _meshVertexCount;
        private int _colliderVertexCount;

        /// <summary>
        /// Creates a new mesher with internal buffers sized from chunk dimensions.
        /// </summary>
        internal GreedyMesher(
            ChunkAccessor accessor, int3 partitionPos, int3 partitionSize, VoxelEngineRenderGenData renderGenData
        )
        {
            _accessor = accessor;
            _partitionPos = partitionPos;
            _chunkPos = partitionPos.xz;
            _partitionSize = partitionSize;
            _yOffset = new int3(0, partitionPos.y, 0);
            _renderGenData = renderGenData;
            _mesh = new MeshBuffer
            {
                VertexBuffer = new NativeList<Vertex>(Allocator.Temp),
                IndexBuffer0 = new NativeList<int>(Allocator.Temp),
                IndexBuffer1 = new NativeList<int>(Allocator.Temp),
                CVertexBuffer = new NativeList<CVertex>(Allocator.Temp),
                CIndexBuffer = new NativeList<int>(Allocator.Temp)
            };
            // Pre-partitionSize buffers to reduce reallocations (rough upper bound heuristic)
            int voxelCount = math.max(1, _partitionSize.x * _partitionSize.y * _partitionSize.z);
            _mesh.VertexBuffer.Capacity = math.max(_mesh.VertexBuffer.Capacity, voxelCount * 4);
            _mesh.IndexBuffer0.Capacity = math.max(_mesh.IndexBuffer0.Capacity, voxelCount * 6);
            _mesh.IndexBuffer1.Capacity = math.max(_mesh.IndexBuffer1.Capacity, voxelCount * 6);
            _mesh.CVertexBuffer.Capacity = math.max(_mesh.CVertexBuffer.Capacity, voxelCount * 4);
            _mesh.CIndexBuffer.Capacity = math.max(_mesh.CIndexBuffer.Capacity, voxelCount * 6);

            _foliageVoxels = new NativeHashMap<int3, byte>(64, Allocator.Temp);
            _meshVertexCount = 0;
            _colliderVertexCount = 0;
        }

        /// <summary>
        /// Executes the meshing process: sweeps 3 axes, builds surface & collider quads, then foliage.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        internal MeshBuffer GenerateMesh()
        {
            // Sweep along each principal axis (X, Y, Z)
            for (int mainAxis = 0; mainAxis < 3; mainAxis++)
            {
                // Define orthogonal axes for the 2D slice (U and V plane)
                int uAxis = (mainAxis + 1) % 3;
                int vAxis = (mainAxis + 2) % 3;

                // We only generate faces for slices starting inside the chunk (0…size-1)
                // so that negative-side faces are owned by the neighboring chunk.
                int mainAxisLimit = _partitionSize[mainAxis];

                AxisInfo axisInfo = new()
                {
                    UAxis = uAxis,
                    VAxis = vAxis,
                    ULimit = _partitionSize[uAxis],
                    VLimit = _partitionSize[vAxis]
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
                    SliceActivity activity = BuildMasks(pos, directionMask, axisInfo,
                        normalMask, colliderMask);

                    // Move to the actual slice index we just built the mask for
                    ++pos[mainAxis];

                    if (activity.HasSurface)
                    {
                        _meshVertexCount = BuildSurfaceQuads(_meshVertexCount, axisInfo, pos,
                            normalMask, directionMask);
                    }

                    if (activity.HasCollider)
                    {
                        _colliderVertexCount = BuildColliderQuads(_colliderVertexCount, axisInfo, pos,
                            colliderMask, directionMask);
                    }
                }

                normalMask.Dispose();
                colliderMask.Dispose();
            }

            _meshVertexCount = BuildFoliage(_meshVertexCount);
            _foliageVoxels.Dispose();

            return _mesh;
        }

        [BurstCompile]
        private int3 GetVoxelWithYOffset(int3 pos)
        {
            return pos + _yOffset;
        }

        /// <summary>
        /// Builds surface quads for one slice using greedy merging.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int BuildSurfaceQuads(int vertexCount, AxisInfo axInfo, int3 pos, NativeArray<Mask> normalMask,
            int3 directionMask)
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
                        int quadHeight = FindQuadHeight(normalMask, maskIndex, current, axInfo.ULimit, axInfo.VLimit,
                            quadWidth, v);

                        uDelta[axInfo.UAxis] = quadWidth;
                        vDelta[axInfo.VAxis] = quadHeight;

                        vertexCount += CreateQuad(
                            _renderGenData.GetRenderDef(current.VoxelId),
                            vertexCount,
                            current,
                            directionMask,
                            new int2(quadWidth, quadHeight),
                            new VQuad(
                                pos,
                                pos + uDelta,
                                pos + vDelta,
                                pos + uDelta + vDelta
                            )
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
        private int BuildFoliage(int vertexCount)
        {
            // Build cross (billboard) quads for flora collected during the surface pass
            foreach (KVPair<int3, byte> entry in _foliageVoxels)
            {
                int3 p = entry.Key;
                if (!_accessor.InChunkBounds(p)) continue;
                ushort voxelId = _accessor.GetVoxelInChunk(_chunkPos, p);
                VoxelRenderDef def = _renderGenData.GetRenderDef(voxelId);
                p -= _yOffset;

                // Diagonal 1
                vertexCount += AddFloraQuad(def, vertexCount,
                    new VQuad(
                        p,
                        p + new float3(0, 1, 0),
                        p + new float3(1, 0, 1),
                        p + new float3(1, 1, 1)
                    ), int4.zero
                );

                // Diagonal 2
                vertexCount += AddFloraQuad(def, vertexCount,
                    new VQuad(
                        p + new float3(1, 0, 0),
                        p + new float3(1, 1, 0),
                        p + new float3(0, 0, 1),
                        p + new float3(0, 1, 1)
                    ), int4.zero
                );
            }

            return vertexCount;
        }

        /// <summary>
        /// Builds collider quads for one slice using greedy merging.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int BuildColliderQuads(int vertexCount, AxisInfo axInfo, int3 pos, NativeArray<Mask> colliderMask,
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
                        int quadHeight = FindQuadHeight(colliderMask, maskIndex, current, axInfo.ULimit, axInfo.VLimit,
                            quadWidth, v);

                        uDelta[axInfo.UAxis] = quadWidth;
                        vDelta[axInfo.VAxis] = quadHeight;

                        vertexCount += CreateColliderQuad(
                            vertexCount,
                            current,
                            directionMask,
                            new VQuad(
                                pos,
                                pos + uDelta,
                                pos + vDelta,
                                pos + uDelta + vDelta
                            )
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

        #region Mask Helpers

        /// <summary>
        /// Builds surface & collider masks for the current slice.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private SliceActivity BuildMasks(int3 chunkItr, int3 directionMask, AxisInfo axInfo,
            NativeArray<Mask> normalMask, NativeArray<Mask> colliderMask)
        {
            int n = 0;
            bool hasSurface = false;
            bool hasCollider = false;
            for (chunkItr[axInfo.VAxis] = 0; chunkItr[axInfo.VAxis] < axInfo.VLimit; ++chunkItr[axInfo.VAxis])
            {
                for (chunkItr[axInfo.UAxis] = 0; chunkItr[axInfo.UAxis] < axInfo.ULimit; ++chunkItr[axInfo.UAxis])
                {
                    int3 currentCoord = GetVoxelWithYOffset(chunkItr);
                    int3 neighborCoord = currentCoord + directionMask;

                    ushort currentVoxel = _accessor.GetVoxelInChunk(_chunkPos, currentCoord);
                    ushort neighborVoxel = _accessor.GetVoxelInChunk(_chunkPos, neighborCoord);

                    VoxelRenderDef currentDef = _renderGenData.GetRenderDef(currentVoxel);
                    VoxelRenderDef neighborDef = _renderGenData.GetRenderDef(neighborVoxel);

                    MeshLayer currentLayer = currentDef.MeshLayer;
                    MeshLayer neighborLayer = neighborDef.MeshLayer;

                    // Flora: collect for separate foliage pass, still emit a backface to keep AO continuity
                    if (currentDef.VoxelType == VoxelType.Flora)
                    {
                        _foliageVoxels.TryAdd(currentCoord, 1);
                        if (neighborDef.VoxelType == VoxelType.Flora)
                        {
                            normalMask[n] = default;
                        }
                        else
                        {
                            int4 floraAo = ComputeAOMask(currentCoord, ref axInfo);
                            sbyte neighborTopOpen = ComputeTopVoxelOfType(neighborCoord, neighborVoxel);
                            normalMask[n] = new Mask(neighborVoxel, neighborLayer, -1, floraAo, neighborTopOpen);
                            hasSurface = true;
                        }
                    }
                    else if (ShouldSkipFace(currentDef, neighborDef))
                    {
                        normalMask[n] = default;
                    }
                    else
                    {
                        bool currentOwns = IsCurrentOwner(currentLayer, neighborDef);
                        if (currentOwns)
                        {
                            int4 ao = ComputeAOMask(neighborCoord, ref axInfo);
                            sbyte topOpen = ComputeTopVoxelOfType(currentCoord, currentVoxel);
                            normalMask[n] = new Mask(currentVoxel, currentLayer, 1, ao, topOpen);
                        }
                        else
                        {
                            int4 ao = ComputeAOMask(currentCoord, ref axInfo);
                            sbyte topOpen = ComputeTopVoxelOfType(neighborCoord, neighborVoxel);
                            normalMask[n] = new Mask(neighborVoxel, neighborLayer, -1, ao, topOpen);
                        }

                        hasSurface = true;
                    }

                    // Collider mask
                    bool currentCollidable = currentDef.Collision;
                    bool compareCollidable = neighborDef.Collision;
                    if (currentCollidable ^ compareCollidable)
                    {
                        sbyte normal = currentCollidable ? (sbyte)1 : (sbyte)-1;
                        colliderMask[n] = new Mask(1, MeshLayer.Solid, normal, new int4(0, 0, 0, 0), 0);
                        hasCollider = true;
                    }
                    else
                    {
                        colliderMask[n] = default;
                    }

                    n++;
                }
            }

            return new SliceActivity { HasSurface = hasSurface, HasCollider = hasCollider };
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
        private sbyte ComputeTopVoxelOfType(int3 coord, ushort currentVoxelId)
        {
            ushort aboveId = _accessor.GetVoxelInChunk(_chunkPos, coord + YOne);
            return (sbyte)(aboveId != currentVoxelId ? 1 : 0);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private static int FindQuadWidth(NativeArray<Mask> normalMask, int n, Mask currentMask, int start, int max)
        {
            int width;
            for (width = 1; start + width < max && CompareMask(normalMask[n + width], currentMask); width++)
            {
            }

            return width;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int CreateQuad(
            VoxelRenderDef info, int vertexCount, Mask mask, int3 directionMask, int2 size, VQuad verts
        )
        {
            switch (mask.MeshLayer)
            {
                case MeshLayer.Solid:
                    return CreateSolidQuad(info, vertexCount, mask, directionMask, size, verts);
                case MeshLayer.Transparent:
                    return CreateTransparentQuad(info, vertexCount, mask, directionMask, size, verts);
                default:
                    return 0;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int CreateColliderQuad(int cVertexCount, Mask mask, int3 directionMask, VQuad verts
        )
        {
            int3 normal = directionMask * mask.Normal;

            AddColliderVertices(_mesh, verts, normal);

            // Use AO zeros for a deterministic diagonal, reuse existing helper for correct winding
            AddQuadIndices(_mesh.CIndexBuffer, cVertexCount, mask.Normal, int4.zero);
            return 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int CreateSolidQuad(
            VoxelRenderDef info, int vertexCount, Mask mask, int3 directionMask, int2 size, VQuad verts
        )
        {
            int3 normal = directionMask * mask.Normal;

            int texIndex = info.GetTextureId(normal.ToDirection());
            UVQuad uv = ComputeFaceUVs(normal, size);

            AddVertices(_mesh, verts, normal, uv, new float4(texIndex, 0, 0, 0), mask.AO);

            AddQuadIndices(_mesh.IndexBuffer0, vertexCount, mask.Normal, mask.AO);
            return 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int CreateTransparentQuad(
            VoxelRenderDef info, int vertexCount, Mask mask, int3 directionMask, int2 size, VQuad verts
        )
        {
            int3 normal = directionMask * mask.Normal;

            switch (info.VoxelType)
            {
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
            }

            int texIndex = info.GetTextureId(normal.ToDirection());
            UVQuad uv = ComputeFaceUVs(normal, size);

            AddVertices(_mesh, verts, normal, uv, new float4(texIndex, info.DepthFadeDistance, 0, 0), mask.AO);

            AddQuadIndices(_mesh.IndexBuffer1, vertexCount, mask.Normal, mask.AO);
            return 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int AddFloraQuad(VoxelRenderDef info, int vertexCount, VQuad verts, float4 ao)
        {
            int texIndex = info.TexUp;
            UVQuad uv = ComputeFaceUVs(new int3(1, 1, 0), new int2(1, 1));
            float3 normal = new(0, 1, 0);

            AddVertices(_mesh, verts, normal, uv, new float4(texIndex, -1, 0, 0), ao);

            NativeList<int> indexBuffer = _mesh.IndexBuffer1;
            EnsureIndexCapacity(indexBuffer, 6);
            indexBuffer.AddNoResize(vertexCount);
            indexBuffer.AddNoResize(vertexCount + 1);
            indexBuffer.AddNoResize(vertexCount + 2);
            indexBuffer.AddNoResize(vertexCount + 2);
            indexBuffer.AddNoResize(vertexCount + 1);
            indexBuffer.AddNoResize(vertexCount + 3);

            return 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private static void AddQuadIndices(NativeList<int> indexBuffer, int baseVertexIndex, sbyte normalSign, int4 ao)
        {
            // Choose diagonal based on AO to minimize artifacts
            EnsureIndexCapacity(indexBuffer, 6);
            if (ao[0] + ao[3] > ao[1] + ao[2])
            {
                indexBuffer.AddNoResize(baseVertexIndex);
                indexBuffer.AddNoResize(baseVertexIndex + 2 - normalSign);
                indexBuffer.AddNoResize(baseVertexIndex + 2 + normalSign);

                indexBuffer.AddNoResize(baseVertexIndex + 3);
                indexBuffer.AddNoResize(baseVertexIndex + 1 + normalSign);
                indexBuffer.AddNoResize(baseVertexIndex + 1 - normalSign);
            }
            else
            {
                indexBuffer.AddNoResize(baseVertexIndex + 1);
                indexBuffer.AddNoResize(baseVertexIndex + 1 + normalSign);
                indexBuffer.AddNoResize(baseVertexIndex + 1 - normalSign);

                indexBuffer.AddNoResize(baseVertexIndex + 2);
                indexBuffer.AddNoResize(baseVertexIndex + 2 - normalSign);
                indexBuffer.AddNoResize(baseVertexIndex + 2 + normalSign);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private static void AddVertices(MeshBuffer mesh, VQuad verts, float3 normal, UVQuad uv0, float4 uv1, float4 uv2)
        {
            Vertex vertex1 = new(verts.V1, normal, uv0.Uv1, uv1, uv2);
            Vertex vertex2 = new(verts.V2, normal, uv0.Uv2, uv1, uv2);
            Vertex vertex3 = new(verts.V3, normal, uv0.Uv3, uv1, uv2);
            Vertex vertex4 = new(verts.V4, normal, uv0.Uv4, uv1, uv2);

            EnsureVertexCapacity(mesh.VertexBuffer, 4);
            mesh.VertexBuffer.AddNoResize(vertex1);
            mesh.VertexBuffer.AddNoResize(vertex2);
            mesh.VertexBuffer.AddNoResize(vertex3);
            mesh.VertexBuffer.AddNoResize(vertex4);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private static void AddColliderVertices(MeshBuffer mesh, VQuad verts, float3 normal)
        {
            CVertex vertex1 = new(verts.V1, normal);
            CVertex vertex2 = new(verts.V2, normal);
            CVertex vertex3 = new(verts.V3, normal);
            CVertex vertex4 = new(verts.V4, normal);

            EnsureCVertexCapacity(mesh.CVertexBuffer, 4);
            mesh.CVertexBuffer.AddNoResize(vertex1);
            mesh.CVertexBuffer.AddNoResize(vertex2);
            mesh.CVertexBuffer.AddNoResize(vertex3);
            mesh.CVertexBuffer.AddNoResize(vertex4);
        }

        #endregion

        #region AO Calculation

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int4 ComputeAOMask(int3 coord, ref AxisInfo axInfo)
        {
            int3 l = coord;
            int3 r = coord;
            int3 b = coord;
            int3 T = coord;

            int3 lbc = coord;
            int3 rbc = coord;
            int3 ltc = coord;
            int3 rtc = coord;

            l[axInfo.VAxis] -= 1;
            r[axInfo.VAxis] += 1;
            b[axInfo.UAxis] -= 1;
            T[axInfo.UAxis] += 1;

            lbc[axInfo.UAxis] -= 1;
            lbc[axInfo.VAxis] -= 1;
            rbc[axInfo.UAxis] -= 1;
            rbc[axInfo.VAxis] += 1;
            ltc[axInfo.UAxis] += 1;
            ltc[axInfo.VAxis] -= 1;
            rtc[axInfo.UAxis] += 1;
            rtc[axInfo.VAxis] += 1;

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
        private struct AxisInfo
        {
            public int UAxis, VAxis, ULimit, VLimit;
        }

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
            internal readonly sbyte TopOpen;

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

        private struct SliceActivity
        {
            public bool HasSurface;
            public bool HasCollider;
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

        private static void EnsureVertexCapacity(NativeList<Vertex> list, int add)
        {
            int need = list.Length + add;
            if (need > list.Capacity)
            {
                int newCap = math.max(list.Capacity * 2, need);
                list.Capacity = newCap;
            }
        }

        private static void EnsureCVertexCapacity(NativeList<CVertex> list, int add)
        {
            int need = list.Length + add;
            if (need > list.Capacity)
            {
                int newCap = math.max(list.Capacity * 2, need);
                list.Capacity = newCap;
            }
        }

        private static void EnsureIndexCapacity(NativeList<int> list, int add)
        {
            int need = list.Length + add;
            if (need > list.Capacity)
            {
                int newCap = math.max(list.Capacity * 2, need);
                list.Capacity = newCap;
            }
        }

        #endregion
    }
}