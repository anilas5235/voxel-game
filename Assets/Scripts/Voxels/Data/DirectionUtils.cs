using UnityEngine;

namespace Voxels.Data
{
    public static class DirectionUtils
    {
        public static Vector3Int GetVector(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector3Int.up,
                Direction.Down => Vector3Int.down,
                Direction.Forward => Vector3Int.forward,
                Direction.Backwards => Vector3Int.back,
                Direction.Right => Vector3Int.right,
                Direction.Left => Vector3Int.left,
              
                _ => throw new System.ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
    }
}