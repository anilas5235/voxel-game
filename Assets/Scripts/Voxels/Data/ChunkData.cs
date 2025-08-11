using UnityEngine;

namespace Voxels.Data
{
    public class ChunkData
    {
        public readonly int[] voxels;
        public VoxelWorld World { get; }
        public Vector3Int WorldPosition { get; private set; }
        public bool modified;

        public ChunkData(VoxelWorld world, Vector3Int worldPosition)
        {
            voxels = new int[VoxelWorld.VoxelsPerChunk];
            World = world;
            WorldPosition = worldPosition;
            modified = false;
        }
    }
}
