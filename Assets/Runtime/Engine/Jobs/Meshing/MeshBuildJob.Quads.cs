using Unity.Burst;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        #region Quad Creation

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void AddVertex(ref PartitionJobData jobData, SubMeshType subMeshType, ref Vertex v)
        {
            jobData.MeshBuffer.AddVertex(ref v);
            jobData.MeshBuffer.AddIndex(jobData.RenderVertexCount, subMeshType);
            jobData.RenderVertexCount++;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void CreateColliderQuad(ref PartitionJobData jobData, CMask mask, int3 directionMask, in VQuad verts)
        {
            float3 normal = directionMask * mask.Normal;

            AddColliderVertices(ref jobData.MeshBuffer, in verts, normal);

            // Use AO zeros for a deterministic diagonal, reuse existing helper for correct winding
            int4 ao = int4.zero;

            int baseVertexIndex = jobData.CollisionVertexCount;
            const SubMeshType subMeshType = SubMeshType.Collider;
            ref MeshBuffer meshBuffer = ref jobData.MeshBuffer;

            meshBuffer.AddIndex(baseVertexIndex + 1, subMeshType);
            meshBuffer.AddIndex(baseVertexIndex + 1 + mask.Normal, subMeshType);
            meshBuffer.AddIndex(baseVertexIndex + 1 - mask.Normal, subMeshType);

            meshBuffer.AddIndex(baseVertexIndex + 2, subMeshType);
            meshBuffer.AddIndex(baseVertexIndex + 2 - mask.Normal, subMeshType);
            meshBuffer.AddIndex(baseVertexIndex + 2 + mask.Normal, subMeshType);

            jobData.CollisionVertexCount += 4;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void AddColliderVertices(ref MeshBuffer mesh, in VQuad verts, float3 normal)
        {
            CVertex vertex1 = new(verts.V1, normal);
            CVertex vertex2 = new(verts.V2, normal);
            CVertex vertex3 = new(verts.V3, normal);
            CVertex vertex4 = new(verts.V4, normal);

            mesh.AddCVertex(vertex1);
            mesh.AddCVertex(vertex2);
            mesh.AddCVertex(vertex3);
            mesh.AddCVertex(vertex4);
        }

        #endregion
    }
}