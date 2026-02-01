using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Meshing
{
    [BurstCompile]
    internal struct PartitionOcclusionData
    {
        internal enum OccDirection : byte
        {
            PositiveX = 0,
            NegativeX = 1,
            PositiveY = 2,
            NegativeY = 3,
            PositiveZ = 4,
            NegativeZ = 5
        }

        public static readonly OccDirection[] AllDirections =
        {
            OccDirection.PositiveX,
            OccDirection.NegativeX,
            OccDirection.PositiveY,
            OccDirection.NegativeY,
            OccDirection.PositiveZ,
            OccDirection.NegativeZ
        };

        public static OccDirection GetOccFromNormal(in int3 normal)
        {
            return normal.x switch
            {
                > 0 => OccDirection.PositiveX,
                < 0 => OccDirection.NegativeX,
                _ => normal.y switch
                {
                    > 0 => OccDirection.PositiveY,
                    < 0 => OccDirection.NegativeY,
                    _ => normal.z > 0 ? OccDirection.PositiveZ : OccDirection.NegativeZ
                }
            };
        }

        private ushort _occlusionFlags;

        public bool ArePartitionFacesConnected(OccDirection a, OccDirection b)
        {
            return (_occlusionFlags & (1 << GetBitIndex(a, b))) != 0;
        }

        private static int GetBitIndex(OccDirection a, OccDirection b)
        {
            // Ensure consistent ordering so (a,b) and (b,a) map to the same bit
            int x = math.min((int)a, (int)b);
            int y = math.max((int)a, (int)b);

            // Triangular matrix indexing: maps 6x6 symmetric pairs to 0-14
            return x * (12 - x) / 2 + (y - x - 1);
        }

        public void SetFaceConnected(OccDirection a, OccDirection b)
        {
            _occlusionFlags |= (ushort)(1 << GetBitIndex(a, b));
        }

        public void SetFaceConnected(NativeHashSet<byte> connectedDirections)
        {
            foreach (byte dirA in connectedDirections)
            foreach (byte dirB in connectedDirections)
            {
                if (dirA == dirB) continue;
                SetFaceConnected((OccDirection)dirA, (OccDirection)dirB);
            }
        }

        public void SetAll(bool b)
        {
            _occlusionFlags = b ? ushort.MaxValue : (ushort)0;
        }
    }
}