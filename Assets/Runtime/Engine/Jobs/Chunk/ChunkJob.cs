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
        private const float BiomeScale = 0.0012f;
        private const float BiomeExaggeration = 1.8f;
        private const float MountainSnowline = 0.75f; // normalized elevation

        // Height field layering
        private const float ContinentScale = 0.0012f;
        private const float HillScale = 0.01f;
        private const float DetailScale = 0.03f;

        // Extra world-noise params
        private const float RiverScale = 0.0008f;
        private const float RiverThreshold = -0.15f; 
        private const float CaveScale = 0.03f; 

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

            int waterLevel = Config.WaterLevel;

            NativeArray<ushort> vox = new NativeArray<ushort>(volume, Allocator.Temp);
            NativeArray<int> ground = new NativeArray<int>(sx * sz, Allocator.Temp);
            NativeArray<byte> biomeMap = new NativeArray<byte>(sx * sz, Allocator.Temp);
            NativeArray<byte> riverMap = new NativeArray<byte>(sx * sz, Allocator.Temp);

            // Step A: prepare per-column maps (height, biome, river)
            PrepareChunkMaps(origin, waterLevel, ground, biomeMap, riverMap);

            // Step B: terrain fill (stone/dirt/top/water) into vox buffer
            FillTerrain(vox, waterLevel, ground, biomeMap, riverMap);

            // Step C: carve caves
            CarveCaves(vox, origin, ground, riverMap);

            // Step D: place ores
            PlaceOres(vox);

            // Step E: vegetation
            PlaceVegetationAndStructures(vox, ground, biomeMap, origin);

            // Step F: validation
            ValidateSurfaceVegetation(vox, ground);

            // Emit RLE
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
        
        // ... (SurfaceHasNeighbor und CanPlaceTree bleiben unverändert) ...
        private static bool SurfaceHasNeighbor(NativeArray<ushort> vox, int cx, int cy, int cz, int sx, int sy, int sz, ushort waterId)
        {
            int neighbors = 0;
            if (cx + 1 < sx && vox[Index(cx + 1, cy, cz, sy, sz)] != 0 && vox[Index(cx + 1, cy, cz, sy, sz)] != waterId) neighbors++;
            if (cx - 1 >= 0 && vox[Index(cx - 1, cy, cz, sy, sz)] != 0 && vox[Index(cx - 1, cy, cz, sy, sz)] != waterId) neighbors++;
            if (cz + 1 < sz && vox[Index(cx, cy, cz + 1, sy, sz)] != 0 && vox[Index(cx, cy, cz + 1, sy, sz)] != waterId) neighbors++;
            if (cz - 1 >= 0 && vox[Index(cx, cy, cz - 1, sy, sz)] != 0 && vox[Index(cx, cy, cz - 1, sy, sz)] != waterId) neighbors++;
            return neighbors > 0;
        }
        private static bool CanPlaceTree(NativeArray<ushort> vox, int cx, int cy, int cz, int sx, int sy, int sz, ushort logA, ushort logB)
        {
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                if (ox == 0 && oz == 0) continue; 
                int tx = cx + ox;
                int tz = cz + oz;
                int ty = cy;
                if (tx < 0 || tx >= sx || tz < 0 || tz >= sz || ty < 0 || ty >= sy) continue;
                ushort v = vox[Index(tx, ty, tz, sy, sz)];
                if (v == logA || v == logB) return false;
            }
            return true;
        }


        private struct ColumnParams
        {
            public float Elev01;
            public Biome Biome;
        }

        // ########## FIX 1: KOMPLETT ERSETZTE FUNKTION ##########
        private void PrepareChunkMaps(int3 origin, int waterLevel, NativeArray<int> ground, NativeArray<byte> biomeMap,
            NativeArray<byte> riverMap)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;

            // ### SCHRITT 1: HÖHE BERECHNEN (mit Klippen) ###
            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int wx = origin.x + x;
                int wz = origin.z + z;

                // 1. Berechne Kontinent-Noise ZUERST
                // WICHTIG: (noise.snoise * 2f) - 1f mappt den 0..1 Noise auf -1..1
                float continent = (noise.snoise(new float2(wx * ContinentScale, wz * ContinentScale)) * 2f) - 1f; // -1..1
                
                // Schwellenwert für Ozean vs. Land
                bool isOcean = continent < -0.15f; 
                float elev; // -1..1

                // 2. Berechne Höhe basierend auf Land ODER Ozean
                if (isOcean)
                {
                    // OZEAN-HÖHE: folge dem Noise für einen unebenen Boden
                    elev = -0.5f + (continent + 0.15f) * 0.5f; 
                }
                else
                {
                    // LAND-HÖHE: (Deine alte Formel, Noise auch auf -1..1 gemappt)
                    float hills = (noise.snoise(new float2(wx * HillScale, wz * HillScale)) * 2f) - 1f;
                    float detail = (noise.snoise(new float2(wx * DetailScale, wz * DetailScale)) * 2f) - 1f;
                    elev = continent * 0.7f + hills * 0.18f + detail * 0.12f;
                }

                // 3. Normalisiere die Höhe (0..1) und berechne groundY
                elev = elev * 0.5f + 0.5f; // Mappt -1..1 zu 0..1
                elev = math.lerp(elev, 0.5f, 0.55f); // Abflachen
                int groundY = math.clamp((int)math.round(elev * (sy - 1)), 1, sy - 1);
                
                ground[z + x * sz] = groundY;
            }

            // ### SCHRITT 2: HÖHE GLÄTTEN (Strände erzeugen) ###
            for (int pass = 0; pass < 30; pass++) // 30 Pässe
            {
                NativeArray<int> copy = new NativeArray<int>(sx * sz, Allocator.Temp);
                for (int i = 0; i < sx * sz; i++) copy[i] = ground[i];
                
                for (int x = 0; x < sx; x++)
                for (int z = 0; z < sz; z++)
                {
                    int i = z + x * sz;
                    int gy = copy[i];
                    int minNeighbor = gy;
                    if (x > 0) minNeighbor = math.min(minNeighbor, copy[z + (x - 1) * sz]);
                    if (x < sx - 1) minNeighbor = math.min(minNeighbor, copy[z + (x + 1) * sz]);
                    if (z > 0) minNeighbor = math.min(minNeighbor, copy[(z - 1) + x * sz]);
                    if (z < sz - 1) minNeighbor = math.min(minNeighbor, copy[(z + 1) + x * sz]);
                    
                    // Deine originale Glättungs-Logik
                    if (minNeighbor < waterLevel && gy > minNeighbor + 1)
                    {
                        ground[i] = math.max(minNeighbor + 1, gy - 1);
                    }
                }
                copy.Dispose();
            }
            
            // ### SCHRITT 3: BIOME & FLÜSSE BESTIMMEN (basiert auf geglätteter Höhe) ###
            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int wx = origin.x + x;
                int wz = origin.z + z;
                int i = z + x * sz;
                
                // 1. Hole die FINALE, GEGLÄTTETE Höhe
                int groundY = ground[i];

                // 2. Berechne Biome-Noise (hier ist 0..1 korrekt für deine Formel)
                float temp = noise.snoise(new float2((wx + 10000) * BiomeScale, (wz + 10000) * BiomeScale)) * 0.5f + 0.5f;
                float hum = noise.snoise(new float2((wx - 10000) * BiomeScale, (wz - 10000) * BiomeScale)) * 0.5f + 0.5f;
                temp = math.clamp(math.smoothstep(0f, 1f, temp) - 0.5f, -0.5f, 0.5f) * BiomeExaggeration + 0.5f;
                hum = math.clamp(math.smoothstep(0f, 1f, hum) - 0.5f, -0.5f, 0.5f) * (BiomeExaggeration * 0.9f) + 0.5f;
                float elev01 = sy > 1 ? math.saturate(groundY / (float)(sy - 1)) : 0f;

                // 3. Rufe BiomeHelper auf (bekommt jetzt korrekte, geglättete Höhe)
                Biome biome = BiomeHelper.SelectBiome(temp, hum, elev01, groundY, waterLevel);
                biomeMap[i] = (byte)biome;
                
                // 4. River mask (Noise auf -1..1 mappen)
                float rv = (noise.snoise(new float2(wx * RiverScale, wz * RiverScale)) * 2f) - 1f;
                bool isRiver = rv < RiverThreshold;
                riverMap[i] = (byte)(isRiver ? 1 : 0);
            }

            // ### SCHRITT 4: FLÜSSE SCHNITZEN ###
            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            {
                int i = z + x * sz;
                if (riverMap[i] == 1)
                {
                    // Flüsse sollten keine Strände sein
                    if (biomeMap[i] == (byte)Biome.Beach) 
                        biomeMap[i] = (byte)Biome.Plains; 

                    ground[i] = math.min(ground[i], waterLevel - 2);
                    
                    int left = z + (x - 1) * sz;
                    int right = z + (x + 1) * sz;
                    int up = (z - 1) + x * sz;
                    int down = (z + 1) + x * sz;
                    ground[left] = math.min(ground[left], ground[i] + 1);
                    ground[right] = math.min(ground[right], ground[i] + 1);
                    ground[up] = math.min(ground[up], ground[i] + 1);
                    ground[down] = math.min(ground[down], ground[i] + 1);
                }
            }
        }

        // ########## FIX 2: KOMPLETT ERSETZTE FUNKTION ##########
        private void FillTerrain(NativeArray<ushort> vox, int waterLevel, NativeArray<int> ground,
            NativeArray<byte> biomeMap, NativeArray<byte> riverMap)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;
            ushort air = 0;
            ushort waterBlock = Config.Water != 0 ? Config.Water : (Config.Ice != 0 ? Config.Ice : (ushort)0);
            ushort stone = Config.Stone != 0 ? Config.Stone : Config.Rock;
            ushort dirt = Config.Dirt != 0 ? Config.Dirt : (Config.StoneSandy != 0 ? Config.StoneSandy : stone);

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int i = z + x * sz;
                int gy = ground[i];
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
                 	if (y < gy - 4) v = st;
                    else if (y < gy) v = under;
                    else if (y == gy) v = (gy < waterLevel) ? waterBlock : top;
                    else v = (y < waterLevel) ? waterBlock : air;

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

        // ... (CarveCaves und PlaceOres bleiben unverändert, BIS AUF DEN NOISE-FIX) ...
        private void CarveCaves(NativeArray<ushort> vox, int3 origin, NativeArray<int> ground,
            NativeArray<byte> riverMap)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;

            uint seed = (uint)((origin.x * 73856093) ^ (origin.z * 19349663) ^ (int)RandomSeed ^ 0x9E3779B9);
            Random rng = new Random(seed == 0 ? 1u : seed);

            NativeList<Worm> worms = new NativeList<Worm>(Allocator.Temp);
            int wormCount = rng.NextInt(1, 4); 
            for (int i = 0; i < wormCount; i++)
            {
                float sxw = origin.x + rng.NextFloat(0, sx);
                float syw = rng.NextFloat(6, sy * 0.6f);
                float szw = origin.z + rng.NextFloat(0, sz);
                float3 dir = math.normalize(new float3(rng.NextFloat(-1f, 1f), rng.NextFloat(-0.12f, 0.12f),
                    rng.NextFloat(-1f, 1f)));
                worms.Add(new Worm
                {
                    Start = new float3(sxw, syw, szw), Dir = dir, Length = rng.NextFloat(24f, 96f),
                    Radius = rng.NextFloat(2.0f, 6.0f)
                });
            }

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int gy = ground[z + x * sz];
                bool river = riverMap[z + x * sz] == 1;
                if (river) continue; 

                for (int y = 1; y < sy - 1; y++)
                {
                    float nearSurface = (y >= gy - 6) ? 1f : 0f;

                    // *** NOISE FIX: 0..1 -> -1..1 ***
                    float n = (noise.snoise(new float3((origin.x + x) * CaveScale, y * CaveScale,
                        (origin.z + z) * CaveScale)) * 2f) - 1f;
                    float threshold = math.lerp(0.36f, 0.64f, nearSurface); // Angepasster Schwellenwert für -1..1
                    bool isCave = n > threshold;

                    if (!isCave)
                    {
                        float3 p = new float3(origin.x + x + 0.5f, y + 0.5f, origin.z + z + 0.5f);
                        for (int wi = 0; wi < worms.Length; wi++)
                        {
                            Worm w = worms[wi];
                            float3 toP = p - w.Start;
                            float t = math.clamp(math.dot(toP, w.Dir), 0f, w.Length);
                            float3 closest = w.Start + w.Dir * t;
                            // *** NOISE FIX: 0..1 -> -1..1 ***
                            float3 disp = new float3(
                                (noise.snoise(new float3(t * 0.2f, (origin.x + x) * 0.02f, (origin.z + z) * 0.02f)) * 2f) - 1f,
                                ((noise.snoise(new float3(t * 0.15f, (origin.x + x) * 0.015f, (origin.z + z) * 0.015f)) * 2f) - 1f) * 0.4f,
                                (noise.snoise(new float3(t * 0.2f + 37.1f, (origin.x + x) * 0.02f + 17.3f, (origin.z + z) * 0.02f + 11.7f)) * 2f) - 1f
                            ) * (w.Radius * 0.6f);
                            float3 center = closest + disp;
                            float dist = math.distance(p, center);
                            float wobble = (noise.snoise(new float3(t, (origin.x + x) * 0.01f, (origin.z + z) * 0.01f)) * 2f) - 1f;
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
                        if (vox[idx] != 0 && vox[idx] != (Config.Water != 0 ? Config.Water : (Config.Ice != 0 ? Config.Ice : (ushort)0))) vox[idx] = 0;
                    }
                }
            }
            
            for (int wi = 0; wi < worms.Length; wi++)
            {
                Worm w = worms[wi];
                float step = math.max(2f, w.Radius * 0.9f);
                for (float t = 0f; t < w.Length; t += step)
                {
                    float3 center = w.Start + w.Dir * t;
                    int cx = (int)math.floor(center.x) - origin.x;
                    int cy = (int)math.floor(center.y);
                    int cz = (int)math.floor(center.z) - origin.z;
                    // *** NOISE FIX: 0..1 -> -1..1 ***
                    int carveRadius = (int)math.ceil(w.Radius * (1.0f + 0.6f * ((noise.snoise(new float3(t * 0.1f, center.x * 0.01f, center.z * 0.01f)) * 2f) - 1f)));

                    for (int ox = -carveRadius; ox <= carveRadius; ox++)
                    for (int oz = -carveRadius; oz <= carveRadius; oz++)
                    for (int oy = -carveRadius; oy <= carveRadius; oy++)
                     {
                         int tx = cx + ox;
                         int ty = cy + oy;
                         int tz = cz + oz;
                         if (!InBounds(tx, ty, tz)) continue;
                         float dist = math.length(new float3(ox, oy, oz));
                         if (dist <= carveRadius)
                         {
                             int idx = Index(tx, ty, tz, sy, sz);
                             if (vox[idx] != 0 && vox[idx] != (Config.Water != 0 ? Config.Water : (Config.Ice != 0 ? Config.Ice : (ushort)0))) vox[idx] = 0;
                         }
                     }
                }
            }

            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            for (int y = 1; y < sy - 1; y++)
            {
                int idx = Index(x, y, z, sy, sz);
                if (vox[idx] == 0 || vox[idx] == Config.Water) continue; 
                int airNeighbors = 0;
                if (vox[Index(x + 1, y, z, sy, sz)] == 0) airNeighbors++;
                if (vox[Index(x - 1, y, z, sy, sz)] == 0) airNeighbors++;
                if (vox[Index(x, y + 1, z, sy, sz)] == 0) airNeighbors++;
                if (vox[Index(x, y - 1, z, sy, sz)] == 0) airNeighbors++;
                if (vox[Index(x, y, z + 1, sy, sz)] == 0) airNeighbors++;
                if (vox[Index(x, y, z - 1, sy, sz)] == 0) airNeighbors++;
                if (airNeighbors >= 3)
                {
                    vox[idx] = 0;
                }
            }

            worms.Dispose();

            ushort lavaId = Config.Lava;
            if (lavaId != 0)
            {
                for (int x = 1; x < sx - 1; x++)
                for (int z = 1; z < sz - 1; z++)
                {
                    int gy = ground[z + x * sz];
                    for (int y = 1; y < sy - 2; y++)
                    {
                        int idx = Index(x, y, z, sy, sz);
                        if (vox[idx] != 0) continue; 
                        if (y >= gy - 6) continue;
                        if (y >= Config.WaterLevel - 8) continue;
                        // *** NOISE FIX: 0..1 -> -1..1 ***
                        float p = math.abs((noise.snoise(new float3((origin.x + x) * 0.12f, y * 0.07f, (origin.z + z) * 0.12f)) * 2f) - 1f);
                        if (p > 0.6f && rng.NextFloat() < 0.35f)
                        {
                            vox[idx] = lavaId;
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

                bool exposed =
                    vox[Index(x + 1, y, z, sy, sz)] == 0 ||
                    vox[Index(x - 1, y, z, sy, sz)] == 0 ||
                    vox[Index(x, y + 1, z, sy, sz)] == 0 ||
                    vox[Index(x, y - 1, z, sy, sz)] == 0 ||
                    vox[Index(x, y, z + 1, sy, sz)] == 0 ||
                    vox[Index(x, y, z - 1, sy, sz)] == 0;

                float depthNorm = 1f - (y / (float)sy);
                // *** NOISE FIX: 0..1 -> -1..1 ***
                float oreNoise = math.abs((noise.snoise(new float3((x) * 0.12f, y * 0.12f, (z) * 0.12f)) * 2f) - 1f);
                float roll = math.max(0f, oreNoise * depthNorm);
                if (exposed) roll *= 1.6f;

                if (roll > 0.85f && oreDiamond != 0) vox[idx] = oreDiamond;
                else if (roll > 0.7f && oreGold != 0 && y < sy * 0.35f) vox[idx] = oreGold;
                else if (roll > 0.6f && oreIron != 0) vox[idx] = oreIron;
                else if (roll > 0.45f && oreCoal != 0) vox[idx] = oreCoal;
            }
        }

        private void PlaceVegetationAndStructures(NativeArray<ushort> vox, NativeArray<int> ground,
            NativeArray<byte> biomeMap, int3 origin)
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
            ushort mushroom = Config.MushroomBrown != 0 ? Config.MushroomBrown : (Config.MushroomRed != 0 ? Config.MushroomRed : Config.MushroomTan);
            ushort grassF = Config.GrassF != 0 ? Config.GrassF : Config.GrassFDry;
            ushort visibleWater = Config.Water != 0 ? Config.Water : (Config.Ice != 0 ? Config.Ice : (ushort)0);

            uint seed = (uint)((origin.x * 73856093) ^ (origin.z * 19349663) ^ (int)RandomSeed ^ 0x85ebca6b);
            Random rng = new Random(seed == 0 ? 1u : seed);

            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            {
                int gi = z + x * sz;
                int gy = ground[gi];
                if (gy <= 0 || gy >= sy - 2) continue;

                Biome biome = (Biome)biomeMap[gi];
                ushort surface = vox[Index(x, gy, z, sy, sz)];
                
                // *** WICHTIGE PRÜFUNG ***
                // Verhindert Bäume/Blumen auf dem Meeresboden
                if (surface == 0 || (visibleWater != 0 && surface == visibleWater)) continue;

                if (!IsEarthLike(surface)) continue;

                // ... (Der Rest der Funktion bleibt unverändert) ...
                switch (biome)
                {
                    case Biome.Forest:
                        // less dense forest: reduce chance and increase size variability
                        if (surface == grass && rng.NextFloat() < 0.22f)
                        {
                            int h = rng.NextInt(5, 9);
                            ushort chosenLog = (ushort)(rng.NextInt(0, 100) < 75 ? logOak : logBirch);
                            if (CanPlaceTree(vox, x, gy + 1, z, sx, sy, sz, logOak, logBirch))
                            {
                                PlaceTree(vox, x, gy + 1, z, h, chosenLog, leaves, ref rng);
                            }
                        }
                        else if (surface == grass && rng.NextFloat() < 0.12f && grassF != 0)
                        {
                            int targetIdx = Index(x, gy + 1, z, sy, sz);
                            // Guard: ensure the surface cell still matches expected surface and target is free
                            if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 && vox[Index(x, gy, z, sy, sz)] == surface && IsEarthLike(surface)
                                && SurfaceHasNeighbor(vox, x, gy, z, sx, sy, sz, visibleWater)) vox[targetIdx] = grassF;
                        }

                        break;
                    case Biome.Plains:
                        if (surface == grass && rng.NextFloat() < 0.015f)
                        {
                            int h = rng.NextInt(6, 10);
                            ushort chosenLog = (ushort)(rng.NextInt(0, 100) < 80 ? logOak : logBirch);
                            if (CanPlaceTree(vox, x, gy + 1, z, sx, sy, sz, logOak, logBirch))
                            {
                                PlaceTree(vox, x, gy + 1, z, h, chosenLog, leaves, ref rng);
                            }
                        }
                        else if (surface == grass && rng.NextFloat() < 0.05f && grassF != 0)
                        {
                            int targetIdx = Index(x, gy + 1, z, sy, sz);
                            if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 && vox[Index(x, gy, z, sy, sz)] == surface && IsEarthLike(surface)
                                && SurfaceHasNeighbor(vox, x, gy, z, sx, sy, sz, visibleWater)) vox[targetIdx] = grassF;
                        }

                        break;
                    case Biome.Jungle:
                        if (surface == grass && rng.NextFloat() < 0.6f)
                        {
                            int h = rng.NextInt(7, 11);
                            if (CanPlaceTree(vox, x, gy + 1, z, sx, sy, sz, logOak, logBirch))
                            {
                                PlaceTree(vox, x, gy + 1, z, h, logOak, leaves, ref rng);
                            }
                        }

                        break;
                    case Biome.Desert:
                        if ((surface == sand || surface == dirt) && rng.NextFloat() < 0.035f && cactus != 0)
                        {
                            // ensure base is still correct and above-block is empty before placing column
                            if (InBounds(x, gy + 1, z) && vox[Index(x, gy, z, sy, sz)] == surface && IsEarthLike(surface) && vox[Index(x, gy + 1, z, sy, sz)] == 0)
                                PlaceColumn(vox, x, gy + 1, z, cactus, rng.NextInt(1, 5));
                        }
                        // dry shrubs / dead bushes in desert
                        if ((surface == sand || surface == dirt) && rng.NextFloat() < 0.08f && Config.GrassFDry != 0)
                        {
                            int targetIdx = Index(x, gy + 1, z, sy, sz);
                            if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 && vox[Index(x, gy, z, sy, sz)] == surface && IsEarthLike(surface)
                                && SurfaceHasNeighbor(vox, x, gy, z, sx, sy, sz, visibleWater)) vox[targetIdx] = Config.GrassFDry;
                        }

                        break;
                    case Biome.Swamp:
                        if (rng.NextFloat() < 0.06f && mushroom != 0)
                        {
                            // only place a mushroom if the target cell above ground is empty (no floating mushrooms)
                            int targetIdx = Index(x, gy + 1, z, sy, sz);
                            if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 && vox[Index(x, gy, z, sy, sz)] == surface && IsEarthLike(surface)
                                && SurfaceHasNeighbor(vox, x, gy, z, sx, sy, sz, visibleWater))
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
                            int targetIdx = Index(x, gy + 1, z, sy, sz);
                            if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 && vox[Index(x, gy, z, sy, sz)] == surface && IsEarthLike(surface)
                                && SurfaceHasNeighbor(vox, x, gy, z, sx, sy, sz, visibleWater)) vox[targetIdx] = grassF;
                        }
                        break;
                    case Biome.RedDesert:
                        // red desert: more cacti (we add more later), but keep big structures rare here
                        if ((surface == sand || surface == dirt) && rng.NextFloat() < 0.02f && cactus != 0)
                        {
                            if (InBounds(x, gy + 1, z) && vox[Index(x, gy, z, sy, sz)] == surface && IsEarthLike(surface) && vox[Index(x, gy + 1, z, sy, sz)] == 0)
                                PlaceColumn(vox, x, gy + 1, z, cactus, rng.NextInt(1, 6));
                        }
                        // keep pyramids/oases very rare here (we place better controlled versions later)
                        if (rng.NextFloat() < 0.00005f)
                        {
                            PlacePyramid(vox, x, gy + 1, z, 3, Config.SandRed != 0 ? Config.SandRed : Config.Sand);
                        }
                        if (rng.NextFloat() < 0.00002f)
                        {
                            PlaceOasis(vox, x, gy, z);
                        }
                        break;
                    case Biome.Beach:
                        // place occasional palm and driftwood
                        if (rng.NextFloat() < 0.02f && InBounds(x, gy + 1, z) && vox[Index(x, gy + 1, z, sy, sz)] == 0)
                        {
                            PlacePalm(vox, x, gy + 1, z, ref rng);
                        }
                        break;
                    case Biome.Ice:
                        // freeze surface water and occasionally spawn igloos
                        if (visibleWater != 0 && surface == visibleWater && InBounds(x, gy, z))
                        {
                            // freeze the surface cell
                            vox[Index(x, gy, z, sy, sz)] = Config.Ice != 0 ? Config.Ice : (ushort)0;
                        }
                        if (rng.NextFloat() < 0.0007f)
                        {
                            PlaceIgloo(vox, x, gy + 1, z);
                        }
                        break;
                }

                // Mineshaft entrances in plains/forest/mountain at low chance
                if ((biome == Biome.Plains || biome == Biome.Forest || biome == Biome.Mountain || biome == Biome.HighStone) && rng.NextFloat() < 0.0009f)
                {
                    PlaceMineShaft(vox, x, gy, z, rng.NextInt(6, 20));
                }

                 // Only place flowers in Plains and only on actual grass blocks (avoid caves/other biomes)
                 // Also guard against ID collisions with wheat stages.
                 if (biome == Biome.Plains && Config.Flowers != 0 && surface == grass
                     && Config.Flowers != Config.WheatStage1 && Config.Flowers != Config.WheatStage2
                     && Config.Flowers != Config.WheatStage3 && Config.Flowers != Config.WheatStage4)
                 {
                     int targetIdx = Index(x, gy + 1, z, sy, sz);
                     // slightly higher chance specifically for plains
                     const float flowerChancePlains = 0.06f;
                     if (InBounds(x, gy + 1, z) && vox[targetIdx] == 0 && vox[Index(x, gy, z, sy, sz)] == surface && IsEarthLike(surface)
                         && rng.NextFloat() < flowerChancePlains && SurfaceHasNeighbor(vox, x, gy, z, sx, sy, sz, visibleWater))
                      {
                          vox[targetIdx] = Config.Flowers;
                      }
                  }

                // Wheat clusters near water in plains
                if (biome == Biome.Plains && surface == grass && Config.WheatStage1 != 0 && SurfaceHasNeighbor(vox, x, gy, z, sx, sy, sz, visibleWater))
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
                             int tgi = tz + tx * sz;
                             int tgy = ground[tgi];
                             // require that target column's ground is earth-like and not underwater
                             if (tgy <= 0 || tgy >= sy - 1) continue;
                             int belowIdx = Index(tx, tgy, tz, sy, sz);
                             ushort below = vox[belowIdx];
                             if (!IsEarthLike(below)) continue;
                             int tIdx = Index(tx, tgy + 1, tz, sy, sz);
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
                        if (InBounds(x, gy + 1, z) && vox[Index(x, gy, z, sy, sz)] == surface && IsEarthLike(surface) && vox[Index(x, gy + 1, z, sy, sz)] == 0)
                            PlaceColumn(vox, x, gy + 1, z, cactus, rng.NextInt(1, 6));
                    }
                    // fewer oases
                    if (rng.NextFloat() < 0.0002f)
                    {
                        PlaceOasis(vox, x, gy, z);
                    }
                    // dead/dry bushes (use GrassFDry if configured)
                    if ((surface == sand || surface == dirt) && Config.GrassFDry != 0 && rng.NextFloat() < 0.12f)
                    {
                        int tIdx = Index(x, gy + 1, z, sy, sz);
                        if (InBounds(x, gy + 1, z) && vox[tIdx] == 0) vox[tIdx] = Config.GrassFDry;
                    }
                    // occasional small pyramid (still rare)
                    if (rng.NextFloat() < 0.001f)
                    {
                        PlacePyramid(vox, x, gy + 1, z, 3, Config.SandRed != 0 ? Config.SandRed : Config.Sand);
                    }
                }

                // Ocean shipwrecks
                if (biome == Biome.Ocean && visibleWater != 0)
                {
                    // Finde den TATSÄCHLICHEN Oberflächenblock (könnte Wasser sein)
                    ushort actualSurface = vox[Index(x, gy, z, sy, sz)];
                    if (actualSurface == visibleWater && rng.NextFloat() < 0.001f)
                    {
                         PlaceShipwreck(vox, x, gy, z); // Platziere auf dem Meeresboden (gy)
                    }
                }
             }
         }

        // ... (IsEarthLike bleibt unverändert) ...
        private bool IsEarthLike(ushort id)
        {
            if (id == 0) return false;
            if (id == Config.Grass) return true;
            if (id == Config.Dirt) return true;
            if (Config.DirtSnowy != 0 && id == Config.DirtSnowy) return true;
            if (id == Config.Sand || id == Config.SandGrey || (Config.SandRed != 0 && id == Config.SandRed)) return true;
            if (Config.StoneSandy != 0 && id == Config.StoneSandy) return true;
            if (Config.SandStoneRed != 0 && id == Config.SandStoneRed) return true;
            if (Config.SandStoneRedSandy != 0 && id == Config.SandStoneRedSandy) return true;
            if (Config.GrassF != 0 && id == Config.GrassF) return true;
            if (Config.GrassFDry != 0 && id == Config.GrassFDry) return true;
            return false;
        }

        // ... (PlaceTree bleibt unverändert) ...
        private void PlaceTree(NativeArray<ushort> vox, int x, int y, int z, int height, int idLog, int idLeaves, ref Random rng)
        {
            if (idLog == Config.LogBirch && Config.LeavesOrange != 0)
            {
                idLeaves = Config.LeavesOrange;
            }

            if (idLog == 0 || idLeaves == 0) return;
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
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
                        int groundIdx = Index(bx, by, bz, sy, sz);
                        int idx = Index(bx, by + 1, bz, sy, sz);
                        if (!InBounds(bx, by + 1, bz)) continue;
                        if (vox[groundIdx] == 0) continue; 
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

        // ... (PlaceColumn und InBounds bleiben unverändert) ...
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


        // ########## FIX 3: KOMPLETT ERSETZTE FUNKTION ##########
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
                case Biome.RedDesert:
                    topBlock = (Config.SandRed != 0 ? Config.SandRed : (Config.Sand != 0 ? Config.Sand : underBlock));
                    underBlock = Config.SandStoneRed != 0
                        ? Config.SandStoneRed
                        : (Config.SandStoneRedSandy != 0 ? Config.SandStoneRedSandy : underBlock);
                    break;
                
                // ### NEUER, WICHTIGER CASE ###
                // Dies stellt sicher, dass der Ozeanboden Sand ist, KEIN Gras.
                case Biome.Ocean:
                    topBlock = (Config.Sand != 0 ? Config.Sand : (Config.SandGrey != 0 ? Config.SandGrey : dirt));
                    underBlock = (Config.StoneSandy != 0 ? Config.StoneSandy : dirt);
                    break;
                // ### ENDE ###

                case Biome.Beach:
                    topBlock = (Config.Sand != 0 ? Config.Sand : (Config.SandGrey != 0 ? Config.SandGrey : underBlock));
                    underBlock = Config.SandStoneRedSandy != 0 ? Config.SandStoneRedSandy : underBlock;
                    break;
                case Biome.Ice:
                    topBlock = Config.DirtSnowy != 0 ? Config.DirtSnowy : (Config.StoneSnowy != 0 ? Config.StoneSnowy : topBlock);
                    underBlock = dirt;
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
                case Biome.HighStone:
                    topBlock = Config.StoneGrey != 0 ? Config.StoneGrey : stone;
                    underBlock = stone;
                    break;
                case Biome.GreyMountain:
                    topBlock = Config.StoneGrey != 0 ? Config.StoneGrey : stone;
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

        // ... (ValidateSurfaceVegetation bleibt unverändert) ...
        private void ValidateSurfaceVegetation(NativeArray<ushort> vox, NativeArray<int> ground)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;

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

            bool IsSurfaceVegLocal(ushort id)
            {
                if (id == 0) return false;
                if (cfgFlowers != 0 && id == cfgFlowers) return true;
                if (cfgGrassF != 0 && id == cfgGrassF) return true;
                if (cfgGrassFDry != 0 && id == cfgGrassFDry) return true;
                if (cfgMushroomBrown != 0 && id == cfgMushroomBrown) return true;
                if (cfgMushroomRed != 0 && id == cfgMushroomRed) return true;
                if (cfgMushroomTan != 0 && id == cfgMushroomTan) return true;
                if (cfgGrass != 0 && id == cfgGrass) return false; 
                if (cfgCactus != 0 && id == cfgCactus) return true; 
                return false;
            }

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
                int gi = z + x * sz;
                int gy = ground[gi];
                if (gy < 0 || gy >= sy - 1) continue;

                int expectedY = gy + 1;
                for (int y = 0; y < sy; y++)
                {
                    int idx = Index(x, y, z, sy, sz);
                    ushort id = vox[idx];
                    if (!IsSurfaceVegLocal(id)) continue;

                    if (y != expectedY)
                    {
                        vox[idx] = 0;
                        continue;
                    }

                    int belowIdx = Index(x, gy, z, sy, sz);
                    ushort below = vox[belowIdx];
                    if (!IsEarthLikeLocal(below))
                    {
                        vox[idx] = 0;
                    }
                }
            }
        }
        
        // ... (Alle 'Place...' Hilfsfunktionen bleiben unverändert) ...
        private void PlacePyramid(NativeArray<ushort> vox, int cx, int cy, int cz, int radius, ushort block)
        {
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
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
                    vox[Index(x, y, z, sy, sz)] = block;
                }
            }
        }
        private void PlaceOasis(NativeArray<ushort> vox, int cx, int cy, int cz)
        {
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
            ushort water = Config.Water;
            ushort grass = Config.Grass;
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                int x = cx + ox;
                int y = cy + 1;
                int z = cz + oz;
                if (!InBounds(x, y, z)) continue;
                if (vox[Index(x, y - 1, z, sy, sz)] == 0) continue; 

                vox[Index(x, y, z, sy, sz)] = water;
                
                int sx0 = x + 1;
                int sx1 = x - 1;
                int sz0 = z + 1;
                int sz1 = z - 1;
                if (InBounds(sx0, y, z) && vox[Index(sx0, y, z, sy, sz)] == 0) vox[Index(sx0, y - 1, z, sy, sz)] = grass;
                if (InBounds(sx1, y, z) && vox[Index(sx1, y, z, sy, sz)] == 0) vox[Index(sx1, y - 1, z, sy, sz)] = grass;
                if (InBounds(x, y, sz0) && vox[Index(x, y, sz0, sy, sz)] == 0) vox[Index(x, y - 1, sz0, sy, sz)] = grass;
                if (InBounds(x, y, sz1) && vox[Index(x, y, sz1, sy, sz)] == 0) vox[Index(x, y - 1, sz1, sy, sz)] = grass;
            }
            var r1 = new Random((uint)(cx * 73856093 ^ cz * 19349663));
            var r2 = new Random((uint)((cx + 7) * 73856093 ^ (cz - 5) * 19349663));
            PlacePalm(vox, cx - 2, cy + 1, cz, ref r1);
            PlacePalm(vox, cx + 2, cy + 1, cz, ref r2);
        }
        private void PlaceIgloo(NativeArray<ushort> vox, int cx, int cy, int cz)
        {
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
            ushort snow = Config.DirtSnowy != 0 ? Config.DirtSnowy : (Config.StoneSnowy != 0 ? Config.StoneSnowy : (ushort)0);
            if (snow == 0) return;
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            for (int oy = 0; oy <= 2; oy++)
            {
                int x = cx + ox;
                int y = cy + oy;
                int z = cz + oz;
                if (!InBounds(x, y, z)) continue;
                if (oy == 0 && ox == 0 && oz == 0) continue;
                if (oy == 1 && ox == 0 && oz == 0) continue;
                vox[Index(x, y, z, sy, sz)] = snow;
            }
            if (InBounds(cx, cy, cz - 1)) vox[Index(cx, cy, cz - 1, sy, sz)] = 0;
            if (InBounds(cx, cy + 1, cz - 1)) vox[Index(cx, cy + 1, cz - 1, sy, sz)] = 0;
        }
        private void PlaceShipwreck(NativeArray<ushort> vox, int cx, int gy, int cz)
        {
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
            ushort planks = Config.Planks;
            if (planks == 0) return;
            for (int ox = -2; ox <= 2; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                int x = cx + ox;
                int y = gy + 1 + math.min(math.abs(ox),1);
                int z = cz + oz;
                if (!InBounds(x, y, z)) continue;
                if(vox[Index(x, y, z, sy, sz)] == (Config.Water != 0 ? Config.Water : (Config.Ice != 0 ? Config.Ice : (ushort)0)))
                    vox[Index(x, y, z, sy, sz)] = planks;
            }
        }
        private void PlacePalm(NativeArray<ushort> vox, int cx, int cy, int cz, ref Random rng)
        {
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
            ushort log = Config.LogOak != 0 ? Config.LogOak : Config.Planks;
            ushort leaves = Config.Leaves != 0 ? Config.Leaves : (Config.LeavesOrange != 0 ? Config.LeavesOrange : (ushort)0);
            if (log == 0) return;
            int h = rng.NextInt(3, 6);
            for (int i = 0; i < h; i++)
            {
                int y = cy + i;
                if (!InBounds(cx, y, cz)) break;
                if (vox[Index(cx, y, cz, sy, sz)] != 0 && vox[Index(cx, y, cz, sy, sz)] != (Config.Water != 0 ? Config.Water : (Config.Ice != 0 ? Config.Ice : (ushort)0))) break;
                vox[Index(cx, y, cz, sy, sz)] = log;
            }
            if (leaves != 0)
            {
                int top = cy + h - 1;
                for (int ox = -2; ox <= 2; ox++)
                for (int oz = -2; oz <= 2; oz++)
                {
                    if (math.abs(ox) + math.abs(oz) > 3) continue;
                    int x = cx + ox;
                    int y = top + rng.NextInt(0,2);
                    int z = cz + oz;
                    if (!InBounds(x, y, z)) continue;
                    if (vox[Index(x, y, z, sy, sz)] == 0 || vox[Index(x, y, z, sy, sz)] == (Config.Water != 0 ? Config.Water : (Config.Ice != 0 ? Config.Ice : (ushort)0)))
                        vox[Index(x, y, z, sy, sz)] = leaves;
                }
            }
        }
        private void PlaceMineShaft(NativeArray<ushort> vox, int cx, int groundY, int cz, int depth)
        {
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
            ushort wood = Config.Planks != 0 ? Config.Planks : (ushort)0;
            for (int y = groundY; y > math.max(1, groundY - depth); y--)
            {
                if (!InBounds(cx, y, cz)) continue;
                vox[Index(cx, y, cz, sy, sz)] = 0;
                if (wood != 0 && ((groundY - y) % 3 == 0) && InBounds(cx + 1, y, cz)) vox[Index(cx + 1, y, cz, sy, sz)] = wood;
                if (wood != 0 && ((groundY - y) % 5 == 0) && InBounds(cx - 1, y, cz)) vox[Index(cx - 1, y, cz, sy, sz)] = wood;
            }
            if (InBounds(cx, groundY + 1, cz)) vox[Index(cx, groundY + 1, cz, sy, sz)] = 0;
        }
    }
}
