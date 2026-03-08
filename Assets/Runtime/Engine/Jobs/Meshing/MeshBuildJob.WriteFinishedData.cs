using Runtime.Engine.Utils.Extensions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        private static readonly Bounds Bounds = new((PartitionSize / (int3)2).GetVector3(), PartitionSize.GetVector3());

        private void WriteResults(int index, ref PartitionJobData jobData)
        {
            var result = new PartitionJobResult
            {
                Index = index,
                PartitionPos = jobData.PartitionPos,
                MeshBounds = Bounds,
                ColliderBounds = Bounds,
            };

            FillRenderMeshData(ref result, ref jobData);

            FillColliderMeshData(in jobData);

            Results.TryAdd(
                jobData.PartitionPos,
                result
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

        private void FillRenderMeshData(ref PartitionJobResult result, ref PartitionJobData jobData)
        {
            result.MeshVertices = new UnsafeList<Vertex>(
                jobData.MeshBuffer.VertexBuffer.Length,
                Allocator.Persistent
            );
            result.MeshVertices.CopyFrom(jobData.MeshBuffer.VertexBuffer.AsArray());
            result.SolidVertexCount = jobData.MeshBuffer.SolidVertexCount;
            result.TransparentVertexCount = jobData.MeshBuffer.TransparentVertexCount;
            result.FoliageVertexCount = jobData.MeshBuffer.FoliageVertexCount;
        }
    }
}