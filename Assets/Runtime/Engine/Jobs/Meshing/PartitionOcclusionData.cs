using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Runtime.Engine.Utils.Extensions.VectorConstants;

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
        
        public static int3 GetNormalFromOcc(OccDirection direction)
        {
            return direction switch
            {
                OccDirection.PositiveX => Int3Right,
                OccDirection.NegativeX => Int3Left,
                OccDirection.PositiveY => Int3Up,
                OccDirection.NegativeY => Int3Down,
                OccDirection.PositiveZ => Int3Forward,
                OccDirection.NegativeZ => Int3Backward,
                _ => throw new System.ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
        
        public static List<OccDirection> GetOccFromNormal(in float3 normal)
        {
            List<OccDirection> result = new(3);
            switch (normal.x)
            {
                case > 0:
                    result.Add(OccDirection.PositiveX);
                    break;
                case < 0:
                    result.Add(OccDirection.NegativeX);
                    break;
            }
            switch (normal.y)
            {
                case > 0:
                    result.Add(OccDirection.PositiveY);
                    break;
                case < 0:
                    result.Add(OccDirection.NegativeY);
                    break;
            }
            switch (normal.z)
            {
                case > 0:
                    result.Add(OccDirection.PositiveZ);
                    break;
                case < 0:
                    result.Add(OccDirection.NegativeZ);
                    break;
            }
            return result;
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