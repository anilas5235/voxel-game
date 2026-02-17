using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Data
{
    /// <summary>
    /// Read-only accessor spanning multiple chunks and their neighbors (wrap-around for border traversal).
    /// Provides utilities for neighbor checks and voxel lookups across chunk boundaries.
    /// </summary>
    [BurstCompile]
    internal readonly struct ChunkAccessor
    {
        private readonly NativeParallelHashMap<int2, ChunkVoxelData>.ReadOnly _chunks;
        private readonly NativeParallelHashMap<int2, ChunkLightData> _lightData;

        /// <summary>
        /// Constructs a new accessor.
        /// </summary>
        internal ChunkAccessor(NativeParallelHashMap<int2, ChunkVoxelData>.ReadOnly chunks, 
            NativeParallelHashMap<int2, ChunkLightData> lightData)
        {
            _chunks = chunks;
            _lightData = lightData;
        }

        /// <summary>
        /// Voxel lookup within a chunk; remaps out-of-range coordinates to neighbor chunks.
        /// </summary>
        internal ushort GetVoxelInPartition(in int3 partitionPos, int3 voxelPos)
        {
            voxelPos.y += partitionPos.y * PartitionHeight;
            if (voxelPos.y is >= ChunkHeight or < 0) return 0;
            int2 chunkOffset = int2.zero;
            
            switch (voxelPos.x)
            {
                case >= ChunkWidth:
                    chunkOffset.x = voxelPos.x / ChunkWidth;
                    voxelPos.x %= ChunkWidth;
                    break;
                case < 0:
                    chunkOffset.x = (voxelPos.x - ChunkWidth + 1) / ChunkWidth;
                    voxelPos.x = (voxelPos.x % ChunkWidth + ChunkWidth) % ChunkWidth;
                    break;
            }

            switch (voxelPos.z)
            {
                case >= ChunkDepth:
                    chunkOffset.y = voxelPos.z / ChunkDepth;
                    voxelPos.z %= ChunkDepth;
                    break;
                case < 0:
                    chunkOffset.y = (voxelPos.z - ChunkDepth + 1) / ChunkDepth;
                    voxelPos.z = (voxelPos.z % ChunkDepth + ChunkDepth) % ChunkDepth;
                    break;
            }

            return TryGetChunk(partitionPos.xz + chunkOffset, out var chunk) ? chunk.GetVoxel(voxelPos) : (ushort)0;
        }

        /// <summary>
        /// Attempts to get a chunk at a position.
        /// </summary>
        internal bool TryGetChunk(int2 pos, out ChunkVoxelData chunk) => _chunks.TryGetValue(pos, out chunk);

        /// <summary>
        /// Checks whether a chunk exists.
        /// </summary>
        internal bool ContainsChunk(int2 coord) => _chunks.ContainsKey(coord);
    
        /// <summary>
        /// Checks whether a voxel coordinate lies inside chunk bounds.
        /// </summary>
        public static bool InChunkBounds(int3 chunkLocalPos)
        {
            return chunkLocalPos.x >= 0 && chunkLocalPos.x < ChunkWidth &&
                   chunkLocalPos.y >= 0 && chunkLocalPos.y < ChunkHeight &&
                   chunkLocalPos.z >= 0 && chunkLocalPos.z < ChunkDepth;
        }
        
        public static bool InPartitionBounds(int3 partitionLocalPos)
        {
            return partitionLocalPos.x >= 0 && partitionLocalPos.x < PartitionWidth &&
                   partitionLocalPos.y >= 0 && partitionLocalPos.y < PartitionHeight &&
                   partitionLocalPos.z >= 0 && partitionLocalPos.z < PartitionDepth;
        }
    }
}