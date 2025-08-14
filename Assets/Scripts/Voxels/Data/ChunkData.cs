using System;
using UnityEngine;
using static Voxels.Data.VoxelWorld;

namespace Voxels.Data
{
    public class ChunkData
    {
        private readonly int[] voxels;
        public VoxelWorld World { get; }
        public Vector3Int WorldPosition { get; private set; }
        public bool modified;

        public ChunkData(VoxelWorld world, Vector3Int worldPosition)
        {
            voxels = new int[VoxelsPerChunk];
            World = world;
            WorldPosition = worldPosition;
            modified = false;
        }

        public int GetVoxel(Vector3Int voxelPosition)
        {
            return voxels[GetIndex(voxelPosition)];
        }
        
        public int GetVoxelFromWoldVoxPos(Vector3Int voxelWorldPos)
        {
            return GetVoxel(GetVoxPosFromWorldVoxPos(voxelWorldPos));
        }

        public void SetVoxel(Vector3Int voxelPosition, int voxelId)
        {
            voxels[GetIndex(voxelPosition)] = voxelId;
            modified = true;
        }
        public void SetVoxelFromWorldVoxPos(Vector3Int voxelWorldPos, int voxelId)
        {
            SetVoxel(GetVoxPosFromWorldVoxPos(voxelWorldPos), voxelId);
        }
        
        private static int GetIndex(Vector3Int voxelPosition)
        {
            return voxelPosition.x +
                   voxelPosition.y * ChunkSize +
                   voxelPosition.z * ChunkSize * ChunkHeight;
        }
        
        private Vector3Int GetVoxPosFromWorldVoxPos(Vector3Int voxelWorldPos)
        {
            return voxelWorldPos - WorldPosition;
        }

    }
}
