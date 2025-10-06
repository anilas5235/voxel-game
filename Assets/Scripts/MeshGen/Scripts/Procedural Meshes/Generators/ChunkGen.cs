using Unity.Mathematics;
using UnityEngine;
using Utils;
using Voxels.Chunk;
using static Voxels.VoxelWorld;

namespace ProceduralMeshes.Generators
{
    public struct ChunkGen : IMeshGenerator
    {
        private static readonly float3[][] FaceVertices =
        {
            // right
            new float3[] { new(1, 0, 0), new(1, 1, 0), new(1, 1, 1), new(1, 0, 1) },
            // left
            new float3[] { new(0, 0, 1), new(0, 1, 1), new(0, 1, 0), new(0, 0, 0) },
            // up
            new float3[] { new(0, 1, 1), new(1, 1, 1), new(1, 1, 0), new(0, 1, 0) },
            // down
            new float3[] { new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1) },
            // forward
            new float3[] { new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1) },
            // back
            new float3[] { new(1, 0, 0), new(0, 0, 0), new(0, 1, 0), new(1, 1, 0) }
        };

        private static readonly int3[] Directions =
        {
            new(1, 0, 0), // right
            new(-1, 0, 0), // left
            new(0, 1, 0), // up
            new(0, -1, 0), // down
            new(0, 0, 1), // forward
            new(0, 0, -1) // back
        };

        public Bounds Bounds => new(Vector3.zero, ChunkSize.GetVector3());
        public int VertexCount => 6 * VoxelsPerChunk;
        public int IndexCount => 6 * 4 * VoxelsPerChunk;
        public int JobLength => Resolution;
        public int Resolution { get; set; }

        public VoxelGrid VoxelGrid;

        public void Execute<S>(S streams) where S : struct, IMeshStreams
        {
            int vertexIndex = 0;
            int triangleIndex = 0;

            for (int x = 0; x < ChunkSize.x; x++)
            {
                for (int y = 0; y < ChunkSize.y; y++)
                {
                    for (int z = 0; z < ChunkSize.z; z++)
                    {
                        int voxelId = VoxelGrid.GetVoxel(x, y, z);
                        var voxelPosition = new int3(x, y, z);
                        if (voxelId == 0) continue;

                        for (int d = 0; d < 6; d++)
                        {
                            var neighbor = voxelPosition + Directions[d];
                            bool outOfBounds = neighbor.x < 0 || neighbor.x >= ChunkSize.x ||
                                               neighbor.y < 0 || neighbor.y >= ChunkSize.y ||
                                               neighbor.z < 0 || neighbor.z >= ChunkSize.z;
                            int neighborId = outOfBounds ? 0 : VoxelGrid.GetVoxel(neighbor);
                            if (neighborId != 0) continue; // Not visible

                            // Emit face
                            int vStart = vertexIndex;
                            for (int fv = 0; fv < 4; fv++)
                            {
                                var pos = voxelPosition + FaceVertices[d][fv];
                                // Set uv: x = FaceVertices[d][fv].x, y = FaceVertices[d][fv].y, z = voxelId
                                streams.SetVertex(vertexIndex++, new Vertex
                                {
                                    Position = pos,
                                    UV0 = new float4(FaceVertices[d][fv].x, FaceVertices[d][fv].y, voxelId, 0),
                                    Normal = Directions[d],
                                    Tangent = new float4(Directions[d].y, Directions[d].z, Directions[d].x, -1)
                                });
                            }

                            // Two triangles per face
                            streams.SetTriangle(triangleIndex++, new int3(vStart, vStart + 1, vStart + 2));
                            streams.SetTriangle(triangleIndex++, new int3(vStart, vStart + 2, vStart + 3));
                        }
                    }
                }
            }
        }
    }
}