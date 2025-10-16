using System.Collections.Generic;
using System.Linq;
using Runtime.Engine.Utils.Logger;
using UnityEngine;

namespace Runtime.Engine.Voxels.Data
{
    internal class TexRegistry
    {
        private static int TextureSize => VoxelRegistry.TextureSize;
        private readonly Dictionary<Texture2D, int> _textureToId = new();
        public Texture2DArray TextureArray { get; private set; }

        public int RegisterTexture(Texture2D tex)
        {
            int textureId = -1;
            if (!tex) return textureId;
            if (tex.width != TextureSize || tex.height != TextureSize)
            {
                VoxelEngineLogger.Warn<VoxelRegistry>(
                    $"Texture {tex.name} size is {tex.width}x{tex.height}, expected {TextureSize}x{TextureSize}. It will be ignored.");
                return textureId;
            }

            if (_textureToId.TryGetValue(tex, out textureId)) return textureId;

            textureId = _textureToId.Count;
            _textureToId[tex] = textureId;

            return textureId;
        }

        internal void PrepareTextureArray()
        {
            if (_textureToId.Count == 0) return;

            Texture2DArray textureArray = new(
                TextureSize,
                TextureSize,
                _textureToId.Count,
                _textureToId.First().Key.format,
                false
            )
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            // Copy each texture into the texture array
            int index = 0;
            foreach (KeyValuePair<Texture2D, int> kvp in _textureToId)
            {
                VoxelEngineLogger.Info<TexRegistry>($"copy texture {kvp.Key.name} to texture array");
                Graphics.CopyTexture(kvp.Key, 0, 0, textureArray, index, 0);
                index++;
            }

            textureArray.Apply();
            TextureArray = textureArray;
        }
    }
}