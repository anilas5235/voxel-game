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

        public Vector2 GetUVTileSize()
        {
            return new Vector2(
                1f/(atlasTextureSize.x / (float)voxelTextureSize.x),
                1f/(atlasTextureSize.y / (float)voxelTextureSize.y)
            );
        }
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