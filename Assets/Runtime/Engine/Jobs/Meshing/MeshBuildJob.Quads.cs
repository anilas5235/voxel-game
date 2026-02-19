using System;
using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Runtime.Engine.Utils.Extensions.VectorConstants;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        private const float UVEdgeInset = 0.005f;
        private const float LiquidSurfaceLowering = 0.2f;

        #region Quad Creation

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void CreateQuad(ref PartitionJobData jobData, VoxelRenderDef info, Mask mask, int3 directionMask,
            int2 size, in VQuad verts
        )
        {
            switch (mask.MeshLayer)
            {
                case MeshLayer.Solid:
                    CreateSolidQuad(ref jobData, info, mask, directionMask, size, in verts);
                    break;
                case MeshLayer.Transparent:
                    CreateTransparentQuad(ref jobData, info, mask, directionMask, size, in verts);
                    break;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void CreateColliderQuad(ref PartitionJobData jobData, CMask mask, int3 directionMask, in VQuad verts)
        {
            float3 normal = directionMask * mask.Normal;

            AddColliderVertices(ref jobData.MeshBuffer, in verts, normal);

            // Use AO zeros for a deterministic diagonal, reuse existing helper for correct winding
            int4 ao = int4.zero;
            AddQuadIndices(ref jobData.MeshBuffer, SubMeshType.Collider, jobData.CollisionVertexCount, mask.Normal, ao);
            jobData.CollisionVertexCount += 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void CreateSolidQuad(ref PartitionJobData jobData, VoxelRenderDef info, Mask mask,
            int3 directionMask, int2 size, in VQuad verts
        )
        {
            int3 normal = directionMask * mask.Normal;

            int texIndex = info.GetTextureId(normal.ToDirection());

            Quad quad = new()
            {
                Positions = verts, 
                Normal = normal, 
                UV0 =  ComputeFaceUVs(normal, size), 
                UV1 = new float4(texIndex, 0, 0, mask.Sunlight), 
                AO = mask.AO, 
            };
            
            AddVertices(ref jobData.MeshBuffer, ref quad);

            AddQuadIndices(ref jobData.MeshBuffer, SubMeshType.Solid, jobData.RenderVertexCount, mask.Normal, mask.AO);
            jobData.RenderVertexCount += 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void CreateTransparentQuad(ref PartitionJobData jobData, VoxelRenderDef info, Mask mask,
            int3 directionMask,
            int2 size, in VQuad verts
        )
        {
            // Create a local copy because liquids may modify vertex positions.
            VQuad mutableVerts = verts;

            int3 normal = directionMask * mask.Normal;

            switch (info.VoxelType)
            {
                case VoxelType.Liquid:
                    // Lower the visible liquid surface
                    if (normal.y == 1)
                    {
                        mutableVerts.OffsetAll(new float3(0, -LiquidSurfaceLowering, 0));
                    }
                    // For vertical faces, if the top is exposed (no liquid above), lower only the top edge
                    else if (normal.y == 0 && mask.TopOpen == 1)
                    {
                        float topY = math.max(math.max(mutableVerts.V1.y, mutableVerts.V2.y),
                            math.max(mutableVerts.V3.y, mutableVerts.V4.y));
                        const float eps = 1e-4f;
                        if (math.abs(mutableVerts.V1.y - topY) < eps) mutableVerts.V1.y -= LiquidSurfaceLowering;
                        if (math.abs(mutableVerts.V2.y - topY) < eps) mutableVerts.V2.y -= LiquidSurfaceLowering;
                        if (math.abs(mutableVerts.V3.y - topY) < eps) mutableVerts.V3.y -= LiquidSurfaceLowering;
                        if (math.abs(mutableVerts.V4.y - topY) < eps) mutableVerts.V4.y -= LiquidSurfaceLowering;
                    }

                    break;
            }

            int texIndex = info.GetTextureId(normal.ToDirection());

            Quad quad = new()
            {
                Positions = mutableVerts, 
                Normal = normal, 
                UV0 =  ComputeFaceUVs(normal, size), 
                UV1 = new float4(texIndex, info.DepthFadeDistance, 0, mask.Sunlight), 
                AO = mask.AO, 
            };
            
            AddVertices(ref jobData.MeshBuffer, ref quad);

            AddQuadIndices(ref jobData.MeshBuffer, SubMeshType.Transparent, jobData.RenderVertexCount, mask.Normal,
                mask.AO);
            jobData.RenderVertexCount += 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void AddFloraQuad(ref PartitionJobData jobData, VoxelRenderDef info, in VQuad verts, float4 ao,
            byte sunLight)
        {
            int texIndex = info.TexUp;
            Quad quad = new()
            {
                Positions = verts, 
                Normal = Float3Up,
                UV0 = ComputeFaceUVs(new int3(1, 1, 0), new int2(1, 1)), 
                UV1 = new float4(texIndex, -1, 0, sunLight), 
                AO = ao,
            };
            AddVertices(ref jobData.MeshBuffer, ref quad);

            ref MeshBuffer meshBuffer = ref jobData.MeshBuffer;
            EnsureCapacity(meshBuffer.FoliageIndexBuffer, 6);

            int vCount = jobData.RenderVertexCount;

            meshBuffer.AddIndex(vCount, SubMeshType.Foliage);
            meshBuffer.AddIndex(vCount + 1, SubMeshType.Foliage);
            meshBuffer.AddIndex(vCount + 2, SubMeshType.Foliage);
            meshBuffer.AddIndex(vCount + 2, SubMeshType.Foliage);
            meshBuffer.AddIndex(vCount + 1, SubMeshType.Foliage);
            meshBuffer.AddIndex(vCount + 3, SubMeshType.Foliage);

            jobData.RenderVertexCount += 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void AddVertices(ref MeshBuffer mesh, ref Quad quad)
        {
            Vertex vertex0 = quad.GetVertex(0);
            Vertex vertex1 = quad.GetVertex(1);
            Vertex vertex2 = quad.GetVertex(2);
            Vertex vertex3 = quad.GetVertex(3);

            EnsureCapacity(mesh.VertexBuffer, 4);
            mesh.AddVertex(ref vertex0);
            mesh.AddVertex(ref vertex1);
            mesh.AddVertex(ref vertex2);
            mesh.AddVertex(ref vertex3);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void AddColliderVertices(ref MeshBuffer mesh, in VQuad verts, float3 normal)
        {
            CVertex vertex1 = new(verts.V1, normal);
            CVertex vertex2 = new(verts.V2, normal);
            CVertex vertex3 = new(verts.V3, normal);
            CVertex vertex4 = new(verts.V4, normal);

            EnsureCapacity(mesh.CVertexBuffer, 4);
            mesh.AddCVertex(vertex1);
            mesh.AddCVertex(vertex2);
            mesh.AddCVertex(vertex3);
            mesh.AddCVertex(vertex4);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private UVQuad ComputeFaceUVs(int3 normal, int2 size)
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
        private void AddQuadIndices(ref MeshBuffer meshBuffer, SubMeshType subMeshType, int baseVertexIndex,
            sbyte normalSign, int4 ao)
        {
            // Choose diagonal based on AO to minimize artifacts
            NativeList<ushort> indexBuffer = subMeshType switch
            {
                SubMeshType.Solid => meshBuffer.SolidIndexBuffer,
                SubMeshType.Transparent => meshBuffer.TransparentIndexBuffer,
                SubMeshType.Foliage => meshBuffer.FoliageIndexBuffer,
                SubMeshType.Collider => meshBuffer.CIndexBuffer,
                _ => throw new ArgumentOutOfRangeException(nameof(subMeshType), subMeshType, null)
            };

            EnsureCapacity(indexBuffer, 6);

            if (ao[0] + ao[3] > ao[1] + ao[2])
            {
                meshBuffer.AddIndex(baseVertexIndex, subMeshType);
                meshBuffer.AddIndex(baseVertexIndex + 2 - normalSign, subMeshType);
                meshBuffer.AddIndex(baseVertexIndex + 2 + normalSign, subMeshType);

                meshBuffer.AddIndex(baseVertexIndex + 3, subMeshType);
                meshBuffer.AddIndex(baseVertexIndex + 1 + normalSign, subMeshType);
                meshBuffer.AddIndex(baseVertexIndex + 1 - normalSign, subMeshType);
            }
            else
            {
                meshBuffer.AddIndex(baseVertexIndex + 1, subMeshType);
                meshBuffer.AddIndex(baseVertexIndex + 1 + normalSign, subMeshType);
                meshBuffer.AddIndex(baseVertexIndex + 1 - normalSign, subMeshType);

                meshBuffer.AddIndex(baseVertexIndex + 2, subMeshType);
                meshBuffer.AddIndex(baseVertexIndex + 2 - normalSign, subMeshType);
                meshBuffer.AddIndex(baseVertexIndex + 2 + normalSign, subMeshType);
            }
        }

        #endregion
    }
}