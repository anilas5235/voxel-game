using Runtime.Engine.Noise;
using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Chunk
{
    /// <summary>
    /// Parallel job that procedurally generates chunk data (terrain, caves, ores, structures, vegetation)
    /// for multiple chunks in parallel.
    /// </summary>
    [BurstCompile]
    public struct ChunkJob : IJobParallelFor
    {
        [ReadOnly] public NoiseProfile NoiseProfile;
        [ReadOnly] public NativeList<int2> Jobs; // Chunk world positions
        [WriteOnly] public NativeParallelHashMap<int2, Data.Chunk>.ParallelWriter Results; // Result mapping
        [ReadOnly] public int RandomSeed;
        [ReadOnly] public GeneratorConfig Config;

        private const float CaveScale = 0.04f; // 3D noise scale for caves (larger features)
        private const int LavaLevel = 5;

        /// <summary>
        /// Executes chunk generation for the given job index and writes the result into the hash map.
        /// </summary>
        /// <param name="index">Index of the chunk position in the <see cref="Jobs"/> list.</param>
        public void Execute(int index)
        {
            int2 position = Jobs[index];
            Data.Chunk chunk = GenerateChunkData(position);
            Results.TryAdd(position, chunk);
        }

        /// <summary>
        /// Generates all voxel data for a given chunk (terrain, ores, caves, structures, vegetation).
        /// </summary>
        private Data.Chunk GenerateChunkData(int2 chunkPos)
        {
            int volume = VoxelsPerChunk;
            int surfaceArea = ChunkSize.x * ChunkSize.z;
            int waterLevel = Config.WaterLevel;

            int3 chunkWorldPos = ChunkSize.MemberMultiply(chunkPos.x, 0, chunkPos.y);

            NativeArray<ushort> vox = new(volume, Allocator.Temp);
            NativeArray<ChunkColumn> chunkColumns = new(surfaceArea, Allocator.Temp);

            ChunkGenerationTerrain.PrepareChunkMaps(ref NoiseProfile, RandomSeed, ref Config, ref chunkWorldPos,
                chunkColumns);
            ChunkGenerationTerrain.FillTerrain(vox, waterLevel, chunkColumns, ref Config);
            ChunkGenerationCavesOres.PlaceOres(vox, Config, RandomSeed);
            ChunkGenerationCavesOres.CarveCaves(vox, chunkWorldPos, chunkColumns, Config, RandomSeed,
                CaveScale, LavaLevel);
            ChunkGenerationStructures.PlaceStructures(ref vox, ref chunkColumns, ref chunkWorldPos,
                RandomSeed, ref Config);
            ChunkGenerationVegetation.PlaceVegetation(ref vox, ref chunkColumns, ref chunkWorldPos,
                RandomSeed, ref Config);

            Data.Chunk data = WriteToChunkData(vox, chunkWorldPos);
            vox.Dispose();
            chunkColumns.Dispose();
            return data;
        }

        /// <summary>
        /// Writes uncompressed voxel data into the chunk using run-length encoding (RLE) compaction.
        /// </summary>
        private Data.Chunk WriteToChunkData(NativeArray<ushort> vox, int3 chunkWordPos)
        {
            Data.Chunk data = new(chunkWordPos.xz);
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