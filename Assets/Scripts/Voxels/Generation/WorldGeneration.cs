using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using Voxels.Chunk;
using Voxels.Data;
using static Voxels.VoxelWorld;

namespace Voxels.Generation
{
    public static class WorldGeneration
    {
        public static ChunkData GenerateVoxels(ChunkData data, float noiseScale, int waterThreshold)
        {
            ushort dirt = VoxelRegistry.GetId("std:Dirt");
            ushort grass = VoxelRegistry.GetId("std:Grass");
            ushort water = VoxelRegistry.GetId("std:Water");
            for (int x = 0; x < ChunkSize.x; x++)
            for (int z = 0; z < ChunkSize.z; z++)
            {
                float noiseValue = Mathf.PerlinNoise((data.WorldPosition.x + x) * noiseScale,
                    (data.WorldPosition.z + z) * noiseScale);
                int groundPosition = Mathf.RoundToInt(noiseValue * HalfChunkSize.y/2) + ChunkSize.y/4;
                for (int y = 0; y < ChunkSize.y; y++)
                {
                    ushort voxelId = dirt;
                    if (y > groundPosition)
                        voxelId = y < waterThreshold ? water : (ushort)0;
                    else if (y == groundPosition) voxelId = grass;

                    ChunkUtils.SetVoxel(data, new int3(x, y, z), voxelId);
                }
            }
            return data;
        }
    }
}