using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        private static readonly int3 YOne = VectorConstants.Int3Up;

        #region AO Calculation

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = true)]
        private int4 ComputeAOMask(in int3 coord, ref PartitionJobData jobData,in AxisInfo axInfo)
        {
            int3 l = coord;
            int3 r = coord;
            int3 b = coord;
            int3 T = coord;

            int3 lbc = coord;
            int3 rbc = coord;
            int3 ltc = coord;
            int3 rtc = coord;

            l[axInfo.VAxis] -= 1;
            r[axInfo.VAxis] += 1;
            b[axInfo.UAxis] -= 1;
            T[axInfo.UAxis] += 1;

            lbc[axInfo.UAxis] -= 1;
            lbc[axInfo.VAxis] -= 1;
            rbc[axInfo.UAxis] -= 1;
            rbc[axInfo.VAxis] += 1;
            ltc[axInfo.UAxis] += 1;
            ltc[axInfo.VAxis] -= 1;
            rtc[axInfo.UAxis] += 1;
            rtc[axInfo.VAxis] += 1;

            int lo = GetMeshLayer(GetVoxel(ref jobData, l), RenderGenData) == 0 ? 1 : 0;
            int ro = GetMeshLayer(GetVoxel(ref jobData, r), RenderGenData) == 0 ? 1 : 0;
            int bo = GetMeshLayer(GetVoxel(ref jobData, b), RenderGenData) == 0 ? 1 : 0;
            int to = GetMeshLayer(GetVoxel(ref jobData, T), RenderGenData) == 0 ? 1 : 0;

            int lbco = GetMeshLayer(GetVoxel(ref jobData, lbc), RenderGenData) == 0 ? 1 : 0;
            int rbco = GetMeshLayer(GetVoxel(ref jobData, rbc), RenderGenData) == 0 ? 1 : 0;
            int ltco = GetMeshLayer(GetVoxel(ref jobData, ltc), RenderGenData) == 0 ? 1 : 0;
            int rtco = GetMeshLayer(GetVoxel(ref jobData, rtc), RenderGenData) == 0 ? 1 : 0;

            return new int4(
                ComputeAO(lo, bo, lbco),
                ComputeAO(lo, to, ltco),
                ComputeAO(ro, bo, rbco),
                ComputeAO(ro, to, rtco)
            );
        }

        [BurstCompile]
        private int ComputeAO(int s1, int s2, int c)
        {
            if (s1 == 1 && s2 == 1)
            {
                return 0;
            }

            return 3 - (s1 + s2 + c);
        }

        #endregion
    }
}