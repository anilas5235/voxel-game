using System;
using System.Collections.Generic;
using UnityEngine;
using Voxels.Chunk;
using Voxels.Data;
using static Voxels.VoxelWorld;

namespace Voxels.MeshGeneration
{
    /// <summary>
    ///     Implements greedy meshing algorithm to reduce the number of quads in a voxel mesh by combining adjacent faces with
    ///     the same properties.
    /// </summary>
    public class GreedyMesher
    {
        // Slight offset to prevent texture bleeding in the final render
        private const float TextureOffset = 0.01f;
        private readonly ChunkData _chunk;
        private readonly MeshData _meshData = new();

        // Dictionary that holds slice meshers for each direction
        // Each direction has an array of slices that correspond to the layers in that direction
        private readonly Dictionary<Direction, SliceGreedyMesher[]> _slices = new();

        private GreedyMesher(ChunkData chunk)
        {
            _chunk = chunk;
            InitializeSlices();
        }

        /// <summary>
        ///     Initializes slice meshers for each direction based on chunk dimensions.
        /// </summary>
        private void InitializeSlices()
        {
            foreach (Direction direction in DirectionUtils.TraversalOrder)
            {
                // Determine the dimensions of the slices based on direction
                Vector2Int size = new(ChunkSize, direction.IsVertical() ? ChunkHeight : ChunkSize);
                _slices[direction] = new SliceGreedyMesher[direction.IsVertical() ? ChunkSize : ChunkHeight];

                // Create a mesher for each slice in this direction
                for (int i = 0; i < _slices[direction].Length; i++)
                    _slices[direction][i] = new SliceGreedyMesher(size.x, size.y, direction, i);
            }
        }

        /// <summary>
        ///     Static entry point to run the greedy meshing algorithm on a chunk.
        /// </summary>
        public static MeshData Run(ChunkData chunk)
        {
            GreedyMesher greedyMesher = new(chunk);
            greedyMesher.GenerateVisibleFaces();
            greedyMesher.AddAllFacesToMeshData();
            return greedyMesher._meshData;
        }

        /// <summary>
        ///     Adds all visible faces from all slices to the mesh data.
        /// </summary>
        private void AddAllFacesToMeshData()
        {
            foreach (Direction direction in DirectionUtils.TraversalOrder)
            foreach (SliceGreedyMesher slice in _slices[direction])
                slice.AddQuadsToMeshData(_meshData);
        }

        /// <summary>
        ///     Analyzes the chunk to determine which voxel faces should be visible and records them in the appropriate slice
        ///     meshers.
        /// </summary>
        private void GenerateVisibleFaces()
        {
            ChunkUtils.LoopThroughVoxels(ProcessVoxel);
        }

        /// <summary>
        ///     Processes a single voxel to determine which of its faces should be visible.
        /// </summary>
        private void ProcessVoxel(Vector3Int pos)
        {
            VoxelType voxelType = VoxelRegistry.Get(_chunk.GetVoxel(pos));
            if (voxelType == null) return;

            // Check each direction to see if we need to create a face
            foreach (Direction direction in DirectionUtils.TraversalOrder)
                if (ShouldCreateFace(pos, direction, voxelType))
                    PutVisibleFace(direction, pos, voxelType.Id);
        }

        /// <summary>
        ///     Determines whether a face should be created by checking the neighbor voxel.
        ///     A face is created if:
        ///     - The neighbor is empty (air)
        ///     - The neighbor is transparent and the current voxel is not
        /// </summary>
        private bool ShouldCreateFace(Vector3Int pos, Direction direction, VoxelType currentVoxelType)
        {
            int neighborId = ChunkUtils.GetVoxel(_chunk, pos + direction.GetVector());
            if (neighborId < 0) return false; // Negative IDs are invalid voxels

            VoxelType neighborVoxelType = VoxelRegistry.Get(neighborId);
            return neighborVoxelType == null || (neighborVoxelType.Transparent && !currentVoxelType.Transparent);
        }

        /// <summary>
        ///     Marks a voxel face as visible in the appropriate slice mesher.
        ///     The face is positioned based on the direction and voxel position.
        /// </summary>
        private void PutVisibleFace(Direction direction, Vector3Int position, int voxelId)
        {
            SliceGreedyMesher[] slices = _slices[direction];
            switch (direction)
            {
                case Direction.Forward:
                case Direction.Backwards:
                    // For forward/backward faces, use Z coordinate as the slice index
                    slices[position.z].SetVoxel(position.x, position.y, voxelId);
                    break;
                case Direction.Right:
                case Direction.Left:
                    // For right/left faces, use X coordinate as the slice index
                    slices[position.x].SetVoxel(position.z, position.y, voxelId);
                    break;
                case Direction.Up:
                case Direction.Down:
                    // For up/down faces, use Y coordinate as the slice index
                    slices[position.y].SetVoxel(position.x, position.z, voxelId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }
}