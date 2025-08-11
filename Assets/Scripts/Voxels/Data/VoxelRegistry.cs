using System.Collections.Generic;
using UnityEngine;

namespace Voxels.Data
{
    public static class VoxelRegistry
    {
        private static readonly Dictionary<string, int> NameToId = new(){{"air", 0}};
        private static readonly List<VoxelType> IDToVoxel = new(){ null };
        
        private static readonly Dictionary<Texture2D,int> TextureToId = new();
        

        public static void Register(string packagePrefix, VoxelDefinition definition)
        {
            VoxelType type = new()
            {
                Id = IDToVoxel.Count,
                Name = packagePrefix + ":" + definition.name,
                Collision = definition.collision,
                Transparent = definition.transparent,
                TexIds = new float[6]
            };
            
            for (int i = 0; i < 6; i++)
            {
                Texture2D tex = definition.GetTexture((Direction)i);
                if (tex)
                {
                    if (!TextureToId.TryGetValue(tex, out int textureId))
                    {
                        textureId = TextureToId.Count;
                        TextureToId[tex] = textureId;
                    }
                    type.TexIds[i] = textureId;
                }
                else
                {
                    type.TexIds[i] = -1; // No texture assigned
                }
            }
            IDToVoxel.Add(type);
            NameToId[type.Name] = type.Id;
        }

        public static int GetId(string name) => NameToId[name];

        public static VoxelType Get(int id) => IDToVoxel[id];

        public static Texture2DArray GetTextureArray()
        {
            if (TextureToId.Count == 0) return null;

            Texture2DArray textureArray = new(
                128, // Assuming all textures are 16x16
                128,
                TextureToId.Count,
                TextureFormat.DXT1,
                false
            );

            int index = 0;
            foreach (KeyValuePair<Texture2D, int> kvp in TextureToId)
            {
                Graphics.CopyTexture(kvp.Key, 0, 0, textureArray, index, 0);
                index++;
            }
            textureArray.Apply();
            return textureArray;
        }
    }

    public class VoxelType
    {
        public int Id; // internal ID
        public string Name;
        public bool Collision;
        public bool Transparent;
        public float[] TexIds; // Texture IDs for the voxel, must have 6 elements (for each face)
    }
}