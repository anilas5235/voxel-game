using Runtime.Engine.Data;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Collections;
using Unity.Mathematics;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        private const byte MaxLightLevel = 15;

        private void CreateLightMap(ref PartitionJobData jobData)
        {
            if (jobData.HasNoVoxels) return;

            NativeHashMap<int3, LightData> lightDataMap = jobData.LightDataMap;
            NativeHashSet<int3>.ReadOnly seeThroughVoxels = jobData.SeeThroughVoxels.AsReadOnly();

            int yOffset = jobData.PartitionPos.y * PartitionHeight;

            for (int x = 0; x < ChunkWidth; x++)
            for (int z = 0; z < ChunkDepth; z++)
            {
                int3 pos = new(x, MaxYPartitionPos, z);
                if (!seeThroughVoxels.Contains(pos)) continue;

                int3 intPos = pos;
                bool openToSky = false;

                VoxelRenderDef def;

                do
                {
                    intPos.y += 1;
                    if (intPos.y + yOffset >= MaxYChunkPos)
                    {
                        openToSky = true;
                        break;
                    }

                    ushort voxelId = Accessor.GetVoxelInPartition(jobData.PartitionPos, intPos);
                    def = RenderGenData.GetRenderDef(voxelId);
                } while (!def.IsSolid);

                if (!openToSky) continue;

                LightData maxLightData = new() { Sunlight = MaxLightLevel };

                lightDataMap.TryAdd(pos, maxLightData);

                intPos = pos;

                do
                {
                    intPos.y -= 1;
                    if (!seeThroughVoxels.Contains(intPos)) break;

                    lightDataMap.TryAdd(intPos, maxLightData);
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
                    byte nextLightLevel = (byte)(lightDataMap[current].Sunlight - 1);
                    LightData nextLightData = new() { Sunlight = nextLightLevel };

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
                            lightDataMap[neighbor] = nextLightData;
                            if (nextLightLevel > 1) lightPropagationQueue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }
    }
}