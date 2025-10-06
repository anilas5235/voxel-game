using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Voxels;

namespace Test
{
    public class ChunkPosTest : MonoBehaviour
    {
        private void Start()
        {
            List<int3> test = new()
            {
                new int3(0, 0, 0),
                new int3(15, 0, 0),
                new int3(16, 0, 0),
                new int3(31, 0, 0),
                new int3(32, 0, 0),
                new int3(-1, 0, 0),
                new int3(-16, 0, 0),
                new int3(-17, 0, 0),
                new int3(-32, 0, 0),
                new int3(-33, 0, 0),
                new int3(0, 0, 15),
                new int3(0, 0, 16),
                new int3(0, 0, 31),
                new int3(0, 0, 32),
                new int3(0, 0, -1),
                new int3(0, 0, -16),
                new int3(0, 0, -17),
                new int3(0, 0, -32),
                new int3(0, 0, -33),
            };

            foreach (int3 pos in test)
            {
                int2 chunkPos = VoxelWorld.GetChunkPosition(pos);
                int3 voxelPosInChunk = VoxelWorld.GetVoxPosFromWorldVoxPos(chunkPos, pos);
                Debug.Log($"Voxel Pos: {pos}, Chunk Pos: {chunkPos}, Voxel Pos In Chunk: {voxelPosInChunk}");
            }

            // Expected Output:
            // Voxel Pos: (0, 0, 0), Chunk Pos: (0, 0), Voxel Pos In Chunk: (0, 0, 0)
            // Voxel Pos: (15, 0, 0), Chunk Pos: (0, 0), Voxel Pos In Chunk: (15, 0, 0)
            // Voxel Pos: (16, 0, 0), Chunk Pos: (1, 0), Voxel Pos In Chunk: (0, 0, 0)
            // Voxel Pos: (31, 0, 0), Chunk Pos: (1, 0), Voxel Pos In Chunk: (15, 0, 0)
            // Voxel Pos: (32, 0, 0), Chunk Pos: (2, 0), Voxel Pos In Chunk: (0, 0, 0)
            // Voxel Pos: (-1, 0, 0), Chunk Pos: (-1, 0), Voxel Pos In Chunk: (15, 0, 0)
            // Voxel Pos: (-16, 0, 0), Chunk Pos: (-1, 0), Voxel Pos In Chunk: (0, 0, 0)
            // Voxel Pos: (-17, 0, 0), Chunk Pos: (-2, 0), Voxel Pos In Chunk: (15, 0, 0)
            // Voxel Pos: (-32, 0, 0), Chunk Pos: (-2, 0), Voxel Pos In Chunk: (0, 0, 0)
            // Voxel Pos: (-33, 0, 0), Chunk Pos: (-3, 0), Voxel Pos In Chunk: (15, 0, 0)
            // Voxel Pos: (0, 0, 15), Chunk Pos: (0, 0), Voxel Pos In Chunk: (0, 0, 15)
            // Voxel Pos: (0, 0, 16), Chunk Pos: (0, 1), Voxel Pos In Chunk: (0, 0, 0)
            // Voxel Pos: (0, 0, 31), Chunk Pos: (0, 1), Voxel Pos In Chunk: (0, 0, 15)
            // Voxel Pos: (0, 0, 32), Chunk Pos: (0, 2), Voxel Pos In Chunk: (0, 0, 0)
            // Voxel Pos: (0, 0, -1), Chunk Pos: (0, -1), Voxel Pos In Chunk: (0, 0, 15)
            // Voxel Pos: (0, 0, -16), Chunk Pos: (0, -1), Voxel Pos In Chunk: (0, 0, 0)
            // Voxel Pos: (0, 0, -17), Chunk Pos: (0, -2), Voxel Pos In Chunk: (0, 0, 15)
            // Voxel Pos: (0, 0, -32), Chunk Pos: (0, -2), Voxel Pos In Chunk: (0, 0, 0)
            // Voxel Pos: (0, 0, -33), Chunk Pos: (0, -3), Voxel Pos In Chunk: (0, 0, 15)
        }
    }
}