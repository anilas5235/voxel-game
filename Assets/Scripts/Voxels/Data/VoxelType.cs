namespace Voxels.Data
{
    public class VoxelType
    {
        public int Id; // internal ID
        public string Name;
        public bool Collision;
        public bool Transparent;
        public int[] TexIds; // Texture IDs for the voxel, must have 6 elements (for each face)
    }
}