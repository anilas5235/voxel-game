using Runtime.Engine.Noise;
using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

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

        // Biome noise tuning
        // make biomes larger (lower frequency) and increase separation
        private const float BiomeScale = 0.0012f;
        private const float BiomeExaggeration = 1.8f;
        private const int MountainSnowline = 215;


        // use a lower scale for caves so noise varies slower -> larger cave features
        private const float CaveScale = 0.04f; // 3D noise scale for caves
        private const int LavaLevel = 5;

        public void Execute(int index)
        {
            int3 position = Jobs[index];
            Data.Chunk chunk = GenerateChunkData(position);
            Results.TryAdd(position, chunk);
        }

        private struct ChunkColumn
        {
            public int Height;
            public Biome Biome;
        }

        private Data.Chunk GenerateChunkData(int3 chunkWordPos)
        {
            int volume = ChunkSize.x * ChunkSize.y * ChunkSize.z;
            int surfaceArea = ChunkSize.x * ChunkSize.z;

            // Compute water level once per chunk
            int waterLevel = Config.WaterLevel;

            // Temporary buffers for this chunk
            NativeArray<ushort> vox = new(volume, Allocator.Temp);
            NativeArray<ChunkColumn> chunkColumns = new(surfaceArea, Allocator.Temp);

            // Step A: prepare per-column maps (height, biome, river)
            PrepareChunkMaps(chunkWordPos, chunkColumns);

            // Step B: terrain fill (stone/dirt/top/water) into vox buffer
            FillTerrain(vox, chunkWordPos, waterLevel, chunkColumns);

            // Step C: carve caves
            CarveCaves(vox, chunkWordPos, chunkColumns);

            // Step D: place ores based on depth and exposure
            PlaceOres(vox);

            // Step E: vegetation and micro-structures on surface
            PlaceVegetationAndStructures(vox, chunkColumns, chunkWordPos);

            // Defensive validation: remove any surface-vegetation that ended up not being on a valid earth-like surface
            ValidateSurfaceVegetation(vox, chunkColumns);


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
            if (PositionIsInChunk(pos, chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            pos = new int3(cx + 1, cy, cz);
            // +x
            if (PositionIsInChunk(pos, chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            pos = new int3(cx, cy, cz + 1);
            // +z
            if (PositionIsInChunk(pos, chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            pos = new int3(cx, cy, cz - 1);
            // -z
            if (PositionIsInChunk(pos, chunkSize))
            {
                ushort voxel = vox[chunkSize.Flatten(pos)];
                if (voxel != 0 && voxel != waterId)
                    neighbors++;
            }

            return neighbors > 0;
        }

        private static bool PositionIsInChunk(int3 pos, int3 chunkSize)
        {
            return pos.x >= 0 && pos.x < chunkSize.x &&
                   pos.y >= 0 && pos.y < chunkSize.y &&
                   pos.z >= 0 && pos.z < chunkSize.z;
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

                if (!PositionIsInChunk(new int3(tx, cy, tz), chunkSize)) continue;
                ushort v = vox[chunkSize.Flatten(tx, cy, tz)];
                if (v == logA || v == logB) return false;
            }

            return true;
        }

        private void PrepareChunkMaps(int3 chunkWordPos, NativeArray<ChunkColumn> chunkColumns)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int i = GetColumIdx(x, z, sz);
                float2 worldPos = new(chunkWordPos.x + x, chunkWordPos.z + z);

                float2 noiseSamplePos = worldPos + new float2(-RandomSeed, RandomSeed);

                float humidity = noise.cnoise((noiseSamplePos - 789f) * BiomeScale);
                float temperature = noise.cnoise((noiseSamplePos + 543) * BiomeScale);

                float rawHeight = NoiseProfile.GetNoise(worldPos);

                int height = math.clamp(
                    (int)(math.lerp(1 / 3f, .85f,
                        rawHeight * math.min(1f, 1f - temperature) * math.min(1f, 1f - humidity)) * ChunkSize.y),
                    1,
                    ChunkSize.y - 1);

                humidity = humidity * .5f + .5f;
                temperature = temperature * .5f + .5f;

                chunkColumns[i] = new ChunkColumn()
                {
                    Height = height,
                    Biome = BiomeHelper.SelectBiome(temperature, humidity,
                        (float)height / sy, height, Config.WaterLevel)
                };
            }
        }

        private void FillTerrain(NativeArray<ushort> vox, int3 chunkWorldPos, int waterLevel,
            NativeArray<ChunkColumn> chunkColumns)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;
            const ushort air = 0;
            ushort waterBlock = Config.Water;
            ushort stone = Config.Stone;
            ushort dirt = Config.Dirt;

            uint seed = (uint)((chunkWorldPos.x * 73856093) ^ (chunkWorldPos.z * 19349663) ^ RandomSeed ^ 0x85ebca6b);
            Random rng = new(seed == 0 ? 1u : seed);

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int i = GetColumIdx(x, z, sz);

                ChunkColumn col = chunkColumns[i];

                SelectSurfaceMaterials(col, ref rng, out ushort top, out ushort under, out ushort st);
                if (st == 0) st = stone;
                if (under == 0) under = dirt;
                if (top == 0) top = dirt;

                int gy = col.Height;

                for (int y = 0; y < sy; y++)
                {
                    ushort v;
                    if (y < gy - 4) v = st;
                    else if (y < gy) v = under;
                    else if (y == gy) v = gy < waterLevel ? waterBlock : top;
                    else v = y < waterLevel ? waterBlock : air;

                    vox[ChunkSize.Flatten(x, y, z)] = v;
                }
            }
        }

        private static int GetColumIdx(int x, int z, int sz)
        {
            return z + x * sz;
        }

        private void CarveCaves(NativeArray<ushort> vox, int3 origin, NativeArray<ChunkColumn> chunkColumns)
        {
            int sx = ChunkSize.x;
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int height = chunkColumns[GetColumIdx(x, z, sz)].Height;
                for (int y = 2; y <= height; y++)
                {
                    ChunkSize.Flatten(x, y, z);
                    int idx = ChunkSize.Flatten(x, y, z);

                    float3 noiseSamplePos = (origin + new float3(x + RandomSeed, y - RandomSeed, z + RandomSeed)) *
                                            CaveScale;

                    float sCaveNoise = noise.snoise(noiseSamplePos) * .5f + .5f;
                    float cellNoise = noise.cellular(noiseSamplePos).x * .5f + .5f;

                    bool sCarve = math.square(sCaveNoise) + math.square(cellNoise) >
                                  math.lerp(.8f, 1.3f, math.square(y / (float)height));

                    // carve if noise below threshold
                    if (sCarve)
                    {
                        vox[idx] = 0;
                        if (y <= LavaLevel)
                        {
                            vox[idx] = Config.Lava;
                        }
                    }
                }
            }
        }

        private void PlaceOres(NativeArray<ushort> vox)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;
            ushort stone = Config.Stone;
            ushort oreCoal = Config.StoneCoalOre;
            ushort oreIron = Config.StoneIronGreenOre;
            ushort oreGold = Config.StoneGoldOre;
            ushort oreDiamond = Config.StoneDiamondOre;

            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            for (int y = 2; y < sy - 2; y++)
            {
                ChunkSize.Flatten(x, y, z);
                int idx = ChunkSize.Flatten(x, y, z);
                if (vox[idx] != stone) continue;

                float depthNorm = 1f - y / (float)sy;
                float oreNoise = math.abs(noise.snoise(new float3(RandomSeed + x, RandomSeed + y,
                    RandomSeed - z) * 0.12f));
                float roll = math.max(0f, oreNoise * depthNorm);

                vox[idx] = roll switch
                {
                    > 0.85f when y < sy * 0.15f => oreDiamond,
                    > 0.7f when y < sy * 0.35f => oreGold,
                    > 0.6f => oreIron,
                    > 0.45f => oreCoal,
                    _ => vox[idx]
                };
            }
        }

        private void PlaceVegetationAndStructures(NativeArray<ushort> vox, NativeArray<ChunkColumn> chunkColumns,
            int3 origin)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;
            ushort grass = Config.Grass;
            ushort sand = Config.Sand != 0 ? Config.Sand : Config.SandGrey;
            ushort dirt = Config.Dirt != 0
                ? Config.Dirt
                : Config.StoneSandy != 0
                    ? Config.StoneSandy
                    : Config.Stone != 0
                        ? Config.Stone
                        : Config.Rock;
            ushort leaves = Config.Leaves != 0
                ? Config.Leaves
                : Config.LeavesOrange != 0
                    ? Config.LeavesOrange
                    : (ushort)0;
            ushort logOak = Config.LogOak;
            ushort logBirch = Config.LogBirch;
            ushort cactus = Config.Cactus;
            ushort mushroom = Config.MushroomBrown;
            ushort grassF = Config.GrassF;
            ushort snowyDirt = Config.DirtSnowy;
            ushort visibleWater = Config.Water;

            // Deterministic per-chunk RNG: combine global seed with origin so different seeds produce different worlds
            uint seed = (uint)((origin.x * 73856093) ^ (origin.z * 19349663) ^ RandomSeed ^ 0x85ebca6b);
            Random rng = new(seed == 0 ? 1u : seed);

            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            {
                int gi = GetColumIdx(x, z, sz);
                ChunkColumn chunkCol = chunkColumns[gi];
                int gy = chunkCol.Height;
                if (gy <= 0 || gy >= sy - 2) continue;

                Biome biome = chunkCol.Biome;
                ushort surface = vox[ChunkSize.Flatten(x, gy, z)];
                if (surface == 0 || (visibleWater != 0 && surface == visibleWater)) continue;

                // Neu: nur auf erdeartigen Blöcken Vegetation platzieren
                if (!IsEarthLike(surface)) continue;

                switch (biome)
                {
                    case Biome.Forest:
                        // less dense forest: reduce chance and increase size variability
                        if (surface == grass && rng.NextFloat() < 0.12f)
                        {
                            int h = rng.NextInt(5, 9);
                            ushort chosenLog = rng.NextInt(0, 100) < 75 ? logOak : logBirch;
                            if (CanPlaceTree(vox, x, gy + 1, z, ChunkSize, logOak, logBirch))
                            {
                                PlaceTree(vox, x, gy + 1, z, h, chosenLog, leaves, ref rng);
                            }
                        }
                        else if (surface == grass && rng.NextFloat() < 0.12f && grassF != 0)
                        {
                            int targetIdx = ChunkSize.Flatten(x, gy + 1, z);
                            // Guard: ensure the surface cell still matches expected surface and target is free
                            if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 &&
                                vox[ChunkSize.Flatten(x, gy, z)] == surface && IsEarthLike(surface)
                                && SurfaceHasNeighbor(vox, x, gy, z, ChunkSize, visibleWater)) vox[targetIdx] = grassF;
                        }

                        break;
                    case Biome.Plains:
                        if (surface == grass && rng.NextFloat() < 0.015f)
                        {
                            int h = rng.NextInt(6, 10);
                            ushort chosenLog = rng.NextInt(0, 100) < 80 ? logOak : logBirch;
                            if (CanPlaceTree(vox, x, gy + 1, z, ChunkSize, logOak, logBirch))
                            {
                                PlaceTree(vox, x, gy + 1, z, h, chosenLog, leaves, ref rng);
                            }
                        }
                        else if (surface == grass && rng.NextFloat() < 0.05f && grassF != 0)
                        {
                            int targetIdx = ChunkSize.Flatten(x, gy + 1, z);
                            if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 &&
                                vox[ChunkSize.Flatten(x, gy, z)] == surface && IsEarthLike(surface)
                                && SurfaceHasNeighbor(vox, x, gy, z, ChunkSize, visibleWater)) vox[targetIdx] = grassF;
                        }

                        break;
                    case Biome.Jungle:
                        if (surface == grass && rng.NextFloat() < 0.6f)
                        {
                            int h = rng.NextInt(7, 11);
                            if (CanPlaceTree(vox, x, gy + 1, z, ChunkSize, logOak, logBirch))
                            {
                                PlaceTree(vox, x, gy + 1, z, h, logOak, leaves, ref rng);
                            }
                        }

                        break;
                    case Biome.Desert:
                        // cacti in desert
                        if ((surface == sand || surface == dirt) && rng.NextFloat() < 0.035f && cactus != 0)
                        {
                            // ensure base is still correct and above-block is empty before placing column
                            if (InBounds(x, gy + 1, z) && vox[ChunkSize.Flatten(x, gy, z)] == surface &&
                                IsEarthLike(surface) && vox[ChunkSize.Flatten(x, gy + 1, z)] == 0)
                                PlaceColumn(vox, x, gy + 1, z, cactus, rng.NextInt(1, 5));
                        }

                        // dry shrubs / dead bushes in desert
                        if ((surface == sand || surface == dirt) && rng.NextFloat() < 0.08f && Config.GrassFDry != 0)
                        {
                            int targetIdx = ChunkSize.Flatten(x, gy + 1, z);
                            if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 &&
                                vox[ChunkSize.Flatten(x, gy, z)] == surface && IsEarthLike(surface)
                                && SurfaceHasNeighbor(vox, x, gy, z, ChunkSize, visibleWater))
                                vox[targetIdx] = Config.GrassFDry;
                        }

                        break;
                    case Biome.Swamp:
                        if (rng.NextFloat() < 0.06f && mushroom != 0)
                        {
                            // only place a mushroom if the target cell above ground is empty (no floating mushrooms)
                            int targetIdx = ChunkSize.Flatten(x, gy + 1, z);
                            if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 &&
                                vox[ChunkSize.Flatten(x, gy, z)] == surface && IsEarthLike(surface)
                                && SurfaceHasNeighbor(vox, x, gy, z, ChunkSize, visibleWater))
                            {
                                // choose among mushroom variants
                                int m = rng.NextInt(0, 100);
                                if (m < 50 && Config.MushroomBrown != 0) vox[targetIdx] = Config.MushroomBrown;
                                else if (m < 80 && Config.MushroomRed != 0) vox[targetIdx] = Config.MushroomRed;
                                else if (Config.MushroomTan != 0) vox[targetIdx] = Config.MushroomTan;
                            }
                        }

                        break;
                    case Biome.HighStone:
                    case Biome.GreyMountain:
                        // sparse vegetation on high rocky biomes
                        if (rng.NextFloat() < 0.02f && surface == dirt && grassF != 0)
                        {
                            int targetIdx = ChunkSize.Flatten(x, gy + 1, z);
                            if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 &&
                                vox[ChunkSize.Flatten(x, gy, z)] == surface && IsEarthLike(surface)
                                && SurfaceHasNeighbor(vox, x, gy, z, ChunkSize, visibleWater)) vox[targetIdx] = grassF;
                        }

                        break;
                    case Biome.RedDesert:
                        // red desert: more cacti (we add more later), but keep big structures rare here
                        if ((surface == sand || surface == dirt) && rng.NextFloat() < 0.02f && cactus != 0)
                        {
                            if (InBounds(x, gy + 1, z) && vox[ChunkSize.Flatten(x, gy, z)] == surface &&
                                IsEarthLike(surface) && vox[ChunkSize.Flatten(x, gy + 1, z)] == 0)
                                PlaceColumn(vox, x, gy + 1, z, cactus, rng.NextInt(1, 6));
                        }

                        if ((surface == sand || surface == dirt) && Config.GrassFDry != 0 && rng.NextFloat() < 0.12f)
                        {
                            int tIdx = ChunkSize.Flatten(x, gy + 1, z);
                            if (InBounds(x, gy + 1, z) && vox[tIdx] == 0) vox[tIdx] = Config.GrassFDry;
                        }

                        // keep pyramids/oases very rare here (we place better controlled versions later)
                        if (rng.NextFloat() < 0.000005f)
                        {
                            PlacePyramid(vox, x, gy + 1, z, 3, Config.SandRed != 0 ? Config.SandRed : Config.Sand);
                        }

                        if (rng.NextFloat() < 0.000002f)
                        {
                            PlaceOasis(vox, x, gy, z);
                        }

                        break;
                    case Biome.Beach:
                        // place occasional palm and driftwood
                        if (rng.NextFloat() < 0.02f && InBounds(x, gy + 1, z) &&
                            vox[ChunkSize.Flatten(x, gy + 1, z)] == 0)
                        {
                            PlacePalm(vox, x, gy + 1, z, ref rng);
                        }

                        break;
                    case Biome.Ice:
                        // freeze surface water and occasionally spawn igloos
                        if (visibleWater != 0 && surface == visibleWater && InBounds(x, gy, z))
                        {
                            // freeze the surface cell
                            vox[ChunkSize.Flatten(x, gy, z)] = Config.Ice;
                        }

                        if (rng.NextFloat() < 0.0007f)
                        {
                            PlaceIgloo(vox, x, gy + 1, z);
                        }

                        break;
                    case Biome.Tundra:
                        if (surface == snowyDirt && rng.NextFloat() < 0.08f && Config.GrassFDead != 0)
                        {
                            int targetIdx = ChunkSize.Flatten(x, gy + 1, z);
                            if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 &&
                                vox[ChunkSize.Flatten(x, gy, z)] == surface && IsEarthLike(surface)
                                && SurfaceHasNeighbor(vox, x, gy, z, ChunkSize, visibleWater))
                                vox[targetIdx] = Config.GrassFDead;
                        }

                        break;
                }

                // Mineshaft entrances in plains/forest/mountain at low chance
                if (biome is Biome.Plains or Biome.Forest or Biome.Mountain or Biome.HighStone &&
                    rng.NextFloat() < 0.0009f)
                {
                    PlaceMineShaft(vox, x, gy, z, rng.NextInt(6, 20));
                }

                // Only place flowers in Plains and only on actual grass blocks (avoid caves/other biomes)
                // Also guard against ID collisions with wheat stages.
                if (biome == Biome.Plains && Config.Flowers != 0 && surface == grass
                    && Config.Flowers != Config.WheatStage1 && Config.Flowers != Config.WheatStage2
                    && Config.Flowers != Config.WheatStage3 && Config.Flowers != Config.WheatStage4)
                {
                    int targetIdx = ChunkSize.Flatten(x, gy + 1, z);
                    // slightly higher chance specifically for plains
                    const float flowerChancePlains = 0.06f;
                    if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 && vox[ChunkSize.Flatten(x, gy, z)] == surface &&
                        IsEarthLike(surface)
                        && rng.NextFloat() < flowerChancePlains &&
                        SurfaceHasNeighbor(vox, x, gy, z, ChunkSize, visibleWater))
                    {
                        vox[targetIdx] = Config.Flowers;
                    }
                }

                // Wheat clusters near water in plains
                if (biome == Biome.Plains && surface == grass && Config.WheatStage1 != 0 &&
                    SurfaceHasNeighbor(vox, x, gy, z, ChunkSize, visibleWater))
                {
                    // reduce overall wheat frequency
                    if (rng.NextFloat() < 0.02f)
                    {
                        int cluster = rng.NextInt(1, 4);
                        for (int wx = -cluster; wx <= cluster; wx++)
                        for (int wz = -cluster; wz <= cluster; wz++)
                        {
                            int tx = x + wx;
                            int tz = z + wz;
                            if (tx < 0 || tx >= sx || tz < 0 || tz >= sz) continue;
                            int tgi = GetColumIdx(tx, tz, sz);
                            int tgy = chunkColumns[tgi].Height;
                            // require that target column's ground is earth-like and not underwater
                            if (tgy <= 0 || tgy >= sy - 1) continue;
                            int belowIdx = ChunkSize.Flatten(tx, tgy, tz);
                            ushort below = vox[belowIdx];
                            if (!IsEarthLike(below)) continue;
                            int tIdx = ChunkSize.Flatten(tx, tgy + 1, tz);
                            if (vox[tIdx] == 0)
                            {
                                int stage = rng.NextInt(1, 5);
                                ushort id = stage switch
                                {
                                    1 => Config.WheatStage1,
                                    2 => Config.WheatStage2,
                                    3 => Config.WheatStage3,
                                    _ => Config.WheatStage4
                                };
                                vox[tIdx] = id;
                            }
                        }
                    }
                }

                // Slightly increase chances for some structures so they are more likely to appear during testing
                // RedDesert: pyramids and oases
                if (biome == Biome.RedDesert)
                {
                    // more cacti
                    if ((surface == sand || surface == dirt) && rng.NextFloat() < 0.06f && cactus != 0)
                    {
                        if (InBounds(x, gy + 1, z) && vox[ChunkSize.Flatten(x, gy, z)] == surface &&
                            IsEarthLike(surface) &&
                            vox[ChunkSize.Flatten(x, gy + 1, z)] == 0)
                            PlaceColumn(vox, x, gy + 1, z, cactus, rng.NextInt(1, 6));
                    }

                    // fewer oases
                    if (rng.NextFloat() < 0.0002f)
                    {
                        PlaceOasis(vox, x, gy, z);
                    }

                    // occasional small pyramid (still rare)
                    if (rng.NextFloat() < 0.001f)
                    {
                        PlacePyramid(vox, x, gy + 1, z, 3, Config.SandRed != 0 ? Config.SandRed : Config.Sand);
                    }
                }

                // Ocean shipwrecks slightly more frequent
                if (biome == Biome.Ocean && visibleWater != 0 && surface == visibleWater)
                {
                    if (rng.NextFloat() < 0.001f)
                    {
                        PlaceShipwreck(vox, x, gy + 1, z);
                    }
                }
            }
        }

        // Helper: returns true if the given block id is an earth-like ground block suitable for vegetation
        private bool IsEarthLike(ushort id)
        {
            if (id == 0) return false;
            if (id == Config.Grass) return true;
            if (id == Config.Dirt) return true;
            if (Config.DirtSnowy != 0 && id == Config.DirtSnowy) return true;
            if (id == Config.Sand || id == Config.SandGrey || (Config.SandRed != 0 && id == Config.SandRed))
                return true;
            if (Config.StoneSandy != 0 && id == Config.StoneSandy) return true;
            if (Config.SandStoneRed != 0 && id == Config.SandStoneRed) return true;
            if (Config.SandStoneRedSandy != 0 && id == Config.SandStoneRedSandy) return true;
            // treat grass-like fertile surfaces as earth
            if (Config.GrassF != 0 && id == Config.GrassF) return true;
            if (Config.GrassFDry != 0 && id == Config.GrassFDry) return true;
            return false;
        }

        private void PlaceTree(NativeArray<ushort> vox, int x, int y, int z, int height, int idLog, int idLeaves,
            ref Random rng)
        {
            // If this is a birch trunk, prefer orange leaves when configured
            if (idLog == Config.LogBirch && Config.LeavesOrange != 0)
            {
                idLeaves = Config.LeavesOrange;
            }

            if (idLog == 0 || idLeaves == 0) return;
            int sy = ChunkSize.y;
            // Trunk: ensure inside chunk before writing
            for (int i = 0; i < height && y + i < sy - 1; i++)
            {
                if (!InBounds(x, y + i, z)) break;
                vox[ChunkSize.Flatten(x, y + i, z)] = (ushort)idLog;
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
                    if (!InBounds(tx, yy, tz)) continue;
                    vox[ChunkSize.Flatten(tx, yy, tz)] = (ushort)idLeaves;
                }
            }

            // If birch trunk, sometimes spawn mushrooms near the base (various types)
            if (idLog == Config.LogBirch)
            {
                for (int ox = -2; ox <= 2; ox++)
                for (int oz = -2; oz <= 2; oz++)
                {
                    if (rng.NextFloat() < 0.12f)
                    {
                        int bx = x + ox;
                        int bz = z + oz;
                        int by = y - 1;
                        if (!InBounds(bx, by, bz)) continue;
                        int groundIdx = ChunkSize.Flatten(bx, by, bz);
                        int idx = ChunkSize.Flatten(bx, by + 1, bz);
                        // Neu: nur auf erdeartigen Böden Pilze platzieren und nicht auf bereits belegte Zellen
                        if (!InBounds(bx, by + 1, bz)) continue;
                        if (!IsEarthLike(vox[groundIdx])) continue;
                        if (vox[idx] == 0)
                        {
                            int m = rng.NextInt(0, 100);
                            if (m < 50 && Config.MushroomBrown != 0) vox[idx] = Config.MushroomBrown;
                            else if (m < 80 && Config.MushroomRed != 0) vox[idx] = Config.MushroomRed;
                            else if (Config.MushroomTan != 0) vox[idx] = Config.MushroomTan;
                        }
                    }
                }
            }
        }

        private void PlaceColumn(NativeArray<ushort> vox, int x, int y, int z, int blockId, int count)
        {
            int sy = ChunkSize.y;
            for (int i = 0; i < count && y + i < sy - 1; i++)
            {
                if (!InBounds(x, y + i, z)) break;
                vox[ChunkSize.Flatten(x, y + i, z)] = (ushort)blockId;
            }
        }

        private bool InBounds(int x, int y, int z)
        {
            return x >= 0 && x < ChunkSize.x && z >= 0 && z < ChunkSize.z && y >= 0 && y < ChunkSize.y;
        }

        private void SelectSurfaceMaterials(in ChunkColumn col, ref Random rng, out ushort topBlock,
            out ushort underBlock,
            out ushort stone)
        {
            stone = Config.Stone != 0 ? Config.Stone : Config.Rock;
            ushort dirt = Config.Dirt != 0 ? Config.Dirt : Config.StoneSandy != 0 ? Config.StoneSandy : stone;

            topBlock = Config.Grass != 0 ? Config.Grass : dirt;
            underBlock = dirt;

            switch (col.Biome)
            {
                case Biome.Desert:
                    topBlock = Config.SandRed != 0 ? Config.SandRed : Config.Sand != 0 ? Config.Sand : underBlock;
                    underBlock = Config.SandStoneRed != 0
                        ? Config.SandStoneRed
                        : Config.SandStoneRedSandy != 0
                            ? Config.SandStoneRedSandy
                            : underBlock;
                    break;
                case Biome.RedDesert:
                    topBlock = Config.SandRed != 0 ? Config.SandRed : Config.Sand != 0 ? Config.Sand : underBlock;
                    underBlock = Config.SandStoneRed != 0
                        ? Config.SandStoneRed
                        : Config.SandStoneRedSandy != 0
                            ? Config.SandStoneRedSandy
                            : underBlock;
                    break;
                case Biome.Beach:
                    topBlock = Config.Sand != 0 ? Config.Sand : Config.SandGrey != 0 ? Config.SandGrey : underBlock;
                    underBlock = Config.SandStoneRedSandy != 0 ? Config.SandStoneRedSandy : underBlock;
                    break;
                case Biome.Ice:
                    // cold rocky/icy surfaces use snowy dirt or stone
                    topBlock = Config.DirtSnowy != 0
                        ? Config.DirtSnowy
                        : Config.StoneSnowy != 0
                            ? Config.StoneSnowy
                            : topBlock;
                    underBlock = dirt;
                    break;
                case Biome.Snow:
                    topBlock = Config.DirtSnowy != 0
                        ? Config.DirtSnowy
                        : Config.StoneSnowy != 0
                            ? Config.StoneSnowy
                            : topBlock;
                    underBlock = dirt;
                    break;
                case Biome.Swamp:
                    topBlock = dirt;
                    underBlock = dirt;
                    break;
                case Biome.Mountain:
                    topBlock = col.Height >= MountainSnowline
                        ? Config.StoneSnowy
                        : stone;
                    underBlock = stone;
                    break;
                case Biome.HighStone:
                    topBlock = col.Height >= MountainSnowline
                        ? Config.Snow
                        : Config.StoneGrey;
                    underBlock = stone;
                    break;
                case Biome.GreyMountain:
                    topBlock = col.Height >= MountainSnowline
                        ? Config.Snow
                        : Config.StoneGrey;
                    underBlock = stone;
                    break;
                case Biome.Tundra:
                    topBlock = rng.NextFloat() < .1f ? Config.Dirt :
                        Config.DirtSnowy != 0 ? Config.DirtSnowy : topBlock;
                    underBlock = dirt;
                    break;
            }
        }

        // Remove surface vegetation blocks that are not placed directly above a valid ground block (ground + 1)
        private void ValidateSurfaceVegetation(NativeArray<ushort> vox, NativeArray<ChunkColumn> chunkColumns)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;

            // copy config fields to locals to avoid capturing 'this' inside local functions
            ushort cfgFlowers = Config.Flowers;
            ushort cfgGrassF = Config.GrassF;
            ushort cfgGrassFDry = Config.GrassFDry;
            ushort cfgMushroomBrown = Config.MushroomBrown;
            ushort cfgMushroomRed = Config.MushroomRed;
            ushort cfgMushroomTan = Config.MushroomTan;
            ushort cfgGrass = Config.Grass;
            ushort cfgCactus = Config.Cactus;
            ushort cfgDirt = Config.Dirt;
            ushort cfgDirtSnowy = Config.DirtSnowy;
            ushort cfgSand = Config.Sand;
            ushort cfgSandGrey = Config.SandGrey;
            ushort cfgSandRed = Config.SandRed;
            ushort cfgStoneSandy = Config.StoneSandy;
            ushort cfgSandStoneRed = Config.SandStoneRed;
            ushort cfgSandStoneRedSandy = Config.SandStoneRedSandy;

            // helper to check vegetation IDs using local copies only
            bool IsSurfaceVegLocal(ushort id)
            {
                if (id == 0) return false;
                if (cfgFlowers != 0 && id == cfgFlowers) return true;
                if (cfgGrassF != 0 && id == cfgGrassF) return true;
                if (cfgGrassFDry != 0 && id == cfgGrassFDry) return true;
                if (cfgMushroomBrown != 0 && id == cfgMushroomBrown) return true;
                if (cfgMushroomRed != 0 && id == cfgMushroomRed) return true;
                if (cfgMushroomTan != 0 && id == cfgMushroomTan) return true;
                if (cfgGrass != 0 && id == cfgGrass) return false; // grass is ground, not surface plant
                if (cfgCactus != 0 && id == cfgCactus) return true; // treat cactus as surface vegetation for validation
                return false;
            }

            // local version of IsEarthLike using only local copies
            bool IsEarthLikeLocal(ushort id)
            {
                if (id == 0) return false;
                if (cfgGrass != 0 && id == cfgGrass) return true;
                if (cfgDirt != 0 && id == cfgDirt) return true;
                if (cfgDirtSnowy != 0 && id == cfgDirtSnowy) return true;
                if (cfgSand != 0 && id == cfgSand) return true;
                if (cfgSandGrey != 0 && id == cfgSandGrey) return true;
                if (cfgSandRed != 0 && id == cfgSandRed) return true;
                if (cfgStoneSandy != 0 && id == cfgStoneSandy) return true;
                if (cfgSandStoneRed != 0 && id == cfgSandStoneRed) return true;
                if (cfgSandStoneRedSandy != 0 && id == cfgSandStoneRedSandy) return true;
                if (cfgGrassF != 0 && id == cfgGrassF) return true;
                if (cfgGrassFDry != 0 && id == cfgGrassFDry) return true;
                return false;
            }

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int gi = GetColumIdx(x, z, sz);
                int gy = chunkColumns[gi].Height;
                if (gy < 0 || gy >= sy - 1) continue;

                int expectedY = gy + 1;
                // check only the column near expected surface and a small neighborhood to be safe
                for (int y = 0; y < sy; y++)
                {
                    int idx = ChunkSize.Flatten(x, y, z);
                    ushort id = vox[idx];
                    if (!IsSurfaceVegLocal(id)) continue;

                    // valid only if exactly at expectedY and block below is earth-like
                    if (y != expectedY)
                    {
                        vox[idx] = 0;
                        continue;
                    }

                    int belowIdx = ChunkSize.Flatten(x, gy, z);
                    ushort below = vox[belowIdx];
                    if (!IsEarthLikeLocal(below))
                    {
                        vox[idx] = 0;
                    }
                }
            }
        }

        // Simple helper: place a small square pyramid centered on (cx,cy,cz)
        private void PlacePyramid(NativeArray<ushort> vox, int cx, int cy, int cz, int radius, ushort block)
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
                    if (!InBounds(x, y, z)) continue;
                    vox[ChunkSize.Flatten(x, y, z)] = block;
                }
            }
        }

        private void PlaceOasis(NativeArray<ushort> vox, int cx, int cy, int cz)
        {
            ushort water = Config.Water;
            ushort grass = Config.Grass;
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                int x = cx + ox;
                int y = cy + 1;
                int z = cz + oz;
                if (!InBounds(x, y, z)) continue;
                vox[ChunkSize.Flatten(x, y, z)] = water;
                // surround with grass where possible
                int sx0 = x + 1;
                int sx1 = x - 1;
                int sz0 = z + 1;
                int sz1 = z - 1;
                if (InBounds(sx0, y, z) && vox[ChunkSize.Flatten(sx0, y, z)] == 0)
                    vox[ChunkSize.Flatten(sx0, y, z)] = grass;
                if (InBounds(sx1, y, z) && vox[ChunkSize.Flatten(sx1, y, z)] == 0)
                    vox[ChunkSize.Flatten(sx1, y, z)] = grass;
                if (InBounds(x, y, sz0) && vox[ChunkSize.Flatten(x, y, sz0)] == 0)
                    vox[ChunkSize.Flatten(x, y, sz0)] = grass;
                if (InBounds(x, y, sz1) && vox[ChunkSize.Flatten(x, y, sz1)] == 0)
                    vox[ChunkSize.Flatten(x, y, sz1)] = grass;
            }

            // plant a couple of small palm-like trees at edges
            Random r1 = new((uint)(cx * 73856093 ^ cz * 19349663));
            Random r2 = new((uint)((cx + 7) * 73856093 ^ (cz - 5) * 19349663));
            PlacePalm(vox, cx - 2, cy + 1, cz, ref r1);
            PlacePalm(vox, cx + 2, cy + 1, cz, ref r2);
        }

        private void PlaceIgloo(NativeArray<ushort> vox, int cx, int cy, int cz)
        {
            ushort snow = Config.Snow;
            if (snow == 0) return;
            // simple 3x3 dome
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            for (int oy = 0; oy <= 2; oy++)
            {
                int x = cx + ox;
                int y = cy + oy;
                int z = cz + oz;
                if (!InBounds(x, y, z)) continue;
                // hollow interior
                if (oy == 0 && ox == 0 && oz == 0) continue;
                vox[ChunkSize.Flatten(x, y, z)] = snow;
            }

            // doorway
            if (InBounds(cx, cy + 1, cz - 1)) vox[ChunkSize.Flatten(cx, cy + 1, cz - 1)] = 0;
        }

        private void PlaceShipwreck(NativeArray<ushort> vox, int cx, int cy, int cz)
        {
            ushort planks = Config.Planks;
            if (planks == 0) return;
            for (int ox = -2; ox <= 2; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                int x = cx + ox;
                int y = cy + 1 + math.min(math.abs(ox), 1);
                int z = cz + oz;
                if (!InBounds(x, y, z)) continue;
                vox[ChunkSize.Flatten(x, y, z)] = planks;
            }
        }

        private void PlacePalm(NativeArray<ushort> vox, int cx, int cy, int cz, ref Random rng)
        {
            ushort log = Config.LogOak != 0 ? Config.LogOak : Config.Planks;
            ushort leaves = Config.Leaves != 0
                ? Config.Leaves
                : Config.LeavesOrange != 0
                    ? Config.LeavesOrange
                    : (ushort)0;
            if (log == 0) return;
            int h = rng.NextInt(3, 6);
            for (int i = 0; i < h; i++)
            {
                int y = cy + i;
                if (!InBounds(cx, y, cz)) break;
                vox[ChunkSize.Flatten(cx, y, cz)] = log;
            }

            if (leaves != 0)
            {
                int top = cy + h - 1;
                for (int ox = -2; ox <= 2; ox++)
                for (int oz = -2; oz <= 2; oz++)
                {
                    if (math.abs(ox) + math.abs(oz) > 3) continue;
                    int x = cx + ox;
                    int y = top + rng.NextInt(0, 2);
                    int z = cz + oz;
                    if (!InBounds(x, y, z)) continue;
                    vox[ChunkSize.Flatten(x, y, z)] = leaves;
                }
            }
        }

        // Simple mineshaft entrance: vertical shaft with a small wooden rim and occasional ladder-like planks
        private void PlaceMineShaft(NativeArray<ushort> vox, int cx, int groundY, int cz, int depth)
        {
            ushort wood = Config.Planks != 0 ? Config.Planks : (ushort)0;
            for (int y = groundY; y > math.max(1, groundY - depth); y--)
            {
                if (!InBounds(cx, y, cz)) continue;
                // carve a 1x1 shaft
                vox[ChunkSize.Flatten(cx, y, cz)] = 0;
                // occasionally place wooden support every 3 blocks
                if (wood != 0 && (groundY - y) % 3 == 0 && InBounds(cx + 1, y, cz))
                    vox[ChunkSize.Flatten(cx + 1, y, cz)] = wood;
                if (wood != 0 && (groundY - y) % 5 == 0 && InBounds(cx - 1, y, cz))
                    vox[ChunkSize.Flatten(cx - 1, y, cz)] = wood;
            }

            // create small entrance on surface
            if (InBounds(cx, groundY + 1, cz)) vox[ChunkSize.Flatten(cx, groundY + 1, cz)] = 0;
        }
    }
}