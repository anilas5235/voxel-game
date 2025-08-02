using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxels.Data
{
    public class VoxelTextureManager : MonoBehaviour
    {
        public static float TextureOffset = 0.001f; // Offset to prevent z-fighting
        public static Dictionary<VoxelType, TextureData> VoxelTextures { get; } = new();
        public VoxelDataSO textureData;
        
        public static Vector2 UVTileSize;

        private void Awake()
        {
            if (!textureData)
            {
                Debug.LogError("VoxelDataSO is not assigned in VoxelTextureManager.");
                return;
            }

            foreach (TextureData texture in textureData.voxelTextures)
            {
                if (!VoxelTextures.TryAdd(texture.voxelType, texture))
                {
                    Debug.LogWarning($"Duplicate voxel type {texture.voxelType} found in VoxelDataSO.");
                }
            }
            
            UVTileSize = textureData.GetUVTileSize();
        }
    }
}