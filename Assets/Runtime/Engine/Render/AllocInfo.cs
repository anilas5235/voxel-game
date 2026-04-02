using Unity.Mathematics;

namespace Runtime.Engine.Render
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

        public uint2 ToIndexAndCount() =>
            new((uint)PageIndex, (uint)PointCount);

        public int2 ToBufferAndPageIndex() =>
            new(BufferIndex, PageIndex);
    }
}