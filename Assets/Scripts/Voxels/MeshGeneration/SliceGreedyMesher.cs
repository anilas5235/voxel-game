using System;
using System.Collections.Generic;
using UnityEngine;
using Voxels.Data;

namespace Voxels.MeshGeneration
{
    /// <summary>
    ///     Handles greedy meshing for a single 2D slice of the chunk in a specific direction.
    /// </summary>
    public class SliceGreedyMesher
    {
        private const float TextureOffset = 0.01f;

        // 2D grid representing the voxel IDs in this slice
        private readonly int[,] _data;

        // The direction this slice faces and its position in that direction
        private readonly Direction _direction;
        private readonly int _height;
        private readonly int _thirdCoord;

        private readonly int _width;

        // Flag to track if there's any data to process
        private bool _hasData;

        public SliceGreedyMesher(int width, int height, Direction direction, int thirdCoord)
        {
            _data = new int[width, height];
            _width = width;
            _height = height;
            _direction = direction;
            _thirdCoord = thirdCoord;
        }

        /// <summary>
        ///     Records a voxel face in this slice.
        /// </summary>
        public void SetVoxel(int x, int y, int voxelId)
        {
            _data[x, y] = voxelId;
            _hasData = true;
        }

        /// <summary>
        ///     Processes this slice to generate optimized quads and adds them to the mesh data.
        /// </summary>
        public void AddQuadsToMeshData(MeshData meshData)
        {
            if (!_hasData) return; // Skip if there's nothing to process

            List<Quad> quads = GenerateQuads();
            foreach (Quad quad in quads)
            {
                VoxelType voxelType = VoxelRegistry.Get(quad.VoxelId);
                if (voxelType == null) continue;

                // Create vertices for this quad based on its direction and position
                Vector3[] positions = GetVerticesForDirection(_direction, quad);
                Vector3 normal = MeshData.GetNormalForDirection(_direction);
                Vector4 tangent = MeshData.GetTangentForDirection(_direction);
                Vector3[] uvs = GenerateFaceUVs(_direction, voxelType, quad.Size);

                // Convert Vector3 UVs to Vector4 (w=0)
                Vector4[] uv4s = new Vector4[4];
                for (int i = 0; i < 4; i++)
                    uv4s[i] = new Vector4(uvs[i].x, uvs[i].y, uvs[i].z, 0);

                for (int i = 0; i < 4; i++)
                    meshData.AddVertex(positions[i], normal, tangent, uv4s[i], voxelType.Collision);

                meshData.AddQuadTriangles(voxelType.Collision);
            }
        }

        /// <summary>
        ///     Generates UV coordinates for a face with texture offsets to prevent bleeding.
        ///     The UVs are ordered: bottom-left, bottom-right, top-right, top-left.
        /// </summary>
        private static Vector3[] GenerateFaceUVs(Direction direction, VoxelType voxelType, Vector2Int size)
        {
            float texIndex = voxelType.TexIds[(int)direction];
            return new Vector3[]
            {
                new(TextureOffset, TextureOffset, texIndex), // Bottom left
                new(size.x - TextureOffset, TextureOffset, texIndex), // Bottom right
                new(size.x - TextureOffset, size.y - TextureOffset, texIndex), // Top right
                new(TextureOffset, size.y - TextureOffset, texIndex) // Top left
            };
        }

        /// <summary>
        ///     Creates the vertices for a quad based on its direction and position.
        ///     Each direction needs different vertex ordering to ensure correct face orientation.
        /// </summary>
        private Vector3[] GetVerticesForDirection(Direction direction, Quad quad)
        {
            Vector3[]
                vertices = new Vector3[4]; // 4 vertices for a quad: bottom-left, bottom-right, top-right, top-left
            Vector2 pos = quad.BottomLeft;
            Vector2 size = quad.Size;

            switch (direction)
            {
                case Direction.Forward: // Z+
                    vertices[0] = new Vector3(pos.x, pos.y, _thirdCoord + 1);
                    vertices[1] = new Vector3(pos.x + size.x, pos.y, _thirdCoord + 1);
                    vertices[2] = new Vector3(pos.x + size.x, pos.y + size.y, _thirdCoord + 1);
                    vertices[3] = new Vector3(pos.x, pos.y + size.y, _thirdCoord + 1);
                    break;
                case Direction.Backward: // Z-
                    vertices[0] = new Vector3(pos.x + size.x, pos.y, _thirdCoord);
                    vertices[1] = new Vector3(pos.x, pos.y, _thirdCoord);
                    vertices[2] = new Vector3(pos.x, pos.y + size.y, _thirdCoord);
                    vertices[3] = new Vector3(pos.x + size.x, pos.y + size.y, _thirdCoord);
                    break;
                case Direction.Right: // X+
                    vertices[0] = new Vector3(_thirdCoord + 1, pos.y, pos.x + size.x);
                    vertices[1] = new Vector3(_thirdCoord + 1, pos.y, pos.x);
                    vertices[2] = new Vector3(_thirdCoord + 1, pos.y + size.y, pos.x);
                    vertices[3] = new Vector3(_thirdCoord + 1, pos.y + size.y, pos.x + size.x);
                    break;
                case Direction.Left: // X-
                    vertices[0] = new Vector3(_thirdCoord, pos.y, pos.x);
                    vertices[1] = new Vector3(_thirdCoord, pos.y, pos.x + size.x);
                    vertices[2] = new Vector3(_thirdCoord, pos.y + size.y, pos.x + size.x);
                    vertices[3] = new Vector3(_thirdCoord, pos.y + size.y, pos.x);
                    break;
                case Direction.Up: // Y+
                    vertices[0] = new Vector3(pos.x, _thirdCoord + 1, pos.y + size.y);
                    vertices[1] = new Vector3(pos.x + size.x, _thirdCoord + 1, pos.y + size.y);
                    vertices[2] = new Vector3(pos.x + size.x, _thirdCoord + 1, pos.y);
                    vertices[3] = new Vector3(pos.x, _thirdCoord + 1, pos.y);
                    break;
                case Direction.Down: // Y-
                    vertices[0] = new Vector3(pos.x, _thirdCoord, pos.y);
                    vertices[1] = new Vector3(pos.x + size.x, _thirdCoord, pos.y);
                    vertices[2] = new Vector3(pos.x + size.x, _thirdCoord, pos.y + size.y);
                    vertices[3] = new Vector3(pos.x, _thirdCoord, pos.y + size.y);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }

            return vertices;
        }

        /// <summary>
        ///     Implements the greedy meshing algorithm to generate optimized quads from the voxel data.
        /// </summary>
        private List<Quad> GenerateQuads()
        {
            List<Quad> quads = new();
            if (!_hasData) return quads;

            // Clone the data array so we can mark processed cells without affecting the original
            int[,] dataCopy = (int[,])_data.Clone();

            // Scan the grid from bottom to top, left to right
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                int voxelId = dataCopy[x, y];
                if (voxelId == 0) continue; // Skip empty or already processed cells

                // Start with a 1x1 quad at the current position
                Quad quad = new(new Vector2Int(x, y), voxelId);

                // Try to expand horizontally first, then vertically
                ExpandQuadHorizontally(quad, dataCopy);
                ExpandQuadVertically(quad, dataCopy);

                quads.Add(quad);
            }

            return quads;
        }

        /// <summary>
        ///     Expands a quad horizontally as far as possible (expanding width).
        /// </summary>
        private void ExpandQuadHorizontally(Quad quad, int[,] data)
        {
            int startX = quad.BottomLeft.x;
            int y = quad.BottomLeft.y;

            // Check each cell to the right of the initial position
            for (int x = startX + 1; x < _width; x++)
            {
                // Stop expanding if we hit a different voxel type
                if (data[x, y] != quad.VoxelId) break;

                // Expand the quad and mark the cell as processed
                quad.Size.x++;
                data[x, y] = 0;
            }
        }

        /// <summary>
        ///     Expands a quad vertically as far as possible (expanding height).
        ///     Can only expand if all cells in the row match the quad's voxel type.
        /// </summary>
        private void ExpandQuadVertically(Quad quad, int[,] data)
        {
            int startX = quad.BottomLeft.x;
            int startY = quad.BottomLeft.y;

            // Check each row above the current one
            for (int y = startY + 1; y < _height; y++)
            {
                // Check if we can expand to this row
                if (!CanExpandToRow(quad, data, startX, y)) break;

                // Expand the quad and clear the row
                quad.Size.y++;
                ClearRow(data, startX, y, quad.Size.x);
            }
        }

        /// <summary>
        ///     Checks if a row can be added to the quad by verifying all cells match the quad's voxel type.
        /// </summary>
        private static bool CanExpandToRow(Quad quad, int[,] data, int startX, int y)
        {
            // Check each cell in the row
            for (int x = startX; x < startX + quad.Size.x; x++)
                if (data[x, y] != quad.VoxelId)
                    return false;

            return true;
        }

        /// <summary>
        ///     Marks a row of cells as processed (sets them to 0).
        /// </summary>
        private static void ClearRow(int[,] data, int startX, int y, int width)
        {
            for (int x = startX; x < startX + width; x++) data[x, y] = 0;
        }
    }
}