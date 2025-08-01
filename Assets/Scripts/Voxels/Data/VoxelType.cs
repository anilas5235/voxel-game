namespace Voxels.Data
{
    public enum VoxelType
    {
        Air = 0, // Unused, but reserved for air
        Grass = 1, // Grass block with sides, top, and bottom textures
        Stone = 2, // Stone block with uniform texture
        Dirt = 3, // Dirt block with sides, top, and bottom textures
        Water = 4, // Water block with sides, top, and bottom textures
        Sand = 5, // Sand block with sides, top, and bottom textures
        Log = 6, // Wood block with sides, top, and bottom textures
        Leaves = 7, // Leaves block with sides, top, and bottom textures
    }
}