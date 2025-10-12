using System;
using Unity.Burst;
using Unity.Collections;

namespace Runtime.Engine.Voxels.Data
{
    [BurstCompile]
    public struct VoxelEngineRenderGenData : IDisposable
    {
        [NativeDisableParallelForRestriction] internal NativeArray<VoxelRenderDef> VoxelRenderDefs;

        public int GetTextureId(ushort id, Direction dir)
        {
            return VoxelRenderDefs[id].GetTextureId(dir);
        }

        public void Dispose()
        {
            VoxelRenderDefs.Dispose();
        }
    }
}