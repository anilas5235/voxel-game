using Runtime.Engine.Utils;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        private void WriteResults(int index, ref PartitionJobData jobData)
        {
            FillRenderMeshData(in jobData);

            FillColliderMeshData(in jobData);

            jobData.MeshBuffer.GetMeshBounds(out Bounds mBounds);
            
            jobData.MeshBuffer.GetColliderBounds(out Bounds cBounds);

            Results.TryAdd(
                jobData.PartitionPos,
                new PartitionJobResult
                {
                    Index = index,
                    PartitionPos = jobData.PartitionPos,
                    MeshBounds = mBounds,
                    ColliderBounds = cBounds
                }
            );
        }
        
        private void FillColliderMeshData(in PartitionJobData jobData)
        {
            MeshBuffer meshBuffer = jobData.MeshBuffer;
            Mesh.MeshData colliderMesh = jobData.ColliderMesh;

            int cVertexCount = meshBuffer.CVertexBuffer.Length;
            colliderMesh.SetVertexBufferParams(cVertexCount, ColliderVertexParams);
            colliderMesh.GetVertexData<CVertex>().CopyFrom(meshBuffer.CVertexBuffer.AsArray());

            int cIndexCount = meshBuffer.CIndexBuffer.Length;
            colliderMesh.SetIndexBufferParams(cIndexCount, IndexFormat.UInt32);
            NativeArray<int> cIndexBuffer = colliderMesh.GetIndexData<int>();
            if (cIndexCount > 0)
                NativeArray<int>.Copy(meshBuffer.CIndexBuffer.AsArray(), 0, cIndexBuffer, 0, cIndexCount);

            colliderMesh.subMeshCount = 1;
            SubMeshDescriptor cDesc = new(0, cIndexCount);
            colliderMesh.SetSubMesh(0, cDesc, VoxelConstants.MeshFlags);
        }

        private void FillRenderMeshData(in PartitionJobData jobData)
        {
            MeshBuffer meshBuffer = jobData.MeshBuffer;
            Mesh.MeshData mesh = jobData.Mesh;

            int vertexCount = meshBuffer.VertexBuffer.Length;
            mesh.SetVertexBufferParams(vertexCount, VertexParams);
            mesh.GetVertexData<Vertex>().CopyFrom(meshBuffer.VertexBuffer.AsArray());

            int solidIndexes = meshBuffer.SolidIndexBuffer.Length;
            int transparentIndexes = meshBuffer.TransparentIndexBuffer.Length;
            int foliageIndexes = meshBuffer.FoliageIndexBuffer.Length;

            mesh.SetIndexBufferParams(solidIndexes + transparentIndexes + foliageIndexes, IndexFormat.UInt32);
            NativeArray<int> indexBuffer = mesh.GetIndexData<int>();
            NativeArray<int>.Copy(meshBuffer.SolidIndexBuffer.AsArray(), 0, indexBuffer, 0, solidIndexes);
            if (transparentIndexes > 1)
            {
                NativeArray<int>.Copy(meshBuffer.TransparentIndexBuffer.AsArray(), 0, indexBuffer, solidIndexes,
                    transparentIndexes);
            }

            if (foliageIndexes > 1)
            {
                NativeArray<int>.Copy(meshBuffer.FoliageIndexBuffer.AsArray(), 0, indexBuffer,
                    solidIndexes + transparentIndexes,
                    foliageIndexes);
            }

            mesh.subMeshCount = 3;
            SubMeshDescriptor descriptor0 = new(0, solidIndexes);
            SubMeshDescriptor descriptor1 = new(solidIndexes, transparentIndexes);
            SubMeshDescriptor descriptor2 = new(solidIndexes + transparentIndexes, foliageIndexes);

            mesh.SetSubMesh(0, descriptor0, VoxelConstants.MeshFlags);
            mesh.SetSubMesh(1, descriptor1, VoxelConstants.MeshFlags);
            mesh.SetSubMesh(2, descriptor2, VoxelConstants.MeshFlags);
        }
    }
}