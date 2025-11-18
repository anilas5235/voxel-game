using Unity.Burst;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Chunk
{
    [BurstCompile]
    internal static class ChunkGenerationUtils
    {
        [BurstCompile]
        public static int GetColumnIdx(int x, int z, int sz)
        {
            return z + x * sz;
        }

        [BurstCompile]
        public static bool InChunk(ref int3 pos, ref int3 chunkSize)
        {
            return InChunk(pos.x, pos.y, pos.z, ref chunkSize);
        }

        [BurstCompile]
        public static bool InChunk(int x, int y, int z, ref int3 chunkSize)
        {
            return x >= 0 && x < chunkSize.x &&
                   y >= 0 && y < chunkSize.y &&
                   z >= 0 && z < chunkSize.z;
        }
    }
}
