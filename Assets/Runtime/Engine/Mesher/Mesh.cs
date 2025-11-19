using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Mesher
{
    [BurstCompile]
    public struct Vertex
    {
        public float3 Position;
        public float3 Normal;
        public float4 UV0;
        public float4 UV1;
        public float4 UV2;

        public Vertex(float3 position, float3 normal, float4 uv0, float4 uv1, float4 uv2)
        {
            Position = position;
            Normal = normal;
            UV0 = uv0;
            UV1 = uv1;
            UV2 = uv2;
        }
    }

    [BurstCompile]
    public struct CVertex
    {
        public float3 Position;
        public float3 Normal;

        public CVertex(float3 position, float3 normal)
        {
            Position = position;
            Normal = normal;
        }
    }

    [BurstCompile]
    internal struct MeshBuffer
    {
        public NativeList<Vertex> VertexBuffer;
        public NativeList<int> IndexBuffer0;
        public NativeList<int> IndexBuffer1;
        public NativeList<CVertex> CVertexBuffer;
        public NativeList<int> CIndexBuffer;

        internal void Dispose()
        {
            VertexBuffer.Dispose();
            IndexBuffer0.Dispose();
            IndexBuffer1.Dispose();
            CVertexBuffer.Dispose();
            CIndexBuffer.Dispose();
        }
    }
}