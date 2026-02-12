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
        public half4 Normal;
        public half4 UV0;
        public float4 UV1;
        public float4 AO;

        /// <summary>
        /// Creates a vertex with all attributes.
        /// </summary>
        public Vertex(float3 position, float3 normal, float4 uv0, float4 uv1, float4 ao)
        {
            Position = new half4((half3)position, half.zero);
            Normal = new half4((half3)normal, half.zero);
            UV0 = (half4)uv0;
            UV1 = uv1;
            AO = ao;
        }

        internal float3 GetPosition() => new(Position.x, Position.y, Position.z);
    }

    /// <summary>
    /// Compact vertex structure for collider mesh (position + normal).
    /// </summary>
    [BurstCompile]
    public struct CVertex
    {
        public half4 Position;
        public half4 Normal;

        /// <summary>
        /// Creates a collider vertex.
        /// </summary>
        public CVertex(float3 position, float3 normal)
        {
            Position = new half4((half3)position, half.zero);
            Normal = new half4((half3)normal, half.zero);
        }

        internal float3 GetPosition() => new(Position.x, Position.y, Position.z);
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
        private float3 _minMBounds;
        private float3 _maxMBounds;

        public NativeList<CVertex> CVertexBuffer;
        public NativeList<int> CIndexBuffer;
        private float3 _minCBounds;
        private float3 _maxCBounds;

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
            _minMBounds = math.min(_minMBounds, pos);
            _maxMBounds = math.max(_maxMBounds, pos);
        }

        public void GetMeshBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bounds.SetMinMax(_minMBounds, _maxMBounds);
        }

        public void AddCVertex(CVertex vertex)
        {
            CVertexBuffer.AddNoResize(vertex);
            float3 pos = vertex.GetPosition();
            _minCBounds = math.min(_minCBounds, pos);
            _maxCBounds = math.max(_maxCBounds, pos);
        }

        public void GetColliderBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bounds.SetMinMax(_minCBounds, _maxCBounds);
        }
    }
}