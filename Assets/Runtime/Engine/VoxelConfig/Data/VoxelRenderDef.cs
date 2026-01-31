using Unity.Burst;

namespace Runtime.Engine.VoxelConfig.Data
{
    /// <summary>
    /// Render definition for a voxel, including texture slots for all faces, mesh layer,
    /// collision flag and additional rendering information.
    /// </summary>
    [BurstCompile]
    public struct VoxelRenderDef
    {
        public ushort Id;
        /// <summary>Mesh layer (solid, transparent or air).</summary>
        public MeshLayer MeshLayer;
        /// <summary>Whether all faces should always be rendered, even when hidden by neighbors.</summary>
        public bool AlwaysRenderAllFaces;
        /// <summary>Semantic voxel type (for example flora or liquid).</summary>
        public VoxelType VoxelType;
        /// <summary>Distance at which depth fading starts for transparent voxels.</summary>
        public float DepthFadeDistance;
        /// <summary>Whether this voxel participates in physics collision.</summary>
        public bool Collision;
        /// <summary>Texture index for the top face.</summary>
        public int TexUp;
        /// <summary>Texture index for the bottom face.</summary>
        public int TexDown;
        /// <summary>Texture index for the left face.</summary>
        public int TexLeft;
        /// <summary>Texture index for the right face.</summary>
        public int TexRight;
        /// <summary>Texture index for the front face.</summary>
        public int TexFront;
        /// <summary>Texture index for the back face.</summary>
        public int TexBack;

        public bool IsAir => MeshLayer == MeshLayer.Air;
        public bool IsFoliage => VoxelType == VoxelType.Flora;
        public bool IsTransparent => MeshLayer == MeshLayer.Transparent;

        /// <summary>
        /// Returns the texture index for a given direction.
        /// </summary>
        /// <param name="dir">Face direction to query.</param>
        /// <returns>Texture index for the face, or -1 if none is defined.</returns>
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