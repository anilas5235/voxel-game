using Runtime.Engine.Data;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Light
{
    internal partial struct LightBuildJob
    {
        private void SunLightPropagation(ref LightJobData jobData)
        {
            NativeHashMap<int3, byte> lightDataMap = jobData.LightDataMap;
            NativeHashSet<int3>.ReadOnly seeThroughVoxels = jobData.SeeThroughVoxels.AsReadOnly();


            NativeArray<int3> lightSeeds = jobData.SunlightSeeds.ToNativeArray(Allocator.Temp);
            NativeQueue<int3> lightPropagationQueue = new(Allocator.Temp);

            foreach (int3 seed in lightSeeds)
            {
                lightPropagationQueue.Enqueue(seed);
                while (!lightPropagationQueue.IsEmpty())
                {
                    int3 current = lightPropagationQueue.Dequeue();
                    byte nextLightLevel = (byte)(lightDataMap[current] - 1);

                    foreach (int3 offset in NeighborOffsets)
                    {
                        int3 neighbor = current + offset;
                        if (!ChunkAccessor.InPartitionBounds(neighbor)) continue;
                        if (lightSeeds.Contains(neighbor)) continue;
                        if (!seeThroughVoxels.Contains(neighbor)) continue;

                        if (lightDataMap.TryGetValue(neighbor, out byte neighborLightData))
                        {
                            if (neighborLightData >= nextLightLevel) continue;

                            neighborLightData = nextLightLevel;
                            lightDataMap[neighbor] = neighborLightData;

                            if (nextLightLevel > 1) lightPropagationQueue.Enqueue(neighbor);
                        }
                        else
                        {
                            lightDataMap[neighbor] = nextLightLevel;
                            if (nextLightLevel > 1) lightPropagationQueue.Enqueue(neighbor);
                        }
                    }
                }
            }


            lightSeeds.Dispose();
            lightPropagationQueue.Dispose();
        }
    }
}