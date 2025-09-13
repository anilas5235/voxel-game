using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Voxels
{
    public class ChunkLoader : MonoBehaviour
    {
        private Vector2Int _chunkPosition;
        public int loadDistance = 3;
        public bool moving = true;

        private void Start()
        {
            _chunkPosition = VoxelWorld.GetChunkPosition(transform.position);
            VoxelWorld.Instance.LoadChunk(ChunksAround(_chunkPosition));
        }

        private List<Vector2Int> ChunksAround(Vector2Int centerChunk)
        {
            List<Vector2Int> chunks = new();
            for (int x = -loadDistance; x <= loadDistance; x++)
            for (int z = -loadDistance; z <= loadDistance; z++)
                chunks.Add(new Vector2Int(centerChunk.x + x, centerChunk.y + z));
            return chunks;
        }

        private void FixedUpdate()
        {
            if (!moving) return;
            Vector2Int newChunkPosition = VoxelWorld.GetChunkPosition(transform.position);
            if (newChunkPosition == _chunkPosition) return;
            List<Vector2Int> old = ChunksAround(_chunkPosition);
            _chunkPosition = newChunkPosition;
            List<Vector2Int> now = ChunksAround(_chunkPosition);
            List<Vector2Int> toLoad = now.Where(chunk => !old.Contains(chunk)).ToList();
            List<Vector2Int> toUnload = old.Where(chunk => !now.Contains(chunk)).ToList();
            VoxelWorld.Instance.LoadChunk(toLoad);
            VoxelWorld.Instance.UnloadChunk(toUnload);
        }
    }
}