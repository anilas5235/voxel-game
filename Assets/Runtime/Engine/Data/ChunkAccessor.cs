using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Runtime.Engine.Utils.VoxelConstants;

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

        /// <summary>
        /// Constructs a new accessor.
        /// </summary>
        internal ChunkAccessor(NativeParallelHashMap<int2, Chunk>.ReadOnly chunks)
        {
            _chunks = chunks;
        }

        /// <summary>
        /// Voxel lookup within a chunk; remaps out-of-range coordinates to neighbor chunks.
        /// </summary>
        internal ushort GetVoxelInChunk(int2 chunkPos, int3 voxelPos)
        {
            if (voxelPos.y >= ChunkHeight || voxelPos.y < 0) return 0;
            int2 key = int2.zero;

            if (!(voxelPos.x >= 0 && voxelPos.x < ChunkWidth))
            {
                key.x += voxelPos.x % (ChunkWidth - 1);
                voxelPos.x -= key.x * ChunkWidth;
            }
            
            if (!(voxelPos.z >= 0 && voxelPos.z < ChunkDepth))
            {
                key.y += voxelPos.z % (ChunkDepth - 1);
                voxelPos.z -= key.y * ChunkDepth;
            }

            key *= ChunkSizeXY;

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
            int2 px = pos + new int2(1 * ChunkWidth, 0);

            return _chunks.TryGetValue(px, out chunk);
        }

        /// <summary>
        /// Forward (+Z) neighbor.
        /// </summary>
        internal bool TryGetNeighborPz(int2 pos, out Chunk chunk)
        {
            int2 pz = pos + new int2(0, 1 * ChunkDepth);

            return _chunks.TryGetValue(pz, out chunk);
        }

        /// <summary>
        /// Left (-X) neighbor.
        /// </summary>
        internal bool TryGetNeighborNx(int2 pos, out Chunk chunk)
        {
            int2 nx = pos + new int2(-1* ChunkWidth, 0) ;

            return _chunks.TryGetValue(nx, out chunk);
        }

        /// <summary>
        /// Back (-Z) neighbor.
        /// </summary>
        internal bool TryGetNeighborNz(int2 pos, out Chunk chunk)
        {
            int2 nz = pos + new int2(0, -1* ChunkDepth);

            return _chunks.TryGetValue(nz, out chunk);
        }

        #endregion

        /// <summary>
        /// Checks whether a voxel coordinate lies inside chunk bounds.
        /// </summary>
        public static bool InChunkBounds(int3 chunkLocalPos)
        {
            return chunkLocalPos.x >= 0 && chunkLocalPos.x < ChunkWidth &&
                   chunkLocalPos.y >= 0 && chunkLocalPos.y < ChunkHeight &&
                   chunkLocalPos.z >= 0 && chunkLocalPos.z < ChunkDepth;
        }
    }
}