using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Voxels.Data
{
    [CreateAssetMenu(fileName = "VoxelDefinition", menuName = "Data/Voxel Data")]
    public class VoxelDefinition : ScriptableObject
    {
        public enum VoxelTexMode
        {
            AllSame,
            TopBottomSides,
            AllUnique
        }

        [SerializeField] private VoxelTexMode textureMode = VoxelTexMode.AllSame;
        public MeshLayer meshLayer;
        
        public VoxelType voxelType;
        public bool alwaysRenderAllFaces;
        public float depthFadeDistance = -1f;
        public VoxelPostProcessData postProcess = new();

        public Texture2D top;
        public Texture2D bottom;
        public Texture2D front;
        public Texture2D back;
        public Texture2D right;
        public Texture2D left;
        public Texture2D side;
        public Texture2D all;

        public bool collision = true;

        public VoxelTexMode TextureMode { get => textureMode; set => textureMode = value; }

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
    
    [Serializable]
    public class VoxelPostProcessData
    {
        public Color postProcessColor;
        public float contrast;
        public float saturation;
        public bool enableFog;
        public float fogDensity = .01f;
    }
}