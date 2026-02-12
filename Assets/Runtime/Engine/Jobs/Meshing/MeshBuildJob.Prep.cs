using Runtime.Engine.Utils;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        private void SortVoxels(ref PartitionJobData jobData)
        {
            for (int y = 0; y < VoxelConstants.PartitionHeight; y++)
            for (int z = 0; z < VoxelConstants.PartitionDepth; z++)
            for (int x = 0; x < VoxelConstants.PartitionWidth; x++)
            {
                int3 localPos = new(x, y, z);
                ushort voxelId = Accessor.GetVoxelInPartition(jobData.PartitionPos, localPos);
                VoxelRenderDef renderDef = RenderGenData.GetRenderDef(voxelId);

                if (renderDef.Collision) jobData.CollisionVoxels.Add(localPos);

                if (renderDef.IsAir)
                {
                    jobData.SeeThroughVoxels.Add(localPos);
                    continue;
                }

                if (renderDef.IsFoliage)
                {
                    jobData.FoliageVoxels.Add(localPos, voxelId);
                    jobData.SeeThroughVoxels.Add(localPos);
                    continue;
                }

                if (renderDef.IsTransparent)
                {
                    jobData.TransparentVoxels.Add(localPos, voxelId);
                    jobData.SeeThroughVoxels.Add(localPos);
                    continue;
                }

                jobData.SolidVoxels.Add(localPos, voxelId);
            }
        }
    }
}