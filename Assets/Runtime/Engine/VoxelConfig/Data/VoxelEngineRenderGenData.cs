using System;
using Unity.Burst;
using Unity.Collections;

namespace Runtime.Engine.VoxelConfig.Data
{
    [BurstCompile]
    public struct VoxelEngineRenderGenData : IDisposable
    {
        [NativeDisableParallelForRestriction] internal NativeArray<VoxelRenderDef> VoxelRenderDefs;

        public void Dispose()
        {
            VoxelRenderDefs.Dispose();
        }

        public readonly MeshLayer GetMeshLayer(ushort voxelId)
        {
            return VoxelRenderDefs[voxelId].MeshLayer;
        }

        public readonly VoxelRenderDef GetRenderDef(ushort maskVoxelId)
        {
            return VoxelRenderDefs[maskVoxelId];
        }
    }
}