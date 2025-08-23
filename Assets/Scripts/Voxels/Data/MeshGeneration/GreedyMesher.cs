using System;
using System.Collections.Generic;
using UnityEngine;
using static Voxels.Data.VoxelHelper;
using static Voxels.Data.VoxelWorld;

namespace Voxels.Data.MeshGeneration
{
    public class GreedyMesher
    {
        private readonly ChunkData _chunk;
        private MeshData _meshData = new();
        public MeshData MeshData => _meshData;

        private readonly Dictionary<Direction, SliceGreedyMesher[]> _visibleFaces = new();

        public GreedyMesher(ChunkData chunk)
        {
            _chunk = chunk;
            foreach (Direction direction in DirectionUtils.TraversalOrder)
            {
                Vector2Int size = new(ChunkSize, IsVerticalFace(direction) ? ChunkHeight : ChunkSize);
                _visibleFaces[direction] = new SliceGreedyMesher[IsVerticalFace(direction) ? ChunkSize : ChunkHeight];
                for (int i = 0; i < _visibleFaces[direction].Length; i++)
                {
                    _visibleFaces[direction][i] = new SliceGreedyMesher(size.x, size.y, direction, i);
                }
            }
        }

        private bool IsVerticalFace(Direction direction)
        {
            return direction is not (Direction.Up or Direction.Down);
        }

        public static MeshData Run(ChunkData chunk)
        {
            GreedyMesher greedyMesher = new(chunk);
            greedyMesher.GenerateVisibleFaces();
            greedyMesher.AddAllFacesToMeshData();
            return greedyMesher.MeshData;
        }

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
                        PutVisibleFace(direction, pos, voxelType.Id);
                }
            });
        }

        private void PutVisibleFace(Direction direction, Vector3Int position, int voxelId)
        {
            SliceGreedyMesher[] slices = _visibleFaces[direction];
            switch (direction)
            {
                case Direction.Forward:
                case Direction.Backwards:
                    slices[position.z].SetVoxel(position.x, position.y, voxelId);
                    break;
                case Direction.Right:
                case Direction.Left:
                    slices[position.x].SetVoxel(position.z, position.y, voxelId);
                    break;
                case Direction.Up:
                case Direction.Down:
                    slices[position.y].SetVoxel(position.x, position.z, voxelId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        private class SliceGreedyMesher
        {
            private readonly int[,] _data;

            private readonly int _width;
            private readonly int _height;

            private readonly Direction _direction;
            private readonly int _thirdCoord;

            private bool _hasData;

            public SliceGreedyMesher(int width, int height, Direction direction, int thirdCoord)
            {
                _data = new int[width, height];
                _width = width;
                _height = height;
                _direction = direction;
                _thirdCoord = thirdCoord;
            }

            public void SetVoxel(int x, int y, int voxelId)
            {
                _data[x, y] = voxelId;
                _hasData = true;
            }

            public void AddQuadsToMeshData(MeshData meshData)
            {
                List<Quad> quads = GenerateQuads();
                foreach (Quad quad in quads)
                {
                    int voxelId = quad.VoxelId;
                    VoxelType voxelType = VoxelRegistry.Get(voxelId);
                    if (voxelType == null) continue;

                    Vector3[] vertices = GetFaceVertices(_direction, quad);
                    foreach (Vector3 vertex in vertices)
                    {
                        meshData.AddVertex(vertex, voxelType.Collision);
                    }

                    meshData.AddQuadTriangles(voxelType.Collision);
                    meshData.UV.AddRange(FaceUVs(_direction, voxelType, quad.Size));
                }
            }

            private static Vector3[] FaceUVs(Direction direction, VoxelType voxelType, Vector2Int size)
            {
                float texIndex = voxelType.TexIds[(int)direction];
                Vector3[] uvs =
                {
                    // Create UVs in counter-clockwise order with a texture offset to avoid texture bleeding
                    new(size.x - TextureOffset, TextureOffset, texIndex), //bottom right
                    new(size.x - TextureOffset, size.y - TextureOffset, texIndex), //top right
                    new(TextureOffset, size.y - TextureOffset, texIndex), //top left
                    new(TextureOffset, TextureOffset, texIndex) //bottom left
                };

                return uvs;
            }

            private Vector3[] GetFaceVertices(Direction direction, Quad quad)
            {
                Vector3 bl, br, tr, tl;
                switch (direction)
                {
                    case Direction.Forward:
                        bl = new Vector3(quad.BottomLeft.x, quad.BottomLeft.y, _thirdCoord + 1);
                        br = new Vector3(quad.BottomLeft.x + quad.Size.x, quad.BottomLeft.y, _thirdCoord + 1);
                        tr = new Vector3(quad.BottomLeft.x + quad.Size.x, quad.BottomLeft.y + quad.Size.y,
                            _thirdCoord + 1);
                        tl = new Vector3(quad.BottomLeft.x, quad.BottomLeft.y + quad.Size.y, _thirdCoord + 1);
                        break;
                    case Direction.Backwards:
                        bl = new Vector3(quad.BottomLeft.x + quad.Size.x, quad.BottomLeft.y, _thirdCoord);
                        br = new Vector3(quad.BottomLeft.x, quad.BottomLeft.y, _thirdCoord);
                        tr = new Vector3(quad.BottomLeft.x, quad.BottomLeft.y + quad.Size.y, _thirdCoord);
                        tl = new Vector3(quad.BottomLeft.x + quad.Size.x, quad.BottomLeft.y + quad.Size.y,
                            _thirdCoord);
                        break;
                    case Direction.Right:
                        bl = new Vector3(_thirdCoord + 1, quad.BottomLeft.y, quad.BottomLeft.x + quad.Size.x);
                        br = new Vector3(_thirdCoord + 1, quad.BottomLeft.y, quad.BottomLeft.x);
                        tr = new Vector3(_thirdCoord + 1, quad.BottomLeft.y + quad.Size.y, quad.BottomLeft.x);
                        tl = new Vector3(_thirdCoord + 1, quad.BottomLeft.y + quad.Size.y, quad.BottomLeft.x + quad.Size.x);
                        break;
                    case Direction.Left:
                        bl = new Vector3(_thirdCoord, quad.BottomLeft.y, quad.BottomLeft.x);
                        br = new Vector3(_thirdCoord, quad.BottomLeft.y, quad.BottomLeft.x + quad.Size.x);
                        tr = new Vector3(_thirdCoord, quad.BottomLeft.y + quad.Size.y, quad.BottomLeft.x + quad.Size.x);
                        tl = new Vector3(_thirdCoord, quad.BottomLeft.y + quad.Size.y, quad.BottomLeft.x);
                        break;
                    case Direction.Up:
                        bl = new Vector3(quad.BottomLeft.x, _thirdCoord + 1, quad.BottomLeft.y + quad.Size.y);
                        br = new Vector3(quad.BottomLeft.x + quad.Size.x, _thirdCoord + 1, quad.BottomLeft.y + quad.Size.y);
                        tr = new Vector3(quad.BottomLeft.x + quad.Size.x, _thirdCoord + 1, quad.BottomLeft.y);
                        tl = new Vector3(quad.BottomLeft.x, _thirdCoord + 1, quad.BottomLeft.y);
                        break;
                    case Direction.Down:
                        bl = new Vector3(quad.BottomLeft.x, _thirdCoord, quad.BottomLeft.y);
                        br = new Vector3(quad.BottomLeft.x + quad.Size.x, _thirdCoord, quad.BottomLeft.y);
                        tr = new Vector3(quad.BottomLeft.x + quad.Size.x, _thirdCoord, quad.BottomLeft.y + quad.Size.y);
                        tl = new Vector3(quad.BottomLeft.x, _thirdCoord, quad.BottomLeft.y + quad.Size.y);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }

                return new[] { br, tr, tl, bl };
            }

            private List<Quad> GenerateQuads()
            {
                List<Quad> quads = new();
                if (!_hasData) return quads;

                for (int y = 0; y < _height; y++)
                {
                    for (int x = 0; x < _width; x++)
                    {
                        if (_data[x, y] == 0) continue;

                        Quad quad = new(new Vector2Int(x, y), _data[x, y]);

                        // Expand width
                        for (int i = x + 1; i < _width; i++)
                        {
                            if (_data[i, y] != quad.VoxelId) break;
                            quad.Size.x++;
                            _data[i, y] = 0;
                        }
                        // Expand height

                        for (int i = y + 1; i < _height; i++)
                        {
                            bool canExpand = true;
                            for (int j = x; j < x + quad.Size.x; j++)
                            {
                                if (_data[j, i] == quad.VoxelId) continue;
                                canExpand = false;
                                break;
                            }

                            if (!canExpand) break;
                            quad.Size.y++;
                            for (int j = x; j < x + quad.Size.x; j++)
                            {
                                _data[j, i] = 0;
                            }
                        }

                        quads.Add(quad);
                    }
                }

                return quads;
            }
        }

        private class Quad
        {
            public Vector2Int BottomLeft { get; }
            public int VoxelId { get; }
            public Vector2Int Size;

            public Quad(Vector2Int bottomLeft, int voxelId)
            {
                BottomLeft = bottomLeft;
                VoxelId = voxelId;
                Size = new Vector2Int(1, 1);
            }

            public Vector2Int CalculateTopRight()
            {
                return new Vector2Int(BottomLeft.x + Size.x - 1, BottomLeft.y + Size.y - 1);
            }
        }
    }
}