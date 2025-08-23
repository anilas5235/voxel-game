using System;
using System.Collections.Generic;
using UnityEngine;
using static Voxels.Data.VoxelWorld;

namespace Voxels.Data.MeshGeneration
{
    /// <summary>
    /// Implements greedy meshing algorithm to reduce the number of quads in a voxel mesh by combining adjacent faces with the same properties.
    /// </summary>
    public class GreedyMesher
    {
        // Slight offset to prevent texture bleeding in the final render
        private const float TextureOffset = 0.01f;
        private readonly ChunkData _chunk;
        private readonly MeshData _meshData = new();

        // Dictionary that holds slice meshers for each direction
        // Each direction has an array of slices that correspond to the layers in that direction
        private readonly Dictionary<Direction, SliceGreedyMesher[]> _visibleFaces = new();

        private GreedyMesher(ChunkData chunk)
        {
            _chunk = chunk;
            InitializeVisibleFaces();
        }

        /// <summary>
        /// Initializes slice meshers for each direction based on chunk dimensions.
        /// </summary>
        private void InitializeVisibleFaces()
        {
            foreach (Direction direction in DirectionUtils.TraversalOrder)
            {
                // Determine the dimensions of the slices based on direction
                Vector2Int size = new(ChunkSize, direction.IsVertical() ? ChunkHeight : ChunkSize);
                _visibleFaces[direction] = new SliceGreedyMesher[direction.IsVertical() ? ChunkSize : ChunkHeight];
                
                // Create a mesher for each slice in this direction
                for (int i = 0; i < _visibleFaces[direction].Length; i++)
                {
                    _visibleFaces[direction][i] = new SliceGreedyMesher(size.x, size.y, direction, i);
                }
            }
        }

        /// <summary>
        /// Static entry point to run the greedy meshing algorithm on a chunk.
        /// </summary>
        public static MeshData Run(ChunkData chunk)
        {
            GreedyMesher greedyMesher = new(chunk);
            greedyMesher.GenerateVisibleFaces();
            greedyMesher.AddAllFacesToMeshData();
            return greedyMesher._meshData;
        }

        /// <summary>
        /// Adds all visible faces from all slices to the mesh data.
        /// </summary>
        private void AddAllFacesToMeshData()
        {
            foreach (Direction direction in DirectionUtils.TraversalOrder)
            {
                foreach (SliceGreedyMesher slice in _visibleFaces[direction])
                {
                    slice.AddQuadsToMeshData(_meshData);
                }
            }
        }

        /// <summary>
        /// Analyzes the chunk to determine which voxel faces should be visible and records them in the appropriate slice meshers.
        /// </summary>
        private void GenerateVisibleFaces()
        {
            Chunk.LoopThroughVoxels(pos =>
            {
                VoxelType voxelType = VoxelRegistry.Get(_chunk.GetVoxel(pos));
                if (voxelType == null) return; // Skip empty voxels

                // Check each direction to see if we need to create a face
                foreach (Direction direction in DirectionUtils.TraversalOrder)
                {
                    if (ShouldCreateFace(pos, direction, voxelType))
                    {
                        PutVisibleFace(direction, pos, voxelType.Id);
                    }
                }
            });
        }

        /// <summary>
        /// Determines whether a face should be created by checking the neighbor voxel.
        /// A face is created if:
        /// - The neighbor is empty (air)
        /// - The neighbor is transparent and the current voxel is not
        /// </summary>
        private bool ShouldCreateFace(Vector3Int pos, Direction direction, VoxelType currentVoxelType)
        {
            int neighborId = Chunk.GetVoxel(_chunk, pos + direction.GetVector());
            if (neighborId < 0) return false; // Negative IDs are invalid voxels

            VoxelType neighborVoxelType = VoxelRegistry.Get(neighborId);
            return neighborVoxelType == null || (neighborVoxelType.Transparent && !currentVoxelType.Transparent);
        }

        /// <summary>
        /// Marks a voxel face as visible in the appropriate slice mesher.
        /// The face is positioned based on the direction and voxel position.
        /// </summary>
        private void PutVisibleFace(Direction direction, Vector3Int position, int voxelId)
        {
            SliceGreedyMesher[] slices = _visibleFaces[direction];
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

        /// <summary>
        /// Handles greedy meshing for a single 2D slice of the chunk in a specific direction.
        /// </summary>
        private class SliceGreedyMesher
        {
            // 2D grid representing the voxel IDs in this slice
            private readonly int[,] _data;

            private readonly int _width;
            private readonly int _height;

            // The direction this slice faces and its position in that direction
            private readonly Direction _direction;
            private readonly int _thirdCoord;

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
            /// Records a voxel face in this slice.
            /// </summary>
            public void SetVoxel(int x, int y, int voxelId)
            {
                _data[x, y] = voxelId;
                _hasData = true;
            }

            /// <summary>
            /// Processes this slice to generate optimized quads and adds them to the mesh data.
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
                    Vector3[] vertices = GetVerticesForDirection(_direction, quad);
                    foreach (Vector3 vertex in vertices)
                    {
                        meshData.AddVertex(vertex, voxelType.Collision);
                    }

                    // Add triangles and UVs for the quad
                    meshData.AddQuadTriangles(voxelType.Collision);
                    meshData.UV.AddRange(GenerateFaceUVs(_direction, voxelType, quad.Size));
                }
            }

            /// <summary>
            /// Generates UV coordinates for a face with texture offsets to prevent bleeding.
            /// The UVs are ordered: bottom-left, bottom-right, top-right, top-left.
            /// </summary>
            private static Vector3[] GenerateFaceUVs(Direction direction, VoxelType voxelType, Vector2Int size)
            {
                float texIndex = voxelType.TexIds[(int)direction];
                return new Vector3[]
                {
                    new(TextureOffset, TextureOffset, texIndex), // Bottom left
                    new(size.x - TextureOffset, TextureOffset, texIndex), // Bottom right
                    new(size.x - TextureOffset, size.y - TextureOffset, texIndex), // Top right
                    new(TextureOffset, size.y - TextureOffset, texIndex), // Top left
                };
            }

            /// <summary>
            /// Creates the vertices for a quad based on its direction and position.
            /// Each direction needs different vertex ordering to ensure correct face orientation.
            /// </summary>
            private Vector3[] GetVerticesForDirection(Direction direction, Quad quad)
            {
                Vector3[] vertices = new Vector3[4]; // 4 vertices for a quad: bottom-left, bottom-right, top-right, top-left
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
                    case Direction.Backwards: // Z-
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
            /// Implements the greedy meshing algorithm to generate optimized quads from the voxel data.
            /// </summary>
            private List<Quad> GenerateQuads()
            {
                List<Quad> quads = new();
                if (!_hasData) return quads;

                // Clone the data array so we can mark processed cells without affecting the original
                int[,] dataCopy = (int[,])_data.Clone();

                // Scan the grid from bottom to top, left to right
                for (int y = 0; y < _height; y++)
                {
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
                }

                return quads;
            }

            /// <summary>
            /// Expands a quad horizontally as far as possible (expanding width).
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
            /// Expands a quad vertically as far as possible (expanding height).
            /// Can only expand if all cells in the row match the quad's voxel type.
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
            /// Checks if a row can be added to the quad by verifying all cells match the quad's voxel type.
            /// </summary>
            private static bool CanExpandToRow(Quad quad, int[,] data, int startX, int y)
            {
                // Check each cell in the row
                for (int x = startX; x < startX + quad.Size.x; x++)
                {
                    if (data[x, y] != quad.VoxelId) return false;
                }
                return true;
            }

            /// <summary>
            /// Marks a row of cells as processed (sets them to 0).
            /// </summary>
            private static void ClearRow(int[,] data, int startX, int y, int width)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    data[x, y] = 0;
                }
            }
        }

        /// <summary>
        /// Represents a merged rectangular group of identical voxel faces.
        /// </summary>
        private class Quad
        {
            /// <summary>
            /// The bottom-left corner of the quad in 2D slice coordinates.
            /// </summary>
            public Vector2Int BottomLeft { get; }
            
            /// <summary>
            /// The voxel ID for all cells in this quad.
            /// </summary>
            public int VoxelId { get; }
            
            /// <summary>
            /// The width and height of the quad in voxel units.
            /// </summary>
            public Vector2Int Size;

            public Quad(Vector2Int bottomLeft, int voxelId)
            {
                BottomLeft = bottomLeft;
                VoxelId = voxelId;
                Size = new Vector2Int(1, 1); // Start with a 1x1 quad
            }
        }
    }
}