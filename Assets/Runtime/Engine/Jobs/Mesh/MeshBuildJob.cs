using Runtime.Engine.Data;
using Runtime.Engine.Mesher;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Runtime.Engine.Jobs.Mesh {

    [BurstCompile]
    internal struct MeshBuildJob : IJobParallelFor {

        [ReadOnly] public int3 ChunkSize;
        [ReadOnly] public NativeArray<VertexAttributeDescriptor> VertexParams;

        [ReadOnly] public ChunkAccessor Accessor;
        [ReadOnly] public NativeList<int3> Jobs;

        [WriteOnly] public NativeParallelHashMap<int3, int>.ParallelWriter Results;

        public UnityEngine.Mesh.MeshDataArray MeshDataArray;

        public void Execute(int index) {
            UnityEngine.Mesh.MeshData mesh = MeshDataArray[index];
            int3 position = Jobs[index];

            MeshBuffer meshBuffer = GreedyMesher.GenerateMesh(Accessor, position, ChunkSize);
            
            // Vertex Buffer
            int vertexCount = meshBuffer.VertexBuffer.Length;

            mesh.SetVertexBufferParams(vertexCount, VertexParams);
            mesh.GetVertexData<Vertex>().CopyFrom(meshBuffer.VertexBuffer.AsArray());

            // Index Buffer
            int index0Count = meshBuffer.IndexBuffer0.Length;
            int index1Count = meshBuffer.IndexBuffer1.Length;
            
            mesh.SetIndexBufferParams(index0Count + index1Count, IndexFormat.UInt32);

            NativeArray<int> indexBuffer = mesh.GetIndexData<int>();
            
            NativeArray<int>.Copy(meshBuffer.IndexBuffer0.AsArray(), 0, indexBuffer, 0, index0Count);
            if (index1Count > 1)
                NativeArray<int>.Copy(meshBuffer.IndexBuffer1.AsArray(), 0, indexBuffer, index0Count, index1Count);

            // Sub Mesh
            mesh.subMeshCount = 2;
            
            SubMeshDescriptor descriptor0 = new SubMeshDescriptor(0, index0Count);
            SubMeshDescriptor descriptor1 = new SubMeshDescriptor(index0Count, index1Count);
            
            mesh.SetSubMesh(0, descriptor0, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetSubMesh(1, descriptor1, MeshUpdateFlags.DontRecalculateBounds);
            
            Results.TryAdd(position, index);

            meshBuffer.Dispose();
        }

    }

}