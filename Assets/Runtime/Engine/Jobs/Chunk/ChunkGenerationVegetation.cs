using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Runtime.Engine.Jobs.Chunk
{
    /// <summary>
    /// Provides Burst-compiled helpers to place biome-dependent vegetation such as trees,
    /// grass, cacti, crops and mushrooms on top of generated terrain.
    /// </summary>
    [BurstCompile]
    internal static class ChunkGenerationVegetation
    {
        /// <summary>
        /// Places vegetation for every suitable surface column in the chunk using biome information
        /// and a deterministic random stream derived from chunk position and global seed.
        /// </summary>
        /// <param name="vox">Voxel buffer that will receive vegetation blocks.</param>
        /// <param name="chunkColumns">Per-column data including biome, height and climate.</param>
        /// <param name="chunkWordPos">World-space origin (in voxels) of the chunk.</param>
        /// <param name="chunkSize">Size of the chunk in voxels (x, y, z).</param>
        /// <param name="randomSeed">Global seed used to derive the per-chunk random generator.</param>
        /// <param name="config">Generator configuration providing voxel IDs for vegetation blocks.</param>
        [BurstCompile]
        public static void PlaceVegetation(ref NativeArray<ushort> vox,
            ref NativeArray<ChunkColumn> chunkColumns,
            ref int3 chunkWordPos, ref int3 chunkSize, int randomSeed, ref GeneratorConfig config)
        {
            int sx = chunkSize.x;
            int sz = chunkSize.z;
            int sy = chunkSize.y;

            uint seed = (uint)((chunkWordPos.x * 73856093) ^ (chunkWordPos.z * 19349663) ^ randomSeed ^ 0x85ebca6b);
            Random rng = new(seed == 0 ? 1u : seed);

            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            {
                int gi = ChunkGenerationUtils.GetColumnIdx(x, z, sz);
                ChunkColumn chunkCol = chunkColumns[gi];
                int gy = chunkCol.Height;
                if (gy <= 0 || gy >= sy - 2) continue;

                Biome biome = chunkCol.Biome;
                ushort surface = vox[chunkSize.Flatten(x, gy, z)];
                if (surface == 0) continue;

                TryPlaceBiomeVegetation(ref vox, ref chunkColumns, ref chunkSize, ref config, surface, x, gy, z, biome,
                    ref rng);
            }
        }

        private static void TryPlaceBiomeVegetation(ref NativeArray<ushort> vox,
            ref NativeArray<ChunkColumn> chunkColumns, ref int3 chunkSize,
            ref GeneratorConfig config, ushort surface, int x, int gy, int z, Biome biome, ref Random rng)
        {
            if (!IsEarthLike(surface, ref config)) return;

            if (!ChunkGenerationUtils.InChunk(x, gy + 1, z, ref chunkSize) ||
                vox[chunkSize.Flatten(x, gy + 1, z)] != 0) return;

            int oneAboveIdx = chunkSize.Flatten(x, gy + 1, z);

            switch (biome)
            {
                case Biome.Forest:
                    if (surface != config.Grass) break;
                    if (rng.NextFloat() < 0.1f)
                    {
                        int h = rng.NextInt(5, 9);
                        ushort chosenLog = rng.NextInt(0, 100) < 75 ? config.LogOak : config.LogBirch;
                        if (CanPlaceTree(ref vox, x, gy + 1, z, ref chunkSize, config.LogOak, config.LogBirch))
                        {
                            PlaceTree(ref vox, x, gy + 1, z, h, chosenLog, ref rng, ref config, ref chunkSize);
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
                        if (CanPlaceTree(ref vox, x, gy + 1, z, ref chunkSize, config.LogOak, config.LogBirch))
                        {
                            PlaceTree(ref vox, x, gy + 1, z, h, chosenLog, ref rng, ref config, ref chunkSize);
                        }
                    }
                    else if (rng.NextFloat() < 0.06f)
                    {
                        vox[oneAboveIdx] = config.Flowers;
                    }
                    else if (SurfaceHasNeighbor(ref vox, x, gy, z, ref chunkSize, config.Water) &&
                             rng.NextFloat() < 0.002f)
                    {
                        SpawnWheatCluster(ref vox, ref chunkColumns, ref chunkSize, ref config, ref rng, x, z);
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
                        if (CanPlaceTree(ref vox, x, gy + 1, z, ref chunkSize, config.LogOak, config.LogBirch))
                        {
                            PlaceTree(ref vox, x, gy + 1, z, h, config.LogOak, ref rng, ref config, ref chunkSize);
                        }
                    }

                    break;
                case Biome.Desert:
                    if (surface == config.Sand && rng.NextFloat() < 0.015f)
                    {
                        PlaceColumn(ref vox, x, gy + 1, z, ref chunkSize, config.Cactus, rng.NextInt(1, 5));
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
                    if (rng.NextFloat() < 0.02f && surface == config.Dirt)
                    {
                        vox[oneAboveIdx] = config.GrassF;
                    }

                    break;
                case Biome.RedDesert:
                    if (surface != config.SandRed) break;
                    if (rng.NextFloat() < 0.004f)
                    {
                        PlaceColumn(ref vox, x, gy + 1, z, ref chunkSize, config.Cactus, rng.NextInt(1, 6));
                    }

                    break;
                case Biome.Beach:
                    if (rng.NextFloat() < 0.02f)
                    {
                        PlacePalm(ref vox, x, gy + 1, z, ref rng, ref chunkSize, ref config);
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

        private static void SpawnWheatCluster(ref NativeArray<ushort> vox,
            ref NativeArray<ChunkColumn> chunkColumns, ref int3 chunkSize,
            ref GeneratorConfig config, ref Random rng, int x, int z)
        {
            int cluster = rng.NextInt(1, 4);
            for (int wx = -cluster; wx <= cluster; wx++)
            for (int wz = -cluster; wz <= cluster; wz++)
            {
                int tx = x + wx;
                int tz = z + wz;
                if (tx < 0 || tx >= chunkSize.x || tz < 0 || tz >= chunkSize.z) continue;
                int tgi = ChunkGenerationUtils.GetColumnIdx(tx, tz, chunkSize.z);
                int tgy = chunkColumns[tgi].Height;
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

        private static bool SurfaceHasNeighbor(ref NativeArray<ushort> vox, int cx, int cy, int cz, ref int3 chunkSize,
            ushort waterId)
        {
            int neighbors = 0;
            int3 pos = new(cx - 1, cy, cz);
            if (ChunkGenerationUtils.InChunk(ref pos, ref chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            pos = new int3(cx + 1, cy, cz);
            if (ChunkGenerationUtils.InChunk(ref pos, ref chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            pos = new int3(cx, cy, cz + 1);
            if (ChunkGenerationUtils.InChunk(ref pos, ref chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            pos = new int3(cx, cy, cz - 1);
            if (ChunkGenerationUtils.InChunk(ref pos, ref chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            return neighbors > 0;
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

        private static bool CanPlaceTree(ref NativeArray<ushort> vox, int cx, int cy, int cz, ref int3 chunkSize,
            ushort logA,
            ushort logB)
        {
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                if (ox == 0 && oz == 0) continue;
                int tx = cx + ox;
                int tz = cz + oz;

                int3 pos = new(tx, cy, tz);
                if (!ChunkGenerationUtils.InChunk(ref pos, ref chunkSize)) continue;
                ushort v = vox[chunkSize.Flatten(tx, cy, tz)];
                if (v == logA || v == logB) return false;
            }

            return true;
        }

        private static void PlaceTree(ref NativeArray<ushort> vox, int x, int y, int z, int height, ushort idLog,
            ref Random rng, ref GeneratorConfig config, ref int3 chunkSize)
        {
            ushort idLeaves = config.Leaves;
            if (idLog == config.LogBirch)
            {
                idLeaves = config.LeavesOrange;
            }

            int sy = chunkSize.y;
            for (int i = 0; i < height && y + i < sy - 1; i++)
            {
                if (!ChunkGenerationUtils.InChunk(x, y + i, z, ref chunkSize)) break;
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
                    if (!ChunkGenerationUtils.InChunk(tx, yy, tz, ref chunkSize)) continue;
                    vox[chunkSize.Flatten(tx, yy, tz)] = idLeaves;
                }
            }

            if (idLog != config.LogBirch) return;

            for (int ox = -2; ox <= 2; ox++)
            for (int oz = -2; oz <= 2; oz++)
            {
                if (!(rng.NextFloat() < 0.12f)) continue;

                int bx = x + ox;
                int bz = z + oz;
                int by = y - 1;
                if (!ChunkGenerationUtils.InChunk(bx, by, bz, ref chunkSize)) continue;
                int groundIdx = chunkSize.Flatten(bx, by, bz);
                int idx = chunkSize.Flatten(bx, by + 1, bz);

                if (!ChunkGenerationUtils.InChunk(bx, by + 1, bz, ref chunkSize) || vox[idx] != 0 ||
                    !IsEarthLike(vox[groundIdx], ref config)) continue;

                int m = rng.NextInt(0, 100);
                vox[idx] = m < 50 ? config.MushroomBrown : (m < 80 ? config.MushroomRed : config.MushroomTan);
            }
        }

        private static void PlaceColumn(ref NativeArray<ushort> vox, int x, int y, int z, ref int3 chunkSize,
            int blockId,
            int count)
        {
            int sy = chunkSize.y;
            for (int i = 0; i < count && y + i < sy - 1; i++)
            {
                if (!ChunkGenerationUtils.InChunk(x, y + i, z, ref chunkSize)) break;
                vox[chunkSize.Flatten(x, y + i, z)] = (ushort)blockId;
            }
        }

        /// <summary>
        /// Places a simple palm-like tree structure at the given position using the provided random stream.
        /// </summary>
        /// <param name="vox">Voxel buffer to modify in place.</param>
        /// <param name="cx">X coordinate of the trunk base in chunk space.</param>
        /// <param name="cy">Y coordinate of the trunk base in chunk space.</param>
        /// <param name="cz">Z coordinate of the trunk base in chunk space.</param>
        /// <param name="rng">Random generator used to vary palm height and leaf placement.</param>
        /// <param name="chunkSize">Size of the chunk in voxels (x, y, z).</param>
        /// <param name="config">Generator configuration providing log and leaf voxel IDs.</param>
        internal static void PlacePalm(ref NativeArray<ushort> vox, int cx, int cy, int cz, ref Random rng,
            ref int3 chunkSize,
            ref GeneratorConfig config)
        {
            ushort log = config.LogOak;
            ushort leaves = config.Leaves;
            int h = rng.NextInt(3, 6);
            for (int i = 0; i < h; i++)
            {
                int y = cy + i;
                if (!ChunkGenerationUtils.InChunk(cx, y, cz, ref chunkSize)) break;
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
                if (!ChunkGenerationUtils.InChunk(x, y, z, ref chunkSize)) continue;
                vox[chunkSize.Flatten(x, y, z)] = leaves;
            }
        }
    }
}