using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Meshing
{
    /// <summary>
    /// Vertex structure for rendered voxels (position, normal, 3 UV layers for texture atlas / AO / extra data).
    /// </summary>
    [BurstCompile]
    public struct Vertex
    {
        public float3 Position;
        public float3 Normal;
        public float4 UV0;
        public float4 UV1;
        public float4 UV2;

        /// <summary>
        /// Creates a vertex with all attributes.
        /// </summary>
        public Vertex(float3 position, float3 normal, float4 uv0, float4 uv1, float4 uv2)
        {
            Position = position;
            Normal = normal;
            UV0 = uv0;
            UV1 = uv1;
            UV2 = uv2;
        }
    }

    /// <summary>
    /// Compact vertex structure for collider mesh (position + normal only).
    /// </summary>
    [BurstCompile]
    public struct CVertex
    {
        public float3 Position;
        public float3 Normal;

        /// <summary>
        /// Creates a collider vertex.
        /// </summary>
        public CVertex(float3 position, float3 normal)
        {
            Position = position;
            Normal = normal;
        }
    }

    /// <summary>
    /// Buffer collection for mesh & collider building (native lists). Internal data container for mesher.
    /// </summary>
    [BurstCompile]
    internal struct MeshBuffer
    {
        public NativeList<Vertex> VertexBuffer;
        public NativeList<int> SolidIndexBuffer;
        public NativeList<int> TransparentIndexBuffer;
        public NativeList<int> FoliageIndexBuffer;
        
        public NativeList<CVertex> CVertexBuffer;
        public NativeList<int> CIndexBuffer;

        /// <summary>
        /// Disposes all native lists.
        /// </summary>
        internal void Dispose()
        {
            VertexBuffer.Dispose();
            SolidIndexBuffer.Dispose();
            TransparentIndexBuffer.Dispose();
            FoliageIndexBuffer.Dispose();
            CVertexBuffer.Dispose();
            CIndexBuffer.Dispose();
        }
    }
}