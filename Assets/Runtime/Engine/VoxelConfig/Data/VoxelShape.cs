using System;
using UnityEngine;

namespace Runtime.Engine.VoxelConfig.Data
{
    [CreateAssetMenu(menuName = "Voxel/Shape/Voxel Shape", fileName = "VoxelShape")]
    public class VoxelShape : ScriptableObject
    {
        public VoxelQuad[] quads;
    }

    [Serializable]
    public class VoxelQuad
    {
        public QuadDefinition quadDef;
        public QuadDrawCondition drawCondition;
    }

    public enum QuadDrawCondition
    {
        Always,
        Up,
        Down,
        Forward,
        Backward,
        Left,
        Right
    }
}