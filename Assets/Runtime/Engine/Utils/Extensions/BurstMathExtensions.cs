using System;
using Runtime.Engine.VoxelConfig.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.Engine.Utils.Extensions
{
    /// <summary>
    /// Burst-kompatible mathematische Erweiterungen für int / int2 / int3 sowie bool3 und Vektor-Konvertierungen.
    /// Fokus auf Flatten/Größenoperationen für Chunk- und Voxelberechnung.
    /// </summary>
    [GenerateTestsForBurstCompatibility]
    public static class BurstMathExtensions
    {
        /// <summary>
        /// Liefert (2*r+1)^3 – Anzahl Zellen in einem kubischen Bereich mit Radius r.
        /// </summary>
        [BurstCompile]
        public static int CubedSize(this int r) => (2 * r + 1) * (2 * r + 1) * (2 * r + 1);

        /// <summary>
        /// Liefert (2*r+1)^2 – Anzahl Zellen in einem quadratischen Bereich mit Radius r.
        /// </summary>
        [BurstCompile]
        public static int SquareSize(this int r) => (2 * r + 1) * (2 * r + 1);

        /// <summary>
        /// Liefert (2*r.x+1)*(2*r.y+1)*(2*r.z+1) für anisotropen Radius.
        /// </summary>
        [BurstCompile]
        public static int CubedSize(this int3 r) => (2 * r.x + 1) * (2 * r.y + 1) * (2 * r.z + 1);

        /// <summary>
        /// Flacht 2D Koordinaten (x,y) auf eindimensionalen Index basierend auf Größe (vec.x, vec.y) ab.
        /// </summary>
        [BurstCompile]
        public static int Flatten(this int2 vec, int x, int y) =>
            x * vec.y +
            y;

        /// <summary>
        /// Flacht 3D Koordinaten (x,y,z) auf eindimensionalen Index ab (x*Y*Z + z*Y + y).
        /// </summary>
        [BurstCompile]
        public static int Flatten(this int3 vec, int x, int y, int z) =>
            x * vec.y * vec.z +
            z * vec.y +
            y;

        /// <summary>
        /// Flacht 3D Position ab unter Verwendung eines int3 Position-Structs.
        /// </summary>
        [BurstCompile]
        public static int Flatten(this int3 vec, int3 pos) =>
            pos.x * vec.y * vec.z +
            pos.z * vec.y +
            pos.y;

        /// <summary>
        /// Reduziert bool3 mit OR Verknüpfung.
        /// </summary>
        [BurstCompile]
        public static bool OrReduce(this bool3 val) => val.x || val.y || val.z;

        /// <summary>
        /// Reduziert bool3 mit AND Verknüpfung.
        /// </summary>
        [BurstCompile]
        public static bool AndReduce(this bool3 val) => val.x && val.y && val.z;

        /// <summary>
        /// Volumen eines int3 (x*y*z).
        /// </summary>
        [BurstCompile]
        public static int Size(this int3 vec) => vec.x * vec.y * vec.z;

        /// <summary>
        /// Konvertiert int3 nach Vector3Int.
        /// </summary>
        [BurstCompile]
        public static Vector3Int GetVector3Int(this int3 vec) => new(vec.x, vec.y, vec.z);

        /// <summary>
        /// Konvertiert int3 nach Vector3.
        /// </summary>
        [BurstCompile]
        public static Vector3 GetVector3(this int3 vec) => new(vec.x, vec.y, vec.z);
        
        /// <summary>
        /// Konvertiert int2 nach Vector2.
        /// </summary>
        [BurstCompile]
        public static Vector3 GetVector2(this int2 vec) => new(vec.x, vec.y);
    }

    /// <summary>
    /// Nicht-Burst spezifische mathematische Erweiterungen für Distanz und komponentenweises Multiplizieren.
    /// </summary>
    public static class MathExtension
    {
        /// <summary>
        /// Quadratische Länge eines int3 Vektors.
        /// </summary>
        public static int SqrMagnitude(this int3 vec) => vec.x * vec.x + vec.y * vec.y + vec.z * vec.z;
        
        /// <summary>
        /// Quadratische Länge eines int3 Vektors.
        /// </summary>
        public static int SqrMagnitude(this int2 vec) => vec.x * vec.x + vec.y * vec.y;

        /// <summary>
        /// Komponentenweise Multiplikation zweier int3.
        /// </summary>
        public static int3 MemberMultiply(this int3 a, int3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);

        /// <summary>
        /// Komponentenweise Multiplikation mit Einzelwerten.
        /// </summary>
        public static int3 MemberMultiply(this int3 a, int x, int y, int z) => new(a.x * x, a.y * y, a.z * z);
    }

    /// <summary>
    /// Erweiterungen für Vektor Konvertierung, Normalisierung und Richtungsermittlung.
    /// </summary>
    public static class VectorExtension
    {
        /// <summary>
        /// Konvertiert Vector3Int zu int3.
        /// </summary>
        public static int3 Int3(this Vector3Int vec) => new(vec.x, vec.y, vec.z);

        /// <summary>
        /// Konvertiert Vector3 (floored) zu int3.
        /// </summary>
        public static int3 Int3(this Vector3 vec) => Vector3Int.FloorToInt(vec).Int3();

        /// <summary>
        /// Konvertiert Vector3 (floored) zu Vector3Int.
        /// </summary>
        public static Vector3Int V3Int(this Vector3 vec) => Vector3Int.FloorToInt(vec);

        /// <summary>
        /// Konvertiert int2 zu float2.
        /// </summary>
        public static float2 Float2(this int2 vec) => new(vec.x, vec.y);

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

        /// <summary>
        /// Normalisiert einen float3.
        /// </summary>
        public static float3 Normalized(this float3 vec) => math.normalize(vec);
    }
}