using Runtime.Engine.Noise;
using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Chunk
{
    /// <summary>
    /// Parallel Job zur prozeduralen Generierung einzelner Chunk-Daten (Terrain, Höhlen, Strukturen, Vegetation).
    /// Nutzt NoiseProfile und GeneratorConfig zur Erzeugung und schreibt komprimierte Voxel-Intervalle.
    /// </summary>
    [BurstCompile]
    public struct ChunkJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkSize;
        [ReadOnly] public NoiseProfile NoiseProfile;
        [ReadOnly] public NativeList<int3> Jobs; // Chunk Weltpositionen
        [WriteOnly] public NativeParallelHashMap<int3, Data.Chunk>.ParallelWriter Results; // Ergebnis Mapping
        [ReadOnly] public int RandomSeed;
        [ReadOnly] public GeneratorConfig Config;

        private const float CaveScale = 0.04f; // 3D noise scale für Höhlen (größere Merkmale)
        private const int LavaLevel = 5;

        /// <summary>
        /// Führt Generierung für Job-Index aus und schreibt Chunk-Daten in Results.
        /// </summary>
        public void Execute(int index)
        {
            int3 position = Jobs[index];
            Data.Chunk chunk = GenerateChunkData(position);
            Results.TryAdd(position, chunk);
        }

        /// <summary>
        /// Erzeugt alle Voxel für gegebenen Chunk (Terrain, Ores, Caves, Structures, Vegetation).
        /// </summary>
        private Data.Chunk GenerateChunkData(int3 chunkWordPos)
        {
            int volume = ChunkSize.x * ChunkSize.y * ChunkSize.z;
            int surfaceArea = ChunkSize.x * ChunkSize.z;
            int waterLevel = Config.WaterLevel;

            NativeArray<ushort> vox = new(volume, Allocator.Temp);
            NativeArray<ChunkColumn> chunkColumns = new(surfaceArea, Allocator.Temp);

            ChunkGenerationTerrain.PrepareChunkMaps(ref ChunkSize, ref NoiseProfile, RandomSeed, ref Config,
                ref chunkWordPos, chunkColumns);
            ChunkGenerationTerrain.FillTerrain(ref ChunkSize, vox, waterLevel, chunkColumns, ref Config);
            ChunkGenerationCavesOres.PlaceOres(ChunkSize, vox, Config, RandomSeed);
            ChunkGenerationCavesOres.CarveCaves(ChunkSize, vox, chunkWordPos, chunkColumns, Config, RandomSeed,
                CaveScale, LavaLevel);
            ChunkGenerationStructures.PlaceStructures(ref vox, ref chunkColumns, ref chunkWordPos, ref ChunkSize,
                RandomSeed, ref Config);
            ChunkGenerationVegetation.PlaceVegetation(ref vox, ref chunkColumns, ref chunkWordPos, ref ChunkSize,
                RandomSeed, ref Config);

            Data.Chunk data = WriteToChunkData(vox, chunkWordPos);
            vox.Dispose();
            chunkColumns.Dispose();
            return data;
        }

        /// <summary>
        /// Schreibt unkomprimierte Voxel-Daten in Chunk mit RLE-Kompaktierung.
        /// </summary>
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
    }
}