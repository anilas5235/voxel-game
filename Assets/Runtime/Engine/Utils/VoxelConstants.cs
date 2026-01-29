using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;

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


        internal const int PartitionWidth = 16;
        internal const int PartitionHeight = 16;
        internal const int PartitionDepth = 16;
        internal static readonly int3 PartitionSize = new(PartitionWidth, PartitionHeight, PartitionDepth);

        internal const int VoxelsPerPartition = PartitionWidth * PartitionHeight * PartitionDepth;
        internal const int PartitionsPerChunk = ChunkHeight / PartitionHeight;
        
        
        internal static readonly NativeArray<VertexAttributeDescriptor> VertexParams = new(5, Allocator.Persistent)
        {
            [0] = new VertexAttributeDescriptor(VertexAttribute.Position),
            [1] = new VertexAttributeDescriptor(VertexAttribute.Normal),
            [2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
            [3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            [4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4)
        };
        
        internal const MeshUpdateFlags MeshFlags = MeshUpdateFlags.DontRecalculateBounds |
                                                   MeshUpdateFlags.DontValidateIndices |
                                                   MeshUpdateFlags.DontResetBoneBounds;
    }
}