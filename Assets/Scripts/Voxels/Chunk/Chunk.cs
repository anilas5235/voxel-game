using System;
using UnityEngine;
using static Voxels.VoxelWorld;

namespace Voxels.Chunk
{
    public static class Chunk
    {
        public static void LoopThroughVoxels(Action<Vector3Int> action)
        {
            for (int y = 0; y < ChunkHeight; y++)
            for (int z = 0; z < ChunkSize; z++)
            for (int x = 0; x < ChunkSize; x++)
                action(new Vector3Int(x, y, z));
        }

        private static bool InRange(Vector3Int voxelPosition)
        {
            return InRangeX(voxelPosition.x) && InRangeY(voxelPosition.y) && InRangeZ(voxelPosition.z);
        }

        private static bool InRangeX(int axisCoordinate)
        {
            return axisCoordinate is >= 0 and < ChunkSize;
        }

        private static bool InRangeY(int axisCoordinate)
        {
            return axisCoordinate is >= 0 and < ChunkHeight;
        }

        private static bool InRangeZ(int axisCoordinate)
        {
            return axisCoordinate is >= 0 and < ChunkSize;
        }

        public static int GetVoxel(ChunkData chunkData, Vector3Int voxelPosition)
        {
            if (InRange(voxelPosition))
                return chunkData.GetVoxel(voxelPosition);

            ChunkData other = chunkData.World.GetChunkFrom(chunkData.WorldPosition + voxelPosition);
            if (other == null) return -1;
            return other.GetVoxelFromWoldVoxPos(chunkData.WorldPosition + voxelPosition);
        }

        public static void SetVoxel(ChunkData chunkData, Vector3Int voxelPosition, int voxelId)
        {
            if (InRange(voxelPosition))
            {
                chunkData.SetVoxel(voxelPosition, voxelId);
                return;
            }

            ChunkData other = chunkData.World.GetChunkFrom(chunkData.WorldPosition + voxelPosition);
            other?.SetVoxelFromWorldVoxPos(chunkData.WorldPosition + voxelPosition, voxelId);
        }
    }
}