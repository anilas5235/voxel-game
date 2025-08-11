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

        public static int GetVoxel(ChunkData chunkData, int x, int y, int z)
        {
            return GetVoxel(chunkData, new Vector3Int(x, y, z));
        }

        public static int GetVoxel(ChunkData chunkData, Vector3Int voxelPosition)
        {
            return InRange(voxelPosition)
                ? chunkData.voxels[GetIndex(voxelPosition)]
                : chunkData.World.GetVoxelFromOtherChunk(chunkData.WorldPosition + voxelPosition);
        }

        public static void SetVoxel(ChunkData chunkData, Vector3Int voxelPosition, int voxelId)
        {
            if (!InRange(voxelPosition))
                throw new ArgumentOutOfRangeException(nameof(voxelPosition), "Voxel position is out of range.");

            int index = GetIndex(voxelPosition);
            chunkData.voxels[index] = voxelId;
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

        public static Vector3Int GetVoxelPosition(Vector3Int voxelWorldPos, ChunkData chunkData)
        {
            return new Vector3Int
            {
                x = voxelWorldPos.x - chunkData.WorldPosition.x,
                y = voxelWorldPos.y - chunkData.WorldPosition.y,
                z = voxelWorldPos.z - chunkData.WorldPosition.z
            };
        }

        public static MeshData GetChunkMeshData(ChunkData chunkData)
        {
            MeshData meshData = new();

            LoopThroughVoxels(pos =>
            {
                meshData = VoxelHelper.GetMeshData(chunkData, pos, meshData,
                    VoxelRegistry.Get(chunkData.voxels[GetIndex(pos)]));
            });

            return meshData;
        }

        internal static Vector3Int GetChunkPosition(Vector3Int voxelWorldPos)
        {
            return new Vector3Int
            {
                x = Mathf.FloorToInt(voxelWorldPos.x / (float)ChunkSize) * ChunkSize,
                y = Mathf.FloorToInt(voxelWorldPos.y / (float)ChunkHeight) * ChunkHeight,
                z = Mathf.FloorToInt(voxelWorldPos.z / (float)ChunkSize) * ChunkSize
            };
        }
    }
}