using System.Collections.Generic;
using UnityEngine;
using Voxels.MeshGeneration;

namespace Voxels.Data
{
    public static class VoxelRegistry
    {
        private const int TextureSize = 128; // Assuming all textures are 128x128
        private static readonly Dictionary<string, ushort> NameToId = new() { { "air", 0 } };
        private static readonly List<VoxelType> IDToVoxel = new() { null };

        private static readonly Dictionary<Texture2D, int> TextureToId = new();


        public static void Register(string packagePrefix, VoxelDefinition definition)
        {
            VoxelType type = new()
            {
                Id = (ushort)IDToVoxel.Count,
                Name = packagePrefix + ":" + definition.name,
                Collision = definition.collision,
                Transparent = definition.transparent,
                TexIds = RegisterTextures(definition)
            };

            IDToVoxel.Add(type);
            NameToId[type.Name] = type.Id;
        }

        private static int[] RegisterTextures(VoxelDefinition definition)
        {
            int[] textureIds = { -1, -1, -1, -1, -1, -1 };
            for (int i = 0; i < textureIds.Length; i++)
            {
                Texture2D tex = definition.GetTexture((Direction)i);
                if (!tex) continue;

                if (!TextureToId.TryGetValue(tex, out int textureId))
                {
                    textureId = TextureToId.Count;
                    TextureToId[tex] = textureId;
                }

                textureIds[i] = textureId;
            }

            return textureIds;
        }

        public static ushort GetId(string name)
        {
            return NameToId[name];
        }

        public static VoxelType Get(ushort id)
        {
            return IDToVoxel[id];
        }

        public static Texture2DArray GetTextureArray()
        {
            if (TextureToId.Count == 0) return null;

            Texture2DArray textureArray = new(
                TextureSize,
                TextureSize,
                TextureToId.Count,
                TextureFormat.DXT1,
                false
            )
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            // Copy each texture into the texture array
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
}