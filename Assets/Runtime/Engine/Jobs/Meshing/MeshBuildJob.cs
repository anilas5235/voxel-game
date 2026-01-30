using Runtime.Engine.Data;
using Runtime.Engine.Mesher;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Runtime.Engine.Jobs.Meshing
{
    /// <summary>
    /// Burst-compiled parallel job that generates render and collider mesh data for a list of chunk positions
    /// using the greedy mesher and writes the results into provided <see cref="UnityEngine.Mesh.MeshDataArray"/>
    /// instances while recording the position-to-index mapping.
    /// </summary>
    [BurstCompile]
    internal struct MeshBuildJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<VertexAttributeDescriptor> VertexParams;
        [ReadOnly] public NativeArray<VertexAttributeDescriptor> ColliderVertexParams;
        [ReadOnly] public ChunkAccessor Accessor;
        [ReadOnly] public NativeList<int3> Jobs;
        [WriteOnly] public NativeParallelHashMap<int3, int>.ParallelWriter Results;
        [ReadOnly] public VoxelEngineRenderGenData VoxelEngineRenderGenData;
        public UnityEngine.Mesh.MeshDataArray MeshDataArray;
        public UnityEngine.Mesh.MeshDataArray ColliderMeshDataArray;

        /// <summary>
        /// Executes mesh generation for the given job index and fills mesh and collider submesh data
        /// for the corresponding chunk position.
        /// </summary>
        /// <param name="index">Index of the chunk position to process within the <see cref="Jobs"/> list.</param>
        public void Execute(int index)
        {
            UnityEngine.Mesh.MeshData mesh = MeshDataArray[index];
            UnityEngine.Mesh.MeshData colliderMesh = ColliderMeshDataArray[index];
            int3 partitionPos = Jobs[index];

            GreedyMesher greedyMesher = new(Accessor, partitionPos, VoxelEngineRenderGenData);
            MeshBuffer meshBuffer = greedyMesher.GenerateMesh();

            // Render mesh
            int vertexCount = meshBuffer.VertexBuffer.Length;
            mesh.SetVertexBufferParams(vertexCount, VertexParams);
            mesh.GetVertexData<Vertex>().CopyFrom(meshBuffer.VertexBuffer.AsArray());

            int index0Count = meshBuffer.IndexBuffer0.Length;
            int index1Count = meshBuffer.IndexBuffer1.Length;
            mesh.SetIndexBufferParams(index0Count + index1Count, IndexFormat.UInt32);
            NativeArray<int> indexBuffer = mesh.GetIndexData<int>();
            NativeArray<int>.Copy(meshBuffer.IndexBuffer0.AsArray(), 0, indexBuffer, 0, index0Count);
            if (index1Count > 1)
                NativeArray<int>.Copy(meshBuffer.IndexBuffer1.AsArray(), 0, indexBuffer, index0Count, index1Count);
            mesh.subMeshCount = 2;
            SubMeshDescriptor descriptor0 = new(0, index0Count);
            SubMeshDescriptor descriptor1 = new(index0Count, index1Count);
            const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices |
                                          MeshUpdateFlags.DontResetBoneBounds;
            mesh.SetSubMesh(0, descriptor0, flags);
            mesh.SetSubMesh(1, descriptor1, flags);

            // Collider mesh
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
            colliderMesh.SetSubMesh(0, cDesc, flags);

            Results.TryAdd(partitionPos, index);

            meshBuffer.Dispose();
        }
    }
}