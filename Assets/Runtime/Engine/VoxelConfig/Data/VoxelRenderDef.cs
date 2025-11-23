using Unity.Burst;

namespace Runtime.Engine.VoxelConfig.Data
{
    /// <summary>
    /// Render-Definition eines Voxels (Textur-Slots für alle Seiten, Layer/Zusatzinformationen und Kollisions-/Rendering Flags).
    /// </summary>
    [BurstCompile]
    public struct VoxelRenderDef
    {
        /// <summary>Mesh Layer (Solid/Transparent/Air).</summary>
        public MeshLayer MeshLayer;
        /// <summary>Ob alle Faces immer gerendert werden sollen (auch wenn verdeckt).</summary>
        public bool AlwaysRenderAllFaces;
        /// <summary>Semantischer Voxel-Typ (z.B. Flora, Liquid).</summary>
        public VoxelType VoxelType;
        /// <summary>Distanz für Depth-Fade bei Transparenten Voxeln.</summary>
        public float DepthFadeDistance;
        /// <summary>Ob der Voxel am Physik-Collider teilnimmt.</summary>
        public bool Collision;
        /// <summary>Texturindex Oberseite.</summary>
        public int TexUp;
        /// <summary>Texturindex Unterseite.</summary>
        public int TexDown;
        /// <summary>Texturindex Links.</summary>
        public int TexLeft;
        /// <summary>Texturindex Rechts.</summary>
        public int TexRight;
        /// <summary>Texturindex Front.</summary>
        public int TexFront;
        /// <summary>Texturindex Rückseite.</summary>
        public int TexBack;

        /// <summary>
        /// Liefert passenden Textur-Slot für gegebene Richtung.
        /// </summary>
        [BurstCompile]
        public readonly int GetTextureId(Direction dir)
        {
            return dir switch
            {
                Direction.Up => TexUp,
                Direction.Down => TexDown,
                Direction.Left => TexLeft,
                Direction.Right => TexRight,
                Direction.Forward => TexFront,
                Direction.Backward => TexBack,
                _ => -1
            };
        }
    }
}