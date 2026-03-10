using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Utils
{
    /// <summary>
    /// Constants for GPU-based voxel rendering pipeline.
    /// </summary>
    public static class VoxelRenderConstants
    {
        /// <summary>
        /// Max points per partition: 32^3 voxels × 3 worst-case faces per voxel.
        /// </summary>
        public const int MaxPointsPerPartition = PartitionWidth * PartitionHeight * PartitionDepth *3;
        
        /// <summary>
        /// Max dirty partitions uploaded per frame for GPU rebuild.
        /// </summary>
        public const int MaxDirtyUploadsPerFrame = 16;

        /// <summary>
        /// Point data stride in bytes: float3 position (12) + uint4 packed (16) = 28 bytes, aligned to 32.
        /// </summary>
        public const int PointDataStride = 32;

        /// <summary>
        /// Partition metadata stride in bytes.
        /// int3 partitionPos (12) + uint pointCount (4) + float3 boundsMin (12) + float3 boundsMax (12) = 40 bytes.
        /// </summary>
        public const int PartitionMetadataStride = 40;

        /// <summary>
        /// Chunk metadata stride in bytes.
        /// int2 chunkPos (8) + float3 boundsMin (12) + float3 boundsMax (12) + uint partitionMask (4) = 36 bytes, aligned to 40.
        /// </summary>
        public const int ChunkMetadataStride = 40;

        /// <summary>
        /// Calculates max active partitions based on draw distance.
        /// </summary>
        public static int MaxActivePartitions(int drawDistance) =>
            (2 * drawDistance + 1) * (2 * drawDistance + 1) * PartitionsPerChunk;

        /// <summary>
        /// Calculates max active chunks based on draw distance.
        /// </summary>
        public static int MaxActiveChunks(int drawDistance) =>
            (2 * drawDistance + 1) * (2 * drawDistance + 1);
    }
}

