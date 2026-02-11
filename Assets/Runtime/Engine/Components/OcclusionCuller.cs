using System.Collections.Generic;
using System.Linq;
using Runtime.Engine.Behaviour;
using Runtime.Engine.Jobs.Meshing;
using Runtime.Engine.Settings;
using Runtime.Engine.Utils.Extensions;
using Unity.Collections;
using Unity.Mathematics;
using static Runtime.Engine.Jobs.Meshing.PartitionOcclusionData;

namespace Runtime.Engine.Components
{
    internal class OcclusionCuller
    {
        private readonly ChunkPool _chunkPool;
        private readonly VoxelEngineSettings _settings;

        private OccDirection[] _lastDirections = new OccDirection[3];
        private int3 _lastFocusPartition;

        internal OcclusionCuller(ChunkPool chunkPool, VoxelEngineSettings settings)
        {
            _chunkPool = chunkPool;
            _settings = settings;
        }

        private struct EnterPartition
        {
            public int3 PartitionPos;
            public OccDirection EnterDirection;
        }

        internal void OccUpdate(int3 focusPartitionPos, int3 focusPosition, float3 viewVector)
        {
            List<OccDirection> directions = GetOccFromNormal(viewVector);
            if (directions.SequenceEqual(_lastDirections) && (focusPosition == _lastFocusPartition).AndReduce()) return;
            HashSet<int3> visiblePartitions = new();
            Queue<EnterPartition> toCheck = new();
            
            foreach (OccDirection initDir in directions)
            {
                toCheck.Clear();
                toCheck.Enqueue(GetNeighbor(focusPartitionPos, initDir));

                while (toCheck.Count > 0)
                {
                    EnterPartition current = toCheck.Dequeue();
                    if (math.dot(viewVector, GetNormalFromOcc(current.EnterDirection)) < 0) continue; // Cull partitions behind the view vector
                    if (visiblePartitions.Contains(current.PartitionPos)) continue; // Skip already visible partitions
                    if (!_chunkPool.IsPartitionActive(current.PartitionPos)) continue;
                    ChunkPartition partition = _chunkPool.GetPartition(current.PartitionPos);
                    PartitionOcclusionData occData = partition.OcclusionData;
                    visiblePartitions.Add(current.PartitionPos);

                    foreach (OccDirection direction in AllDirections)
                    {
                        if(direction == current.EnterDirection) continue; // Don't go back the way we came
                        if (!occData.ArePartitionFacesConnected(current.EnterDirection, direction))
                            continue; // Skip occluded directions
                        EnterPartition neighbor = GetNeighbor(current.PartitionPos, direction);
                        if (!toCheck.Any(p =>
                                p.PartitionPos.Equals(neighbor.PartitionPos))) // Avoid duplicates in the queue
                        {
                            toCheck.Enqueue(neighbor);
                        }
                    }
                }
            }

            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                int3 neighbor = focusPartitionPos + new int3(x, y, z);
                visiblePartitions.Add(neighbor);
            }

            _chunkPool.UpdateAllVisibilities(visiblePartitions);
            _lastDirections = directions.ToArray();
        }

        private static EnterPartition GetNeighbor(in int3 partition, in OccDirection direction)
        {
            return new EnterPartition()
            {
                PartitionPos = partition + GetNormalFromOcc(direction),
                EnterDirection = direction
            };
        }
    }
}