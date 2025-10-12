using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Runtime.Engine.Voxels.Data
{
    public class VoxelRegistry : IDisposable
    {
        private static readonly int Textures = Shader.PropertyToID("_Textures");
        internal const int TextureSize = 128; // Assuming all textures are 128x128
        private readonly Dictionary<string, ushort> _nameToId = new();
        private readonly Dictionary<ushort, string> _idToName = new();

        private readonly Dictionary<ushort, VoxelRenderDef> _idToVoxel = new(100);

        private VoxelEngineRenderGenData _voxelEngineRenderGenData;

        private readonly TexRegistry _solidTexRegistry = new();
        private readonly TexRegistry _transparentTexRegistry = new();
        private readonly TexRegistry _foliageTexRegistry = new();


        private bool _initialized;

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Register("air", new VoxelRenderDef
            {
                Collision = false,
                Transparent = true,
                TexUp = -1,
                TexDown = -1,
                TexFront = -1,
                TexBack = -1,
                TexLeft = -1,
                TexRight = -1
            });
            _voxelEngineRenderGenData = new VoxelEngineRenderGenData();
        }

        public void Register(string packagePrefix, VoxelDefinition definition)
        {
            VoxelRenderDef type = new()
            {
                VoxelType = definition.voxelType,
                Collision = definition.collision,
                Transparent = definition.transparent,
                TexUp = RegisterTexture(definition, Direction.Up),
                TexDown = RegisterTexture(definition, Direction.Down),
                TexFront = RegisterTexture(definition, Direction.Forward),
                TexBack = RegisterTexture(definition, Direction.Backward),
                TexLeft = RegisterTexture(definition, Direction.Left),
                TexRight = RegisterTexture(definition, Direction.Right)
            };

            Register(packagePrefix + ":" + definition.name, type);
        }

        private void Register(string name, VoxelRenderDef renderDef)
        {
            Initialize();
            if (_nameToId.ContainsKey(name))
            {
                Debug.LogWarning($"Voxel with name {name} is already registered.");
                return;
            }

            ushort id = (ushort)_idToVoxel.Count;
            _idToVoxel.Add(id, renderDef);
            _nameToId.Add(name, id);
            _idToName.Add(id, name);
        }

        private int RegisterTexture(VoxelDefinition definition, Direction dir)
        {
            Texture2D tex = definition.GetTexture(dir);
            return definition.voxelType switch
            {
                VoxelType.Solid => _solidTexRegistry.RegisterTexture(tex),
                VoxelType.Transparent => _transparentTexRegistry.RegisterTexture(tex),
                VoxelType.Foliage => _foliageTexRegistry.RegisterTexture(tex),
                _ => -1
            };
        }

        public ushort GetId(string name)
        {
            return _nameToId[name];
        }

        public string GetName(ushort id)
        {
            return _idToName[id];
        }

        public void FinalizeRegistry()
        {
            PrepareTextureArray();
            PrepareVoxelGenData();
        }

        private void PrepareVoxelGenData()
        {
            if (_voxelEngineRenderGenData.VoxelRenderDefs.IsCreated)
                _voxelEngineRenderGenData.VoxelRenderDefs.Dispose();
            _voxelEngineRenderGenData.VoxelRenderDefs =
                new NativeArray<VoxelRenderDef>(_idToVoxel.Count, Allocator.Persistent);
            for (int i = 0; i < _idToVoxel.Count; i++)
            {
                _voxelEngineRenderGenData.VoxelRenderDefs[i] = _idToVoxel[(ushort)i];
            }
        }

        public VoxelEngineRenderGenData GetVoxelGenData()
        {
            return _voxelEngineRenderGenData;
        }

        private void PrepareTextureArray()
        {
            _solidTexRegistry.PrepareTextureArray();
            _transparentTexRegistry.PrepareTextureArray();
            _foliageTexRegistry.PrepareTextureArray();
        }

        public Texture2DArray GetTextureArray(VoxelType voxelType)
        {
            return voxelType switch
            {
                VoxelType.Solid => _solidTexRegistry.TextureArray,
                VoxelType.Transparent => _transparentTexRegistry.TextureArray,
                VoxelType.Foliage => _foliageTexRegistry.TextureArray,
                _ => null
            };
        }

        public void Dispose()
        {
            _voxelEngineRenderGenData.VoxelRenderDefs.Dispose();
        }

        public void ApplyToMaterial(Material voxelSolidMaterial, VoxelType solid)
        {
            if (voxelSolidMaterial)
            {
                Texture2DArray texArray = GetTextureArray(solid);
                if (texArray)
                    voxelSolidMaterial.SetTexture(Textures, texArray);
                else
                    Debug.LogWarning("Texture array is null, cannot assign to material.");
            }
            else
            {
                Debug.LogWarning("Voxel material is null, cannot assign texture array.");
            }
        }
    }
}