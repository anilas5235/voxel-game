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
        public static bool PositionIsInChunk(ref int3 pos, ref int3 chunkSize)
        {
            return pos.x >= 0 && pos.x < chunkSize.x &&
                   pos.y >= 0 && pos.y < chunkSize.y &&
                   pos.z >= 0 && pos.z < chunkSize.z;
        }
    }
}

