using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Voxels.MeshGeneration;
using static Voxels.VoxelWorld;

namespace Voxels.Chunk
{
    public static class ChunkUtils
    {
        public static void LoopThroughVoxels(Action<Vector3Int> action)
        {
            for (int y = 0; y < ChunkHeight; y++)
            for (int z = 0; z < ChunkSize; z++)
            for (int x = 0; x < ChunkSize; x++)
                action(new Vector3Int(x, y, z));
        }

        private static bool IsEdgeVoxel(Vector3Int voxelPosition)
        {
            return voxelPosition.x is 0 or ChunkSize - 1 ||
                   voxelPosition.z is 0 or ChunkSize - 1;
        }

        private static List<ChunkData> GetChunksFromEdgeVoxel(ChunkData chunkData, Vector3Int voxelPosition)
        {
            List<ChunkData> chunks = new();
            if (!IsEdgeVoxel(voxelPosition)) return chunks;
            switch (voxelPosition.x)
            {
                case 0:
                    chunks.Add(GetAdjacentChunk(chunkData, Direction.Left));
                    break;
                case ChunkSize - 1:
                    chunks.Add(GetAdjacentChunk(chunkData, Direction.Right));
                    break;
            }

            switch (voxelPosition.z)
            {
                case 0:
                    chunks.Add(GetAdjacentChunk(chunkData, Direction.Backwards));
                    break;
                case ChunkSize - 1:
                    chunks.Add(GetAdjacentChunk(chunkData, Direction.Forward));
                    break;
            }

            chunks = chunks.Where(c => c != null).ToList();
            return chunks;
        }

        private static ChunkData GetAdjacentChunk(ChunkData chunkData, Direction direction)
        {
            Vector3Int worldPos = chunkData.WorldPosition + direction.GetVector() * ChunkSize;
            return chunkData.World.GetChunkFrom(worldPos);
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
            return InRange(voxelPosition)
                ? chunkData.GetVoxel(voxelPosition)
                : chunkData.World.GetVoxelFromWoldVoxPos(chunkData.WorldPosition + voxelPosition);
        }

        public static void SetVoxel(ChunkData chunkData, Vector3Int voxelPosition, int voxelId)
        {
            if (InRange(voxelPosition))
            {
                chunkData.SetVoxel(voxelPosition, voxelId);
                List<ChunkData> adjacentChunks = GetChunksFromEdgeVoxel(chunkData, voxelPosition);
                foreach (ChunkData chunk in adjacentChunks)
                {
                    chunk.dirty = true;
                    chunkData.World.UpdateChunkMesh(chunk.ChunkPosition);
                }
                return;
            }
            chunkData.World.SetVoxelFromWorldVoxPos(chunkData.WorldPosition + voxelPosition, voxelId);
        }
    }
}