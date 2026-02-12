using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Jobs.Meshing
{
    internal partial struct MeshBuildJob
    {
        #region Structs

        [BurstCompile]
        private struct AxisInfo
        {
            public int UAxis, VAxis, ULimit, VLimit;
        }

        [BurstCompile]
        private struct UVQuad
        {
            public half4 Uv1, Uv2, Uv3, Uv4;
        }

        [BurstCompile]
        private struct VQuad
        {
            public float3 V1, V2, V3, V4;

            public VQuad(float3 v1, float3 v2, float3 v3, float3 v4)
            {
                V1 = v1;
                V2 = v2;
                V3 = v3;
                V4 = v4;
            }

            public void OffsetAll(float3 offset)
            {
                V1 += offset;
                V2 += offset;
                V3 += offset;
                V4 += offset;
            }
        }
        
        private interface IMaskComparable<T>
        {
            bool CompareTo(T other);
        }
        
        [BurstCompile]
        private struct CMask: IMaskComparable<CMask>
        {
            internal readonly sbyte Normal;

            public CMask( sbyte normal)
            {
                Normal = normal;
            }

            public bool CompareTo(CMask other)
            {
                return Normal == other.Normal ;
            }
        }

        [BurstCompile]
        private struct Mask: IMaskComparable<Mask>
        {
            public readonly ushort VoxelId;

            internal readonly MeshLayer MeshLayer;
            internal readonly sbyte Normal;
            internal readonly sbyte TopOpen;

            internal int4 AO;

            public Mask(ushort voxelId, MeshLayer meshLayer, sbyte normal, int4 ao, sbyte topOpen = 0)
            {
                MeshLayer = meshLayer;
                VoxelId = voxelId;
                Normal = normal;
                AO = ao;
                TopOpen = topOpen;
            }

            public bool CompareTo(Mask other)
            {
                return
                    MeshLayer == other.MeshLayer &&
                    VoxelId == other.VoxelId &&
                    Normal == other.Normal &&
                    AO[0] == other.AO[0] &&
                    AO[1] == other.AO[1] &&
                    AO[2] == other.AO[2] &&
                    AO[3] == other.AO[3];
            }
        }
        #endregion

        #region Helpers

        [BurstCompile]
        private MeshLayer GetMeshLayer(ushort voxelId, VoxelEngineRenderGenData renderGenData)
        {
            return renderGenData.GetMeshLayer(voxelId);
        }

        [BurstCompile]
        private void ClearMaskRegion(NativeArray<Mask> normalMask, int n, int width, int height, int axis1Limit)
        {
            for (int l = 0; l < height; ++l)
            for (int k = 0; k < width; ++k)
                normalMask[n + k + l * axis1Limit] = default;
        }
        
        [BurstCompile]
        private void ClearColMaskRegion(NativeArray<CMask> normalMask, int n, int width, int height, int axis1Limit)
        {
            for (int l = 0; l < height; ++l)
            for (int k = 0; k < width; ++k)
                normalMask[n + k + l * axis1Limit] = default;
        }

        private void EnsureCapacity<T>(NativeList<T> list, int add) where T : unmanaged
        {
            int need = list.Length + add;
            if (need <= list.Capacity) return;
            int newCap = math.max(list.Capacity * 2, need);
            list.Capacity = newCap;
        }

        #endregion
    }
}