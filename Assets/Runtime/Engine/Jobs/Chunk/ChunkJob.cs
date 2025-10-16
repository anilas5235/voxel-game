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
            ushort currentVoxelId = GetVoxel(ref noise);

            int count = 0;

            // Loop order should be same as flatten order for AddVoxels to work properly
            for (int x = 0; x < ChunkSize.x; x++)
            for (int z = 0; z < ChunkSize.z; z++)
            for (int y = 0; y < ChunkSize.y; y++)
            {
                noise = NoiseProfile.GetNoise(position + new int3(x, y, z));
                ushort voxelId = GetVoxel(ref noise);

                // 50% chance to generate grass (id:5) on grassblock (id:3)
                if (voxelId == 0 && currentVoxelId == 3 && random.NextFloat() < 0.5f)
                {
                    voxelId = 5;
                }

                if (voxelId == currentVoxelId)
                {
                    count++;
                }
                else
                {
                    data.AddVoxels(currentVoxelId, count);
                    currentVoxelId = voxelId;
                    count = 1;
                }
            }

            data.AddVoxels(currentVoxelId, count); // Finale interval

            return data;
        }

        private static ushort GetVoxel(ref NoiseValue noise)
        {
            int y = noise.Position.y;

            if (y > noise.Height) return (ushort)(y > noise.WaterLevel ? 0 : 4);
            if (y == noise.Height) return 3;
            if (y <= noise.Height - 1 && y >= noise.Height - 3) return 2;

            return 1;
        }
    }
}