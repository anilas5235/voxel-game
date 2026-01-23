using Unity.Mathematics;

namespace Runtime.Engine.Utils
{
    internal static class VoxelConstants
    {
        internal const int ChunkWidth = 16;
        internal const int ChunkHeight = 256;
        internal const int ChunkDepth = 16;
        internal static readonly int3 ChunkSize = new(ChunkWidth, ChunkHeight, ChunkDepth);
        internal static readonly int2 ChunkSizeXY = new(ChunkWidth, ChunkDepth);

        internal const int VoxelsPerChunk = ChunkWidth * ChunkHeight * ChunkDepth;


        private const int PartitionWidth = 16;
        internal const int PartitionHeight = 16;
        private const int PartitionDepth = 16;
        internal static readonly int3 PartitionSize = new(PartitionWidth, PartitionHeight, PartitionDepth);

        internal const int VoxelsPerPartition = PartitionWidth * PartitionHeight * PartitionDepth;
    }
}