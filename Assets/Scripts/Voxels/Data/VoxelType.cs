namespace Voxels.Data
{
    public class VoxelType
    {
        public bool Collision;
        public int Id; // internal ID
        public string Name;
        public int[] TexIds; // Texture IDs for the voxel, must have 6 elements (for each face)
        public bool Transparent;
    }
}