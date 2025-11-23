using System;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.Engine.Settings
{
    /// <summary>
    /// Einstellungen für Chunkgröße und Sicht-/Lade-Distanzen. Beeinflusst Speicher- und Performance-Charakteristik.
    /// </summary>
    [Serializable]
    public class ChunkSettings
    {
        /// <summary>
        /// Prefab welches MeshRenderer + Filter + Collider enthält für einen Chunk.
        /// </summary>
        public GameObject ChunkPrefab;

        /// <summary>
        /// Sichtweite (aktive gerenderte Chunks in jede Richtung). Formel: aktive Chunks = (2 * DrawDistance + 1)^2.
        /// </summary>
        [Tooltip("Number of active chunks = (2 * draw_distance + 1)^2")]
        public int DrawDistance = 2;

        /// <summary>
        /// Dimensionen eines einzelnen Chunks (XYZ).
        /// </summary>
        [Tooltip("Chunk dimensions")] public int3 ChunkSize = 32 * new int3(1, 1, 1);

        /// <summary>
        /// Zusätzliche Lade-Distanz (im Speicher gehalten) über Sichtweite hinaus. Wird intern berechnet.
        /// </summary>
        [HideInInspector] [Tooltip("Number of chunks in memory = (draw_distance + 2)")]
        public int LoadDistance = 0;

        /// <summary>
        /// Distanz für Updates (Collider/Aktivitätsbereich). Muss &lt;= DrawDistance sein.
        /// </summary>
        [HideInInspector] [Tooltip("Should be less than equal to DrawDistance")]
        public int UpdateDistance = 0;
    }
}