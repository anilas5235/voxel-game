using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxels.Data
{
    [CreateAssetMenu(fileName = "VoxelData", menuName = "Data/Voxel Data")]
    public class VoxelDataSO : ScriptableObject
    {
        public Vector2Int atlasTextureSize;
        public Vector2Int voxelTextureSize;
        public List<TextureData> voxelTextures;
    }
    
    [Serializable]
    public class TextureData
    {
        public VoxelType voxelType;
        public Vector2Int up, down, side;
        public bool isSolid = true;
        public bool collision = true;
    }
}