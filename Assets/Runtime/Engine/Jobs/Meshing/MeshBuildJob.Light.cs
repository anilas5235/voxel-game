using Runtime.Engine.Data;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Collections;
using Unity.Mathematics;
using static Runtime.Engine.Utils.Extensions.VectorConstants;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        private static readonly NativeArray<int3> NeighborOffsets = new(Int3Directions, Allocator.Persistent);
        private const byte MaxLightLevel = 15;


        private void CreateLightMap(ref PartitionJobData jobData)
        {
            NativeHashMap<int3, LightData> lightDataMap = jobData.LightDataMap;
            NativeHashSet<int3>.ReadOnly seeThroughVoxels = jobData.SeeThroughVoxels.AsReadOnly();

            for (int x = 0; x < ChunkWidth; x++)
            for (int y = 0; y < ChunkDepth; y++)
            {
                int3 pos = new(x, y, MaxYPartitionPos);
                if (!seeThroughVoxels.Contains(pos)) continue;

                int3 intPos = pos;
                bool openToSky = false;

                VoxelRenderDef def;

                do
                {
                    intPos.y += 1;
                    if (intPos.y >= MaxYChunkPos)
                    {
                        openToSky = true;
                        break;
                    }

                    ushort voxelId = Accessor.GetVoxelInPartition(jobData.PartitionPos, intPos);
                    def = RenderGenData.GetRenderDef(voxelId);
                } while (!def.IsSolid);

                if (!openToSky) continue;

                lightDataMap.TryAdd(pos, new LightData { Sunlight = MaxLightLevel });

                intPos = pos;

                do
                {
                    intPos.y -= 1;
                    if (!seeThroughVoxels.Contains(intPos)) break;

                    lightDataMap.TryAdd(intPos, new LightData { Sunlight = MaxLightLevel });
                } while (intPos.y >= 0);
            }

            NativeArray<int3> lightSeeds = lightDataMap.GetKeyArray(Allocator.Temp);
            NativeQueue<int3> lightPropagationQueue = new(Allocator.Temp);

            foreach (int3 seed in lightSeeds)
            {
                lightPropagationQueue.Enqueue(seed);
                while (!lightPropagationQueue.IsEmpty())
                {
                    int3 current = lightPropagationQueue.Dequeue();
                    byte nextLightLevel = lightDataMap[current].Sunlight;
                    nextLightLevel--;

                    foreach (int3 offset in NeighborOffsets)
                    {
                        int3 neighbor = current + offset;
                        if (!ChunkAccessor.InPartitionBounds(neighbor)) continue;
                        if (!seeThroughVoxels.Contains(neighbor)) continue;
                        
                        if (lightDataMap.TryGetValue(neighbor, out LightData neighborLightData))
                        {
                            if (neighborLightData.Sunlight >= nextLightLevel) continue;

                            neighborLightData.Sunlight = nextLightLevel;
                            lightDataMap[neighbor] = neighborLightData;

                            if (nextLightLevel > 1) lightPropagationQueue.Enqueue(neighbor);
                        }
                        else
                        {
                            lightDataMap[neighbor] = new LightData { Sunlight = nextLightLevel };
                            if (nextLightLevel > 1) lightPropagationQueue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }
    }
}