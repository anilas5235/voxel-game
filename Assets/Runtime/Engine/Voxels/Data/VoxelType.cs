namespace Voxels.Data
{
    public class VoxelType
    {
        public bool Collision;
        public ushort Id; // internal ID
        public string Name;
        public int[] TexIds; // Texture IDs for the voxel, must have 6 elements (for each face)
        public bool Transparent;
    }
}