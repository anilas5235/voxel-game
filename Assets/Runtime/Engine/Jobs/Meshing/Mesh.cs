using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Meshing
{
    [BurstCompile, StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex
    {
        public float3 Position;
        public ushort QuadIndex;
        public ushort padding;
        public ushort TextureIndex;
        private byte LightDataAndAO;
        public byte padding1;
        public uint padding2;
        public uint padding3;

        public Vertex(float3 position, ushort quadIndex, ushort textureIndex, byte light, byte ao)
        {
            Position = position;
            QuadIndex = quadIndex;
            TextureIndex = textureIndex;
            LightDataAndAO = 0;
            padding = 0;
            padding1 = 0;
            padding2 = 0;
            padding3 = 0;

            SetLight(light);
            SetAO(ao);
        }


        public void SetLight(byte sunlight) => LightDataAndAO = (byte)(LightDataAndAO | (sunlight & 0b1111));

        public void SetAO(byte ao) => LightDataAndAO = (byte)(LightDataAndAO | (ao << 4));
    }

    /// <summary>
    /// Compact vertex structure for collider mesh (position + normal).
    /// </summary>
    [BurstCompile, StructLayout(LayoutKind.Sequential, Pack = 1)]
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

        public NativeList<CVertex> CVertexBuffer;
        public NativeList<ushort> CIndexBuffer;

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
            EnsureCapacity(VertexBuffer, 1);
            VertexBuffer.AddNoResize(vertex);
        }

        public void AddIndex(int index, SubMeshType subMeshType)
        {
            switch (subMeshType)
            {
                case SubMeshType.Solid:
                    EnsureCapacity(SolidIndexBuffer, 1);
                    SolidIndexBuffer.AddNoResize((ushort)index);
                    break;
                case SubMeshType.Transparent:
                    EnsureCapacity(TransparentIndexBuffer, 1);
                    TransparentIndexBuffer.AddNoResize((ushort)index);
                    break;
                case SubMeshType.Foliage:
                    EnsureCapacity(FoliageIndexBuffer, 1);
                    FoliageIndexBuffer.AddNoResize((ushort)index);
                    break;
                case SubMeshType.Collider:
                    EnsureCapacity(CIndexBuffer, 1);
                    CIndexBuffer.AddNoResize((ushort)index);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(subMeshType), subMeshType, null);
            }
        }

        public void AddCVertex(CVertex vertex)
        {
            EnsureCapacity(CVertexBuffer, 1);
            CVertexBuffer.AddNoResize(vertex);
        }
        
        private void EnsureCapacity<T>(NativeList<T> list, int add) where T : unmanaged
        {
            int need = list.Length + add;
            if (need <= list.Capacity) return;
            int newCap = math.max(list.Capacity * 2, need);
            list.Capacity = newCap;
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