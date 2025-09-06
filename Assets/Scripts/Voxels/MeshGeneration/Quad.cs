using UnityEngine;

namespace Voxels.MeshGeneration
{
    /// <summary>
    ///     Represents a merged rectangular group of identical voxel faces.
    /// </summary>
    public class Quad
    {
        /// <summary>
        ///     The width and height of the quad in voxel units.
        /// </summary>
        public Vector2Int Size;

        public Quad(Vector2Int bottomLeft, int voxelId)
        {
            BottomLeft = bottomLeft;
            VoxelId = voxelId;
            Size = new Vector2Int(1, 1); // Start with a 1x1 quad
        }

        /// <summary>
        ///     The bottom-left corner of the quad in 2D slice coordinates.
        /// </summary>
        public Vector2Int BottomLeft { get; }

        /// <summary>
        ///     The voxel ID for all cells in this quad.
        /// </summary>
        public int VoxelId { get; }
    }
}