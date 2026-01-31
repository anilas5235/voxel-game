using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace Runtime.Engine.VoxelConfig.Data
{
    /// <summary>
    /// Registry that manages voxel render definitions, texture arrays and name-to-ID mappings.
    /// Can be finalized to build a NativeArray for Burst-compatible meshing data.
    /// </summary>
    public class VoxelRegistry : IDisposable
    {
        private static readonly int Textures = Shader.PropertyToID("_Textures");
        internal const int TextureSize = 128; // Texture resolution (square)
        private readonly Dictionary<string, ushort> _nameToId = new();
        private readonly Dictionary<ushort, string> _idToName = new();
        private readonly Dictionary<ushort, VoxelRenderDef> _idToVoxel = new(100);
        private readonly Dictionary<ushort, VoxelDefinition> _idToVoxelDefinition = new(100);
        private VoxelEngineRenderGenData _voxelEngineRenderGenData;
        private readonly TexRegistry _solidTexRegistry = new();
        private readonly TexRegistry _transparentTexRegistry = new();
        private bool _initialized;

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Register("std:air", new VoxelRenderDef
            {
                MeshLayer = MeshLayer.Air,
                Collision = false,
                TexUp = -1,
                TexDown = -1,
                TexFront = -1,
                TexBack = -1,
                TexLeft = -1,
                TexRight = -1
            });
            _voxelEngineRenderGenData = new VoxelEngineRenderGenData();
        }

        /// <summary>
        /// Registers a voxel definition, builds its texture-based <see cref="VoxelRenderDef"/>, 
        /// and assigns a new voxel ID.
        /// </summary>
        /// <param name="packagePrefix">Prefix of the package this definition belongs to.</param>
        /// <param name="definition">Voxel definition asset to register.</param>
        public void Register(string packagePrefix, VoxelDefinition definition)
        {
            VoxelRenderDef type = new()
            {
                MeshLayer = definition.meshLayer,
                AlwaysRenderAllFaces = definition.alwaysRenderAllFaces,
                VoxelType = definition.voxelType,
                DepthFadeDistance = definition.depthFadeDistance,
                Collision = definition.collision,
                TexUp = RegisterTexture(definition, Direction.Up),
                TexDown = RegisterTexture(definition, Direction.Down),
                TexFront = RegisterTexture(definition, Direction.Forward),
                TexBack = RegisterTexture(definition, Direction.Backward),
                TexLeft = RegisterTexture(definition, Direction.Left),
                TexRight = RegisterTexture(definition, Direction.Right)
            };

            ushort id = Register(packagePrefix + ":" + definition.name, type);
            if (id == 0) return;
            type.Id = id;
            _idToVoxelDefinition.Add(id, definition);
        }

        private ushort Register(string name, VoxelRenderDef renderDef)
        {
            Initialize();
            if (_nameToId.ContainsKey(name))
            {
                Debug.LogWarning($"Voxel with name {name} is already registered.");
                return 0;
            }

            ushort id = (ushort)_idToVoxel.Count;
            _idToVoxel.Add(id, renderDef);
            _nameToId.Add(name, id);
            _idToName.Add(id, name);
            return id;
        }

        private int RegisterTexture(VoxelDefinition definition, Direction dir)
        {
            Texture2D tex = definition.GetTexture(dir);
            return definition.meshLayer switch
            {
                MeshLayer.Solid => _solidTexRegistry.RegisterTexture(tex),
                MeshLayer.Transparent => _transparentTexRegistry.RegisterTexture(tex),
                _ => -1
            };
        }

        /// <summary>
        /// Tries to get an ID for a given name.
        /// </summary>
        /// <param name="name">Registered voxel name.</param>
        /// <param name="id">Resulting voxel ID if found.</param>
        /// <returns><c>true</c> if the name exists; otherwise, <c>false</c>.</returns>
        public bool GetId(string name, out ushort id) => _nameToId.TryGetValue(name, out id);

        /// <summary>
        /// Gets the ID for a given name or throws if it does not exist.
        /// </summary>
        /// <param name="name">Registered voxel name.</param>
        /// <returns>Voxel ID associated with the name.</returns>
        public ushort GetIdOrThrow(string name) => _nameToId[name];

        /// <summary>
        /// Tries to get the registered name for a voxel ID.
        /// </summary>
        /// <param name="id">Voxel ID.</param>
        /// <param name="name">Output name if found.</param>
        /// <returns><c>true</c> if the ID exists; otherwise, <c>false</c>.</returns>
        public bool GetName(ushort id, out string name) => _idToName.TryGetValue(id, out name);

        /// <summary>
        /// Tries to get the <see cref="VoxelDefinition"/> associated with the given ID.
        /// </summary>
        /// <param name="id">Voxel ID.</param>
        /// <param name="def">Output voxel definition if found.</param>
        /// <returns><c>true</c> if the ID has an associated definition; otherwise, <c>false</c>.</returns>
        public bool GetVoxelDefinition(ushort id, out VoxelDefinition def) =>
            _idToVoxelDefinition.TryGetValue(id, out def);

        /// <summary>
        /// Finalizes registration: builds texture arrays and a native array for render definitions.
        /// </summary>
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

        /// <summary>
        /// Retrieves the data package used for meshing and rendering.
        /// </summary>
        /// <returns>Voxel engine render generation data structure.</returns>
        public VoxelEngineRenderGenData GetVoxelGenData()
        {
            return _voxelEngineRenderGenData;
        }

        private void PrepareTextureArray()
        {
            _solidTexRegistry.PrepareTextureArray();
            _transparentTexRegistry.PrepareTextureArray();
        }

        private Texture2DArray GetTextureArray(MeshLayer meshLayer)
        {
            return meshLayer switch
            {
                MeshLayer.Solid => _solidTexRegistry.TextureArray,
                MeshLayer.Transparent => _transparentTexRegistry.TextureArray,
                _ => null
            };
        }

        /// <summary>
        /// Releases Burst-native resources used by the registry.
        /// </summary>
        public void Dispose()
        {
            if (_voxelEngineRenderGenData.VoxelRenderDefs.IsCreated)
                _voxelEngineRenderGenData.VoxelRenderDefs.Dispose();
        }

        /// <summary>
        /// Applies the texture array for a given mesh layer to the specified material (shader property "_Textures").
        /// </summary>
        /// <param name="material">Material to assign the texture array to.</param>
        /// <param name="solid">Mesh layer whose texture array should be used.</param>
        public void ApplyToMaterial(Material material, MeshLayer solid)
        {
            if (material)
            {
                Texture2DArray texArray = GetTextureArray(solid);
                if (texArray)
                    material.SetTexture(Textures, texArray);
                else
                    Debug.LogWarning("Texture array is null, cannot assign to material.");
            }
            else
            {
                Debug.LogWarning("Voxel material is null, cannot assign texture array.");
            }
        }

        /// <summary>
        /// Returns a list of all registered IDs and their corresponding names.
        /// </summary>
        /// <returns>List of ID/name pairs.</returns>
        public List<KeyValuePair<ushort, string>> GetAllEntries()
        {
            return _idToName.ToList();
        }
    }
}