using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Voxels.Chunk;
using Voxels.Data;

namespace Voxels.MeshGeneration
{
    /// <summary>
    ///     Implements greedy meshing algorithm to reduce the number of quads in a voxel mesh by combining adjacent faces with
    ///     the same properties.
    /// </summary>
    public class GreedyMesher
    {
        public Mesh.MeshData MeshData { get; }
        private readonly ChunkData _chunk;
        internal readonly MeshData _meshData = new();

        private readonly int3 _chunkSize; // Store chunk size

        // Dictionary that holds slice meshers for each direction
        // Each direction has an array of slices that correspond to the layers in that direction
        private readonly Dictionary<Direction, SliceGreedyMesher[]> _slices = new();

        public GreedyMesher(ChunkData chunk, Mesh.MeshData meshData, int3 chunkSize)
        {
            MeshData = meshData;
            _chunk = chunk;
            _chunkSize = chunkSize;
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
                int2 size = new(
                    _chunkSize.x,
                    direction.IsVertical() ? _chunkSize.y : _chunkSize.z
                );
                _slices[direction] = new SliceGreedyMesher[
                    direction.IsVertical() ? _chunkSize.x : _chunkSize.y
                ];

                // Create a mesher for each slice in this direction
                for (int i = 0; i < _slices[direction].Length; i++)
                    _slices[direction][i] = new SliceGreedyMesher(size.x, size.y, direction, i);
            }
        }

        /// <summary>
        ///     Adds all visible faces from all slices to the mesh data.
        /// </summary>
        internal void AddAllFacesToMeshData()
        {
            foreach (Direction direction in DirectionUtils.TraversalOrder)
            foreach (SliceGreedyMesher slice in _slices[direction])
                slice.AddQuadsToMeshData(_meshData);
        }

        public void WriteMeshData()
        {
            _meshData.WriteTo(MeshData);
        }

        /// <summary>
        ///     Analyzes the chunk to determine which voxel faces should be visible and records them in the appropriate slice
        ///     meshers.
        /// </summary>
        internal void GenerateVisibleFaces()
        {
            ChunkUtils.LoopThroughVoxels(ProcessVoxel);
        }

        /// <summary>
        ///     Processes a single voxel to determine which of its faces should be visible.
        /// </summary>
        private void ProcessVoxel(int3 pos)
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
        private bool ShouldCreateFace(int3 pos, Direction direction, VoxelType currentVoxelType)
        {
            bool valid = ChunkUtils.GetVoxel(_chunk, pos + direction.GetInt3(), out ushort neighborId);
            if (!valid) return false;
            VoxelType neighborVoxelType = VoxelRegistry.Get(neighborId);
            // Only draw face if neighbor is air or transparent (and current is not transparent)
            if (neighborVoxelType == null) return true;
            if (neighborVoxelType.Transparent && !currentVoxelType.Transparent) return true;
            // Otherwise, neighbor is solid, do not draw face
            return false;
        }

        /// <summary>
        ///     Marks a voxel face as visible in the appropriate slice mesher.
        ///     The face is positioned based on the direction and voxel position.
        /// </summary>
        private void PutVisibleFace(Direction direction, int3 position, ushort voxelId)
        {
            SliceGreedyMesher[] slices = _slices[direction];
            switch (direction)
            {
                case Direction.Forward:
                case Direction.Backward:
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