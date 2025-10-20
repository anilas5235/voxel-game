using Runtime.Engine.Noise;
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

        [ReadOnly] public uint RandomSeed;

        [ReadOnly] public GeneratorConfig Config;

        // Biome noise tuning
        private const float BiomeScale = 0.003f;
        private const float BiomeExaggeration = 1.5f;
        private const float MountainSnowline = 0.75f; // normalized elevation

        // Height field layering (ported from WorldGeneration)
        private const float ContinentScale = 0.0012f;
        private const float HillScale = 0.01f;
        private const float DetailScale = 0.03f;

        // Extra world-noise params (ported from old WorldGeneration)
        private const float RiverScale = 0.0008f;
        private const float RiverThreshold = -0.6f; // snoise threshold for river channels
        private const float CaveScale = 0.08f; // 3D noise scale for caves

        public void Execute(int index)
        {
            int3 position = Jobs[index];
            Data.Chunk chunk = GenerateChunkData(position);
            Results.TryAdd(position, chunk);
        }

        private Data.Chunk GenerateChunkData(int3 origin)
        {
            int sx = ChunkSize.x;
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
            int volume = sx * sy * sz;

            // Compute water level once per chunk
            int waterLevel = Config.WaterLevel;

            // Temporary buffers for this chunk
            NativeArray<ushort> vox = new NativeArray<ushort>(volume, Allocator.Temp);
            NativeArray<int> ground = new NativeArray<int>(sx * sz, Allocator.Temp);
            NativeArray<byte> biomeMap = new NativeArray<byte>(sx * sz, Allocator.Temp);
            NativeArray<byte> riverMap = new NativeArray<byte>(sx * sz, Allocator.Temp);

            // Step A: prepare per-column maps (height, biome, river)
            PrepareChunkMaps(origin, waterLevel, ground, biomeMap, riverMap);

            // Step B: terrain fill (stone/dirt/top/water) into vox buffer
            FillTerrain(vox, waterLevel, ground, biomeMap, riverMap);

            // Step C: carve caves (3D snoise + worm tunnels)
            CarveCaves(vox, origin, ground, riverMap);

            // Step D: place ores based on depth and exposure
            PlaceOres(vox);

            // Step E: vegetation and micro-structures on surface
            PlaceVegetationAndStructures(vox, ground, biomeMap);

            // Emit RLE from vox buffer in (x,z,y) order to Data.Chunk
            Data.Chunk data = new(origin, ChunkSize);
            ushort last = 0;
            int run = 0;
            bool hasLast = false;
            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            for (int y = 0; y < sy; y++)
            {
                ushort v = vox[Index(x, y, z, sy, sz)];
                if (hasLast && v == last)
                {
                    run++;
                }
                else
                {
                    if (hasLast) data.AddVoxels(last, run);
                    last = v;
                    run = 1;
                    hasLast = true;
                }
            }

            if (hasLast && run > 0) data.AddVoxels(last, run);

            // Dispose temps
            vox.Dispose();
            ground.Dispose();
            biomeMap.Dispose();
            riverMap.Dispose();
            return data;
        }

        private static int Index(int x, int y, int z, int sy, int sz) => ((x * sz) + z) * sy + y;

        private struct ColumnParams
        {
            public float Elev01;
            public Biome Biome;
        }

        private void PrepareChunkMaps(int3 origin, int waterLevel, NativeArray<int> ground, NativeArray<byte> biomeMap,
            NativeArray<byte> riverMap)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int wx = origin.x + x;
                int wz = origin.z + z;

                // Height from layered noise (continents/hills/detail) -> flatter world
                float continent = noise.snoise(new float2(wx * ContinentScale, wz * ContinentScale));
                float hills = noise.snoise(new float2(wx * HillScale, wz * HillScale));
                float detail = noise.snoise(new float2(wx * DetailScale, wz * DetailScale));
                float elev = continent * 0.7f + hills * 0.18f + detail * 0.12f; // -1..1
                elev = elev * 0.5f + 0.5f; // 0..1
                // Pull towards mid-level to flatten
                elev = math.lerp(elev, 0.5f, 0.55f);
                int groundY = math.clamp((int)math.round(elev * (sy - 1)), 1, sy - 1);

                // Biome fields
                float temp = noise.snoise(new float2((wx + 10000) * BiomeScale, (wz + 10000) * BiomeScale)) * 0.5f +
                             0.5f;
                float hum = noise.snoise(new float2((wx - 10000) * BiomeScale, (wz - 10000) * BiomeScale)) * 0.5f +
                            0.5f;
                temp = math.clamp(math.smoothstep(0f, 1f, temp) - 0.5f, -0.5f, 0.5f) * BiomeExaggeration + 0.5f;
                hum = math.clamp(math.smoothstep(0f, 1f, hum) - 0.5f, -0.5f, 0.5f) * (BiomeExaggeration * 0.9f) + 0.5f;
                float elev01 = sy > 1 ? math.saturate(groundY / (float)(sy - 1)) : 0f;

                // River mask
                float rv = noise.snoise(new float2(wx * RiverScale, wz * RiverScale));
                bool isRiver = rv < RiverThreshold;

                // Biome selection
                Biome biome = BiomeHelper.SelectBiome(temp, hum, elev01, groundY, waterLevel);
                if (biome == Biome.Ocean)
                {
                    groundY = math.min(groundY, math.max(0, waterLevel - 1));
                }

                int i = z + x * sz;
                ground[i] = groundY;
                biomeMap[i] = (byte)biome;
                riverMap[i] = (byte)(isRiver ? 1 : 0);
            }
        }

        private void FillTerrain(NativeArray<ushort> vox, int waterLevel, NativeArray<int> ground,
            NativeArray<byte> biomeMap, NativeArray<byte> riverMap)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;
            ushort air = 0;
            ushort water = Config.Water;
            ushort stone = Config.Stone != 0 ? Config.Stone : Config.Rock;
            ushort dirt = Config.Dirt != 0 ? Config.Dirt : (Config.StoneSandy != 0 ? Config.StoneSandy : stone);

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int i = z + x * sz;
                int gy = ground[i];
                bool river = riverMap[i] == 1;
                Biome biome = (Biome)biomeMap[i];

                SelectSurfaceMaterials(
                    new ColumnParams { Elev01 = sy > 1 ? math.saturate(gy / (float)(sy - 1)) : 0f, Biome = biome },
                    out ushort top, out ushort under, out ushort st);
                if (st == 0) st = stone;
                if (under == 0) under = dirt;
                if (top == 0) top = dirt;

                for (int y = 0; y < sy; y++)
                {
                    ushort v;
                    if (river)
                    {
                        // flat river channel to water level-1
                        v = (y <= waterLevel - 1) ? water : air;
                    }
                    else if (y < gy - 4) v = st;
                    else if (y < gy) v = under;
                    else if (y == gy) v = (gy < waterLevel) ? water : top;
                    else v = (y < waterLevel) ? water : air;

                    vox[Index(x, y, z, sy, sz)] = v;
                }
            }
        }

        private struct Worm
        {
            public float3 Start;
            public float3 Dir;
            public float Length;
            public float Radius;
        }

        private void CarveCaves(NativeArray<ushort> vox, int3 origin, NativeArray<int> ground,
            NativeArray<byte> riverMap)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;

            // Deterministic RNG per chunk
            uint seed = (uint)((origin.x * 73856093) ^ (origin.z * 19349663) ^ 0x9E3779B9);
            Random rng = new Random(seed == 0 ? 1u : seed);

            // Generate a few worms
            NativeList<Worm> worms = new NativeList<Worm>(Allocator.Temp);
            int wormCount = rng.NextInt(0, 2);
            for (int i = 0; i < wormCount; i++)
            {
                float sxw = origin.x + rng.NextFloat(0, sx);
                float syw = rng.NextFloat(8, sy * 0.45f);
                float szw = origin.z + rng.NextFloat(0, sz);
                float3 dir = math.normalize(new float3(rng.NextFloat(-1f, 1f), rng.NextFloat(-0.1f, 0.1f),
                    rng.NextFloat(-1f, 1f)));
                worms.Add(new Worm
                {
                    Start = new float3(sxw, syw, szw), Dir = dir, Length = rng.NextFloat(10f, 48f),
                    Radius = rng.NextFloat(1.2f, 3.5f)
                });
            }

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int gy = ground[z + x * sz];
                bool river = riverMap[z + x * sz] == 1;
                if (river) continue; // avoid underwater caves in rivers

                for (int y = 1; y < sy - 1; y++)
                {
                    // skip near-surface
                    float nearSurface = (y >= gy - 6) ? 1f : 0f;

                    // 3D noise thresholding
                    float n = noise.snoise(new float3((origin.x + x) * CaveScale, y * CaveScale,
                        (origin.z + z) * CaveScale));
                    float threshold = math.lerp(0.6f, 0.78f, nearSurface);
                    bool isCave = n > threshold;

                    if (!isCave)
                    {
                        // Worm influence
                        float3 p = new float3(origin.x + x + 0.5f, y + 0.5f, origin.z + z + 0.5f);
                        for (int wi = 0; wi < worms.Length; wi++)
                        {
                            Worm w = worms[wi];
                            float3 toP = p - w.Start;
                            float t = math.clamp(math.dot(toP, w.Dir), 0f, w.Length);
                            float3 closest = w.Start + w.Dir * t;
                            float3 disp = new float3(
                                noise.snoise(new float3(t * 0.2f, (origin.x + x) * 0.02f, (origin.z + z) * 0.02f)),
                                noise.snoise(new float3(t * 0.15f, (origin.x + x) * 0.015f, (origin.z + z) * 0.015f)) *
                                0.4f,
                                noise.snoise(new float3(t * 0.2f + 37.1f, (origin.x + x) * 0.02f + 17.3f,
                                    (origin.z + z) * 0.02f + 11.7f))
                            ) * (w.Radius * 0.6f);
                            float3 center = closest + disp;
                            float dist = math.distance(p, center);
                            float wobble = noise.snoise(new float3(t, (origin.x + x) * 0.01f, (origin.z + z) * 0.01f));
                            if (y < gy - 6 && dist < w.Radius * (0.6f + 0.4f * wobble))
                            {
                                isCave = true;
                                break;
                            }
                        }
                    }

                    if (isCave)
                    {
                        int idx = Index(x, y, z, sy, sz);
                        if (vox[idx] != 0 && vox[idx] != Config.Water) vox[idx] = 0;
                    }
                }
            }

            worms.Dispose();
        }

        private void PlaceOres(NativeArray<ushort> vox)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;
            ushort stone = Config.Stone != 0 ? Config.Stone : Config.Rock;
            ushort oreCoal = Config.StoneCoalOre;
            ushort oreIron = (Config.StoneIronGreenOre != 0) ? Config.StoneIronGreenOre : Config.StoneIronBrownOre;
            ushort oreGold = Config.StoneGoldOre;
            ushort oreDiamond = Config.StoneDiamondOre;

            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            for (int y = 2; y < sy - 2; y++)
            {
                int idx = Index(x, y, z, sy, sz);
                if (vox[idx] != stone) continue;

                // Exposure check: adjacent to air (6-neighborhood)
                bool exposed =
                    vox[Index(x + 1, y, z, sy, sz)] == 0 ||
                    vox[Index(x - 1, y, z, sy, sz)] == 0 ||
                    vox[Index(x, y + 1, z, sy, sz)] == 0 ||
                    vox[Index(x, y - 1, z, sy, sz)] == 0 ||
                    vox[Index(x, y, z + 1, sy, sz)] == 0 ||
                    vox[Index(x, y, z - 1, sy, sz)] == 0;

                float depthNorm = 1f - (y / (float)sy);
                float oreNoise = math.abs(noise.snoise(new float3((x) * 0.12f, y * 0.12f, (z) * 0.12f)));
                float roll = math.max(0f, oreNoise * depthNorm);
                if (exposed) roll *= 1.6f;

                if (roll > 0.85f && oreDiamond != 0) vox[idx] = oreDiamond;
                else if (roll > 0.7f && oreGold != 0 && y < sy * 0.35f) vox[idx] = oreGold;
                else if (roll > 0.6f && oreIron != 0) vox[idx] = oreIron;
                else if (roll > 0.45f && oreCoal != 0) vox[idx] = oreCoal;
            }
        }

        private void PlaceVegetationAndStructures(NativeArray<ushort> vox, NativeArray<int> ground,
            NativeArray<byte> biomeMap)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;
            ushort grass = Config.Grass;
            ushort sand = Config.Sand != 0 ? Config.Sand : Config.SandGrey;
            ushort dirt = Config.Dirt != 0
                ? Config.Dirt
                : (Config.StoneSandy != 0 ? Config.StoneSandy : (Config.Stone != 0 ? Config.Stone : Config.Rock));
            ushort leaves = Config.Leaves != 0
                ? Config.Leaves
                : (Config.LeavesOrange != 0 ? Config.LeavesOrange : (ushort)0);
            ushort logOak = Config.LogOak != 0 ? Config.LogOak : Config.Planks;
            ushort logBirch = Config.LogBirch != 0 ? Config.LogBirch : Config.PlanksRed;
            ushort cactus = Config.Cactus;
            ushort mushroom = Config.MushroomBrown != 0 ? Config.MushroomBrown : Config.MushroomRed;
            ushort grassF = Config.GrassF != 0 ? Config.GrassF : Config.GrassFDry;

            // Deterministic per-chunk RNG
            uint seed = (uint)((sx * 73856093) ^ (sz * 19349663) ^ 0x85ebca6b);
            Random rng = new Random(seed == 0 ? 1u : seed);

            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            {
                int gi = z + x * sz;
                int gy = ground[gi];
                if (gy <= 0 || gy >= sy - 2) continue;

                Biome biome = (Biome)biomeMap[gi];
                ushort surface = vox[Index(x, gy, z, sy, sz)];
                if (surface == 0 || surface == Config.Water) continue;

                switch (biome)
                {
                    case Biome.Forest:
                        if (surface == grass && rng.NextFloat() < 0.5f)
                        {
                            PlaceTree(vox, x, gy + 1, z, 4, rng.NextInt(0, 100) < 75 ? logOak : logBirch, leaves);
                        }
                        else if (rng.NextFloat() < 0.15f && grassF != 0)
                        {
                            vox[Index(x, gy + 1, z, sy, sz)] = grassF;
                        }

                        break;
                    case Biome.Plains:
                        if (surface == grass && rng.NextFloat() < 0.01f)
                        {
                            PlaceTree(vox, x, gy + 1, z, 5, rng.NextInt(0, 100) < 80 ? logOak : logBirch, leaves);
                        }
                        else if (rng.NextFloat() < 0.05f && grassF != 0)
                        {
                            vox[Index(x, gy + 1, z, sy, sz)] = grassF;
                        }

                        break;
                    case Biome.Jungle:
                        if (surface == grass && rng.NextFloat() < 0.6f)
                        {
                            PlaceTree(vox, x, gy + 1, z, 6, logOak, leaves);
                        }

                        break;
                    case Biome.Desert:
                        if ((surface == sand || surface == dirt) && rng.NextFloat() < 0.02f && cactus != 0)
                        {
                            PlaceColumn(vox, x, gy + 1, z, cactus, rng.NextInt(1, 4));
                        }

                        break;
                    case Biome.Swamp:
                        if (rng.NextFloat() < 0.06f && mushroom != 0)
                        {
                            vox[Index(x, gy + 1, z, sy, sz)] = mushroom;
                        }

                        break;
                }
            }
        }

        private void PlaceTree(NativeArray<ushort> vox, int x, int y, int z, int height, int idLog, int idLeaves)
        {
            if (idLog == 0 || idLeaves == 0) return;
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
            // Trunk: ensure inside chunk before writing
            for (int i = 0; i < height && y + i < sy - 1; i++)
            {
                if (!InBounds(x, y + i, z)) break;
                vox[Index(x, y + i, z, sy, sz)] = (ushort)idLog;
            }

            int radius = 2;
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
                    vox[Index(tx, yy, tz, sy, sz)] = (ushort)idLeaves;
                }
            }
        }

        private void PlaceColumn(NativeArray<ushort> vox, int x, int y, int z, int blockId, int count)
        {
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
            for (int i = 0; i < count && y + i < sy - 1; i++)
            {
                if (!InBounds(x, y + i, z)) break;
                vox[Index(x, y + i, z, sy, sz)] = (ushort)blockId;
            }
        }

        private bool InBounds(int x, int y, int z)
        {
            return x >= 0 && x < ChunkSize.x && z >= 0 && z < ChunkSize.z && y >= 0 && y < ChunkSize.y;
        }

        private void SelectSurfaceMaterials(in ColumnParams col, out ushort topBlock, out ushort underBlock,
            out ushort stone)
        {
            stone = Config.Stone != 0 ? Config.Stone : Config.Rock;
            ushort dirt = Config.Dirt != 0 ? Config.Dirt : (Config.StoneSandy != 0 ? Config.StoneSandy : stone);

            topBlock = Config.Grass != 0 ? Config.Grass : dirt;
            underBlock = dirt;

            switch (col.Biome)
            {
                case Biome.Desert:
                    topBlock = (Config.SandRed != 0 ? Config.SandRed : (Config.Sand != 0 ? Config.Sand : underBlock));
                    underBlock = Config.SandStoneRed != 0
                        ? Config.SandStoneRed
                        : (Config.SandStoneRedSandy != 0 ? Config.SandStoneRedSandy : underBlock);
                    break;
                case Biome.Snow:
                    topBlock = Config.DirtSnowy != 0
                        ? Config.DirtSnowy
                        : (Config.StoneSnowy != 0 ? Config.StoneSnowy : topBlock);
                    underBlock = dirt;
                    break;
                case Biome.Swamp:
                    topBlock = dirt;
                    underBlock = dirt;
                    break;
                case Biome.Mountain:
                    topBlock = col.Elev01 >= MountainSnowline
                        ? (Config.StoneSnowy != 0 ? Config.StoneSnowy : stone)
                        : stone;
                    underBlock = stone;
                    break;
                case Biome.Tundra:
                    topBlock = Config.GrassFDead != 0
                        ? Config.GrassFDead
                        : (Config.DirtSnowy != 0 ? Config.DirtSnowy : topBlock);
                    underBlock = dirt;
                    break;
            }
        }
    }
}