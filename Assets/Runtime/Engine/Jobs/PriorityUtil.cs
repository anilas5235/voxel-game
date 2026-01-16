using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs
{
    [BurstCompile]
    public static class PriorityUtil
    {
        /// <summary>
        /// Calculates priority based on squared distance from focus.
        /// </summary>
        [BurstCompile]
        public static int DistPriority(ref int3 position, ref int3 focus)
        {
            return (position - focus).SqrMagnitude();
        }

        /// <summary>
        /// Calculates priority based on squared distance from focus.
        /// </summary>
        [BurstCompile]
        public static int DistPriority(ref int2 position,ref int3 focus)
        {
            return (position - focus.xz).SqrMagnitude();
        }
    }
}