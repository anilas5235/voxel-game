using System;
using Runtime.Engine.Behaviour;
using Runtime.Engine.Jobs.Meshing;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Test
{
    public class PartitionRenderTest : MonoBehaviour
    {
        [SerializeField] private PartitionRenderer partitionRenderer;


        private const int PointCount = 6;
        private const int TPointCount = 6;


        private void Start()
        {
            // ── Input-Buffer befüllen ──────────────────────────────────────────
            // stride = 28 bytes (3 floats + 4 uints)

            const int totalPoints = PointCount + TPointCount;
            UnsafeList<Vertex> points = new(totalPoints, Allocator.Persistent);
            for (int i = 0; i < PointCount; i++)
            {
                points.Add(new Vertex(new float3(1, 1, 1), (ushort)i, 1));
            }

            for (int i = 0; i < TPointCount; i++)
            {
                points.Add(new Vertex(new float3(-1, 1, 1), (ushort)i, 1));
            }

            partitionRenderer.Init(null);
            PartitionMeshGPUData data = new(points, PointCount, TPointCount, 0, new Bounds(
                Vector3.zero, Vector3.one * 32f));
            partitionRenderer.MeshUpdate(ref data);
        }
    }
}