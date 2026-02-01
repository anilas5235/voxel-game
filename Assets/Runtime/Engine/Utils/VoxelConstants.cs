using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Runtime.Engine.Utils
{
    public static class VoxelConstants
    {
        internal const int ChunkWidth = 16;
        internal const int ChunkHeight = 256;
        internal const int ChunkDepth = 16;
        internal static readonly int3 ChunkSize = new(ChunkWidth, ChunkHeight, ChunkDepth);
        internal static readonly int2 ChunkSizeXY = new(ChunkWidth, ChunkDepth);

        internal const int VoxelsPerChunk = ChunkWidth * ChunkHeight * ChunkDepth;


        internal const int PartitionWidth = 16;
        internal const int PartitionHeight = 16;
        internal const int PartitionDepth = 16;
        internal static readonly int3 PartitionSize = new(PartitionWidth, PartitionHeight, PartitionDepth);

        internal const int VoxelsPerPartition = PartitionWidth * PartitionHeight * PartitionDepth;
        internal const int PartitionsPerChunk = ChunkHeight / PartitionHeight;

        internal const MeshUpdateFlags MeshFlags = MeshUpdateFlags.DontRecalculateBounds |
                                                   MeshUpdateFlags.DontValidateIndices |
                                                   MeshUpdateFlags.DontResetBoneBounds;
    }
}