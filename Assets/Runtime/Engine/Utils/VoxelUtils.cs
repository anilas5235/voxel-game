using Runtime.Engine.Utils.Extensions;
using Unity.Mathematics;
using UnityEngine;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Utils
{
    /// <summary>
    /// Utility methods for converting between world, chunk and voxel coordinates,
    /// and common direction vectors used for neighbor queries.
    /// </summary>
    public static class VoxelUtils
    {
        /// <summary>
        /// Gets the chunk origin coordinates for a world position specified as <see cref="Vector3Int"/>.
        /// </summary>
        /// <param name="position">World position in integer voxel coordinates.</param>
        /// <returns>Chunk origin coordinates in voxel space.</returns>
        public static int2 GetChunkCoords(Vector3Int position) => GetChunkCoords(position.Int3());

        /// <summary>
        /// Computes chunk origin coordinates (bottom-left in XZ) for the given world position,
        /// correctly handling negative coordinates.
        /// </summary>
        /// <param name="position">World position in voxel coordinates.</param>
        /// <returns>Chunk origin coordinates this position belongs to.</returns>
        public static int2 GetChunkCoords(int3 position)
        {
            int2 cCoords = new(
                FloorDivide(position.x, ChunkWidth),
                FloorDivide(position.z, ChunkDepth)
            );
            return cCoords;
        }

        public static int3 GetPartitionCoords(Vector3 position) =>
            GetPartitionCoords(Vector3Int.FloorToInt(position).Int3());

        public static int3 GetPartitionCoords(int3 position)
        {
            int2 cCoords = GetChunkCoords(position);
            return new int3(
                cCoords.x,
                position.y / PartitionHeight,
                cCoords.y
            );
        }


        /// <summary>
        /// Gets the local voxel coordinates within its chunk for a world position/>.
        /// </summary>
        /// <param name="position">World position in integer voxel coordinates.</param>
        /// <returns>Local voxel coordinates within the chunk.</returns>
        public static int3 GetLocalVoxelCoords(Vector3Int position)
        {
            int2 chunkCoords = GetChunkCoords(position);
            return position.Int3() - ChunkSize.MemberMultiply(chunkCoords.x, 0, chunkCoords.y);
        }

        private static int FloorDivide(int value, int divisor) =>
            value / divisor - (value < 0 && value % divisor != 0 ? 1 : 0);
    }
}