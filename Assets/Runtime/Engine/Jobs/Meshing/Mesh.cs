using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Meshing
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct QuadData
    {
        public float3 position00;
        public float3 position01;
        public float3 position02;
        public float3 position03;
        public float3 normal;
        public float2 uv00;
        public float2 uv01;
        public float2 uv02;
        public float2 uv03;
    };

    [BurstCompile, StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex
    {
        public float3 Position;
        private uint4 PackedData;

        public Vertex(float3 position, ushort quadIndex, ushort textureIndex)
        {
            Position = position;
            PackedData = uint4.zero;

            SetQuadIndex(quadIndex);
            SetTextureIndex(textureIndex);
        }

        public enum LightIndex
        {
            SunUL = 0,
            SunUR = 4,
            SunDR = 8,
            SunDL = 12,
            ArtificialUL = 16,
            ArtificialUR = 20,
            ArtificialDR = 24,
            ArtificialDL = 28,
        }

        public void SetQuadIndex(ushort quadIndex) => PackedData.x |= quadIndex;

        public void SetTextureIndex(ushort textureIndex) => PackedData.x |= (uint)textureIndex << 16;

        public void SetLight(byte sunlight, LightIndex index) =>
            PackedData.y |= (uint)(sunlight & 0b1111) << (int)index;

        public void SetAO(byte ao) => PackedData.z |= ao;

        public void SetDepthFade(half depthFade) => PackedData.z |= (uint)depthFade.value << 8;

        public void SetGlow(byte glow) => PackedData.z |= (uint)glow << 24;
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
        public uint SolidVertexCount;
        public uint TransparentVertexCount;
        public uint FoliageVertexCount;

        public NativeList<CVertex> CVertexBuffer;
        public NativeList<ushort> CIndexBuffer;

        /// <summary>
        /// Disposes all native lists.
        /// </summary>
        internal void Dispose()
        {
            VertexBuffer.Dispose();
            CVertexBuffer.Dispose();
            CIndexBuffer.Dispose();
        }

        public void AddVertex(ref Vertex vertex, SubMeshType type)
        {
            EnsureCapacity(VertexBuffer, 1);
            VertexBuffer.AddNoResize(vertex);
            switch (type)
            {
                case SubMeshType.Solid:
                    SolidVertexCount++;
                    break;
                case SubMeshType.Transparent:
                    TransparentVertexCount++;
                    break;
                case SubMeshType.Foliage:
                    FoliageVertexCount++;
                    break;
            }
        }

        public void AddCIndex(int index)
        {
            EnsureCapacity(CIndexBuffer, 1);
            CIndexBuffer.AddNoResize((ushort)index);
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
    }
}