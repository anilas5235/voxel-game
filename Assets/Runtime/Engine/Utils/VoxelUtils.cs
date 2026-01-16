using Runtime.Engine.Utils.Extensions;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.Engine.Utils
{
    /// <summary>
    /// Utility methods for converting between world, chunk and voxel coordinates,
    /// and common direction vectors used for neighbor queries.
    /// </summary>
    public static class VoxelUtils
    {
        private static readonly int3 ChunkSize = VoxelEngineProvider.Current.Settings.Chunk.ChunkSize;

        /// <summary>
        /// Gets the chunk origin coordinates for a world position specified as <see cref="Vector3"/>.
        /// </summary>
        /// <param name="position">World position in floating-point coordinates.</param>
        /// <returns>Chunk origin coordinates in voxel space.</returns>
        public static int3 GetChunkCoords(Vector3 position) => GetChunkCoords(Vector3Int.FloorToInt(position));

        public static int3 GetPartitionCoords(Vector3 position)
        {
            int3 pCoords = GetChunkCoords(position);
            pCoords[1] = (int)math.floor(position.y/16f) * 16;
            return pCoords;
        } 

        /// <summary>
        /// Gets the chunk origin coordinates for a world position specified as <see cref="Vector3Int"/>.
        /// </summary>
        /// <param name="position">World position in integer voxel coordinates.</param>
        /// <returns>Chunk origin coordinates in voxel space.</returns>
        public static int3 GetChunkCoords(Vector3Int position) => GetChunkCoords(position.Int3());

        /// <summary>
        /// Computes chunk origin coordinates (bottom-left in XZ) for the given world position,
        /// correctly handling negative coordinates.
        /// </summary>
        /// <param name="position">World position in voxel coordinates.</param>
        /// <returns>Chunk origin coordinates this position belongs to.</returns>
        public static int3 GetChunkCoords(int3 position)
        {
            int modX = position.x % ChunkSize.x;
            int modZ = position.z % ChunkSize.z;
            int x = position.x - modX;
            int z = position.z - modZ;
            x = position.x < 0 && modX != 0 ? x - ChunkSize.x : x;
            z = position.z < 0 && modZ != 0 ? z - ChunkSize.z : z;
            return new int3(x, 0, z);
        }

        /// <summary>
        /// Gets the local voxel index inside its chunk for a world position.
        /// </summary>
        /// <param name="position">World position in floating-point coordinates.</param>
        /// <returns>Voxel index relative to the origin of its chunk.</returns>
        public static int3 GetVoxelIndex(Vector3 position) => GetVoxelIndex(Vector3Int.FloorToInt(position));

        /// <summary>
        /// Gets the local voxel index inside its chunk for a world position specified as <see cref="Vector3Int"/>.
        /// </summary>
        /// <param name="position">World position in integer voxel coordinates.</param>
        /// <returns>Voxel index relative to the origin of its chunk.</returns>
        public static int3 GetVoxelIndex(Vector3Int position)
        {
            int3 chunkCoords = GetChunkCoords(position);
            return new int3(position.x - chunkCoords.x, position.y - chunkCoords.y, position.z - chunkCoords.z);
        }

        /// <summary>
        /// Six Cartesian directions used for neighbor queries (+/-X, +/-Y, +/-Z).
        /// </summary>
        public static readonly int3[] Directions =
        {
            new(1, 0, 0),
            new(-1, 0, 0),
            new(0, 1, 0),
            new(0, -1, 0),

            new(0, 0, 1),
            new(0, 0, -1),
        };
    }
}