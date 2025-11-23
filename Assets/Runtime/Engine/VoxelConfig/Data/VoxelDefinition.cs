using System;
using UnityEngine;

namespace Runtime.Engine.VoxelConfig.Data
{
    /// <summary>
    /// ScriptableObject that describes a single voxel type, including textures, mesh layer,
    /// voxel type, collision and optional post-processing data.
    /// </summary>
    [CreateAssetMenu(fileName = "VoxelDefinition", menuName = "Data/Voxel Data")]
    public class VoxelDefinition : ScriptableObject
    {
        /// <summary>
        /// Defines how textures are assigned to voxel faces.
        /// </summary>
        public enum VoxelTexMode
        {
            /// <summary>
            /// One texture is used for all faces.
            /// </summary>
            AllSame,
            /// <summary>
            /// Separate textures for top and bottom, and one shared texture for all side faces.
            /// </summary>
            TopBottomSides,
            /// <summary>
            /// All faces can have unique textures.
            /// </summary>
            AllUnique
        }

        [SerializeField] private VoxelTexMode textureMode = VoxelTexMode.AllSame;

        /// <summary>
        /// Mesh layer used when rendering this voxel instance.
        /// </summary>
        public MeshLayer meshLayer;

        /// <summary>
        /// Semantic voxel type (e.g. solid, liquid, flora).
        /// </summary>
        public VoxelType voxelType;

        /// <summary>
        /// If true, all faces are always rendered even when hidden by neighbors.
        /// </summary>
        public bool alwaysRenderAllFaces;

        /// <summary>
        /// Distance at which transparent voxels start fading; negative value disables depth fading.
        /// </summary>
        public float depthFadeDistance = -1f;

        /// <summary>
        /// Optional post processing data applied when rendering this voxel.
        /// </summary>
        public VoxelPostProcessData postProcess = new();

        /// <summary>Texture used for the top face.</summary>
        public Texture2D top;
        /// <summary>Texture used for the bottom face.</summary>
        public Texture2D bottom;
        /// <summary>Texture used for the forward (+Z) face.</summary>
        public Texture2D front;
        /// <summary>Texture used for the backward (-Z) face.</summary>
        public Texture2D back;
        /// <summary>Texture used for the right (+X) face.</summary>
        public Texture2D right;
        /// <summary>Texture used for the left (-X) face.</summary>
        public Texture2D left;
        /// <summary>Texture used for side faces when using <see cref="VoxelTexMode.TopBottomSides"/>.</summary>
        public Texture2D side;
        /// <summary>Single texture used for all faces when using <see cref="VoxelTexMode.AllSame"/>.</summary>
        public Texture2D all;

        /// <summary>
        /// If true, this voxel participates in physics collisions.
        /// </summary>
        public bool collision = true;

        /// <summary>
        /// Gets or sets the texture mapping mode for this voxel.
        /// </summary>
        public VoxelTexMode TextureMode
        {
            get => textureMode;
            set => textureMode = value;
        }

        /// <summary>
        /// Returns the texture that should be used for the specified face direction
        /// according to the current <see cref="TextureMode"/>.
        /// </summary>
        /// <param name="direction">Face direction to retrieve the texture for.</param>
        /// <returns>Texture for the given direction, or <c>null</c> if none is assigned.</returns>
        public Texture2D GetTexture(Direction direction)
        {
            return textureMode switch
            {
                VoxelTexMode.AllSame => all,
                VoxelTexMode.TopBottomSides => direction switch
                {
                    Direction.Up => top,
                    Direction.Down => bottom,
                    _ => side
                },
                VoxelTexMode.AllUnique => direction switch
                {
                    Direction.Up => top,
                    Direction.Down => bottom,
                    Direction.Forward => front,
                    Direction.Backward => back,
                    Direction.Left => left,
                    Direction.Right => right,
                    _ => null
                },
                _ => null
            };
        }
    }

    /// <summary>
    /// Optional per-voxel post processing parameters such as color grading and fog.
    /// </summary>
    [Serializable]
    public class VoxelPostProcessData
    {
        /// <summary>
        /// Color tint applied during post processing.
        /// </summary>
        public Color postProcessColor;

        /// <summary>
        /// Contrast adjustment factor.
        /// </summary>
        public float contrast;

        /// <summary>
        /// Saturation adjustment factor.
        /// </summary>
        public float saturation;

        /// <summary>
        /// Enables additional fog for this voxel type.
        /// </summary>
        public bool enableFog;

        /// <summary>
        /// Fog density value used when <see cref="enableFog"/> is true.
        /// </summary>
        public float fogDensity = .01f;
    }
}