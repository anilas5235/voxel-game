using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Data
{
    /// <summary>
    /// Read-only accessor spanning multiple chunks and their neighbors (wrap-around for border traversal).
    /// Provides utilities for neighbor checks and voxel lookups across chunk boundaries.
    /// </summary>
    [BurstCompile]
    internal readonly struct ChunkAccessor
    {
        private readonly NativeParallelHashMap<int2, Chunk>.ReadOnly _chunks;
        private readonly int3 _chunkSize;

        /// <summary>
        /// Constructs a new accessor.
        /// </summary>
        internal ChunkAccessor(NativeParallelHashMap<int2, Chunk>.ReadOnly chunks, int3 chunkSize)
        {
            _chunks = chunks;
            _chunkSize = chunkSize;
        }

        /// <summary>
        /// Voxel lookup within a chunk; remaps out-of-range coordinates to neighbor chunks.
        /// </summary>
        internal ushort GetVoxelInChunk(int2 chunkPos, int3 voxelPos)
        {
            if (voxelPos.y >= _chunkSize.y || voxelPos.y < 0) return 0;
            int2 key = int2.zero;

            for (int index = 0; index < 3; index++)
            {
                if (voxelPos[index] >= 0 && voxelPos[index] < _chunkSize[index]) continue;

                key[index] += voxelPos[index] % (_chunkSize[index] - 1);
                voxelPos[index] -= key[index] * _chunkSize[index];
            }

            key *= _chunkSize.xz;

            return TryGetChunk(chunkPos + key, out Chunk chunk) ? chunk.GetVoxel(voxelPos) : (ushort)0;
        }

        /// <summary>
        /// Attempts to get a chunk at a position.
        /// </summary>
        internal bool TryGetChunk(int2 pos, out Chunk chunk) => _chunks.TryGetValue(pos, out chunk);

        /// <summary>
        /// Checks whether a chunk exists.
        /// </summary>
        internal bool ContainsChunk(int2 coord) => _chunks.ContainsKey(coord);

        #region Try Neighbours

        /// <summary>
        /// Right (+X) neighbor.
        /// </summary>
        internal bool TryGetNeighborPx(int2 pos, out Chunk chunk)
        {
            int2 px = pos + new int2(1 * _chunkSize.x, 0);

            return _chunks.TryGetValue(px, out chunk);
        }

        /// <summary>
        /// Forward (+Z) neighbor.
        /// </summary>
        internal bool TryGetNeighborPz(int2 pos, out Chunk chunk)
        {
            int2 pz = pos + new int2(0, 1 * _chunkSize.z);

            return _chunks.TryGetValue(pz, out chunk);
        }

        /// <summary>
        /// Left (-X) neighbor.
        /// </summary>
        internal bool TryGetNeighborNx(int2 pos, out Chunk chunk)
        {
            int2 nx = pos + new int2(-1* _chunkSize.x, 0) ;

            return _chunks.TryGetValue(nx, out chunk);
        }

        /// <summary>
        /// Back (-Z) neighbor.
        /// </summary>
        internal bool TryGetNeighborNz(int2 pos, out Chunk chunk)
        {
            int2 nz = pos + new int2(0, -1* _chunkSize.z);

            return _chunks.TryGetValue(nz, out chunk);
        }

        #endregion

        /// <summary>
        /// Checks whether a voxel coordinate lies inside chunk bounds.
        /// </summary>
        public bool InChunkBounds(int3 chunkItr)
        {
            return chunkItr.x >= 0 && chunkItr.x < _chunkSize.x &&
                   chunkItr.y >= 0 && chunkItr.y < _chunkSize.y &&
                   chunkItr.z >= 0 && chunkItr.z < _chunkSize.z;
        }
    }
}