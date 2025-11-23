using System;

namespace Runtime.Engine.Settings
{
    /// <summary>
    /// Renderer-bezogene Optionen für Chunk Darstellung.
    /// </summary>
    [Serializable]
    public class RendererSettings
    {
        /// <summary>
        /// Ob Chunk Mesh Schatten werfen darf.
        /// </summary>
        public bool CastShadows;
    }
}