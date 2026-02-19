using Runtime.Engine.Utils;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Light
{
    internal partial struct LightBuildJob
    {
        private void AddSunlightSeeds(ref LightJobData jobData)
        {
            NativeHashSet<int3> sunlightSeeds = jobData.SunlightSeeds;
            NativeHashSet<int3>.ReadOnly seeThroughVoxels = jobData.SeeThroughVoxels.AsReadOnly();

            int yOffset = jobData.PartitionPos.y * VoxelConstants.PartitionHeight;

            for (int x = 0; x < VoxelConstants.ChunkWidth; x++)
            for (int z = 0; z < VoxelConstants.ChunkDepth; z++)
            {
                int3 pos = new(x, VoxelConstants.MaxYPartitionPos, z);
                if (!seeThroughVoxels.Contains(pos)) continue;

                int3 intPos = pos;
                bool openToSky = false;

                VoxelRenderDef def;

                do
                {
                    intPos.y += 1;
                    if (intPos.y + yOffset >= VoxelConstants.MaxYChunkPos)
                    {
                        openToSky = true;
                        break;
                    }

                    ushort voxelId = Accessor.GetVoxelInPartition(jobData.PartitionPos, intPos);
                    def = RenderGenData.GetRenderDef(voxelId);
                } while (!def.IsSolid);

                if (!openToSky) continue;

                sunlightSeeds.Add(pos);

                intPos = pos;

                do
                {
                    intPos.y -= 1;
                    if (!seeThroughVoxels.Contains(intPos)) break;

                    sunlightSeeds.Add(intPos);
                } while (intPos.y >= 0);
            }
        }
    }
}