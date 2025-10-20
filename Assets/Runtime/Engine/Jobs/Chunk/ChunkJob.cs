using Runtime.Engine.Data;
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

        public void Execute(int index)
        {
            int3 position = Jobs[index];
            // Seed random per job
            Random random = new(RandomSeed + (uint)index);
            Data.Chunk chunk = GenerateChunkData(position, ref random);
            Results.TryAdd(position, chunk);
        }

        private Data.Chunk GenerateChunkData(int3 position, ref Random random)
        {
            Data.Chunk data = new(position, ChunkSize);

            NoiseValue noise = NoiseProfile.GetNoise(position);
            ushort lastVoxelId = GetVoxel(ref noise);

            int count = 0;

            // Loop order should be same as flatten order for AddVoxels to work properly
            for (int x = 0; x < ChunkSize.x; x++)
            for (int z = 0; z < ChunkSize.z; z++)
            for (int y = 0; y < ChunkSize.y; y++)
            {
                noise = NoiseProfile.GetNoise(position + new int3(x, y, z));
                ushort currVoxelId = GetVoxel(ref noise);

                // 40% chance to generate grass (id:5) on grassblock (id:3)
                if (currVoxelId == 0 && lastVoxelId == 3 && random.NextFloat() < 0.4f)
                {
                    currVoxelId = 5;
                }

                if (currVoxelId == lastVoxelId)
                {
                    count++;
                }
                else
                {
                    data.AddVoxels(lastVoxelId, count);
                    lastVoxelId = currVoxelId;
                    count = 1;
                }
            }

            data.AddVoxels(lastVoxelId, count); // Finale interval

            return data;
        }

        private static ushort GetVoxel(ref NoiseValue noise)
        {
            int y = noise.Position.y;

            if (y > noise.Height) return (ushort)(y > noise.WaterLevel ? 0 : 4);
            if (y == noise.Height) return (ushort)(y >= noise.WaterLevel ? 3 : 2);
            if (y <= noise.Height - 1 && y >= noise.Height - 3) return 2;

            return 1;
        }
    }
}