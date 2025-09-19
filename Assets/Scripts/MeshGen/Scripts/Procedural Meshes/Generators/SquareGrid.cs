using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace ProceduralMeshes.Generators
{
    public struct SquareGrid : IMeshGenerator
    {
        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(1f, 0f, 1f));

        public int VertexCount => 4 * Resolution * Resolution;

        public int IndexCount => 6 * Resolution * Resolution;

        public int JobLength => Resolution;

        public int Resolution { get; set; }

        public void Execute<S>(int z, S streams) where S : struct, IMeshStreams
        {
            int vi = 4 * Resolution * z, ti = 2 * Resolution * z;

            for (int x = 0; x < Resolution; x++, vi += 4, ti += 2)
            {
                float2 xCoordinates = float2(x, x + 1f) / Resolution - 0.5f;
                float2 zCoordinates = float2(z, z + 1f) / Resolution - 0.5f;

                Vertex vertex = new Vertex();
                vertex.Normal.y = 1f;
                vertex.Tangent.xw = float2(1f, -1f);

                vertex.Position.x = xCoordinates.x;
                vertex.Position.z = zCoordinates.x;
                streams.SetVertex(vi + 0, vertex);

                vertex.Position.x = xCoordinates.y;
                vertex.UV0 = float4(1f, 0f, 0f, 0f);
                streams.SetVertex(vi + 1, vertex);

                vertex.Position.x = xCoordinates.x;
                vertex.Position.z = zCoordinates.y;
                vertex.UV0 = float4(0f, 1f, 0f, 0f);
                streams.SetVertex(vi + 2, vertex);

                vertex.Position.x = xCoordinates.y;
                vertex.UV0 = 1f;
                streams.SetVertex(vi + 3, vertex);

                streams.SetTriangle(ti + 0, vi + int3(0, 2, 1));
                streams.SetTriangle(ti + 1, vi + int3(1, 2, 3));
            }
        }
    }
}