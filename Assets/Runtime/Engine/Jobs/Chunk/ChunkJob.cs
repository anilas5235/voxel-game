using Runtime.Engine.Noise;
using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Runtime.Engine.Jobs.Chunk
{
    [BurstCompile]
    public struct ChunkJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkSize;
        [ReadOnly] public NoiseProfile NoiseProfile;

        [ReadOnly] public NativeList<int3> Jobs;

        [WriteOnly] public NativeParallelHashMap<int3, Data.Chunk>.ParallelWriter Results;

        [ReadOnly] public int RandomSeed;

        [ReadOnly] public GeneratorConfig Config;


        // use a lower scale for caves so noise varies slower -> larger cave features
        private const float CaveScale = 0.04f; // 3D noise scale for caves
        private const int LavaLevel = 5;

        public void Execute(int index)
        {
            int3 position = Jobs[index];
            Data.Chunk chunk = GenerateChunkData(position);
            Results.TryAdd(position, chunk);
        }

        private Data.Chunk GenerateChunkData(int3 chunkWordPos)
        {
            int volume = ChunkSize.x * ChunkSize.y * ChunkSize.z;
            int surfaceArea = ChunkSize.x * ChunkSize.z;

            // Compute water level once per chunk
            int waterLevel = Config.WaterLevel;

            // Temporary buffers for this chunk
            NativeArray<ushort> vox = new(volume, Allocator.Temp);
            NativeArray<ChunkGenerationTerrain.ChunkColumn> chunkColumns = new(surfaceArea, Allocator.Temp);

            ChunkGenerationTerrain.PrepareChunkMaps(ref ChunkSize, ref NoiseProfile, RandomSeed, ref Config,
                ref chunkWordPos, chunkColumns);

            ChunkGenerationTerrain.FillTerrain(ref ChunkSize, vox, waterLevel, chunkColumns, ref Config);

            ChunkGenerationCavesOres.CarveCaves(ChunkSize, vox, chunkWordPos, chunkColumns, Config, RandomSeed,
                CaveScale, LavaLevel);

            ChunkGenerationCavesOres.PlaceOres(ChunkSize, vox, Config, RandomSeed);

            PlaceStructures(vox, chunkColumns, chunkWordPos, ChunkSize, RandomSeed, ref Config);
            // Step E: vegetation and micro-structures on surface
            PlaceVegetation(vox, chunkColumns, chunkWordPos, ChunkSize, RandomSeed, ref Config);

            Data.Chunk data = WriteToChunkData(vox, chunkWordPos);

            // Dispose temps
            vox.Dispose();
            chunkColumns.Dispose();
            return data;
        }

        private Data.Chunk WriteToChunkData(NativeArray<ushort> vox, int3 chunkWordPos)
        {
            Data.Chunk data = new(chunkWordPos, ChunkSize);
            ushort last = 0;
            int run = 0;
            bool hasLast = false;
            for (int x = 0; x < ChunkSize.x; x++)
            for (int z = 0; z < ChunkSize.z; z++)
            for (int y = 0; y < ChunkSize.y; y++)
            {
                ushort voxelId = vox[ChunkSize.Flatten(x, y, z)];
                if (hasLast && voxelId == last)
                {
                    run++;
                }
                else
                {
                    if (hasLast) data.AddVoxels(last, run);
                    last = voxelId;
                    run = 1;
                    hasLast = true;
                }
            }

            if (hasLast && run > 0) data.AddVoxels(last, run);

            return data;
        }

        // Helper to check if a surface column has at least one neighboring top-block (non-air, non-water).
        private static bool SurfaceHasNeighbor(NativeArray<ushort> vox, int cx, int cy, int cz, int3 chunkSize,
            ushort waterId)
        {
            int neighbors = 0;
            // -x
            int3 pos = new(cx - 1, cy, cz);
            if (InBounds(pos, chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            pos = new int3(cx + 1, cy, cz);
            // +x
            if (InBounds(pos, chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            pos = new int3(cx, cy, cz + 1);
            // +z
            if (InBounds(pos, chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            pos = new int3(cx, cy, cz - 1);
            // -z
            if (InBounds(pos, chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            return neighbors > 0;
        }

        private static bool InBounds(int3 pos, int3 chunkSize)
        {
            return InBounds(pos.x, pos.y, pos.z, chunkSize);
        }

        private static bool InBounds(int x, int y, int z, int3 chunkSize)
        {
            return x >= 0 && x < chunkSize.x &&
                   z >= 0 && z < chunkSize.z &&
                   y >= 0 && y < chunkSize.y;
        }

        // Ensure a tree trunk at (cx,cy,cz) would have at least 1 block empty around it (no adjacent trunks).
        private static bool CanPlaceTree(NativeArray<ushort> vox, int cx, int cy, int cz, int3 chunkSize, ushort logA,
            ushort logB)
        {
            // check a 3x3 area centered on trunk position at trunk base height (cy)
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                if (ox == 0 && oz == 0) continue; // skip center
                int tx = cx + ox;
                int tz = cz + oz;

                if (!InBounds(new int3(tx, cy, tz), chunkSize)) continue;
                ushort v = vox[chunkSize.Flatten(tx, cy, tz)];
                if (v == logA || v == logB) return false;
            }

            return true;
        }

        private static void PlaceStructures(NativeArray<ushort> vox,
            NativeArray<ChunkGenerationTerrain.ChunkColumn> chunkColumns, int3 chunkWordPos, int3 chunkSize,
            int randomSeed, ref GeneratorConfig config)
        {
            int sx = chunkSize.x;
            int sz = chunkSize.z;
            int sy = chunkSize.y;

            // Deterministic per-chunk RNG: combine global seed with origin so different seeds produce different worlds
            uint seed = (uint)((chunkWordPos.x * 43524) ^ (chunkWordPos.z * 7856) ^ randomSeed ^ 0x85ebca6b);
            Random rng = new(seed == 0 ? 1u : seed);

            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            {
                int gi = GetColumIdx(x, z, sz);
                ChunkGenerationTerrain.ChunkColumn chunkCol = chunkColumns[gi];
                int gy = chunkCol.Height;
                if (gy <= 0 || gy >= sy - 2) continue;

                Biome biome = chunkCol.Biome;
                ushort surface = vox[chunkSize.Flatten(x, gy, z)];
                if (surface == 0) continue;

                switch (biome)
                {
                    case Biome.Desert:
                        if (rng.NextFloat() < 0.00005f)
                        {
                            PlacePyramid(vox, x, gy + 1, z, 5, config.SandStoneRed, chunkSize);
                        }
                        else if (rng.NextFloat() < 0.000002f)
                        {
                            PlaceOasis(vox, x, gy, z, chunkSize, ref config, ref rng);
                        }

                        break;
                    case Biome.Ice:
                        if (rng.NextFloat() < 0.0007f)
                        {
                            PlaceIgloo(vox, x, gy + 1, z, chunkSize, ref config);
                        }

                        break;
                    case Biome.Ocean:
                        if (surface == config.Water && rng.NextFloat() < 0.00001f)
                        {
                            PlaceShipwreck(vox, x, config.WaterLevel - 1, z, chunkSize, ref config);
                        }

                        break;
                }

                // Mineshaft entrances in plains/forest/mountain at low chance
                if (biome is Biome.Plains or Biome.Forest or Biome.Mountain or Biome.HighStone &&
                    rng.NextFloat() < 0.0009f)
                {
                    PlaceMineShaft(vox, x, gy, z, chunkSize, config, rng.NextInt(6, 20));
                }
            }
        }


        private static void PlaceVegetation(NativeArray<ushort> vox,
            NativeArray<ChunkGenerationTerrain.ChunkColumn> chunkColumns,
            int3 chunkWordPos, int3 chunkSize, int randomSeed, ref GeneratorConfig config)
        {
            int sx = chunkSize.x;
            int sz = chunkSize.z;
            int sy = chunkSize.y;

            // Deterministic per-chunk RNG: combine global seed with origin so different seeds produce different worlds
            uint seed = (uint)((chunkWordPos.x * 73856093) ^ (chunkWordPos.z * 19349663) ^ randomSeed ^ 0x85ebca6b);
            Random rng = new(seed == 0 ? 1u : seed);

            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            {
                int gi = GetColumIdx(x, z, sz);
                ChunkGenerationTerrain.ChunkColumn chunkCol = chunkColumns[gi];
                int gy = chunkCol.Height;
                if (gy <= 0 || gy >= sy - 2) continue;

                Biome biome = chunkCol.Biome;
                ushort surface = vox[chunkSize.Flatten(x, gy, z)];
                if (surface == 0) continue;

                TryPlaceBiomVegetation(vox, chunkColumns, chunkSize, ref config, surface, x, gy, z, biome, ref rng);
            }
        }

        private static void TryPlaceBiomVegetation(NativeArray<ushort> vox,
            NativeArray<ChunkGenerationTerrain.ChunkColumn> chunkColumns, int3 chunkSize,
            ref GeneratorConfig config, ushort surface, int x, int gy, int z, Biome biome, ref Random rng)
        {
            // nur auf erdeartigen Blöcken Vegetation platzieren
            if (!IsEarthLike(surface, ref config)) return;
            // only place vegetation if the block above ground is empty
            if (!InBounds(x, gy + 1, z, chunkSize) || vox[chunkSize.Flatten(x, gy + 1, z)] != 0) return;

            int oneAboveIdx = chunkSize.Flatten(x, gy + 1, z);

            switch (biome)
            {
                case Biome.Forest:
                    if (surface != config.Grass) break;
                    // less dense forest: reduce chance and increase size variability
                    if (rng.NextFloat() < 0.1f)
                    {
                        int h = rng.NextInt(5, 9);
                        ushort chosenLog = rng.NextInt(0, 100) < 75 ? config.LogOak : config.LogBirch;
                        if (CanPlaceTree(vox, x, gy + 1, z, chunkSize, config.LogOak, config.LogBirch))
                        {
                            PlaceTree(vox, x, gy + 1, z, h, chosenLog, ref rng, ref config, chunkSize);
                        }
                    }
                    else if (rng.NextFloat() < 0.12f)
                    {
                        vox[oneAboveIdx] = config.GrassF;
                    }

                    break;
                case Biome.Plains:
                    if (surface != config.Grass) break;
                    if (rng.NextFloat() < 0.015f)
                    {
                        int h = rng.NextInt(6, 10);
                        ushort chosenLog = rng.NextInt(0, 100) < 80 ? config.LogOak : config.LogBirch;
                        if (CanPlaceTree(vox, x, gy + 1, z, chunkSize, config.LogOak, config.LogBirch))
                        {
                            PlaceTree(vox, x, gy + 1, z, h, chosenLog, ref rng, ref config, chunkSize);
                        }
                    }
                    else if (rng.NextFloat() < 0.06f)
                    {
                        // slightly higher chance specifically for plains
                        vox[oneAboveIdx] = config.Flowers;
                    }
                    else if (SurfaceHasNeighbor(vox, x, gy, z, chunkSize, config.Water) && rng.NextFloat() < 0.002f)
                    {
                        SpawnWheatCluster(vox, chunkColumns, chunkSize, ref config, ref rng, x, z);
                    }
                    else if (rng.NextFloat() < 0.05f)
                    {
                        vox[oneAboveIdx] = config.GrassF;
                    }

                    break;
                case Biome.Jungle:
                    if (surface == config.Grass && rng.NextFloat() < 0.6f)
                    {
                        int h = rng.NextInt(7, 11);
                        if (CanPlaceTree(vox, x, gy + 1, z, chunkSize, config.LogOak, config.LogBirch))
                        {
                            PlaceTree(vox, x, gy + 1, z, h, config.LogOak, ref rng, ref config, chunkSize);
                        }
                    }

                    break;
                case Biome.Desert:
                    // cacti in desert
                    if (surface == config.Sand && rng.NextFloat() < 0.015f)
                    {
                        PlaceColumn(vox, x, gy + 1, z, chunkSize, config.Cactus, rng.NextInt(1, 5));
                    }
                    else if ((surface == config.Sand || surface == config.Dirt) && rng.NextFloat() < 0.08f)
                    {
                        vox[oneAboveIdx] = config.GrassFDry;
                    }

                    break;
                case Biome.Swamp:
                    if (rng.NextFloat() < 0.06f)
                    {
                        int m = rng.NextInt(0, 100);
                        vox[oneAboveIdx] = m switch
                        {
                            < 50 => config.MushroomBrown,
                            < 80 => config.MushroomRed,
                            _ => config.MushroomTan
                        };
                    }

                    break;
                case Biome.HighStone:
                case Biome.GreyMountain:
                    // sparse vegetation on high rocky biomes
                    if (rng.NextFloat() < 0.02f && surface == config.Dirt)
                    {
                        vox[oneAboveIdx] = config.GrassF;
                    }

                    break;
                case Biome.RedDesert:
                    if (surface != config.SandRed) break;
                    // red desert: more cacti (we add more later), but keep big structures rare here
                    if (rng.NextFloat() < 0.004f)
                    {
                        PlaceColumn(vox, x, gy + 1, z, chunkSize, config.Cactus, rng.NextInt(1, 6));
                    }

                    break;
                case Biome.Beach:
                    // place occasional palm trees
                    if (rng.NextFloat() < 0.02f)
                    {
                        PlacePalm(vox, x, gy + 1, z, ref rng, chunkSize, ref config);
                    }

                    break;
                case Biome.Tundra:
                    if (surface == config.DirtSnowy && rng.NextFloat() < 0.08f)
                    {
                        vox[oneAboveIdx] = config.GrassFDead;
                    }

                    break;
            }
        }

        private static void SpawnWheatCluster(NativeArray<ushort> vox,
            NativeArray<ChunkGenerationTerrain.ChunkColumn> chunkColumns, int3 chunkSize,
            ref GeneratorConfig config, ref Random rng, int x, int z)
        {
            int cluster = rng.NextInt(1, 4);
            for (int wx = -cluster; wx <= cluster; wx++)
            for (int wz = -cluster; wz <= cluster; wz++)
            {
                int tx = x + wx;
                int tz = z + wz;
                if (tx < 0 || tx >= chunkSize.x || tz < 0 || tz >= chunkSize.z) continue;
                int tgi = GetColumIdx(tx, tz, chunkSize.z);
                int tgy = chunkColumns[tgi].Height;
                // require that target column's ground is earth-like and not underwater
                if (tgy <= 0 || tgy >= chunkSize.y - 1) continue;
                int belowIdx = chunkSize.Flatten(tx, tgy, tz);
                ushort below = vox[belowIdx];
                if (!IsEarthLike(below, ref config)) continue;
                int tIdx = chunkSize.Flatten(tx, tgy + 1, tz);
                if (vox[tIdx] == 0)
                {
                    int stage = rng.NextInt(1, 5);
                    ushort id = stage switch
                    {
                        1 => config.WheatStage1,
                        2 => config.WheatStage2,
                        3 => config.WheatStage3,
                        _ => config.WheatStage4
                    };
                    vox[tIdx] = id;
                }
            }
        }

        private static bool IsEarthLike(ushort id, ref GeneratorConfig config)
        {
            if (id == 0) return false;
            return id == config.Grass ||
                   id == config.Dirt ||
                   id == config.DirtSnowy ||
                   id == config.Sand ||
                   id == config.SandGrey ||
                   id == config.SandRed ||
                   id == config.StoneSandy;
        }

        private static void PlaceTree(NativeArray<ushort> vox, int x, int y, int z, int height, ushort idLog,
            ref Random rng, ref GeneratorConfig config, int3 chunkSize)
        {
            ushort idLeaves = config.Leaves;
            if (idLog == config.LogBirch)
            {
                idLeaves = config.LeavesOrange;
            }

            int sy = chunkSize.y;
            // Trunk: ensure inside chunk before writing
            for (int i = 0; i < height && y + i < sy - 1; i++)
            {
                if (!InBounds(x, y + i, z, chunkSize)) break;
                vox[chunkSize.Flatten(x, y + i, z)] = idLog;
            }

            const int radius = 2;
            int top = y + height - 1;
            for (int ox = -radius; ox <= radius; ox++)
            for (int oz = -radius; oz <= radius; oz++)
            for (int oy = -1; oy <= 1; oy++)
            {
                float dist = math.length(new float3(ox, oy, oz));
                if (dist <= radius + 0.2f)
                {
                    int yy = top + oy;
                    int tx = x + ox;
                    int tz = z + oz;
                    if (!InBounds(tx, yy, tz, chunkSize)) continue;
                    vox[chunkSize.Flatten(tx, yy, tz)] = idLeaves;
                }
            }

            // If birch trunk, sometimes spawn mushrooms near the base (various types)
            if (idLog != config.LogBirch) return;

            for (int ox = -2; ox <= 2; ox++)
            for (int oz = -2; oz <= 2; oz++)
            {
                if (!(rng.NextFloat() < 0.12f)) continue;
                
                int bx = x + ox;
                int bz = z + oz;
                int by = y - 1;
                if (!InBounds(bx, by, bz, chunkSize)) continue;
                int groundIdx = chunkSize.Flatten(bx, by, bz);
                int idx = chunkSize.Flatten(bx, by + 1, bz);


                if (vox[idx] != 0 ||
                    InBounds(bx, by + 1, bz, chunkSize) ||
                    !IsEarthLike(vox[groundIdx], ref config)) continue;

                int m = rng.NextInt(0, 100);
                vox[idx] = m switch
                {
                    < 50 => config.MushroomBrown,
                    < 80 => config.MushroomRed,
                    _ => config.MushroomTan
                };
            }
        }

        private static void PlaceColumn(NativeArray<ushort> vox, int x, int y, int z, int3 chunkSize, int blockId,
            int count)
        {
            int sy = chunkSize.y;
            for (int i = 0; i < count && y + i < sy - 1; i++)
            {
                if (!InBounds(x, y + i, z, chunkSize)) break;
                vox[chunkSize.Flatten(x, y + i, z)] = (ushort)blockId;
            }
        }

        private static int GetColumIdx(int x, int z, int sz)
        {
            return z + x * sz;
        }

        // Simple helper: place a small square pyramid centered on (cx,cy,cz)
        private static void PlacePyramid(NativeArray<ushort> vox, int cx, int cy, int cz, int radius, ushort block,
            int3 chunkSize)
        {
            for (int layer = 0; layer <= radius; layer++)
            {
                int r = radius - layer;
                for (int ox = -r; ox <= r; ox++)
                for (int oz = -r; oz <= r; oz++)
                {
                    int x = cx + ox;
                    int y = cy + layer;
                    int z = cz + oz;
                    if (!InBounds(x, y, z, chunkSize)) continue;
                    vox[chunkSize.Flatten(x, y, z)] = block;
                }
            }
        }

        private static void PlaceOasis(NativeArray<ushort> vox, int cx, int cy, int cz, int3 chunkSize,
            ref GeneratorConfig config, ref Random rng)
        {
            ushort water = config.Water;
            ushort grass = config.Grass;
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                int x = cx + ox;
                int y = cy + 1;
                int z = cz + oz;
                if (!InBounds(x, y, z, chunkSize)) continue;
                vox[chunkSize.Flatten(x, y, z)] = water;
                // surround with grass where possible
                int sx0 = x + 1;
                int sx1 = x - 1;
                int sz0 = z + 1;
                int sz1 = z - 1;
                if (InBounds(sx0, y, z, chunkSize) && vox[chunkSize.Flatten(sx0, y, z)] == 0)
                    vox[chunkSize.Flatten(sx0, y, z)] = grass;
                if (InBounds(sx1, y, z, chunkSize) && vox[chunkSize.Flatten(sx1, y, z)] == 0)
                    vox[chunkSize.Flatten(sx1, y, z)] = grass;
                if (InBounds(x, y, sz0, chunkSize) && vox[chunkSize.Flatten(x, y, sz0)] == 0)
                    vox[chunkSize.Flatten(x, y, sz0)] = grass;
                if (InBounds(x, y, sz1, chunkSize) && vox[chunkSize.Flatten(x, y, sz1)] == 0)
                    vox[chunkSize.Flatten(x, y, sz1)] = grass;
            }

            // plant a couple of small palm-like trees at edges
            PlacePalm(vox, cx - 2, cy + 1, cz, ref rng, chunkSize, ref config);
            PlacePalm(vox, cx + 2, cy + 1, cz, ref rng, chunkSize, ref config);
        }

        private static void PlaceIgloo(NativeArray<ushort> vox, int cx, int cy, int cz, int3 chunkSize,
            ref GeneratorConfig config)
        {
            ushort snow = config.Snow;
            // simple 3x3 dome
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            for (int oy = 0; oy <= 2; oy++)
            {
                int x = cx + ox;
                int y = cy + oy;
                int z = cz + oz;
                if (!InBounds(x, y, z, chunkSize)) continue;
                // hollow interior
                if (ox == 0 && (oy == 0 && oz == 0)||(oy ==1 && oz == -1)||(oy==0 && oz == -1)) continue;
                vox[chunkSize.Flatten(x, y, z)] = snow;
            }
        }

        private static void PlaceShipwreck(NativeArray<ushort> vox, int cx, int cy, int cz, int3 chunkSize,
            ref GeneratorConfig config)
        {
            ushort planks = config.Planks;
            for (int ox = -2; ox <= 2; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                int x = cx + ox;
                int y = cy + 1 + math.min(math.abs(ox), 1);
                int z = cz + oz;
                if (!InBounds(x, y, z, chunkSize)) continue;
                vox[chunkSize.Flatten(x, y, z)] = planks;
            }
        }

        private static void PlacePalm(NativeArray<ushort> vox, int cx, int cy, int cz, ref Random rng, int3 chunkSize,
            ref GeneratorConfig config)
        {
            ushort log = config.LogOak;
            ushort leaves = config.Leaves;
            int h = rng.NextInt(3, 6);
            for (int i = 0; i < h; i++)
            {
                int y = cy + i;
                if (!InBounds(cx, y, cz, chunkSize)) break;
                vox[chunkSize.Flatten(cx, y, cz)] = log;
            }

            int top = cy + h - 1;
            for (int ox = -2; ox <= 2; ox++)
            for (int oz = -2; oz <= 2; oz++)
            {
                if (math.abs(ox) + math.abs(oz) > 3) continue;
                int x = cx + ox;
                int y = top + rng.NextInt(0, 2);
                int z = cz + oz;
                if (!InBounds(x, y, z, chunkSize)) continue;
                vox[chunkSize.Flatten(x, y, z)] = leaves;
            }
        }

        // Simple mineshaft entrance: vertical shaft with a small wooden rim and occasional ladder-like planks
        private static void PlaceMineShaft(NativeArray<ushort> vox, int cx, int groundY, int cz, int3 chunkSize,
            GeneratorConfig config, int depth)
        {
            ushort planks = config.Planks;
            for (int y = groundY; y > math.max(1, groundY - depth); y--)
            {
                if (!InBounds(cx, y, cz, chunkSize)) continue;
                // carve a 1x1 shaft
                vox[chunkSize.Flatten(cx, y, cz)] = 0;
                // occasionally place wooden support every 3 blocks
                if ((groundY - y) % 3 == 0 && InBounds(cx + 1, y, cz, chunkSize))
                    vox[chunkSize.Flatten(cx + 1, y, cz)] = planks;
                if ((groundY - y) % 5 == 0 && InBounds(cx - 1, y, cz, chunkSize))
                    vox[chunkSize.Flatten(cx - 1, y, cz)] = planks;
            }

            // create small entrance on surface
            if (InBounds(cx, groundY + 1, cz, chunkSize)) vox[chunkSize.Flatten(cx, groundY + 1, cz)] = 0;
        }
    }
}