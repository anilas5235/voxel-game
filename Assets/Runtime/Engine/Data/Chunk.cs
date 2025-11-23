using System;
using Runtime.Engine.Utils.Collections;
using Runtime.Engine.Utils.Extensions;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Runtime.Engine.Data
{
    /// <summary>
    /// Speicher-Repräsentation eines Chunks mit komprimierten Voxel-Daten (Run-Length basiertes Layout).
    /// Enthält Dirty-Flag für Änderungs-Tracking und einfache API für Lesen/Schreiben.
    /// </summary>
    [BurstCompile]
    public struct Chunk : IDisposable
    {
        /// <summary>
        /// Welt-Position (Chunk Ursprungskoordinate).
        /// </summary>
        public int3 Position { get; }
        /// <summary>
        /// Flag ob seit letztem Mesh-Build Änderungen vorgenommen wurden.
        /// </summary>
        public bool Dirty { get; private set; }

        private readonly int3 _chunkSize;
        private UnsafeIntervalList _data;

        /// <summary>
        /// Erstellt neuen Chunk mit Position und Größe, initialisiert Datenstruktur.
        /// </summary>
        public Chunk(int3 position, int3 chunkSize)
        {
            Dirty = false;
            Position = position;
            _chunkSize = chunkSize;
            _data = new UnsafeIntervalList(128, Allocator.Persistent);
        }

        /// <summary>
        /// Fügt eine Serie von Voxeln gleichen Typs hinzu (während Initialisierung / Generierung).
        /// </summary>
        public void AddVoxels(ushort voxelId, int count)
        {
            _data.AddInterval(voxelId, count);
        }

        /// <summary>
        /// Setzt Voxel per Einzelkoordinaten. Markiert Dirty bei Änderung.
        /// </summary>
        public bool SetVoxel(int x, int y, int z, ushort block)
        {
            bool result = _data.Set(_chunkSize.Flatten(x, y, z), block);
            if (result) Dirty = true;
            return result;
        }

        /// <summary>
        /// Setzt Voxel per int3 Position. Markiert Dirty bei Änderung.
        /// </summary>
        public bool SetVoxel(int3 pos, ushort voxelId)
        {
            bool result = _data.Set(_chunkSize.Flatten(pos), voxelId);
            if (result) Dirty = true;
            return result;
        }

        /// <summary>
        /// Liest Voxel an Koordinaten.
        /// </summary>
        public ushort GetVoxel(int x, int y, int z)
        {
            return _data.Get(_chunkSize.Flatten(x, y, z));
        }

        /// <summary>
        /// Liest Voxel an int3 Position.
        /// </summary>
        public ushort GetVoxel(int3 pos)
        {
            return GetVoxel(pos.x, pos.y, pos.z);
        }

        /// <summary>
        /// Gibt native Ressourcen frei.
        /// </summary>
        public void Dispose()
        {
            _data.Dispose();
        }

        /// <summary>
        /// Debug Darstellung inkl. Dirty Status und komprimierter Daten.
        /// </summary>
        public override string ToString()
        {
            return $"Pos : {Position}, Dirty : {Dirty}, Data : {_data.ToString()}";
        }
    }
}