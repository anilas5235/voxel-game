using System;
using Unity.Mathematics;
using UnityEngine;
using static Runtime.Engine.Utils.Extensions.VectorConstants;

namespace Runtime.Engine.VoxelConfig.Data
{
    /// <summary>
    /// Cardinal directions used for voxel face orientation and neighbor queries.
    /// </summary>
    public enum Direction
    {
        Up = 0, // +y direction
        Down = 1, // -y direction
        Forward = 2, // z+ direction
        Backward = 3, // -z direction
        Right = 4, // +x direction
        Left = 5, // -x direction
    }

    /// <summary>
    /// Extension helpers for converting <see cref="Direction"/> values to vectors and getting opposite directions.
    /// </summary>
    public static class DirectionUtils
    {
        /// <summary>
        /// Ermittelt Richtung für Normal-Vektor int3 basierend auf dominanter Komponente.
        /// </summary>
        public static Direction ToDirection(this int3 vec)
        {
            if (vec.x < vec.y && vec.z < vec.y) return Direction.Up;
            if (vec.x > vec.y && vec.z > vec.y) return Direction.Down;
            if (vec.y < vec.x && vec.z < vec.x) return Direction.Right;
            if (vec.y > vec.x && vec.z > vec.x) return Direction.Left;
            if (vec.x < vec.z && vec.y < vec.z) return Direction.Forward;
            if (vec.x > vec.z && vec.y > vec.z) return Direction.Backward;

            throw new Exception("Invalid direction vector");
        }

        public static int3 ToInt3(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Int3Up,
                Direction.Down => Int3Down,
                Direction.Forward => Int3Forward,
                Direction.Backward => Int3Backward,
                Direction.Right => Int3Right,
                Direction.Left => Int3Left,

                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
        
        public static int3 RelativeUp(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Int3Forward,
                Direction.Down => Int3Backward,
                _ => Int3Up
            };
        }

        public static int3 RelativeDown(this Direction direction) => GetOpposite(direction).RelativeUp();
        
        public static int3 RelativeRight(this Direction direction)
        {
            return direction switch
            {
                Direction.Right => Int3Forward,
                Direction.Left => Int3Backward,
                Direction.Forward => Int3Left,
                Direction.Backward => Int3Right,
                Direction.Up => Int3Right,
                Direction.Down => Int3Left,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
        
        public static int3 RelativeLeft(this Direction direction) => GetOpposite(direction).RelativeRight();

        /// <summary>
        /// Converts a <see cref="Direction"/> into a corresponding <see cref="Vector3Int"/> unit vector.
        /// </summary>
        /// <param name="direction">Direction to convert.</param>
        /// <returns>Unit vector matching the given direction.</returns>
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

        /// <summary>
        /// Converts a <see cref="Direction"/> into a corresponding <see cref="int3"/> unit vector.
        /// </summary>
        /// <param name="direction">Direction to convert.</param>
        /// <returns>Unit vector matching the given direction.</returns>
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
        /// Gets the opposite direction for the given <see cref="Direction"/> value.
        /// </summary>
        /// <param name="direction">Direction whose opposite should be returned.</param>
        /// <returns>Opposite direction.</returns>
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