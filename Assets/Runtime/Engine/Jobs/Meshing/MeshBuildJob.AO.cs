using Runtime.Engine.Utils.Extensions;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        private static readonly int3 YOne = VectorConstants.Int3Up;

        #region AO Calculation

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private void ComputeAO(in int3 coord, ref PartitionJobData jobData, in Direction direction, out byte aoMask)
        {
            aoMask = 0;
            int3 up = coord + direction.RelativeUp();
            int3 right = coord + direction.RelativeRight();
            int3 down = coord + direction.RelativeDown();
            int3 left = coord + direction.RelativeLeft();

            if (GetMeshLayer(GetVoxel(ref jobData, up), RenderGenData) == MeshLayer.Solid) 
                SetBit(ref aoMask, 0, true);
            if (GetMeshLayer(GetVoxel(ref jobData, right), RenderGenData) == MeshLayer.Solid)
                SetBit(ref aoMask, 1, true);
            if (GetMeshLayer(GetVoxel(ref jobData, down), RenderGenData) == MeshLayer.Solid)
                SetBit(ref aoMask, 2, true);
            if (GetMeshLayer(GetVoxel(ref jobData, left), RenderGenData) == MeshLayer.Solid)
                SetBit(ref aoMask, 3, true);
        }

        private void SetBit(ref byte mask, int bitIndex, bool value)
        {
            if (value)
            {
                mask |= (byte)(1 << bitIndex);
            }
            else
            {
                mask &= (byte)~(1 << bitIndex);
            }
        }

        #endregion
    }
}