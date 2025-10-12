using Unity.Burst;

namespace Runtime.Engine.Voxels.Data
{
    [BurstCompile]
    public struct VoxelRenderDef
    {
        public VoxelType VoxelType;
        public bool Collision;
        public bool Transparent;
        public int TexUp;
        public int TexDown;
        public int TexLeft;
        public int TexRight;
        public int TexFront;
        public int TexBack;

        [BurstCompile]
        public int GetTextureId(Direction dir)
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