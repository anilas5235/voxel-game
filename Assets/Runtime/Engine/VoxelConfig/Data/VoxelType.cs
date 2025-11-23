namespace Runtime.Engine.VoxelConfig.Data
{
    /// <summary>
    /// High level classification of voxel behavior used by the engine (solid block, liquid, flora, ...).
    /// </summary>
    public enum VoxelType
    {
        /// <summary>
        /// Full solid block occupying the entire voxel.
        /// </summary>
        Full,
        /// <summary>
        /// Liquid voxel such as water or lava.
        /// </summary>
        Liquid,
        /// <summary>
        /// Non-solid flora such as plants, grass or crops.
        /// </summary>
        Flora,
    }
}