using Runtime.Engine.Utils.Extensions;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        private static readonly  Bounds Bounds = new((PartitionSize / (int3)2).GetVector3(), PartitionSize.GetVector3());
        private void WriteResults(int index, ref PartitionJobData jobData)
        {
            FillRenderMeshData(in jobData);

            FillColliderMeshData(in jobData);

            Results.TryAdd(
                jobData.PartitionPos,
                new PartitionJobResult
                {
                    Index = index,
                    PartitionPos = jobData.PartitionPos,
                    MeshBounds = Bounds,
                    ColliderBounds = Bounds
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
            colliderMesh.SetIndexBufferParams(cIndexCount, IndexFormat.UInt16);
            NativeArray<ushort> cIndexBuffer = colliderMesh.GetIndexData<ushort>();
            if (cIndexCount > 0)
                NativeArray<ushort>.Copy(meshBuffer.CIndexBuffer.AsArray(), 0, cIndexBuffer, 0, cIndexCount);

            colliderMesh.subMeshCount = 1;
            SubMeshDescriptor cDesc = new(0, cIndexCount);
            colliderMesh.SetSubMesh(0, cDesc, MeshFlags);
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

            mesh.SetIndexBufferParams(solidIndexes + transparentIndexes + foliageIndexes, IndexFormat.UInt16);
            NativeArray<ushort> indexBuffer = mesh.GetIndexData<ushort>();
            NativeArray<ushort>.Copy(meshBuffer.SolidIndexBuffer.AsArray(), 0, indexBuffer, 0, solidIndexes);
            if (transparentIndexes > 1)
            {
                NativeArray<ushort>.Copy(meshBuffer.TransparentIndexBuffer.AsArray(), 0, indexBuffer, solidIndexes,
                    transparentIndexes);
            }

            if (foliageIndexes > 1)
            {
                NativeArray<ushort>.Copy(meshBuffer.FoliageIndexBuffer.AsArray(), 0, indexBuffer,
                    solidIndexes + transparentIndexes,
                    foliageIndexes);
            }

            mesh.subMeshCount = 3;
            SubMeshDescriptor descriptor0 = new(0, solidIndexes);
            SubMeshDescriptor descriptor1 = new(solidIndexes, transparentIndexes);
            SubMeshDescriptor descriptor2 = new(solidIndexes + transparentIndexes, foliageIndexes);

            mesh.SetSubMesh(0, descriptor0, MeshFlags);
            mesh.SetSubMesh(1, descriptor1, MeshFlags);
            mesh.SetSubMesh(2, descriptor2, MeshFlags);
        }
    }
}