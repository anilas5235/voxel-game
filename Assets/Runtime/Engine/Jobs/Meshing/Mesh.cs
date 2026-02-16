using System;
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
        public half4 Position; // xyz = position, w = 0 (unused could hold extra data)
        public half4 Normal; // xyz = normal, w = 0 (unused could hold extra data)
        public half4 UV0; // xy = Sized UV for texture atlas, zw = normalized UV for shader sampling
        public half4 UV1; // x = texture ID, y = depth fade factor, z = unused, w = sunlight level
        public half4 AO; // xyzw = AO values 

        /// <summary>
        /// Creates a vertex with all attributes.
        /// </summary>
        public Vertex(float3 position, float3 normal, float4 uv0, float4 uv1, float4 ao)
        {
            Position = new half4((half3)position, half.zero);
            Normal = new half4((half3)normal, half.zero);
            UV0 = (half4)uv0;
            UV1 = (half4)uv1;
            AO = (half4)ao;
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
        public NativeList<ushort> SolidIndexBuffer;
        public NativeList<ushort> TransparentIndexBuffer;
        public NativeList<ushort> FoliageIndexBuffer;
        private float3 _minMBounds;
        private float3 _maxMBounds;

        public NativeList<CVertex> CVertexBuffer;
        public NativeList<ushort> CIndexBuffer;
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

        public void AddIndex(int index, SubMeshType subMeshType)
        {
            switch (subMeshType)
            {
                case SubMeshType.Solid:
                    SolidIndexBuffer.AddNoResize((ushort)index);
                    break;
                case SubMeshType.Transparent:
                    TransparentIndexBuffer.AddNoResize((ushort)index);
                    break;
                case SubMeshType.Foliage:
                    FoliageIndexBuffer.AddNoResize((ushort)index);
                    break;
                case SubMeshType.Collider:
                    CIndexBuffer.AddNoResize((ushort)index);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(subMeshType), subMeshType, null);
            }
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
    
    internal enum SubMeshType : byte
    {
        Solid = 0,
        Transparent = 1,
        Foliage = 2,
        Collider = 3
    }
}