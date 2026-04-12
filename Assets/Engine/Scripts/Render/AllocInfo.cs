using Unity.Mathematics;

namespace Engine.Scripts.Render
{
    public readonly struct AllocInfo
    {
        public readonly int BufferIndex;
        public readonly int PageIndex;
        public readonly int PointCount;

        public AllocInfo(int bufferIndex, int pageIndex, int pointCount)
        {
            BufferIndex = bufferIndex;
            PageIndex = pageIndex;
            PointCount = pointCount;
        }

        public uint2 ToIndexAndCount()
        {
            return new uint2((uint)PageIndex, (uint)PointCount);
        }

        public int2 ToBufferAndPageIndex()
        {
            return new int2(BufferIndex, PageIndex);
        }
    }
}