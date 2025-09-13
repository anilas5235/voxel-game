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
            int dirt = VoxelRegistry.GetId("std:Dirt");
            int grass = VoxelRegistry.GetId("std:Grass");
            int water = VoxelRegistry.GetId("std:Water");
            for (int x = 0; x < ChunkSize; x++)
            for (int z = 0; z < ChunkSize; z++)
            {
                float noiseValue = Mathf.PerlinNoise((data.WorldPosition.x + x) * noiseScale,
                    (data.WorldPosition.z + z) * noiseScale);
                int groundPosition = Mathf.RoundToInt(noiseValue * ChunkHeight/2) + ChunkHeight/4;
                for (int y = 0; y < ChunkHeight; y++)
                {
                    int voxelId = dirt;
                    if (y > groundPosition)
                        voxelId = y < waterThreshold ? water : 0;
                    else if (y == groundPosition) voxelId = grass;

                    ChunkUtils.SetVoxel(data, new Vector3Int(x, y, z), voxelId);
                }
            }
            data.dirty = true;
            return data;
        }
    }
}