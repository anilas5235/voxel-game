using UnityEngine;

namespace Runtime.Engine.Voxels.Data
{
    [CreateAssetMenu(fileName = "VoxelDefinition", menuName = "Data/Voxel Data")]
    public class VoxelDefinition : ScriptableObject
    {
        public enum VoxelTexMode
        {
            AllSame,
            TopBottomSides,
            AllUnique
        }

        [SerializeField] private VoxelTexMode textureMode = VoxelTexMode.AllSame;
        public VoxelType voxelType;

        public Texture2D top;
        public Texture2D bottom;
        public Texture2D front;
        public Texture2D back;
        public Texture2D right;
        public Texture2D left;
        public Texture2D side;
        public Texture2D all;

        public bool collision = true;
        public bool transparent;

        public VoxelTexMode TextureMode => textureMode;

        public Texture2D GetTexture(Direction direction)
        {
            return textureMode switch
            {
                VoxelTexMode.AllSame => all,
                VoxelTexMode.TopBottomSides => direction switch
                {
                    Direction.Up => top,
                    Direction.Down => bottom,
                    _ => side
                },
                VoxelTexMode.AllUnique => direction switch
                {
                    Direction.Up => top,
                    Direction.Down => bottom,
                    Direction.Forward => front,
                    Direction.Backward => back,
                    Direction.Left => left,
                    Direction.Right => right,
                    _ => null
                },
                _ => null
            };
        }
    }
}