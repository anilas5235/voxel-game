using System;
using UnityEngine;
using static Voxels.Data.VoxelWorld;

namespace Voxels.Data
{
    public static class Chunk
    {
        public static void LoopThroughVoxels(Action<Vector3Int> action)
        {
            for (int i = 0; i < VoxelsPerChunk; i++)
            {
                Vector3Int pos = GetVoxelPosition(i);
                action(pos);
            }
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

        public static VoxelType GetVoxel(ChunkData chunkData, int x, int y, int z)
        {
            return GetVoxel(chunkData, new Vector3Int(x, y, z));
        }

        public static VoxelType GetVoxel(ChunkData chunkData, Vector3Int voxelPosition)
        {
            return InRange(voxelPosition)
                ? chunkData.voxelData[GetIndex(voxelPosition)]
                : chunkData.World.GetVoxelFromChunkCoordinates(chunkData, chunkData.WorldPosition + voxelPosition);
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
                   voxelPosition.y * ChunkSize +
                   voxelPosition.z * ChunkSize * ChunkHeight;
        }

        /// <summary>
        /// Gets the voxel position in the chunk based on the index.
        /// </summary> 
        /// <param name="index">The index of the voxel in the chunk.</param>
        /// <returns>A Vector3Int representing the voxel position in the chunk.</returns>
        private static Vector3Int GetVoxelPosition(int index)
        {
            int x = index % ChunkSize;
            int y = (index / ChunkSize) % ChunkHeight;
            int z = index / (ChunkSize * ChunkHeight);

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

            LoopThroughVoxels(pos =>
            {
                meshData = VoxelHelper.GetMeshData(chunkData, pos, meshData,
                    chunkData.voxelData[GetIndex(pos)]);
            });

            return meshData;
        }

        internal static Vector3Int ChunkPositionFromVoxelCoords(Vector3Int pos)
        {
            return new Vector3Int
            {
                x = Mathf.FloorToInt(pos.x / (float)ChunkSize) * ChunkSize,
                y = Mathf.FloorToInt(pos.y / (float)ChunkHeight) * ChunkHeight,
                z = Mathf.FloorToInt(pos.z / (float)ChunkSize) * ChunkSize
            };
        }
    }
}