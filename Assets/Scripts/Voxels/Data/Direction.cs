using UnityEngine;

namespace Voxels.Data
{
    public enum Direction
    {
        Forward = 0, // z+ direction
        Right = 1, // +x direction
        Backwards = 2, // -z direction
        Left = 3, // -x direction
        Up = 4, // +y direction
        Down = 5 // -y direction
    }

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
        
        public static Direction GetOpposite(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Down,
                Direction.Down => Direction.Up,
                Direction.Forward => Direction.Backwards,
                Direction.Backwards => Direction.Forward,
                Direction.Right => Direction.Left,
                Direction.Left => Direction.Right,
                
                _ => throw new System.ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
        
        public static readonly Direction[] TraversalOrder =
        {
            Direction.Backwards,
            Direction.Down,
            Direction.Forward,
            Direction.Left,
            Direction.Right,
            Direction.Up,
        };
    }
}