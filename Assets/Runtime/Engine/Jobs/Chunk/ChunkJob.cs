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
        // make biomes larger (lower frequency) and increase separation
        private const float BiomeScale = 0.0012f;
        private const float BiomeExaggeration = 1.8f;
        private const float MountainSnowline = 0.75f; // normalized elevation

        // Height field layering (ported from WorldGeneration)
        private const float ContinentScale = 0.0012f;
        private const float HillScale = 0.01f;
        private const float DetailScale = 0.03f;

        // Extra world-noise params (ported from old WorldGeneration)
        private const float RiverScale = 0.0008f;
        private const float RiverThreshold = -0.15f; // snoise threshold for river channels (higher -> more rivers)
        // use a lower scale for caves so noise varies slower -> larger cave features
        private const float CaveScale = 0.03f; // 3D noise scale for caves

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
            PlaceVegetationAndStructures(vox, ground, biomeMap, origin);

            // Defensive validation: remove any surface-vegetation that ended up not being on a valid earth-like surface
            ValidateSurfaceVegetation(vox, ground);

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

        // Helper to check if a surface column has at least one neighboring top-block (non-air, non-water).
        // Static to avoid capturing 'this' inside the struct job.
        private static bool SurfaceHasNeighbor(NativeArray<ushort> vox, int cx, int cy, int cz, int sx, int sy, int sz, ushort waterId)
        {
            int neighbors = 0;
            // +x
            if (cx + 1 >= 0 && cx + 1 < sx && cy >= 0 && cy < sy && cz >= 0 && cz < sz)
            {
                if (vox[Index(cx + 1, cy, cz, sy, sz)] != 0 && vox[Index(cx + 1, cy, cz, sy, sz)] != waterId) neighbors++;
            }
            // -x
            if (cx - 1 >= 0 && cx - 1 < sx && cy >= 0 && cy < sy && cz >= 0 && cz < sz)
            {
                if (vox[Index(cx - 1, cy, cz, sy, sz)] != 0 && vox[Index(cx - 1, cy, cz, sy, sz)] != waterId) neighbors++;
            }
            // +z
            if (cx >= 0 && cx < sx && cy >= 0 && cy < sy && cz + 1 >= 0 && cz + 1 < sz)
            {
                if (vox[Index(cx, cy, cz + 1, sy, sz)] != 0 && vox[Index(cx, cy, cz + 1, sy, sz)] != waterId) neighbors++;
            }
            // -z
            if (cx >= 0 && cx < sx && cy >= 0 && cy < sy && cz - 1 >= 0 && cz - 1 < sz)
            {
                if (vox[Index(cx, cy, cz - 1, sy, sz)] != 0 && vox[Index(cx, cy, cz - 1, sy, sz)] != waterId) neighbors++;
            }
            return neighbors > 0;
        }

        // Ensure a tree trunk at (cx,cy,cz) would have at least 1 block empty around it (no adjacent trunks).
        private static bool CanPlaceTree(NativeArray<ushort> vox, int cx, int cy, int cz, int sx, int sy, int sz, ushort logA, ushort logB)
        {
            // check a 3x3 area centered on trunk position at trunk base height (cy)
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                if (ox == 0 && oz == 0) continue; // skip center
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

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int wx = origin.x + x;
                int wz = origin.z + z;

                // --- KORRIGIERTE HÖHENLOGIK ---

                // 1. Berechne Kontinent-Noise ZUERST
                // *** DER ENTSCHEIDENDE FIX IST HIER: (noise.snoise * 2f) - 1f ***
                // Dies erzwingt, dass der Noise von [0, 1] auf [-1, 1] gemappt wird.
                float continent = (noise.snoise(new float2(wx * ContinentScale, wz * ContinentScale)) * 2f) - 1f; // Ergibt -1..1
                
                // Passe diesen Schwellenwert an, um Ozeangröße zu ändern (z.B. -0.2f für kleinere Ozeane)
                bool isOcean = continent < -0.15f; 

                float elev; // Dies wird unsere normalisierte Höhe -1..1

                // 2. Berechne Höhe basierend auf Land ODER Ozean
                if (isOcean)
                {
                    // OZEAN-HÖHE:
                    // Der Ozeanboden ist nicht flach, er folgt dem Kontinent-Noise
                    // Wir mappen den Noise-Bereich [-1, -0.15] auf einen niedrigen Höhenbereich
                    elev = -0.5f + (continent + 0.15f) * 0.5f; // Ergibt einen Bereich von ca. -0.5f (tief) bis -0.4f (flach)
                }
                else
                {
                    // LAND-HÖHE: (Deine alte Formel, jetzt nur für Land)
                    float hills = (noise.snoise(new float2(wx * HillScale, wz * HillScale)) * 2f) - 1f; // AUCH HIER ANWENDEN
                    float detail = (noise.snoise(new float2(wx * DetailScale, wz * DetailScale)) * 2f) - 1f; // AUCH HIER ANWENDEN
                    elev = continent * 0.7f + hills * 0.18f + detail * 0.12f;
                }

                // 3. Normalisiere die Höhe (0..1) und berechne groundY
                elev = elev * 0.5f + 0.5f; // Mappt -1..1 zu 0..1
                elev = math.lerp(elev, 0.5f, 0.55f); // Abflachen (angewendet auf Land UND Ozean)
                int groundY = math.clamp((int)math.round(elev * (sy - 1)), 1, sy - 1);

                // --- BIOME-BERECHNUNG (Jetzt funktioniert sie) ---

                // 4. Biome-Noise berechnen (AUCH HIER ANWENDEN)
                float temp = (noise.snoise(new float2((wx + 10000) * BiomeScale, (wz + 10000) * BiomeScale)) * 0.5f + 0.5f);
                float hum = (noise.snoise(new float2((wx - 10000) * BiomeScale, (wz - 10000) * BiomeScale)) * 0.5f + 0.5f);
                temp = math.clamp(math.smoothstep(0f, 1f, temp) - 0.5f, -0.5f, 0.5f) * BiomeExaggeration + 0.5f;
                hum = math.clamp(math.smoothstep(0f, 1f, hum) - 0.5f, -0.5f, 0.5f) * (BiomeExaggeration * 0.9f) + 0.5f;
                float elev01 = sy > 1 ? math.saturate(groundY / (float)(sy - 1)) : 0f;

                // 5. BiomeHelper aufrufen
                // Er bekommt jetzt eine KORREKTE niedrige 'groundY' in Ozeangebieten
                Biome biome = BiomeHelper.SelectBiome(temp, hum, elev01, groundY, waterLevel);
                
                // 6. River mask (AUCH HIER ANWENDEN)
                float rv = (noise.snoise(new float2(wx * RiverScale, wz * RiverScale)) * 2f) - 1f;
                bool isRiver = rv < RiverThreshold;

                int i = z + x * sz;
                ground[i] = groundY;
                biomeMap[i] = (byte)biome;
                riverMap[i] = (byte)(isRiver ? 1 : 0);
            }

            // Post-process smoothing pass on ground heights to create gentler coastal transitions.
            // ########## FIX 2: ERHÖHTE GLÄTTUNGS-PÄSSE ##########
            for (int pass = 0; pass < 30; pass++) // <-- VON 2 AUF 30 ERHÖHT
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
                    if (minNeighbor < waterLevel && gy > minNeighbor + 1)
                    {
                        ground[i] = math.max(minNeighbor + 1, gy - 1);
                    }
                }
                copy.Dispose();
            }

            // Carve river channels: where river mask is set, ensure the column sits below water so rivers become visible.
            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            {
                int i = z + x * sz;
                if (riverMap[i] == 1)
                {
                    // Lower the river core to be at least 2 blocks below water level
                    ground[i] = math.min(ground[i], waterLevel - 2);
                    // gently lower immediate neighbors to form a slope
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

        // ########## FIX 3: KOMPLETT ERSETZTE FUNKTION ##########
        private void FillTerrain(NativeArray<ushort> vox, int waterLevel, NativeArray<int> ground,
            NativeArray<byte> biomeMap, NativeArray<byte> riverMap)
        {
            int sx = ChunkSize.x;
            int sz = ChunkSize.z;
            int sy = ChunkSize.y;
            ushort air = 0;
            // Choose a visible water block id; fallback to Ice if Water unset
            ushort waterBlock = Config.Water != 0 ? Config.Water : (Config.Ice != 0 ? Config.Ice : (ushort)0);
            ushort stone = Config.Stone != 0 ? Config.Stone : Config.Rock;
            ushort dirt = Config.Dirt != 0 ? Config.Dirt : (Config.StoneSandy != 0 ? Config.StoneSandy : stone);

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int i = z + x * sz;
                // use ground[] as the authoritative surface height (already smoothed in PrepareChunkMaps)
                int gy = ground[i];
                 // bool river = riverMap[i] == 1; // Alte Logik wird nicht mehr gebraucht
                 Biome biome = (Biome)biomeMap[i];

                SelectSurfaceMaterials(
                    new ColumnParams { Elev01 = sy > 1 ? math.saturate(gy / (float)(sy - 1)) : 0f, Biome = biome },
                    out ushort top, out ushort under, out ushort st);
                if (st == 0) st = stone;
                if (under == 0) under = dirt;
                if (top == 0) top = dirt;

                // KORRIGIERTE FÜLL-LOGIK (Baut soliden Boden)
                for (int y = 0; y < sy; y++)
                {
                    ushort v;
                    if (y < gy - 4)
                    {
                        // 1. Tiefer Stein
                        v = st;
                    }
                    else if (y < gy)
                    {
                        // 2. Untergrund (z.B. Dirt)
                        v = under;
                    }
                    else if (y == gy)
                    {
                        // 3. Die feste Oberfläche (Sand, Gras, etc.)
                        // Diese wird *immer* platziert, egal ob über oder unter Wasser.
                        v = top;
                    }
                    else // y > gy (Alles über dem festen Boden)
                    {
                        // 4. Fülle mit Wasser bis zum Wasserlevel, darüber Luft
                        v = (y < waterLevel) ? waterBlock : air;
                    }

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
            // include global RandomSeed so world seed changes caves/structures deterministically
            uint seed = (uint)((origin.x * 73856093) ^ (origin.z * 19349663) ^ (int)RandomSeed ^ 0x9E3779B9);
            Random rng = new Random(seed == 0 ? 1u : seed);

            // Generate a few longer/thicker worms that create large connected tunnels
            NativeList<Worm> worms = new NativeList<Worm>(Allocator.Temp);
            int wormCount = rng.NextInt(1, 4); // at least one worm per chunk, up to 3
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
                if (river) continue; // avoid underwater caves in rivers

                for (int y = 1; y < sy - 1; y++)
                {
                    // skip near-surface
                    float nearSurface = (y >= gy - 6) ? 1f : 0f;

                    // 3D noise thresholding: raise base threshold so isolated small pockets are rarer
                    // *** AUCH HIER DEN 0..1 -> -1..1 FIX ANWENDEN ***
                    float n = (noise.snoise(new float3((origin.x + x) * CaveScale, y * CaveScale,
                        (origin.z + z) * CaveScale)) * 2f) - 1f;
                    float threshold = math.lerp(0.36f, 0.64f, nearSurface); // Schwellenwerte an -1..1 angepasst
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
                                (noise.snoise(new float3(t * 0.2f, (origin.x + x) * 0.02f, (origin.z + z) * 0.02f)) * 2f) - 1f,
                                ((noise.snoise(new float3(t * 0.15f, (origin.x + x) * 0.015f, (origin.z + z) * 0.015f)) * 2f) - 1f) *
                                0.4f,
                                (noise.snoise(new float3(t * 0.2f + 37.1f, (origin.x + x) * 0.02f + 17.3f,
                                    (origin.z + z) * 0.02f + 11.7f)) * 2f) - 1f
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

            // Expand caverns along worm paths to create larger connected pockets
            for (int wi = 0; wi < worms.Length; wi++)
            {
                Worm w = worms[wi];
                // sample along the worm path and carve spheres
                float step = math.max(2f, w.Radius * 0.9f);
                for (float t = 0f; t < w.Length; t += step)
                {
                    float3 center = w.Start + w.Dir * t;
                    // convert to chunk-local coords
                    int cx = (int)math.floor(center.x) - origin.x;
                    int cy = (int)math.floor(center.y);
                    int cz = (int)math.floor(center.z) - origin.z;
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

            // Simple smoothing pass: convert some stone blocks adjacent to multiple air blocks into air
            // This helps to connect nearby pockets into larger caverns.
            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            for (int y = 1; y < sy - 1; y++)
            {
                int idx = Index(x, y, z, sy, sz);
                if (vox[idx] == 0 || vox[idx] == Config.Water) continue; // leave cave smoothing as-is; only skips water cells
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

            // After carving caves: occasionally fill low caves with lava to create lava lakes
            // Place lava in cave pockets below a threshold (e.g., well below water level)
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
                        if (vox[idx] != 0) continue; // only in air/cave
                        // Only deep pockets: below ground - 6 and below water level - 8
                        if (y >= gy - 6) continue;
                        // use Generator-configured water level as numeric water height check
                        if (y >= Config.WaterLevel - 8) continue;
                        // noise-based chance to fill with lava
                        float p = math.abs((noise.snoise(new float3((origin.x + x) * 0.12f, y * 0.07f, (origin.z + z) * 0.12f)) * 2f) - 1f);
                        if (p > 0.6f && rng.NextFloat() < 0.35f)
                        {
                            // replace a few cells in a small radius to form lakes
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

                // Exposure check: adjacent to air (6-neighborhood)
                bool exposed =
                    vox[Index(x + 1, y, z, sy, sz)] == 0 ||
                    vox[Index(x - 1, y, z, sy, sz)] == 0 ||
                    vox[Index(x, y + 1, z, sy, sz)] == 0 ||
                    vox[Index(x, y - 1, z, sy, sz)] == 0 ||
                    vox[Index(x, y, z + 1, sy, sz)] == 0 ||
                    vox[Index(x, y, z - 1, sy, sz)] == 0;

                float depthNorm = 1f - (y / (float)sy);
                // *** AUCH HIER DEN 0..1 -> -1..1 FIX ANWENDEN ***
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
            // pick a default mushroom id: Brown > Red > Tan
            ushort mushroom = Config.MushroomBrown != 0 ? Config.MushroomBrown : (Config.MushroomRed != 0 ? Config.MushroomRed : Config.MushroomTan);
            ushort grassF = Config.GrassF != 0 ? Config.GrassF : Config.GrassFDry;
            // visible water id (fallback to ice if Water unset)
            ushort visibleWater = Config.Water != 0 ? Config.Water : (Config.Ice != 0 ? Config.Ice : (ushort)0);

            // Deterministic per-chunk RNG: combine global seed with origin so different seeds produce different worlds
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
                // Springe, wenn die Oberfläche Luft (0) ODER Wasser ist.
                // Dadurch wird verhindert, dass Bäume UNTER Wasser wachsen.
                if (surface == 0 || (visibleWater != 0 && surface == visibleWater)) continue;

                // Neu: nur auf erdeartigen Blöcken Vegetation platzieren
                if (!IsEarthLike(surface)) continue;

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

                // Ocean shipwrecks slightly more frequent
                // *** WICHTIGE PRÜFUNG ***
                // Diese Prüfung muss `surface == visibleWater` sein, um Wracks UNTER Wasser zu platzieren
                if (biome == Biome.Ocean && visibleWater != 0)
                {
                    // Finde den TATSÄCHLICHEN Oberflächenblock (könnte Wasser sein)
                    ushort actualSurface = vox[Index(x, gy, z, sy, sz)];
                    if (actualSurface == visibleWater && rng.NextFloat() < 0.001f)
                    {
                         PlaceShipwreck(vox, x, gy, z); // Hinweis: gy, nicht gy+1
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
            if (id == Config.Sand || id == Config.SandGrey || (Config.SandRed != 0 && id == Config.SandRed)) return true;
            if (Config.StoneSandy != 0 && id == Config.StoneSandy) return true;
            if (Config.SandStoneRed != 0 && id == Config.SandStoneRed) return true;
            if (Config.SandStoneRedSandy != 0 && id == Config.SandStoneRedSandy) return true;
            // treat grass-like fertile surfaces as earth
            if (Config.GrassF != 0 && id == Config.GrassF) return true;
            if (Config.GrassFDry != 0 && id == Config.GrassFDry) return true;
            return false;
        }

        private void PlaceTree(NativeArray<ushort> vox, int x, int y, int z, int height, int idLog, int idLeaves, ref Random rng)
        {
            // If this is a birch trunk, prefer orange leaves when configured
            if (idLog == Config.LogBirch && Config.LeavesOrange != 0)
            {
                idLeaves = Config.LeavesOrange;
            }

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
                        int groundIdx = Index(bx, by, bz, sy, sz);
                        int idx = Index(bx, by + 1, bz, sy, sz);
                        // Neu: nur auf erdeartigen Böden Pilze platzieren und nicht auf bereits belegte Zellen
                        if (!InBounds(bx, by + 1, bz)) continue;
                        if (vox[groundIdx] == 0) continue; // Verhindert Pilze auf Luft
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

        // ########## FIX 4: KOMPLETT ERSETZTE FUNKTION ##########
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
                    // cold rocky/icy surfaces use snowy dirt or stone
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

        // Remove surface vegetation blocks that are not placed directly above a valid ground block (ground + 1)
        private void ValidateSurfaceVegetation(NativeArray<ushort> vox, NativeArray<int> ground)
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
                int gi = z + x * sz;
                int gy = ground[gi];
                if (gy < 0 || gy >= sy - 1) continue;

                int expectedY = gy + 1;
                // check only the column near expected surface and a small neighborhood to be safe
                for (int y = 0; y < sy; y++)
                {
                    int idx = Index(x, y, z, sy, sz);
                    ushort id = vox[idx];
                    if (!IsSurfaceVegLocal(id)) continue;

                    // valid only if exactly at expectedY and block below is earth-like
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

        // Simple helper: place a small square pyramid centered on (cx,cy,cz)
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
                int y = cy + 1; // Oase 1 Block über dem Boden
                int z = cz + oz;
                if (!InBounds(x, y, z)) continue;
                // Stelle sicher, dass wir auf dem Boden sind
                if (vox[Index(x, y - 1, z, sy, sz)] == 0) continue; 

                vox[Index(x, y, z, sy, sz)] = water;
                // surround with grass where possible
                int sx0 = x + 1;
                int sx1 = x - 1;
                int sz0 = z + 1;
                int sz1 = z - 1;
                if (InBounds(sx0, y, z) && vox[Index(sx0, y, z, sy, sz)] == 0) vox[Index(sx0, y - 1, z, sy, sz)] = grass; // Gras auf Boden-Level
                if (InBounds(sx1, y, z) && vox[Index(sx1, y, z, sy, sz)] == 0) vox[Index(sx1, y - 1, z, sy, sz)] = grass; // Gras auf Boden-Level
                if (InBounds(x, y, sz0) && vox[Index(x, y, sz0, sy, sz)] == 0) vox[Index(x, y - 1, sz0, sy, sz)] = grass; // Gras auf Boden-Level
                if (InBounds(x, y, sz1) && vox[Index(x, y, sz1, sy, sz)] == 0) vox[Index(x, y - 1, sz1, sy, sz)] = grass; // Gras auf Boden-Level
            }
            // plant a couple of small palm-like trees at edges
            var r1 = new Random((uint)(cx * 73856093 ^ cz * 19349663));
            var r2 = new Random((uint)((cx + 7) * 73856093 ^ (cz - 5) * 19349663));
            PlacePalm(vox, cx - 2, cy, cz, ref r1); // cy statt cy+1
            PlacePalm(vox, cx + 2, cy, cz, ref r2); // cy statt cy+1
        }

        private void PlaceIgloo(NativeArray<ushort> vox, int cx, int cy, int cz)
        {
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
            ushort snow = Config.DirtSnowy != 0 ? Config.DirtSnowy : (Config.StoneSnowy != 0 ? Config.StoneSnowy : (ushort)0);
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
                if (oy == 1 && ox == 0 && oz == 0) continue; // 1 hoch
                vox[Index(x, y, z, sy, sz)] = snow;
            }
            // doorway
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
                int y = gy + 1 + math.min(math.abs(ox),1); // Auf gy+1 (dem Meeresboden) platzieren
                int z = cz + oz;
                if (!InBounds(x, y, z)) continue;
                // Nur platzieren, wenn es Wasser ist (nicht in einer Unterwasserhöhle)
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
                // Stelle sicher, dass wir nur Luft/Wasser ersetzen
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
                    // Stelle sicher, dass wir nur Luft/Wasser ersetzen
                    if (vox[Index(x, y, z, sy, sz)] == 0 || vox[Index(x, y, z, sy, sz)] == (Config.Water != 0 ? Config.Water : (Config.Ice != 0 ? Config.Ice : (ushort)0)))
                        vox[Index(x, y, z, sy, sz)] = leaves;
                }
            }
        }

        // Simple mineshaft entrance: vertical shaft with a small wooden rim and occasional ladder-like planks
        private void PlaceMineShaft(NativeArray<ushort> vox, int cx, int groundY, int cz, int depth)
        {
            int sy = ChunkSize.y;
            int sz = ChunkSize.z;
            ushort wood = Config.Planks != 0 ? Config.Planks : (ushort)0;
            for (int y = groundY; y > math.max(1, groundY - depth); y--)
            {
                if (!InBounds(cx, y, cz)) continue;
                // carve a 1x1 shaft
                vox[Index(cx, y, cz, sy, sz)] = 0;
                // occasionally place wooden support every 3 blocks
                if (wood != 0 && ((groundY - y) % 3 == 0) && InBounds(cx + 1, y, cz)) vox[Index(cx + 1, y, cz, sy, sz)] = wood;
                if (wood != 0 && ((groundY - y) % 5 == 0) && InBounds(cx - 1, y, cz)) vox[Index(cx - 1, y, cz, sy, sz)] = wood;
            }
            // create small entrance on surface
            if (InBounds(cx, groundY + 1, cz)) vox[Index(cx, groundY + 1, cz, sy, sz)] = 0;
        }
    }
}
