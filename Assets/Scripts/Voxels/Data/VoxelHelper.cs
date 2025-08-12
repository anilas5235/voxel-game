using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxels.Data
{
    public static class VoxelHelper
    {
        private const float TextureOffset = 0.01f;
        private const float OneMinusTextureOffset = 1 - TextureOffset;

        public static MeshData GetMeshData(ChunkData chunk, Vector3Int pos, MeshData meshData, VoxelType voxelType)
        {
            if (voxelType == null) return meshData;

            foreach (Direction direction in DirectionUtils.TraversalOrder)
            {
                int id = Chunk.GetVoxel(chunk, pos + direction.GetVector());
                if (id < 0) continue;
                VoxelType neighbourVoxelType = VoxelRegistry.Get(id);
                if (neighbourVoxelType == null || (neighbourVoxelType.Transparent && !voxelType.Transparent))
                    meshData = GetFaceDataIn(direction, chunk, pos, meshData, voxelType);
            }

            return meshData;
        }

        private static Vector3[] FaceUVs(Direction direction, VoxelType voxelType)
        {
            float texIndex = voxelType.TexIds[(int)direction];
            Vector3[] uvs =
            {
                // Create UVs in counter-clockwise order with a texture offset to avoid texture bleeding
                new(OneMinusTextureOffset, TextureOffset, texIndex), //(1,0)
                new(OneMinusTextureOffset, OneMinusTextureOffset, texIndex), //(1,1)
                new(TextureOffset, OneMinusTextureOffset, texIndex), //(0,1)
                new(TextureOffset, TextureOffset, texIndex) //(0,0)
            };

            return uvs;
        }

        private static MeshData GetFaceDataIn(Direction direction, ChunkData chunk, Vector3Int pos, MeshData meshData,
            VoxelType voxelType)
        {
            Vector3[] vertices = GetFaceVertices(direction, pos);
            foreach (Vector3 vertex in vertices)
            {
                meshData.AddVertex(vertex, voxelType.Collision);
            }
            meshData.AddQuadTriangles(voxelType.Collision);
            meshData.UV.AddRange(FaceUVs(direction, voxelType));

            return meshData;
        }

        private const float HalfVoxelSize = 0.5f;
        private static readonly Vector3[] VertexOffsets =
        {
            new(-HalfVoxelSize, -HalfVoxelSize, -HalfVoxelSize),
            new(-HalfVoxelSize, +HalfVoxelSize, -HalfVoxelSize),
            new(+HalfVoxelSize, +HalfVoxelSize, -HalfVoxelSize),
            new(+HalfVoxelSize, -HalfVoxelSize, -HalfVoxelSize),
            new(+HalfVoxelSize, -HalfVoxelSize, +HalfVoxelSize),
            new(+HalfVoxelSize, +HalfVoxelSize, +HalfVoxelSize),
            new(-HalfVoxelSize, +HalfVoxelSize, +HalfVoxelSize),
            new(-HalfVoxelSize, -HalfVoxelSize, +HalfVoxelSize),
        };
        
        private static readonly int[] ForwardVertexOffsetIndices = { 4, 5, 6, 7 };
        private static readonly int[] RightVertexOffsetIndices = { 3, 2, 5, 4 };
        private static readonly int[] BackwardVertexOffsetIndices = { 0, 1, 2, 3 };
        private static readonly int[] LeftVertexOffsetIndices = { 7, 6, 1, 0 };
        private static readonly int[] UpVertexOffsetIndices = { 6, 5, 2, 1 };
        private static readonly int[] DownVertexOffsetIndices = { 0, 3, 4, 7 };

        private static Vector3[] GetFaceVertices(Direction direction, Vector3 pos)
        {
            int[] vertexIndices = direction switch
            {
                Direction.Forward => ForwardVertexOffsetIndices,
                Direction.Right => RightVertexOffsetIndices,
                Direction.Backwards => BackwardVertexOffsetIndices,
                Direction.Left => LeftVertexOffsetIndices,
                Direction.Up => UpVertexOffsetIndices,
                Direction.Down => DownVertexOffsetIndices,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
            return new[]
            {
                VertexOffsets[vertexIndices[0]] + pos,
                VertexOffsets[vertexIndices[1]] + pos,
                VertexOffsets[vertexIndices[2]] + pos,
                VertexOffsets[vertexIndices[3]] + pos
            };
        }
    }
}