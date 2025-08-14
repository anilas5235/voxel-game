using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxels.Data.MeshGeneration
{
    public class GreedyMesher
    {
        private readonly ChunkData _chunk;
        private MeshData _meshData = new();

        private readonly Dictionary<Direction, Dictionary<Vector3Int, int>> _visibleFaces = new()
        {
            { Direction.Up, new Dictionary<Vector3Int, int>() },
            { Direction.Down, new Dictionary<Vector3Int, int>() },
            { Direction.Forward, new Dictionary<Vector3Int, int>() },
            { Direction.Backwards, new Dictionary<Vector3Int, int>() },
            { Direction.Right, new Dictionary<Vector3Int, int>() },
            { Direction.Left, new Dictionary<Vector3Int, int>() }
        };

        public GreedyMesher(ChunkData chunk)
        {
            _chunk = chunk;
        }

        public void Run()
        {
            GenerateVisibleFaces();
            DoXY();
            DoXZ();
            DoYZ();
        }

        private void GenerateVisibleFaces()
        {
            Chunk.LoopThroughVoxels(pos =>
            {
                VoxelType voxelType = VoxelRegistry.Get(_chunk.GetVoxel(pos));
                if (voxelType == null) return;

                foreach (Direction direction in DirectionUtils.TraversalOrder)
                {
                    int neighborId = Chunk.GetVoxel(_chunk, pos + direction.GetVector());
                    if (neighborId < 0) continue;
                    VoxelType neighbourVoxelType = VoxelRegistry.Get(neighborId);
                    if (neighbourVoxelType == null || (neighbourVoxelType.Transparent && !voxelType.Transparent))
                        _visibleFaces[direction][pos] = voxelType.Id;
                }
            });
        }

        private void DoYZ()
        {
            for (int x = 0; x < VoxelWorld.ChunkHeight; x++)
            {
                for (int z = 0; z < VoxelWorld.ChunkSize; z++)
                {
                }
            }
        }

        private void DoXZ()
        {
            throw new NotImplementedException();
        }

        private void DoXY()
        {
            for (int z = 0; z < VoxelWorld.ChunkSize; z++)
            {
                for (int x = 0; x < VoxelWorld.ChunkSize; x++)
                {
                    for (int y = 0; y < VoxelWorld.ChunkHeight; y++)
                    {
                    }
                }
            }
        }
    }
}