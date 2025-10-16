using System;
using Unity.Burst;
using Unity.Collections;

namespace Runtime.Engine.Voxels.Data
{
    [BurstCompile]
    public struct VoxelEngineRenderGenData : IDisposable
    {
        [NativeDisableParallelForRestriction] internal NativeArray<VoxelRenderDef> VoxelRenderDefs;

        public void Dispose()
        {
            VoxelRenderDefs.Dispose();
        }

        public byte GetMeshIndex(ushort voxelId)
        {
            return VoxelRenderDefs[voxelId].MeshIndex;
        }

        public VoxelRenderDef GetRenderDef(ushort maskVoxelId)
        {
            return VoxelRenderDefs[maskVoxelId];
        }
    }
}