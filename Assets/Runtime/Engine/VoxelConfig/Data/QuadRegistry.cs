using System;
using System.Collections.Generic;
using System.Linq;
using Runtime.Engine.Utils.Logger;
using UnityEngine;

namespace Runtime.Engine.VoxelConfig.Data
{
    public class QuadRegistry
    {
        private readonly Dictionary<QuadDefinition, ushort> _quadToId = new();


        public QuadDefinition.QuadData[] QuadArray { get; private set; }


        public ushort Register(QuadDefinition quad)
        {
            ushort quadId = 0;
            if (!quad)
            {
                VoxelEngineLogger.Error<VoxelRegistry>("Attempted to register a null quad definition.");
                return quadId;
            }


            if (_quadToId.TryGetValue(quad, out quadId)) return quadId;

            quadId = (ushort)_quadToId.Count;
            _quadToId[quad] = quadId;

            return quadId;
        }

        /// <summary>
        /// Builds a <see cref="Texture2DArray"/> from all registered textures using point filtering and repeat wrapping.
        /// </summary>
        internal void PrepareArray()
        {
            if (_quadToId.Count == 0)
            {
                QuadArray = Array.Empty<QuadDefinition.QuadData>();
                return;
            }

            QuadArray = new QuadDefinition.QuadData[_quadToId.Count];
            // Copy each texture into the texture array
            int index = 0;
            foreach (var kvp in _quadToId)
            {
                VoxelEngineLogger.Info<TexRegistry>($"copy quad {kvp.Key.name} to Quad array");
                QuadArray[index] = kvp.Key.ToStruct();
                index++;
            }
        }
    }
}