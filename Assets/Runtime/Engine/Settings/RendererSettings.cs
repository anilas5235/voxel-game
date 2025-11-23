using System;

namespace Runtime.Engine.Settings
{
    /// <summary>
    /// Renderer-related options for chunk presentation.
    /// </summary>
    [Serializable]
    public class RendererSettings
    {
        /// <summary>
        /// Whether chunk meshes cast shadows.
        /// </summary>
        public bool CastShadows;
    }
}