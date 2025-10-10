using Runtime.Engine.Data;
using Runtime.Engine.Noise;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Chunk {

    [BurstCompile]
    public struct ChunkJob : IJobParallelFor {

        [ReadOnly] public int3 ChunkSize;
        [ReadOnly] public NoiseProfile NoiseProfile;

        [ReadOnly] public NativeList<int3> Jobs;
        
        [WriteOnly] public NativeParallelHashMap<int3, Data.Chunk>.ParallelWriter Results;

        public void Execute(int index) {
            int3 position = Jobs[index];

            Data.Chunk chunk = GenerateChunkData(position);

            Results.TryAdd(position, chunk);
        }
        
        private Data.Chunk GenerateChunkData(int3 position) {
            Data.Chunk data = new(position, ChunkSize);
            
            NoiseValue noise = NoiseProfile.GetNoise(position);
            int currentBlock = GetBlock(ref noise);
            
            int count = 0;
        
            // Loop order should be same as flatten order for AddBlocks to work properly
            for (int y = 0; y < ChunkSize.y; y++) {
                for (int z = 0; z < ChunkSize.z; z++) {
                    for (int x = 0; x < ChunkSize.x; x++) {
                        noise = NoiseProfile.GetNoise(position + new int3(x, y, z));
                        
                        int block = GetBlock(ref noise);
        
                        if (block == currentBlock) {
                            count++;
                        } else {
                            data.AddBlocks(currentBlock, count);
                            currentBlock = block;
                            count = 1;
                        }
                    }
                }
            }
            
            data.AddBlocks(currentBlock, count); // Finale interval

            return data;
        }
        
        private static int GetBlock(ref NoiseValue noise) {
            int y = noise.Position.y;

            if (y > noise.Height) return y > noise.WaterLevel ? (int) Block.AIR : (int) Block.WATER;
            if (y == noise.Height) return (int) Block.GRASS;
            if (y <= noise.Height - 1 && y >= noise.Height - 3) return (int)Block.DIRT;

            return (int) Block.STONE;
        }

    }

}