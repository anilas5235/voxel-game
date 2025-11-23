using System.Collections.Generic;
using System.Linq;
using Runtime.Engine.Utils.Logger;
using UnityEngine;

namespace Runtime.Engine.VoxelConfig.Data
{
    /// <summary>
    /// Registers textures for voxel definitions and builds a shared <see cref="Texture2DArray"/> atlas.
    /// </summary>
    internal class TexRegistry
    {
        private static int TextureSize => VoxelRegistry.TextureSize;
        private readonly Dictionary<Texture2D, int> _textureToId = new();

        /// <summary>
        /// Gets the resulting texture array after <see cref="PrepareTextureArray"/> has been called.
        /// </summary>
        public Texture2DArray TextureArray { get; private set; }

        /// <summary>
        /// Registers a texture and assigns an index ID if its size matches the expected atlas size.
        /// Returns the index or -1 on failure.
        /// </summary>
        /// <param name="tex">Texture to register.</param>
        /// <returns>Assigned texture index, or -1 if registration failed.</returns>
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

        /// <summary>
        /// Builds a <see cref="Texture2DArray"/> from all registered textures using point filtering and repeat wrapping.
        /// </summary>
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