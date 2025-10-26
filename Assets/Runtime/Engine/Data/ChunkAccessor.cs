using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Data
{
    [BurstCompile]
    internal readonly struct ChunkAccessor
    {
        private readonly NativeParallelHashMap<int3, Chunk>.ReadOnly _chunks;
        private readonly int3 _chunkSize;

        internal ChunkAccessor(NativeParallelHashMap<int3, Chunk>.ReadOnly chunks, int3 chunkSize)
        {
            _chunks = chunks;
            _chunkSize = chunkSize;
        }

        internal ushort GetVoxelInChunk(int3 chunkPos, int3 voxelPos)
        {
            int3 key = int3.zero;

            for (int index = 0; index < 3; index++)
            {
                if (voxelPos[index] >= 0 && voxelPos[index] < _chunkSize[index]) continue;

                key[index] += voxelPos[index] % (_chunkSize[index] - 1);
                voxelPos[index] -= key[index] * _chunkSize[index];
            }

            key *= _chunkSize;

            return TryGetChunk(chunkPos + key, out Chunk chunk) ? chunk.GetVoxel(voxelPos) : (ushort)0;
        }

        internal bool TryGetChunk(int3 pos, out Chunk chunk) => _chunks.TryGetValue(pos, out chunk);

        internal bool ContainsChunk(int3 coord) => _chunks.ContainsKey(coord);

        #region Try Neighbours

        internal bool TryGetNeighborPx(int3 pos, out Chunk chunk)
        {
            int3 px = pos + new int3(1, 0, 0) * _chunkSize;

            return _chunks.TryGetValue(px, out chunk);
        }

        internal bool TryGetNeighborPz(int3 pos, out Chunk chunk)
        {
            int3 pz = pos + new int3(0, 0, 1) * _chunkSize;

            return _chunks.TryGetValue(pz, out chunk);
        }

        internal bool TryGetNeighborNx(int3 pos, out Chunk chunk)
        {
            int3 nx = pos + new int3(-1, 0, 0) * _chunkSize;

            return _chunks.TryGetValue(nx, out chunk);
        }

        internal bool TryGetNeighborNz(int3 pos, out Chunk chunk)
        {
            int3 nz = pos + new int3(0, 0, -1) * _chunkSize;

            return _chunks.TryGetValue(nz, out chunk);
        }

        #endregion

        public bool InChunkBounds(int3 chunkItr)
        {
            return chunkItr.x >= 0 && chunkItr.x < _chunkSize.x &&
                   chunkItr.y >= 0 && chunkItr.y < _chunkSize.y &&
                   chunkItr.z >= 0 && chunkItr.z < _chunkSize.z;
        }
    }
}