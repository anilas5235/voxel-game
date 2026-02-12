using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.Engine.Jobs.Meshing
{
    /// <summary>
    /// Vertex structure for rendered voxels (position, normal, 3 UV layers for texture atlas / AO / extra data).
    /// </summary>
    [BurstCompile]
    public struct Vertex
    {
        public half4 Position;
        public float3 Normal;
        public half4 UV0;
        public float4 UV1;
        public float4 AO;

        /// <summary>
        /// Creates a vertex with all attributes.
        /// </summary>
        public Vertex(float3 position, float3 normal, float4 uv0, float4 uv1, float4 ao)
        {
            Position = new half4((half3)position,(half) 1f);
            Normal = normal;
            UV0 = (half4)uv0;
            UV1 = uv1;
            AO = ao;
        }
        
        internal float3 GetPosition() => new(Position.x, Position.y, Position.z);
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
        public float3 MinBounds;
        public float3 MaxBounds;
        
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
        
        public void AddVertex(ref Vertex vertex)
        {
            VertexBuffer.AddNoResize(vertex);
            float3 pos = vertex.GetPosition();
            MinBounds = math.min(MinBounds, pos);
            MaxBounds = math.max(MaxBounds, pos);
        }

        public void GetMeshBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bounds.SetMinMax(MinBounds, MaxBounds);
        }
    }
}