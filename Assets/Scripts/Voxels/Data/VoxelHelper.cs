using System;
using UnityEngine;
using static Voxels.Data.VoxelTextureManager;

namespace Voxels.Data
{
    public static class VoxelHelper
    {
        private static readonly Direction[] Directions =
        {
            Direction.Backwards,
            Direction.Down,
            Direction.Forward,
            Direction.Left,
            Direction.Right,
            Direction.Up,
        };

        private static Vector2Int TexturePosition(Direction direction, VoxelType voxelType)
        {
            return direction switch
            {
                Direction.Up => VoxelData[voxelType].up,
                Direction.Down => VoxelData[voxelType].down,
                Direction.Forward => VoxelData[voxelType].side,
                Direction.Backwards => VoxelData[voxelType].side,
                Direction.Right => VoxelData[voxelType].side,
                Direction.Left => VoxelData[voxelType].side,
                _ => throw new System.ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
        
        public static MeshData GetMeshData
            (ChunkData chunk, Vector3Int pos, MeshData meshData, VoxelType voxelType)
        {
            if (voxelType is VoxelType.Air or VoxelType.Nothing)
                return meshData;

            foreach (Direction direction in Directions)
            {
                Vector3Int neighbourBlockCoordinates = pos + direction.GetVector();
                VoxelType neighbourBlockType = Chunk.GetVoxel(chunk, neighbourBlockCoordinates);

                if (neighbourBlockType == VoxelType.Nothing || VoxelData[neighbourBlockType].isSolid) continue;
                
                if (voxelType == VoxelType.Water)
                {
                    if (neighbourBlockType == VoxelType.Air)
                        meshData.WaterMeshData = GetFaceDataIn(direction, chunk, pos, meshData.WaterMeshData, voxelType);
                }
                else
                {
                    meshData = GetFaceDataIn(direction, chunk, pos, meshData, voxelType);
                }
            }

            return meshData;
        }

        private static Vector2[] FaceUVs(Direction direction, VoxelType voxelType)
        {
            Vector2[] uvs = new Vector2[4];
            Vector2Int tilePos = TexturePosition(direction, voxelType);

            uvs[0] = new Vector2(
                tilePos.x * UVTileSize.x + UVTileSize.x - TextureOffset,
                tilePos.y * UVTileSize.y + TextureOffset
            );

            uvs[1] = new Vector2(
                tilePos.x * UVTileSize.x + UVTileSize.x - TextureOffset,
                tilePos.y * UVTileSize.y + UVTileSize.y - TextureOffset
            );

            uvs[2] = new Vector2(
                tilePos.x * UVTileSize.x + TextureOffset,
                tilePos.y * UVTileSize.y + UVTileSize.y - TextureOffset
            );

            uvs[3] = new Vector2(
                tilePos.x * UVTileSize.x + TextureOffset,
                tilePos.y * UVTileSize.y + TextureOffset
            );

            return uvs;
        }

        private static MeshData GetFaceDataIn(Direction direction, ChunkData chunk, Vector3Int pos, MeshData meshData, VoxelType voxelType)
        {
            GetFaceVertices(direction, pos, meshData, voxelType);
            meshData.AddQuadTriangles(VoxelData[voxelType].collision);
            meshData.UV.AddRange(FaceUVs(direction, voxelType));
            
            return meshData;
        }

        private static void GetFaceVertices(Direction direction, Vector3Int pos, MeshData meshData,
            VoxelType blockType)
        {
            bool col = VoxelData[blockType].collision;
            //order of vertices matters for the normals and how we render the mesh
            Vector3[] vertices =
            {
                new(pos.x - 0.5f, pos.y - 0.5f, pos.z - 0.5f),
                new(pos.x - 0.5f, pos.y + 0.5f, pos.z - 0.5f),
                new(pos.x + 0.5f, pos.y + 0.5f, pos.z - 0.5f),
                new(pos.x + 0.5f, pos.y - 0.5f, pos.z - 0.5f),
                new(pos.x + 0.5f, pos.y - 0.5f, pos.z + 0.5f),
                new(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f),
                new(pos.x - 0.5f, pos.y + 0.5f, pos.z + 0.5f),
                new(pos.x - 0.5f, pos.y - 0.5f, pos.z + 0.5f),
            };
            switch (direction)
            {
                case Direction.Backwards:
                    meshData.AddVertex(vertices[0], col);
                    meshData.AddVertex(vertices[1], col);
                    meshData.AddVertex(vertices[2], col);
                    meshData.AddVertex(vertices[3], col);
                    break;
                case Direction.Forward:
                    meshData.AddVertex(vertices[4], col);
                    meshData.AddVertex(vertices[5], col);
                    meshData.AddVertex(vertices[6], col);
                    meshData.AddVertex(vertices[7], col);
                    break;
                case Direction.Left:
                    meshData.AddVertex(vertices[7], col);
                    meshData.AddVertex(vertices[6], col);
                    meshData.AddVertex(vertices[1], col);
                    meshData.AddVertex(vertices[0], col);
                    break;
                case Direction.Right:
                    meshData.AddVertex(vertices[3], col);
                    meshData.AddVertex(vertices[2], col);
                    meshData.AddVertex(vertices[5], col);
                    meshData.AddVertex(vertices[4], col);
                    break;
                case Direction.Down:
                    meshData.AddVertex(vertices[0], col);
                    meshData.AddVertex(vertices[3], col);
                    meshData.AddVertex(vertices[4], col);
                    meshData.AddVertex(vertices[7], col);
                    break;
                case Direction.Up:
                    meshData.AddVertex(vertices[6], col);
                    meshData.AddVertex(vertices[5], col);
                    meshData.AddVertex(vertices[2], col);
                    meshData.AddVertex(vertices[1], col);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }
}