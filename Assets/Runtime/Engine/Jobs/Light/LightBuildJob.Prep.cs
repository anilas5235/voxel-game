using Runtime.Engine.VoxelConfig.Data;
using Unity.Mathematics;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Light
{
    internal partial struct LightBuildJob
    {
        private void SortVoxels(ref LightJobData jobData)
        {
            for (int y = 0; y < PartitionHeight; y++)
            for (int z = 0; z < PartitionDepth; z++)
            for (int x = 0; x < PartitionWidth; x++)
            {
                int3 localPos = new(x, y, z);
                ushort voxelId = Accessor.GetVoxelInPartition(jobData.PartitionPos, localPos);
                VoxelRenderDef renderDef = RenderGenData.GetRenderDef(voxelId);

                if (!renderDef.IsSolid)
                {
                    jobData.SeeThroughVoxels.Add(localPos);
                }
                else
                {
                    jobData.HasNoSolid = false;
                }
            }
        }
    }
}