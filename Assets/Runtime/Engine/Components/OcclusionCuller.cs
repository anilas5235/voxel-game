using System.Collections.Generic;
using System.Linq;
using Runtime.Engine.Behaviour;
using Runtime.Engine.Jobs.Meshing;
using Runtime.Engine.Settings;
using Unity.Mathematics;
using static Runtime.Engine.Jobs.Meshing.PartitionOcclusionData;

namespace Runtime.Engine.Components
{
    internal class OcclusionCuller
    {
        private readonly ChunkPool _chunkPool;
        private readonly VoxelEngineSettings _settings;

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

        internal void OccUpdate(int3 focusPartitionPos, float3 viewVector)
        {
            List<OccDirection> directions = GetOccFromNormal(viewVector);

            Queue<EnterPartition> toCheck = new();
            toCheck.Enqueue(GetNeighbor(focusPartitionPos, directions.First()));
            HashSet<int3> visiblePartitions = new();

            while (toCheck.Count > 0)
            {
                EnterPartition current = toCheck.Dequeue();
                if (math.dot(viewVector, GetNormalFromOcc(current.EnterDirection)) < 0)
                    continue; // Cull partitions behind the view vector
                if(visiblePartitions.Contains(current.PartitionPos)) continue; // Skip already visible partitions
                if (!_chunkPool.IsPartitionActive(current.PartitionPos)) continue;
                visiblePartitions.Add(current.PartitionPos);
                ChunkPartition partition = _chunkPool.GetPartition(current.PartitionPos);
                PartitionOcclusionData occData = partition.OcclusionData;

                foreach (OccDirection direction in AllDirections)
                {
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
            
            _chunkPool.UpdateAllVisibilities(visiblePartitions);
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