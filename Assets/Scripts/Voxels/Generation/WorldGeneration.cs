using System;
using System.Collections.Generic;
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
        // Flattened world: reduce hill/detail influence so world is overall much flatter.
        private const float ContinentScale = 0.0012f; // very large
        private const float HillScale = 0.01f; // reduced medium
        private const float DetailScale = 0.03f; // reduced fine detail
        // Make biomes larger; decreasing BiomeScale creates larger biome regions.
        private const float BiomeScale = 0.003f;
        private const float BiomeExaggeration = 1.5f; // push cold/warm further apart

        // River parameters
        private const float RiverScale = 0.0008f;
        private const float RiverThreshold = -0.6f;
        private const float CaveScale = 0.08f;

        public struct GeneratorConfig
        {
            public int Stone;
            public int Dirt;
            public int Grass;
            public int Bedrock;
            public int Water;
            public int Sand;
            public int Sandstone;
            // generic tree fallback
            public int Log;
            public int Leaves;
            // specific tree variants
            public int OakLog;
            public int OakLeaves;
            public int BirchLog;
            public int BirchLeaves;
            public int Cactus;
            public int Coal;
            public int Iron;
            public int Gold;
            public int Diamond;
            public int Boat;
            public int TallGrass;
            public int Mushroom;
            public int StoneBrick;
            public int Snow;
        }

        public static ChunkData GenerateVoxels(ChunkData data, float noiseScale, int waterThreshold, long seed, GeneratorConfig config)
        {
            // Thread-safe RNG seeded from world seed and chunk position
            uint baseSeed = (uint)(seed ^ ((data.ChunkPosition.x * 73856093) ^ (data.ChunkPosition.y * 19349663)));
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random(baseSeed == 0 ? 1u : baseSeed);

            // Get commonly used voxel ids with safe fallback to air(0)
            int idStone = config.Stone;
            int idDirt = config.Dirt;
            int idGrass = config.Grass;
            int idBedrock = config.Bedrock;
            int idWater = config.Water;
            int idSand = config.Sand;
            int idSandstone = config.Sandstone;
            int idLog = config.Log;
            int idLeaves = config.Leaves;
            int idCactus = config.Cactus;
            int idCoal = config.Coal;
            int idIron = config.Iron;
            int idGold = config.Gold;
            int idDiamond = config.Diamond;
            int idBoat = config.Boat; // if exists, else ignored

            // Precompute heightmap and biome maps for this chunk
            float[,] height = new float[ChunkSize, ChunkSize];
            float[,] temperature = new float[ChunkSize, ChunkSize];
            float[,] humidity = new float[ChunkSize, ChunkSize];

            // World-space origin for this chunk
            int worldX0 = data.WorldPosition.x;
            int worldZ0 = data.WorldPosition.z;

            // Build height, temperature and humidity maps
            for (int x = 0; x < ChunkSize; x++)
            for (int z = 0; z < ChunkSize; z++)
            {
                float wx = (worldX0 + x);
                float wz = (worldZ0 + z);

                // Base continents (large scale)
                float continent = noise.snoise(new float2(wx * ContinentScale, wz * ContinentScale));
                // Hills and valleys
                float hills = noise.snoise(new float2(wx * HillScale, wz * HillScale));
                // Detail
                float detail = noise.snoise(new float2(wx * DetailScale, wz * DetailScale));

                // Combine layers with weights and bias -> reduce hills/detail for flatter world
                float elev = continent * 0.7f + hills * 0.18f + detail * 0.12f;
                // Normalize from -1..1 to 0..1
                elev = elev * 0.5f + 0.5f;

                // Make world overall flatter by pulling elevation towards mid-level.
                elev = math.lerp(elev, 0.5f, 0.55f);
                // Apply world noiseScale multiplier for user control (dampened)
                elev = math.lerp(elev, elev * math.clamp(noiseScale * 6f, 0f, 1f), 0.15f);

                height[x, z] = elev;

                // Biome fields
                temperature[x, z] = noise.snoise(new float2((wx + 10000) * BiomeScale, (wz + 10000) * BiomeScale)) * 0.5f + 0.5f;
                humidity[x, z] = noise.snoise(new float2((wx - 10000) * BiomeScale, (wz - 10000) * BiomeScale)) * 0.5f + 0.5f;

                // Push extremes apart and exaggerate biomes so cold and warm areas are more distant
                temperature[x, z] = math.smoothstep(0f, 1f, temperature[x, z]);
                humidity[x, z] = math.smoothstep(0f, 1f, humidity[x, z]);
                temperature[x, z] = math.clamp((temperature[x, z] - 0.5f) * BiomeExaggeration + 0.5f, 0f, 1f);
                humidity[x, z] = math.clamp((humidity[x, z] - 0.5f) * (BiomeExaggeration * 0.9f) + 0.5f, 0f, 1f);

                // River indicator (separate noise field)
                float riverVal = noise.snoise(new float2(wx * RiverScale, wz * RiverScale));
                bool isRiverField = riverVal < RiverThreshold;
                // store river flag temporarily by encoding into height negative sentinel? We'll keep a separate local map below.
                height[x, z] = isRiverField ? -1f : height[x, z];
            }

            // Pre-generate a set of "worms" (tunnels) that may pass through this chunk. Rarer but more tortuous.
            List<Worm> worms = GenerateWormsForChunk(data.ChunkPosition, rng);

            // Precompute simple river map for this chunk
            bool[,] riverMap = new bool[ChunkSize, ChunkSize];

            // Precompute 3D cave noise cache for this chunk to avoid redundant calls
            float[,,] caveNoise = new float[ChunkSize, ChunkHeight, ChunkSize];
            for (int x = 0; x < ChunkSize; x++)
            for (int z = 0; z < ChunkSize; z++)
            {
                int wx = worldX0 + x;
                int wz = worldZ0 + z;
                for (int y = 0; y < ChunkHeight; y++)
                {
                    caveNoise[x, y, z] = noise.snoise(new float3(wx * CaveScale, y * CaveScale, wz * CaveScale));
                }
                // precompute river map using same RiverScale
                float rv = noise.snoise(new float2((worldX0 + x) * RiverScale, (worldZ0 + z) * RiverScale));
                riverMap[x, z] = rv < RiverThreshold;
            }

            // Fill voxels by vertical column with layers
            for (int x = 0; x < ChunkSize; x++)
            for (int z = 0; z < ChunkSize; z++)
            {
                float elev = height[x, z];
                float temp = temperature[x, z];
                float hum = humidity[x, z];
                bool isRiver = riverMap[x, z];

                // Convert normalized elevation to world Y
                int groundY = (int)math.clamp(elev * (ChunkHeight - 1f), 1f, ChunkHeight - 1f);

                // If the biome is ocean, flatten the surface to exact water threshold so ocean surface is smooth
                if (SelectBiome(temp, hum, elev, groundY, waterThreshold) == Biome.Ocean)
                {
                    groundY = waterThreshold - 1;
                }

                // Biome selection
                Biome biome = SelectBiome(temp, hum, elev, groundY, waterThreshold);

                // Top blocks depending on biome
                int topBlock = idGrass;
                int underBlock = idDirt;
                if (biome == Biome.Desert)
                {
                    topBlock = idSand == 0 ? idDirt : idSand;
                    underBlock = idSandstone == 0 ? idDirt : idSandstone;
                }
                else if (biome == Biome.Snow)
                {
                    topBlock = config.Snow;
                    underBlock = idDirt;
                }
                else if (biome == Biome.Swamp)
                {
                    topBlock = idDirt;
                    underBlock = idDirt;
                }

                for (int y = 0; y < ChunkHeight; y++)
                {
                    int voxelId = 0; // air by default

                    // Bedrock layer
                    if (y == 0)
                    {
                        voxelId = idBedrock;
                        data.SetVoxel(new UnityEngine.Vector3Int(x, y, z), voxelId);
                        continue;
                    }

                    // If this column is part of a river, carve channel and fill to water level (flat surface)
                    if (isRiver)
                    {
                        if (y <= waterThreshold - 1)
                        {
                            voxelId = idWater;
                        }
                        else
                        {
                            // carve out content above water to make a channel up to original ground
                            voxelId = 0;
                        }
                        data.SetVoxel(new UnityEngine.Vector3Int(x, y, z), voxelId);
                        continue;
                    }

                    // If below ground -> fill stone/dirt
                    if (y < groundY - 4)
                    {
                        voxelId = idStone;
                    }
                    else if (y < groundY)
                    {
                        voxelId = underBlock;
                    }
                    else if (y == groundY)
                    {
                        // Surface block
                        if (groundY < waterThreshold)
                            voxelId = idWater; // submerged
                        else
                            voxelId = topBlock;
                    }
                    else
                    {
                        // Above ground
                        if (y < waterThreshold)
                            voxelId = idWater;
                        else
                            voxelId = 0;
                    }

                    data.SetVoxel(new UnityEngine.Vector3Int(x, y, z), voxelId);
                }

                // After vertical fill, carve caves using 3D noise (pass groundY so we avoid carving near-surface)
                CarveCavesInColumn(data, x, z, worms, caveNoise, groundY, idWater);

                // Place ores exposed in cave walls and stone
                PlaceOresInColumn(data, x, z, idStone, idCoal, idIron, idGold, idDiamond, rng);

                // Place vegetation / small features
                if (IsSurfaceSolid(data, x, groundY, z, config))
                {
                    TryPlaceVegetation(data, x, groundY, z, biome, rng, config);
                }

                // Place boats near coasts (simple heuristic) — delegated to StructureGenerator
                if (biome == Biome.Ocean && rng.NextInt(0, 200) == 0)
                {
                    StructureGenerator.TryPlaceBoat(data, x, groundY, z, rng, idBoat, config);
                }

                // Place medium/rich structures rarely (delegated to StructureGenerator)
                if (rng.NextInt(0, 10000) == 0)
                {
                    StructureGenerator.TryPlaceLargeStructure(data, x, groundY, z, rng, biome, new WorldGeneration.GeneratorConfig
                    {
                        Stone = idStone,
                        Dirt = idDirt,
                        Grass = idGrass,
                        Bedrock = idBedrock,
                        Water = idWater,
                        Sand = idSand,
                        Sandstone = idSandstone,
                        Log = idLog,
                        Leaves = idLeaves,
                        OakLog = config.OakLog,
                        OakLeaves = config.OakLeaves,
                        BirchLog = config.BirchLog,
                        BirchLeaves = config.BirchLeaves,
                        Cactus = idCactus,
                        Coal = idCoal,
                        Iron = idIron,
                        Gold = idGold,
                        Diamond = idDiamond,
                        Boat = idBoat,
                        TallGrass = config.TallGrass,
                        Mushroom = config.Mushroom,
                        StoneBrick = config.StoneBrick,
                        Snow = config.Snow
                    });
                }

                // Very rare: place a tiny village (delegated)
                if (rng.NextInt(0, 20000) == 0)
                {
                    StructureGenerator.TryPlaceVillage(data, x, groundY, z, rng, new WorldGeneration.GeneratorConfig
                    {
                        Stone = idStone,
                        Dirt = idDirt,
                        Grass = idGrass,
                        Bedrock = idBedrock,
                        Water = idWater,
                        Sand = idSand,
                        Sandstone = config.Sandstone,
                        Log = idLog,
                        Leaves = idLeaves,
                        OakLog = config.OakLog,
                        OakLeaves = config.OakLeaves,
                        BirchLog = config.BirchLog,
                        BirchLeaves = config.BirchLeaves,
                        Cactus = idCactus,
                        Coal = idCoal,
                        Iron = idIron,
                        Gold = idGold,
                        Diamond = idDiamond,
                        Boat = idBoat,
                        TallGrass = config.TallGrass,
                        Mushroom = config.Mushroom,
                        StoneBrick = config.StoneBrick,
                        Snow = config.Snow
                    });
                }

                // Place small ruins occasionally (delegated)
                if (rng.NextInt(0, 2000) == 0)
                {
                    StructureGenerator.TryPlaceRuin(data, x, groundY, z, rng, new WorldGeneration.GeneratorConfig
                    {
                        StoneBrick = config.StoneBrick,
                        Stone = idStone,
                        Dirt = idDirt,
                        Grass = idGrass,
                        Bedrock = idBedrock,
                        Water = idWater,
                        Sand = idSand,
                        Sandstone = idSandstone,
                        Log = idLog,
                        Leaves = idLeaves,
                        OakLog = config.OakLog,
                        OakLeaves = config.OakLeaves,
                        BirchLog = config.BirchLog,
                        BirchLeaves = config.BirchLeaves,
                        Cactus = idCactus,
                        Coal = idCoal,
                        Iron = idIron,
                        Gold = idGold,
                        Diamond = idDiamond,
                        Boat = idBoat,
                        TallGrass = config.TallGrass,
                        Mushroom = config.Mushroom,
                        Snow = config.Snow
                    });
                }
            }

            return data;
        }

        // Make Biome visible to other generation helpers like StructureGenerator.
        public enum Biome
        {
            Plains,
            Forest,
            Desert,
            Jungle,
            Swamp,
            Snow,
            Mountain,
            Ocean,
            Tundra
        }

        private static Biome SelectBiome(float temp, float hum, float elev, int groundY, int waterThreshold)
        {
            if (groundY < waterThreshold - 2) return Biome.Ocean;

            if (temp > 0.75f && hum < 0.25f) return Biome.Desert;
            if (temp > 0.8f && hum > 0.65f) return Biome.Jungle;
            if (temp > 0.5f && hum > 0.5f) return Biome.Forest;
            if (temp < 0.2f && hum < 0.4f) return Biome.Tundra;
            if (temp < 0.3f && elev > 0.7f) return Biome.Mountain;
            if (hum > 0.7f && temp > 0.3f && temp < 0.6f) return Biome.Swamp;
            if (temp < 0.15f) return Biome.Snow;
            return Biome.Plains;
        }

        private struct Worm
        {
            public float3 start;
            public float3 dir;
            public float length;
            public float radius;
        }

        private static List<Worm> GenerateWormsForChunk(UnityEngine.Vector2Int chunkPos, Unity.Mathematics.Random rng)
        {
            List<Worm> worms = new List<Worm>();
            // Deterministic number of worms per chunk — make them rarer (0..1) and less vertical
            int count = rng.NextInt(0, 2);
            for (int i = 0; i < count; i++)
            {
                float sx = chunkPos.x * ChunkSize + rng.NextFloat(0, ChunkSize);
                float sy = rng.NextFloat(8, ChunkHeight * 0.45f);
                float sz = chunkPos.y * ChunkSize + rng.NextFloat(0, ChunkSize);

                float dx = rng.NextFloat(-1f, 1f);
                // reduce vertical component so worms are mostly horizontal
                float dy = rng.NextFloat(-0.1f, 0.1f);
                float dz = rng.NextFloat(-1f, 1f);
                float len = rng.NextFloat(10f, 48f);
                float rad = rng.NextFloat(1.2f, 3.5f);

                Worm w = new Worm { start = new float3(sx, sy, sz), dir = math.normalize(new float3(dx, dy, dz)), length = len, radius = rad };
                worms.Add(w);
            }

            return worms;
        }

        private static void CarveCavesInColumn(ChunkData data, int cx, int cz, List<Worm> worms, float[,,] caveNoise, int groundY, int waterId)
        {
            // If the surface of this column is water, skip carving caves here to avoid underwater caves.
            try
            {
                int surfaceId = data.GetVoxel(new UnityEngine.Vector3Int(cx, groundY, cz));
                if (surfaceId == waterId) return;
            }
            catch
            {
                // If any error reading voxel, fall back to safe behavior and skip carving
                return;
            }

            int worldX0 = data.WorldPosition.x;
            int worldZ0 = data.WorldPosition.z;

            for (int y = 1; y < ChunkHeight - 1; y++)
            {
                // use cached noise value
                float n = caveNoise[cx, y, cz];
                // Increase threshold to make caves rarer; near-surface require higher threshold
                float baseThreshold = 0.6f;
                float threshold = baseThreshold;
                if (y >= groundY - 6) // near surface
                {
                    threshold = 0.78f; // much rarer near top
                }
                bool isCave = n > threshold;

                // Additional worm carving
                if (!isCave)
                {
                    foreach (var w in worms)
                    {
                        // Find nearest point on worm line segment to this voxel center
                        float3 p = new float3(worldX0 + cx + 0.5f, y + 0.5f, worldZ0 + cz + 0.5f);
                        float3 toP = p - w.start;
                        float t = math.clamp(math.dot(toP, w.dir), 0f, w.length);
                        float3 closest = w.start + w.dir * t;
                        // displace the path center using noise to make the worm curvy (spaghetti-like)
                        float3 disp = new float3(
                            noise.snoise(new float3(t * 0.2f, (worldX0 + cx) * 0.02f, (worldZ0 + cz) * 0.02f)),
                            noise.snoise(new float3(t * 0.15f, (worldX0 + cx) * 0.015f, (worldZ0 + cz) * 0.015f)) * 0.4f,
                            noise.snoise(new float3(t * 0.2f + 37.1f, (worldX0 + cx) * 0.02f + 17.3f, (worldZ0 + cz) * 0.02f + 11.7f))
                        );
                        disp *= w.radius * 0.6f;
                        float3 center = closest + disp;
                        float dist = math.distance(p, center);
                        float wobble = noise.snoise(new float3(t, (worldX0 + cx) * 0.01f, (worldZ0 + cz) * 0.01f));
                        // Only allow worm carving if sufficiently deep (avoid carving into surface layers)
                        if (y < groundY - 6 && dist < w.radius * (0.6f + 0.4f * wobble))
                        {
                            isCave = true;
                            break;
                        }
                    }
                }

                if (isCave)
                {
                    data.SetVoxel(new UnityEngine.Vector3Int(cx, y, cz), 0);
                }
            }
        }

        private static void PlaceOresInColumn(ChunkData data, int cx, int cz, int stoneId, int coalId, int ironId, int goldId, int diamondId, Unity.Mathematics.Random rng)
        {
            int worldX0 = data.WorldPosition.x;
            int worldZ0 = data.WorldPosition.z;

            for (int y = 2; y < ChunkHeight - 2; y++)
            {
                int wx = worldX0 + cx;
                int wz = worldZ0 + cz;

                // Only in stone
                int current = ChunkUtils.GetVoxel(data, new UnityEngine.Vector3Int(cx, y, cz));
                if (current != stoneId) continue;

                // Determine if this stone voxel is exposed to a cave (adjacent to air)
                // check 6-neighbors
                var neighs = new UnityEngine.Vector3Int[]
                {
                    new UnityEngine.Vector3Int(cx+1,y,cz), new UnityEngine.Vector3Int(cx-1,y,cz),
                    new UnityEngine.Vector3Int(cx,y+1,cz), new UnityEngine.Vector3Int(cx,y-1,cz),
                    new UnityEngine.Vector3Int(cx,y,cz+1), new UnityEngine.Vector3Int(cx,y,cz-1)
                };
                bool exposed = false;
                foreach (var n in neighs)
                {
                    if (n.x < 0 || n.x >= ChunkSize || n.z < 0 || n.z >= ChunkSize || n.y < 0 || n.y >= ChunkHeight) continue;
                    int v = ChunkUtils.GetVoxel(data, n);
                    if (v == 0) { exposed = true; break; }
                }

                // Use layered rarity with noise + rng; boost roll if exposed to cave
                float depthNorm = 1f - (y / (float)ChunkHeight);
                float oreNoise = math.abs(noise.snoise(new float3(wx * 0.12f, y * 0.12f, wz * 0.12f)));
                float roll = math.max(0f, oreNoise * depthNorm);
                if (exposed) roll *= 1.6f; // increase chance when exposed in cave walls

                if (roll > 0.85f && diamondId != 0)
                {
                    data.SetVoxel(new UnityEngine.Vector3Int(cx, y, cz), diamondId);
                }
                else if (roll > 0.7f && goldId != 0 && y < ChunkHeight * 0.35f)
                {
                    data.SetVoxel(new UnityEngine.Vector3Int(cx, y, cz), goldId);
                }
                else if (roll > 0.6f && ironId != 0)
                {
                    data.SetVoxel(new UnityEngine.Vector3Int(cx, y, cz), ironId);
                }
                else if (roll > 0.45f && coalId != 0)
                {
                    data.SetVoxel(new UnityEngine.Vector3Int(cx, y, cz), coalId);
                }
            }
        }

        private static bool IsSurfaceSolid(ChunkData data, int x, int groundY, int z, GeneratorConfig config)
        {
            var id = data.GetVoxel(new UnityEngine.Vector3Int(x, groundY, z));
            return id != 0 && id != config.Water;
        }

        private static void TryPlaceVegetation(ChunkData data, int x, int y, int z, Biome biome, Unity.Mathematics.Random rng, GeneratorConfig config)
        {
            // Small deterministic RNG from column coordinates
            uint hash = (uint)((data.ChunkPosition.x * 73856093) ^ (data.ChunkPosition.y * 19349663) ^ (x * 83492791) ^ (z * 961748941));
            Unity.Mathematics.Random local = new Unity.Mathematics.Random(hash == 0 ? 1u : hash);

            // Detect if this column is adjacent to water (simple beach test)
            bool adjacentToWater = false;
            var neighs2D = new (int dx, int dz)[] { (1,0), (-1,0), (0,1), (0,-1) };
            foreach (var n in neighs2D)
            {
                int nx = x + n.dx;
                int nz = z + n.dz;
                if (nx < 0 || nx >= ChunkSize || nz < 0 || nz >= ChunkSize) continue;
                int nv = data.GetVoxel(new UnityEngine.Vector3Int(nx, y, nz));
                if (nv == config.Water) { adjacentToWater = true; break; }
            }

            // General rule: if the surface block is grass, occasionally place a tree on it
            // (skip this rule in Forest/Jungle since those biomes already place trees frequently)
            try
            {
                 int surfaceId = data.GetVoxel(new UnityEngine.Vector3Int(x, y, z));
                 if (surfaceId == config.Grass && biome != Biome.Forest && biome != Biome.Jungle)
                 {
-                    // ~8% chance to place a small/medium tree on grass
-                    if (local.NextFloat(0, 1) < 0.08f)
+                    // ~1% chance to place a small/medium tree on grass (reduced to 1/8 of previous)
+                    if (local.NextFloat(0, 1) < 0.01f)
                     {
                         if (local.NextInt(0, 100) < 80)
                             PlaceTree(data, x, y + 1, z, 5, local, config.OakLog, config.OakLeaves, config);
                         else
                             PlaceTree(data, x, y + 1, z, 5, local, config.BirchLog, config.BirchLeaves, config);
                         return;
                     }
                 }
             }
             catch
             {
                 // If reading fails for any reason, continue with existing rules
             }

            if (biome == Biome.Forest)
            {
                // Much more frequent trees in forests
                if (local.NextFloat(0, 1) < 0.5f)
                {
                    // Choose oak or birch: more oaks than birch
                    int choose = local.NextInt(0, 100);
                    if (choose < 75)
                        PlaceTree(data, x, y + 1, z, 4, local, config.OakLog, config.OakLeaves, config);
                    else
                        PlaceTree(data, x, y + 1, z, 4, local, config.BirchLog, config.BirchLeaves, config);
                }
            }
            else if (biome == Biome.Plains)
            {
                // Beach palms: if adjacent to water, small chance to place a palm instead of plain vegetation
                if (adjacentToWater && local.NextFloat(0, 1) < 0.12f)
                {
                    PlacePalm(data, x, y + 1, z, local, config.OakLog, config.OakLeaves, config);
                    return;
                }

                if (local.NextFloat(0, 1) < 0.05f)
                {
                    // Tall grass - represented by small block if defined
                    int tall = config.TallGrass;
                    if (tall != 0) data.SetVoxel(new UnityEngine.Vector3Int(x, y + 1, z), tall);
                }
                else if (local.NextFloat(0, 1) < 0.01f)
                {
                    // occasional tree (oak preferred)
                    if (local.NextInt(0, 100) < 80)
                        PlaceTree(data, x, y + 1, z, 5, local, config.OakLog, config.OakLeaves, config);
                    else
                        PlaceTree(data, x, y + 1, z, 5, local, config.BirchLog, config.BirchLeaves, config);
                }
            }
            else if (biome == Biome.Desert)
            {
                if (local.NextFloat(0, 1) < 0.02f)
                {
                    // Prefer palms in deserts, occasionally cactus
                    if (adjacentToWater || local.NextFloat(0,1) < 0.5f)
                    {
                        PlacePalm(data, x, y + 1, z, local, config.OakLog, config.OakLeaves, config);
                    }
                    else
                    {
                        int cactus = config.Cactus;
                        if (cactus != 0) PlaceColumn(data, x, y + 1, z, cactus, local.NextInt(1, 4));
                    }
                }
            }
            else if (biome == Biome.Jungle)
            {
                // Jungles get very frequent tall trees
                if (local.NextFloat(0, 1) < 0.6f)
                {
                    PlaceTree(data, x, y + 1, z, 6, local, config.OakLog, config.OakLeaves, config);
                }
            }
            else if (biome == Biome.Swamp)
            {
                if (local.NextFloat(0, 1) < 0.06f)
                {
                    // mushrooms or small trees
                    int mush = config.Mushroom;
                    if (mush != 0) data.SetVoxel(new UnityEngine.Vector3Int(x, y + 1, z), mush);
                }
            }
        }

        private static void PlaceTree(ChunkData data, int x, int y, int z, int height, Unity.Mathematics.Random rng, int idLog, int idLeaves, GeneratorConfig config)
        {
            // Fallback to generic tree IDs if species-specific IDs are missing.
            if (idLog == 0) idLog = config.Log;
            if (idLeaves == 0) idLeaves = config.Leaves;
            if (idLog == 0 || idLeaves == 0) return;

            // simple straight trunk
            for (int i = 0; i < height; i++)
            {
                SetIfInBounds(data, x, y + i, z, idLog);
            }

            // simple leaves blob
            int radius = 2;
            for (int ox = -radius; ox <= radius; ox++)
            for (int oz = -radius; oz <= radius; oz++)
            for (int oy = -1; oy <= 1; oy++)
            {
                float dist = math.length(new float3(ox, oy, oz));
                if (dist <= radius + 0.2f)
                {
                    SetIfInBounds(data, x + ox, y + height + oy - 1, z + oz, idLeaves);
                }
            }
        }

        // Helper to avoid writing outside chunk bounds.
        private static void SetIfInBounds(ChunkData data, int x, int y, int z, int voxelId)
        {
            if (x < 0 || x >= ChunkSize) return;
            if (z < 0 || z >= ChunkSize) return;
            if (y < 0 || y >= ChunkHeight) return;
            data.SetVoxel(new UnityEngine.Vector3Int(x, y, z), voxelId);
        }

        private static void PlaceColumn(ChunkData data, int x, int y, int z, int blockId, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (y + i >= ChunkHeight - 1) break;
                data.SetVoxel(new UnityEngine.Vector3Int(x, y + i, z), blockId);
            }
        }

        private static void PlacePalm(ChunkData data, int x, int y, int z, Unity.Mathematics.Random rng, int idLog, int idLeaves, GeneratorConfig config)
        {
            // Fallback to generic IDs
            if (idLog == 0) idLog = config.Log;
            if (idLeaves == 0) idLeaves = config.Leaves;
            if (idLog == 0 || idLeaves == 0) return;

            int height = rng.NextInt(4, 7);
            // trunk
            for (int i = 0; i < height; i++) SetIfInBounds(data, x, y + i, z, idLog);

            int top = y + height;
            // simple fronds: cross and diagonals
            var offsets = new (int ox, int oz)[] { (1,0), (2,0), (-1,0), (-2,0), (0,1), (0,2), (0,-1), (0,-2), (1,1), (-1,1), (1,-1), (-1,-1) };
            foreach (var o in offsets)
            {
                SetIfInBounds(data, x + o.ox, top, z + o.oz, idLeaves);
                // a little sagging below
                SetIfInBounds(data, x + o.ox, top - 1, z + o.oz, idLeaves);
            }
            // top center leaf
            SetIfInBounds(data, x, top, z, idLeaves);
        }
    }
}
