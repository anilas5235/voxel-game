using System;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.Engine.Voxels.Data
{
    public enum Direction
    {
        Forward = 0, // z+ direction
        Right = 1, // +x direction
        Backward = 2, // -z direction
        Left = 3, // -x direction
        Up = 4, // +y direction
        Down = 5 // -y direction
    }

    public static class DirectionUtils
    {
        public static readonly Direction[] TraversalOrder =
        {
            Direction.Backward,
            Direction.Down,
            Direction.Forward,
            Direction.Left,
            Direction.Right,
            Direction.Up
        };

        public static Vector3Int GetVector(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector3Int.up,
                Direction.Down => Vector3Int.down,
                Direction.Forward => Vector3Int.forward,
                Direction.Backward => Vector3Int.back,
                Direction.Right => Vector3Int.right,
                Direction.Left => Vector3Int.left,

                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
        
        public static int3 GetInt3(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => new int3(0, 1, 0),
                Direction.Down => new int3(0, -1, 0),
                Direction.Forward => new int3(0, 0, 1),
                Direction.Backward => new int3(0, 0, -1),
                Direction.Right => new int3(1, 0, 0),
                Direction.Left => new int3(-1, 0, 0),

                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }

        /// <summary>
        ///     Determines if a face is vertical (not up or down).
        /// </summary>
        public static bool IsVertical(this Direction direction)
        {
            return direction is not (Direction.Up or Direction.Down);
        }

        public static Direction GetOpposite(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Down,
                Direction.Down => Direction.Up,
                Direction.Forward => Direction.Backward,
                Direction.Backward => Direction.Forward,
                Direction.Right => Direction.Left,
                Direction.Left => Direction.Right,

                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
    }
}