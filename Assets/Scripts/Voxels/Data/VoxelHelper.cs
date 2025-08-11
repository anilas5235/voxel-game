using System;
using UnityEngine;

namespace Voxels.Data
{
    public static class VoxelHelper
    {
        private static float TextureOffset = 0.01f;

        private static readonly Direction[] Directions =
        {
            Direction.Backwards,
            Direction.Down,
            Direction.Forward,
            Direction.Left,
            Direction.Right,
            Direction.Up,
        };

        public static MeshData GetMeshData(ChunkData chunk, Vector3Int pos, MeshData meshData, VoxelType voxelType)
        {
            if (voxelType == null) return meshData;

            foreach (Direction direction in Directions)
            {
                int id = Chunk.GetVoxel(chunk, pos + direction.GetVector());
                if (id < 0) continue;
                VoxelType neighbourVoxelType = VoxelRegistry.Get(id);
                if (neighbourVoxelType == null || neighbourVoxelType.Transparent)
                    meshData = GetFaceDataIn(direction, chunk, pos, meshData, voxelType);
            }

            return meshData;
        }

        private static Vector3[] FaceUVs(Direction direction, VoxelType voxelType)
        {
            Vector3[] uvs = new Vector3[4];
            float texIndex = voxelType.TexIds[(int)direction];

            uvs[0] = new Vector3(
                1 - TextureOffset,
                0 + TextureOffset,
                texIndex
            );

            uvs[1] = new Vector3(
                1 - TextureOffset,
                1 - TextureOffset,
                texIndex
            );

            uvs[2] = new Vector3(
                0 + TextureOffset,
                1 - TextureOffset,
                texIndex
            );

            uvs[3] = new Vector3(
                0 + TextureOffset,
                0 + TextureOffset,
                texIndex
            );

            return uvs;
        }

        private static MeshData GetFaceDataIn(Direction direction, ChunkData chunk, Vector3Int pos, MeshData meshData,
            VoxelType voxelType)
        {
            GetFaceVertices(direction, pos, meshData, voxelType);
            meshData.AddQuadTriangles(voxelType.Collision);
            meshData.UV.AddRange(FaceUVs(direction, voxelType));

            return meshData;
        }

        private static void GetFaceVertices(Direction direction, Vector3Int pos, MeshData meshData,
            VoxelType voxelType)
        {
            bool col = voxelType.Collision;
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