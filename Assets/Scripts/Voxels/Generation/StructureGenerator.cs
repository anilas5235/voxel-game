using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Voxels.Chunk;
using static Voxels.VoxelWorld;

namespace Voxels.Generation
{
    // Small helper that contains structure placement logic separated from terrain generation.
    // Structures are snapped to the terrain by sampling the local column heights and aligning
    // the structure base to the average column top within the footprint to avoid floating.
    public static class StructureGenerator
    {
        public static void TryPlaceLargeStructure(ChunkData data, int x, int y, int z, Unity.Mathematics.Random rng, WorldGeneration.Biome biome, WorldGeneration.GeneratorConfig config)
        {
            // Choose structure by biome
            if (biome == WorldGeneration.Biome.Desert)
            {
                TryPlacePyramid(data, x, z, 6, config);
            }
            else if (biome == WorldGeneration.Biome.Snow)
            {
                TryPlaceIgloo(data, x, z, 4, config);
            }
            else
            {
                TryPlaceTower(data, x, z, 6, config);
            }
        }

        public static void TryPlaceRuin(ChunkData data, int x, int y, int z, Unity.Mathematics.Random rng, WorldGeneration.GeneratorConfig config)
        {
            int wall = config.StoneBrick;
            if (wall == 0) return;
            // Ruin footprint 3x1
            int w = 3;
            int d = 1;
            var footprint = new List<(int ox, int oz)>();
            for (int ox = 0; ox < w; ox++) for (int oz = 0; oz < d; oz++) footprint.Add((ox, oz));
            int baseY = SampleBaseY(data, x, z, footprint, config);
            for (int ox = 0; ox < w; ox++)
            for (int oz = 0; oz < d; oz++)
            for (int oy = 0; oy < 2; oy++)
            {
                SetIfInBounds(data, x + ox, baseY + oy, z + oz, wall);
            }
        }

        private static int SampleBaseY(ChunkData data, int baseX, int baseZ, List<(int ox, int oz)> footprint, WorldGeneration.GeneratorConfig config)
        {
            // For each footprint cell sample the top-most solid voxel (excluding water) in the column.
            // Then compute the rounded average and clamp to valid range.
            long sum = 0;
            int count = 0;
            foreach (var (ox, oz) in footprint)
            {
                int sx = baseX + ox;
                int sz = baseZ + oz;
                if (sx < 0 || sx >= ChunkSize || sz < 0 || sz >= ChunkSize) continue;
                int top = GetColumnTop(data, sx, sz, config);
                sum += top;
                count++;
            }

            if (count == 0) return 1;
            int avg = (int)(sum / count);
            avg = math.clamp(avg, 1, ChunkHeight - 2);
            return avg + 1; // place on top of sampled ground
        }

        private static int GetColumnTop(ChunkData data, int x, int z, WorldGeneration.GeneratorConfig config)
        {
            // Return the highest y where a non-air and non-water voxel exists, else 0.
            for (int y = ChunkHeight - 1; y >= 0; y--)
            {
                int v = data.GetVoxel(new Vector3Int(x, y, z));
                if (v != 0 && v != config.Water) return y;
            }
            return 0;
        }

        private static void TryPlacePyramid(ChunkData data, int baseX, int baseZ, int size, WorldGeneration.GeneratorConfig config)
        {
            int sand = config.Sandstone;
            if (sand == 0) return;
            // footprint roughly (size*2+1)^2 -> limit to small area
            int half = size;
            var footprint = new List<(int ox, int oz)>();
            for (int ox = -half; ox <= half; ox++) for (int oz = -half; oz <= half; oz++) footprint.Add((ox, oz));
            int baseY = SampleBaseY(data, baseX, baseZ, footprint, config);
            for (int layer = 0; layer < size; layer++)
            {
                int s = size - layer;
                for (int ox = -s; ox <= s; ox++)
                for (int oz = -s; oz <= s; oz++)
                {
                    SetIfInBounds(data, baseX + ox, baseY + layer, baseZ + oz, sand);
                }
            }
        }

        private static void TryPlaceIgloo(ChunkData data, int baseX, int baseZ, int radius, WorldGeneration.GeneratorConfig config)
        {
            int snow = config.Snow;
            if (snow == 0) return;
            var footprint = new List<(int ox, int oz)>();
            for (int ox = -radius; ox <= radius; ox++) for (int oz = -radius; oz <= radius; oz++) footprint.Add((ox, oz));
            int baseY = SampleBaseY(data, baseX, baseZ, footprint, config);
            for (int ox = -radius; ox <= radius; ox++)
            for (int oz = -radius; oz <= radius; oz++)
            for (int oy = 0; oy <= radius; oy++)
            {
                float d = math.distance(new float3(ox, oy, oz), float3.zero);
                if (d <= radius + 0.2f && d >= radius - 1.5f)
                {
                    SetIfInBounds(data, baseX + ox, baseY + oy, baseZ + oz, snow);
                }
            }
        }

        private static void TryPlaceTower(ChunkData data, int baseX, int baseZ, int height, WorldGeneration.GeneratorConfig config)
        {
            int stone = config.Stone;
            if (stone == 0) return;

            // Radius is in world-voxels; adjust as needed.
            const float NoTowerRadius = 64f; // 64 voxels (~4 chunks) default
            // Compute world coordinates for the tower base
            float worldX = data.WorldPosition.x + baseX;
            float worldZ = data.WorldPosition.z + baseZ;
            if (math.length(new float2(worldX, worldZ)) <= NoTowerRadius)
            {
                // skip tower placement close to spawn
                return;
            }

            // footprint 1x1
            var footprint = new List<(int ox, int oz)> { (0, 0) };
            int baseY = SampleBaseY(data, baseX, baseZ, footprint, config);
            for (int i = 0; i < height; i++)
            {
                SetIfInBounds(data, baseX, baseY + i, baseZ, stone);
            }
        }

        private static void SetIfInBounds(ChunkData data, int x, int y, int z, int voxelId)
        {
            if (x < 0 || x >= ChunkSize) return;
            if (z < 0 || z >= ChunkSize) return;
            if (y < 0 || y >= ChunkHeight) return;
            data.SetVoxel(new Vector3Int(x, y, z), voxelId);
        }

        // Place a small boat block on water surface or beach.
        public static void TryPlaceBoat(ChunkData data, int x, int y, int z, Unity.Mathematics.Random rng, int boatId, WorldGeneration.GeneratorConfig config)
        {
            if (boatId == 0) return;
            // Place small boat as single block on water surface or on beach
            int below = data.GetVoxel(new Vector3Int(x, y - 1, z));
            if (below == config.Water || below == config.Sand)
            {
                SetIfInBounds(data, x, y + 1, z, boatId);
            }
        }

        // Very small village generator: place a few 3x3 huts and a central path. Rare; simple layout.
        public static void TryPlaceVillage(ChunkData data, int baseX, int baseY, int baseZ, Unity.Mathematics.Random rng, WorldGeneration.GeneratorConfig config)
        {
            // Require a primary building material
            int wood = config.Log;
            int wall = config.StoneBrick != 0 ? config.StoneBrick : config.Stone;
            if (wood == 0 || wall == 0) return;

            // village footprint 7x7
            int size = 3; // half-extent for simplicity -> footprint 7x7
            var footprint = new List<(int ox, int oz)>();
            for (int ox = -size; ox <= size; ox++) for (int oz = -size; oz <= size; oz++) footprint.Add((ox, oz));

            // Check that most of the footprint is on solid ground (not water)
            int solid = 0;
            foreach (var (ox, oz) in footprint)
            {
                int sx = baseX + ox;
                int sz = baseZ + oz;
                if (sx < 0 || sx >= ChunkSize || sz < 0 || sz >= ChunkSize) continue;
                int top = GetColumnTop(data, sx, sz, config);
                if (top > 0) solid++;
            }

            if (solid < footprint.Count * 0.6f) return; // too much water/invalid terrain

            // Place 3 small huts at relative positions
            var huts = new (int ox, int oz)[] { (-2, -2), (2, -1), (0, 2) };
            foreach (var h in huts)
            {
                PlaceHut(data, baseX + h.ox, baseY, baseZ + h.oz, wood, wall);
            }

            // Small central well / path (stone center)
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                SetIfInBounds(data, baseX + ox, baseY, baseZ + oz, wall);
            }
        }

        private static void PlaceHut(ChunkData data, int cx, int baseY, int cz, int wood, int wall)
        {
            // 3x3 hut with wooden roof and stone walls
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                // walls and floor
                SetIfInBounds(data, cx + ox, baseY, cz + oz, wall);
                SetIfInBounds(data, cx + ox, baseY + 1, cz + oz, 0); // interior air
                // simple roof
                SetIfInBounds(data, cx + ox, baseY + 2, cz + oz, wood);
            }
        }
    }
}
