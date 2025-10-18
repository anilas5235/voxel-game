namespace Runtime.Engine.Voxels.Data
{
    public enum MeshLayer : byte
    {
        Solid = 0,
        Transparent = 1,
        Air = byte.MaxValue
    }
}