using System;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.Engine.Settings
{
    /// <summary>
    /// Settings for chunk size and view/load distances. Impacts memory and performance characteristics.
    /// </summary>
    [Serializable]
    public class ChunkSettings
    {
        /// <summary>
        /// Prefab containing MeshRenderer + Filter + Collider for a chunk.
        /// </summary>
        public GameObject ChunkPrefab;

        /// <summary>
        /// View distance (actively rendered chunks in each direction). Formula: active = (2*DrawDistance+1)^2.
        /// </summary>
        [Tooltip("Number of active chunks = (2 * draw_distance + 1)^2")]
        public int DrawDistance = 2;

        /// <summary>
        /// Dimensions of a single chunk (XYZ).
        /// </summary>
        [Tooltip("Chunk dimensions")] public int3 ChunkSize = 32 * new int3(1, 1, 1);

        /// <summary>
        /// Extra load distance (kept in memory) beyond view distance. Computed internally.
        /// </summary>
        [HideInInspector] [Tooltip("Number of chunks in memory = (draw_distance + 2)")]
        public int LoadDistance = 0;

        /// <summary>
        /// Distance used for updates (collider/activity). Must be &lt;= draw distance.
        /// </summary>
        [HideInInspector] [Tooltip("Should be less than equal to DrawDistance")]
        public int UpdateDistance = 0;
    }
}