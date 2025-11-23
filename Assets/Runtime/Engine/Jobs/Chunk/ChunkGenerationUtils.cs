using Unity.Burst;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Chunk
{
    /// <summary>
    /// Utility helpers used by chunk generation code for index conversion and bounds checks.
    /// </summary>
    [BurstCompile]
    internal static class ChunkGenerationUtils
    {
        /// <summary>
        /// Computes the linear column index from x and z coordinates in a chunk.
        /// </summary>
        /// <param name="x">Column x coordinate in the chunk.</param>
        /// <param name="z">Column z coordinate in the chunk.</param>
        /// <param name="sz">Size of the chunk along the z axis.</param>
        /// <returns>The zero-based linear index for the column within the column array.</returns>
        [BurstCompile]
        public static int GetColumnIdx(int x, int z, int sz)
        {
            return z + x * sz;
        }

        /// <summary>
        /// Checks whether the given position lies inside the chunk bounds.
        /// </summary>
        /// <param name="pos">Position in chunk-local voxel coordinates.</param>
        /// <param name="chunkSize">Size of the chunk in voxels (x, y, z).</param>
        /// <returns><c>true</c> if the position is inside the chunk; otherwise, <c>false</c>.</returns>
        [BurstCompile]
        public static bool InChunk(ref int3 pos, ref int3 chunkSize)
        {
            return InChunk(pos.x, pos.y, pos.z, ref chunkSize);
        }

        /// <summary>
        /// Checks whether the given coordinates lie inside the chunk bounds.
        /// </summary>
        /// <param name="x">X coordinate in chunk-local voxel space.</param>
        /// <param name="y">Y coordinate in chunk-local voxel space.</param>
        /// <param name="z">Z coordinate in chunk-local voxel space.</param>
        /// <param name="chunkSize">Size of the chunk in voxels (x, y, z).</param>
        /// <returns><c>true</c> if the coordinates are inside the chunk; otherwise, <c>false</c>.</returns>
        [BurstCompile]
        public static bool InChunk(int x, int y, int z, ref int3 chunkSize)
        {
            return x >= 0 && x < chunkSize.x &&
                   y >= 0 && y < chunkSize.y &&
                   z >= 0 && z < chunkSize.z;
        }
    }
}