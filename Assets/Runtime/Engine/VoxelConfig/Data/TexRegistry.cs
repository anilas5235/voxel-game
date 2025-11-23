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
        /// Resulting texture array after <see cref="PrepareTextureArray"/>.
        /// </summary>
        public Texture2DArray TextureArray { get; private set; }

        /// <summary>
        /// Registers a texture and assigns an index ID (if size matches expectations).
        /// Returns index or -1 on failure.
        /// </summary>
        /// <returns>Index or -1 on failure.</returns>
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
        /// Builds a Texture2DArray from all registered textures (Point filter, Repeat wrap).
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