using System;
using UnityEngine;

namespace Voxels.Data
{
    public static class Chunk
    {
        public static void LoopThroughVoxels(ChunkData chunkData, Action<int, int, int> action)
        {
            for (int i = 0; i < ChunkData.VoxelsPerChunk; i++)
            {
                Vector3Int pos = GetVoxelPosition(i);
                action(pos.x, pos.y, pos.z);
            }
        }

        private static bool InRange(Vector3Int voxelPosition)
        {
            return InRangeX(voxelPosition.x) && InRangeY(voxelPosition.y) && InRangeZ(voxelPosition.z);
        }

        private static bool InRangeX(int axisCoordinate)
        {
            return axisCoordinate is >= 0 and < ChunkData.ChunkSize;
        }

        private static bool InRangeY(int axisCoordinate)
        {
            return axisCoordinate is >= 0 and < ChunkData.ChunkHeight;
        }

        private static bool InRangeZ(int axisCoordinate)
        {
            return axisCoordinate is >= 0 and < ChunkData.ChunkSize;
        }

        public static VoxelType GetVoxel(ChunkData chunkData, int x, int y, int z)
        {
            return GetVoxel(chunkData, new Vector3Int(x, y, z));
        }
        
        public static VoxelType GetVoxel(ChunkData chunkData, Vector3Int voxelPosition)
        {
            if (!InRange(voxelPosition))
                throw new ArgumentOutOfRangeException(nameof(voxelPosition), "Voxel position is out of range.");
            
            int index = GetIndex(voxelPosition);
            return chunkData.voxelData[index];
        }

        public static void SetVoxel(ChunkData chunkData, Vector3Int voxelPosition, VoxelType voxelType)
        {
            if (!InRange(voxelPosition))
                throw new ArgumentOutOfRangeException(nameof(voxelPosition), "Voxel position is out of range.");

            int index = GetIndex(voxelPosition);
            chunkData.voxelData[index] = voxelType;
        }

        private static int GetIndex(Vector3Int voxelPosition)
        {
            return voxelPosition.x +
                   voxelPosition.y * ChunkData.ChunkSize +
                   voxelPosition.z * ChunkData.ChunkSize * ChunkData.ChunkHeight;
        }

        /// <summary>
        /// Gets the voxel position in the chunk based on the index.
        /// </summary> 
        /// <param name="index">The index of the voxel in the chunk.</param>
        /// <returns>A Vector3Int representing the voxel position in the chunk.</returns>
        private static Vector3Int GetVoxelPosition(int index)
        {
            int x = index % ChunkData.ChunkSize;
            int y = (index / ChunkData.ChunkSize) % ChunkData.ChunkHeight;
            int z = index / (ChunkData.ChunkSize * ChunkData.ChunkHeight);

            return new Vector3Int(x, y, z);
        }

        public static Vector3Int GetVoxelPosition(Vector3Int worldPosition, ChunkData chunkData)
        {
            return new Vector3Int
            {
                x = worldPosition.x - chunkData.WorldPosition.x,
                y = worldPosition.y - chunkData.WorldPosition.y,
                z = worldPosition.z - chunkData.WorldPosition.z
            };
        }

        public static MeshData GetChunkMeshData(ChunkData chunkData)
        {
            MeshData meshData = new();

            return meshData;
        }
    }
}