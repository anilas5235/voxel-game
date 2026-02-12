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
            int2 size,
            in VQuad verts
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
            AddQuadIndices(ref jobData.MeshBuffer.CIndexBuffer, jobData.CollisionVertexCount, mask.Normal, ao);
            jobData.CollisionVertexCount += 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void CreateSolidQuad(ref PartitionJobData jobData, VoxelRenderDef info, Mask mask,
            int3 directionMask, int2 size, in VQuad verts
        )
        {
            int3 normal = directionMask * mask.Normal;

            int texIndex = info.GetTextureId(normal.ToDirection());
            UVQuad uv = ComputeFaceUVs(normal, size);

            float4 uv1 = new(texIndex, 0, 0, 0);
            float3 n = normal;
            float4 ao = mask.AO;
            AddVertices(ref jobData.MeshBuffer, in verts, n, in uv, uv1, ao);

            AddQuadIndices(ref jobData.MeshBuffer.SolidIndexBuffer, jobData.RenderVertexCount, mask.Normal, mask.AO);
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
            UVQuad uv = ComputeFaceUVs(normal, size);

            float4 uv1 = new(texIndex, info.DepthFadeDistance, 0, 0);
            float3 n = normal;
            float4 ao = mask.AO;
            AddVertices(ref jobData.MeshBuffer, in mutableVerts, n, in uv, uv1, ao);

            AddQuadIndices(ref jobData.MeshBuffer.TransparentIndexBuffer, jobData.RenderVertexCount, mask.Normal, mask.AO);
            jobData.RenderVertexCount += 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void AddFloraQuad(ref PartitionJobData jobData, VoxelRenderDef info, in VQuad verts, float4 ao)
        {
            int texIndex = info.TexUp;
            UVQuad uv = ComputeFaceUVs(new int3(1, 1, 0), new int2(1, 1));
            float4 uv1 = new(texIndex, -1, 0, 0);
            AddVertices(ref jobData.MeshBuffer, in verts, Float3Up, in uv, uv1, ao);

            NativeList<ushort> indexBuffer = jobData.MeshBuffer.FoliageIndexBuffer;
            EnsureCapacity(indexBuffer, 6);

            int vCount = jobData.RenderVertexCount;
            indexBuffer.AddNoResize((ushort)(vCount));
            indexBuffer.AddNoResize((ushort)(vCount + 1));
            indexBuffer.AddNoResize((ushort)(vCount + 2));
            indexBuffer.AddNoResize((ushort)(vCount + 2));
            indexBuffer.AddNoResize((ushort)(vCount + 1));
            indexBuffer.AddNoResize((ushort)(vCount + 3));

            jobData.RenderVertexCount += 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void AddVertices(ref MeshBuffer mesh, in VQuad verts, float3 normal, in UVQuad uv0,
            float4 uv1, float4 uv2)
        {
            Vertex vertex1 = new(verts.V1, normal, uv0.Uv1, uv1, uv2);
            Vertex vertex2 = new(verts.V2, normal, uv0.Uv2, uv1, uv2);
            Vertex vertex3 = new(verts.V3, normal, uv0.Uv3, uv1, uv2);
            Vertex vertex4 = new(verts.V4, normal, uv0.Uv4, uv1, uv2);

            EnsureCapacity(mesh.VertexBuffer, 4);
            mesh.AddVertex(ref vertex1);
            mesh.AddVertex(ref vertex2);
            mesh.AddVertex(ref vertex3);
            mesh.AddVertex(ref vertex4);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void AddColliderVertices(ref MeshBuffer mesh , in VQuad verts, float3 normal)
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
        private void AddQuadIndices(ref NativeList<ushort> indexBuffer, int baseVertexIndex, sbyte normalSign, int4 ao)
        {
            // Choose diagonal based on AO to minimize artifacts
            EnsureCapacity(indexBuffer, 6);
            if (ao[0] + ao[3] > ao[1] + ao[2])
            {
                indexBuffer.AddNoResize((ushort)(baseVertexIndex));
                indexBuffer.AddNoResize((ushort)(baseVertexIndex + 2 - normalSign));
                indexBuffer.AddNoResize((ushort)(baseVertexIndex + 2 + normalSign));

                indexBuffer.AddNoResize((ushort)(baseVertexIndex + 3));
                indexBuffer.AddNoResize((ushort)(baseVertexIndex + 1 + normalSign));
                indexBuffer.AddNoResize((ushort)(baseVertexIndex + 1 - normalSign));
            }
            else
            {
                indexBuffer.AddNoResize((ushort)(baseVertexIndex + 1));
                indexBuffer.AddNoResize((ushort)(baseVertexIndex + 1 + normalSign));
                indexBuffer.AddNoResize((ushort)(baseVertexIndex + 1 - normalSign));

                indexBuffer.AddNoResize((ushort)(baseVertexIndex + 2));
                indexBuffer.AddNoResize((ushort)(baseVertexIndex + 2 - normalSign));
                indexBuffer.AddNoResize((ushort)(baseVertexIndex + 2 + normalSign));
            }
        }

        #endregion
    }
}