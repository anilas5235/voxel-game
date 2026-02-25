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
            int3 up = direction.RelativeUp();
            int3 right = direction.RelativeRight();
            int3 down = direction.RelativeDown();
            int3 left = direction.RelativeLeft();

            SetBit(ref aoMask, 0, IsSolid(ref jobData, coord + up));
            SetBit(ref aoMask, 1, IsSolid(ref jobData, coord + up + right));
            SetBit(ref aoMask, 2, IsSolid(ref jobData, coord + right));
            SetBit(ref aoMask, 3, IsSolid(ref jobData, coord + down + right));
            SetBit(ref aoMask, 4, IsSolid(ref jobData, coord + down));
            SetBit(ref aoMask, 5, IsSolid(ref jobData, coord + down + left));
            SetBit(ref aoMask, 6, IsSolid(ref jobData, coord + left));
            SetBit(ref aoMask, 7, IsSolid(ref jobData, coord + up + left));
        }

        private bool IsSolid(ref PartitionJobData jobData, in int3 itr) =>
            GetMeshLayer(GetVoxel(ref jobData, itr), RenderGenData) == MeshLayer.Solid;

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