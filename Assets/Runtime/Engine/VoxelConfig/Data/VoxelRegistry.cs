using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace Runtime.Engine.VoxelConfig.Data
{
    /// <summary>
    /// Registry zur Verwaltung von Voxel Render Definitionen, Textur-Arrays und Mapping Name ↔ ID.
    /// Bietet Finalisierung zur Erstellung eines NativeArrays für Burst-kompatible Meshing-Daten.
    /// </summary>
    public class VoxelRegistry : IDisposable
    {
        private static readonly int Textures = Shader.PropertyToID("_Textures");
        internal const int TextureSize = 128; // Texturauflösung (quadratisch)
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
        /// Registriert eine Voxel Definition samt texturabhängigen RenderDef und weist eine neue ID zu.
        /// </summary>
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
        /// Holt eine ID zu einem Namen, oder false bei Nichtexistenz.
        /// </summary>
        public bool GetId(string name, out ushort id) => _nameToId.TryGetValue(name, out id);
        /// <summary>
        /// Liefert ID oder wirft bei nicht vorhanden.
        /// </summary>
        public ushort GetIdOrThrow(string name) => _nameToId[name];
        /// <summary>
        /// Holt Namen zu ID.
        /// </summary>
        public bool GetName(ushort id, out string name) => _idToName.TryGetValue(id, out name);
        /// <summary>
        /// Holt VoxelDefinition zu ID.
        /// </summary>
        public bool GetVoxelDefinition(ushort id, out VoxelDefinition def) =>
            _idToVoxelDefinition.TryGetValue(id, out def);

        /// <summary>
        /// Finalisiert Registrierung: erzeugt Texture Arrays und NativeArray für RenderDefs.
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
        /// Liefert das Datenpaket für Meshing/Rendering.
        /// </summary>
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
        /// Gibt Burst Native Ressourcen frei.
        /// </summary>
        public void Dispose()
        {
            if (_voxelEngineRenderGenData.VoxelRenderDefs.IsCreated)
                _voxelEngineRenderGenData.VoxelRenderDefs.Dispose();
        }

        /// <summary>
        /// Wendet Texture Array des Layers auf Material an (Shader Property _Textures).
        /// </summary>
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
        /// Liefert Liste aller registrierten IDs mit Namen.
        /// </summary>
        public List<KeyValuePair<ushort, string>> GetAllEntries()
        {
            return _idToName.ToList();
        }
    }
}