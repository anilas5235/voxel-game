using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Chunk
{
    [BurstCompile]
    internal static class ChunkGenerationCavesOres
    {
        public static void CarveCaves(int3 chunkSize, NativeArray<ushort> vox, int3 origin,
            NativeArray<ChunkColumn> chunkColumns, GeneratorConfig config, int randomSeed,
            float caveScale, int lavaLevel)
        {
            int sx = chunkSize.x;
            int sz = chunkSize.z;

            for (int x = 0; x < sx; x++)
            for (int z = 0; z < sz; z++)
            {
                int height = chunkColumns[ChunkGenerationUtils.GetColumnIdx(x, z, sz)].Height;
                for (int y = 2; y <= height; y++)
                {
                    int idx = chunkSize.Flatten(x, y, z);

                    float3 noiseSamplePos = (origin + new float3(x + randomSeed, y - randomSeed, z + randomSeed)) *
                                            caveScale;

                    float sCaveNoise = noise.snoise(noiseSamplePos) * .5f + .5f;
                    float cellNoise = noise.cellular(noiseSamplePos).x * .5f + .5f;

                    bool sCarve = math.square(sCaveNoise) + math.square(cellNoise) >
                                  math.lerp(.8f, 1.3f, math.square(y / (float)height));

                    if (sCarve)
                    {
                        vox[idx] = 0;
                        if (y <= lavaLevel)
                        {
                            vox[idx] = config.Lava;
                        }
                    }
                }
            }
        }

        public static void PlaceOres(int3 chunkSize, NativeArray<ushort> vox, GeneratorConfig config, int randomSeed)
        {
            int sx = chunkSize.x;
            int sz = chunkSize.z;
            int sy = chunkSize.y;
            ushort stone = config.Stone;
            ushort oreCoal = config.StoneCoalOre;
            ushort oreIron = config.StoneIronGreenOre;
            ushort oreGold = config.StoneGoldOre;
            ushort oreDiamond = config.StoneDiamondOre;

            for (int x = 1; x < sx - 1; x++)
            for (int z = 1; z < sz - 1; z++)
            for (int y = 2; y < sy - 2; y++)
            {
                int idx = chunkSize.Flatten(x, y, z);
                if (vox[idx] != stone) continue;

                float depthNorm = 1f - y / (float)sy;
                float oreNoise = math.abs(noise.snoise(new float3(randomSeed + x, randomSeed + y,
                    randomSeed - z) * 0.12f));
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
    }
}