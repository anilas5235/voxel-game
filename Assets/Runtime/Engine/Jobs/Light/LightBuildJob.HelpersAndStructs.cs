using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Jobs.Light
{
    internal partial struct LightBuildJob
    {
        [BurstCompile]
        private struct LightJobData : IDisposable
        {
            public readonly int3 PartitionPos;
            public bool HasNoSolid;

            public NativeHashSet<int3> SunlightSeeds;
            
            public NativeHashMap<int3, byte> LightDataMap;
            public NativeHashSet<int3> SeeThroughVoxels;
            public LightJobData(int3 partition)
            {
                PartitionPos = partition;
                HasNoSolid = true;
                SunlightSeeds = new NativeHashSet<int3>();
                SeeThroughVoxels = new NativeHashSet<int3>();
                LightDataMap = new NativeHashMap<int3, byte>(VoxelsPerPartition/2, Allocator.TempJob);
            }


            public void Dispose()
            {
                SunlightSeeds.Dispose();
                SeeThroughVoxels.Dispose();
            }
        }
    }
}