using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Voxels.MeshGeneration;
using static Voxels.VoxelWorld;

namespace Voxels.Chunk
{
    public static class ChunkUtils
    {
        public static void LoopThroughVoxels(Action<int3> action)
        {
            for (int y = 0; y < ChunkSize.y; y++)
            for (int z = 0; z < ChunkSize.x; z++)
            for (int x = 0; x < ChunkSize.z; x++)
                action(new int3(x, y, z));
        }

        private static bool IsEdgeVoxel(int3 voxelPosition)
        {
            return voxelPosition.x == 0 || voxelPosition.x == ChunkSize.x - 1 ||
                   voxelPosition.z == 0 || voxelPosition.z == ChunkSize.z - 1;
        }

        private static List<ChunkData> GetChunksFromEdgeVoxel(ChunkData chunkData, int3 voxelPosition)
        {
            List<ChunkData> chunks = new();
            if (!IsEdgeVoxel(voxelPosition)) return chunks;
            if (voxelPosition.x == 0)
            {
                chunks.Add(GetAdjacentChunk(chunkData, Direction.Left));
            }
            else if (voxelPosition.x == ChunkSize.x - 1)
            {
                chunks.Add(GetAdjacentChunk(chunkData, Direction.Right));
            }

            if (voxelPosition.z == 0)
            {
                chunks.Add(GetAdjacentChunk(chunkData, Direction.Backward));
            }
            else if (voxelPosition.z == ChunkSize.z - 1)
            {
                chunks.Add(GetAdjacentChunk(chunkData, Direction.Forward));
            }

            return chunks;
        }

        private static ChunkData GetAdjacentChunk(ChunkData chunkData, Direction direction)
        {
            int3 worldPos = chunkData.WorldPosition + direction.GetInt3() * ChunkSize.x;
            return VoxelWorld.Instance.GetChunkFrom(worldPos);
        }

        private static bool InRange(int3 voxelPosition)
        {
            return InRangeX(voxelPosition.x) && InRangeY(voxelPosition.y) && InRangeZ(voxelPosition.z);
        }

        private static bool InRangeX(int axisCoordinate)
        {
            return axisCoordinate >= 0 && axisCoordinate < ChunkSize.x;
        }

        private static bool InRangeY(int axisCoordinate)
        {
            return axisCoordinate >= 0 && axisCoordinate < ChunkSize.y;
        }

        private static bool InRangeZ(int axisCoordinate)
        {
            return axisCoordinate >= 0 && axisCoordinate < ChunkSize.z;
        }

        public static bool GetVoxel(ChunkData chunkData, int3 voxelPosition, out ushort voxelId)
        {
            voxelId = 0;
            if (!InRange(voxelPosition))
            {
                return VoxelWorld.Instance.GetVoxelFromWoldVoxPos(chunkData.WorldPosition + voxelPosition, out voxelId);
            }

            voxelId = chunkData.GetVoxel(voxelPosition);
            return true;
        }

        public static void SetVoxel(ChunkData chunkData, int3 voxelPosition, ushort voxelId)
        {
            if (InRange(voxelPosition))
            {
                chunkData.SetVoxel(voxelPosition, voxelId);
                List<ChunkData> adjacentChunks = GetChunksFromEdgeVoxel(chunkData, voxelPosition);
                return;
            }

            VoxelWorld.Instance.SetVoxelFromWorldVoxPos(chunkData.WorldPosition + voxelPosition, voxelId);
        }
    }
}