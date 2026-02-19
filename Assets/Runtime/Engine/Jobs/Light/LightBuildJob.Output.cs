using Runtime.Engine.Data;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Light
{
    internal partial struct LightBuildJob
    {
        private void WriteResults(int index, ref LightJobData jobData)
        {
            if (jobData.HasNoSolid)
            {
                PartitionLightData result = new(16);
                result.SetAllLights(MaxLightLevel);
                Results.TryAdd(jobData.PartitionPos, result);
            }
        }
    }
}