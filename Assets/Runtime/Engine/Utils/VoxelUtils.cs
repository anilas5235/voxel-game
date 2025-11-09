using Runtime.Engine.Utils.Extensions;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.Engine.Utils
{
    public static class VoxelUtils
    {
        private static readonly int3 ChunkSize = VoxelEngineProvider.Current.Settings.Chunk.ChunkSize;

        public static int3 GetChunkCoords(Vector3 position) => GetChunkCoords(Vector3Int.FloorToInt(position));

        public static int3 GetChunkCoords(Vector3Int position) => GetChunkCoords(position.Int3());

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

        public static int3 GetVoxelIndex(Vector3 position) => GetVoxelIndex(Vector3Int.FloorToInt(position));

        public static int3 GetVoxelIndex(Vector3Int position)
        {
            int3 chunkCoords = GetChunkCoords(position);

            return new int3(position.x - chunkCoords.x, position.y - chunkCoords.y, position.z - chunkCoords.z);
        }

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