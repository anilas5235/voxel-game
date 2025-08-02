using UnityEngine;

namespace Voxels.Data
{
    public class ChunkData
    {
        public readonly VoxelType[] voxelData;
        public VoxelWorld World { get; }
        public Vector3Int WorldPosition { get; private set; }
        public bool modified;

        public ChunkData(VoxelWorld world, Vector3Int worldPosition)
        {
            voxelData = new VoxelType[VoxelWorld.VoxelsPerChunk];
            World = world;
            WorldPosition = worldPosition;
            modified = false;
        }
    }
}
