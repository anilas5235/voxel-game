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
        /// Gets the chunk origin coordinates for a world position specified as <see cref="Vector3"/>.
        /// </summary>
        /// <param name="position">World position in floating-point coordinates.</param>
        /// <returns>Chunk origin coordinates in voxel space.</returns>
        public static int3 GetChunkCoords(Vector3 position) => GetChunkCoords(Vector3Int.FloorToInt(position));
        
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
        
        public static int3 GetPartitionCoords(Vector3 position) =>
            GetPartitionCoords(Vector3Int.FloorToInt(position).Int3());

        public static int3 GetPartitionCoords(int3 position)
        {
            int3 pCoords = GetChunkCoords(position);
            pCoords[1] = position.y / PartitionHeight;
            return pCoords;
        }
      

        /// <summary>
        /// Gets the local voxel coordinates within its chunk for a world position/>.
        /// </summary>
        /// <param name="position">World position in integer voxel coordinates.</param>
        /// <returns>Local voxel coordinates within the chunk.</returns>
        public static int3 GetLocalVoxelCoords(Vector3Int position)
        {
            int3 chunkCoords = GetChunkCoords(position);
            return new int3(position.x - chunkCoords.x, position.y - chunkCoords.y, position.z - chunkCoords.z);
        }
    }
}