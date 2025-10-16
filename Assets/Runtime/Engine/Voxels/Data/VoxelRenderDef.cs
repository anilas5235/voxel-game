using Unity.Burst;
using Unity.Mathematics;

namespace Runtime.Engine.Voxels.Data
{
    [BurstCompile]
    public struct VoxelRenderDef
    {
        public byte MeshIndex;
        public VoxelType VoxelType;
        public float4 OverrideColor;
        public bool Collision;
        public int TexUp;
        public int TexDown;
        public int TexLeft;
        public int TexRight;
        public int TexFront;
        public int TexBack;

        [BurstCompile]
        public readonly int GetTextureId(Direction dir)
        {
            return dir switch
            {
                Direction.Up => TexUp,
                Direction.Down => TexDown,
                Direction.Left => TexLeft,
                Direction.Right => TexRight,
                Direction.Forward => TexFront,
                Direction.Backward => TexBack,
                _ => -1
            };
        }
    }
}