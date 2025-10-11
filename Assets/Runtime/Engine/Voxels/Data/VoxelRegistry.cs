using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.Engine.Voxels.Data
{
    public class VoxelRegistry : IDisposable
    {
        private const int TextureSize = 128; // Assuming all textures are 128x128
        private readonly Dictionary<string, ushort> _nameToId = new() { { "air", 0 } };
        private readonly List<VoxelType> _idToVoxel = new() { null };

        private readonly Dictionary<Texture2D, int> _textureToId = new();
        private VoxelGenData _voxelGenData;
        private Texture2DArray _textureArray;

        public void Register(string packagePrefix, VoxelDefinition definition)
        {
            VoxelType type = new()
            {
                Id = (ushort)_idToVoxel.Count,
                Name = packagePrefix + ":" + definition.name,
                Collision = definition.collision,
                Transparent = definition.transparent,
                TexIds = RegisterTextures(definition)
            };

            _idToVoxel.Add(type);
            _nameToId[type.Name] = type.Id;
        }

        private int[] RegisterTextures(VoxelDefinition definition)
        {
            int[] textureIds = { -1, -1, -1, -1, -1, -1 };
            for (int i = 0; i < textureIds.Length; i++)
            {
                Texture2D tex = definition.GetTexture((Direction)i);
                if (!tex) continue;

                if (!_textureToId.TryGetValue(tex, out int textureId))
                {
                    textureId = _textureToId.Count;
                    _textureToId[tex] = textureId;
                }

                textureIds[i] = textureId;
            }

            return textureIds;
        }

        public ushort GetId(string name)
        {
            return _nameToId[name];
        }

        public VoxelType Get(ushort id)
        {
            return _idToVoxel[id];
        }
        
        public void FinalizeRegistry()
        {
            PrepareTextureArray();
            PrepareVoxelGenData();
        }

        private void PrepareVoxelGenData()
        {
            if (_voxelGenData.Voxels.IsCreated) _voxelGenData.Voxels.Dispose();
            _voxelGenData.Voxels = new NativeArray<VoxelInfo>(_idToVoxel.Count, Allocator.Persistent);
            for (int i = 0; i < _idToVoxel.Count; i++)
            {
                VoxelType type = _idToVoxel[i];
                if (type == null)
                {
                    _voxelGenData.Voxels[i] = new VoxelInfo
                    {
                        Id = 0,
                        Collision = false,
                        Transparent = true,
                        TexUp = -1,
                        TexDown = -1,
                        TexLeft = -1,
                        TexRight = -1,
                        TexFront = -1,
                        TexBack = -1
                    };
                    continue;
                }

                _voxelGenData.Voxels[i] = new VoxelInfo
                {
                    Id = type.Id,
                    Collision = type.Collision,
                    Transparent = type.Transparent,
                    TexUp = type.TexIds[0],
                    TexDown = type.TexIds[1],
                    TexLeft = type.TexIds[2],
                    TexRight = type.TexIds[3],
                    TexFront = type.TexIds[4],
                    TexBack = type.TexIds[5]
                };
            }
        }

        public VoxelGenData GetVoxelGenData()
        {
            return _voxelGenData;
        }

        private void PrepareTextureArray()
        {
            if (_textureToId.Count == 0) return;

            Texture2DArray textureArray = new(
                TextureSize,
                TextureSize,
                _textureToId.Count,
                TextureFormat.DXT1,
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
                Graphics.CopyTexture(kvp.Key, 0, 0, textureArray, index, 0);
                index++;
            }

            textureArray.Apply();
            _textureArray = textureArray;
        }

        public Texture2DArray GetTextureArray()
        {
            return _textureArray;
        }

        public void Dispose()
        {
            if (_voxelGenData.Voxels.IsCreated) _voxelGenData.Voxels.Dispose();
            if (_textureArray) UnityEngine.Object.Destroy(_textureArray);
        }
    }

    [BurstCompile]
    public struct VoxelGenData : IDisposable
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<VoxelInfo> Voxels;
        
        public int GetTextureId(ushort voxelId, int3 normal)
        {
            int vId = voxelId;
            return vId >= Voxels.Length ? int.MaxValue : Voxels[vId].GetTextureId(normal);
        }

        public void Dispose()
        {
            Voxels.Dispose();
        }
    }

    [BurstCompile]
    public struct VoxelInfo
    {
        public ushort Id;
        public bool Collision;
        public bool Transparent;
        public int TexUp;
        public int TexDown;
        public int TexLeft;
        public int TexRight;
        public int TexFront;
        public int TexBack;
        
        [BurstCompile]
        public int GetTextureId(Direction dir)
        {
            return dir switch
            {
                Direction.Up => TexUp,
                Direction.Down => TexDown,
                Direction.Left => TexLeft,
                Direction.Right => TexRight,
                Direction.Forward => TexFront,
                Direction.Backward => TexBack,
                _ => -1
            };
        }

        public int GetTextureId(int3 normal)
        {
            return normal.y switch
            {
                > 0 => TexUp,
                < 0 => TexDown,
                _ => normal.x switch
                {
                    > 0 => TexRight,
                    < 0 => TexLeft,
                    _ => normal.z switch
                    {
                        > 0 => TexFront,
                        < 0 => TexBack,
                        _ => -1
                    }
                }
            };
        }
    }
}