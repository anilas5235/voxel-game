using System.Runtime.InteropServices;
using Unity.Burst;

namespace Runtime.Engine.Jobs.Chunk
{
    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    public struct NoiseData
    {
        public float height;
        public float temperature;
        public float humidity;
        public bool isRiver;
    }
}