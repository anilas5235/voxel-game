using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using Utils;

namespace Voxels
{
    public class ChunkLoader : MonoBehaviour
    {
        private int2 _chunkPosition;
        public int loadDistance = 3;
        public bool moving = true;

        private void Start()
        {
            _chunkPosition = VoxelWorld.GetChunkPosition(transform.position);
        }

        private List<int2> ChunksAround(int2 centerChunk)
        {
            List<int2> chunks = new();
            for (int x = -loadDistance; x <= loadDistance; x++)
            for (int z = -loadDistance; z <= loadDistance; z++)
                chunks.Add(new int2(centerChunk.x + x, centerChunk.y + z));
            return chunks;
        }

        private void FixedUpdate()
        {
            if (!moving) return;
            int2 newChunkPosition = VoxelWorld.GetChunkPosition(transform.position);
            if ((newChunkPosition == _chunkPosition).AndReduce()) return;
            List<int2> old = ChunksAround(_chunkPosition);
            _chunkPosition = newChunkPosition;
            List<int2> now = ChunksAround(_chunkPosition);
            List<int2> toLoad = now.Where(chunk => !old.Contains(chunk)).ToList();
            List<int2> toUnload = old.Where(chunk => !now.Contains(chunk)).ToList();
        }
    }
}