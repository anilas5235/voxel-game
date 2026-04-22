using System;
using UnityEngine.Rendering;

namespace Engine.Scripts.Settings
{
    /// <summary>
    ///     Renderer-related options for chunk presentation.
    /// </summary>
    [Serializable]
    public class RendererSettings
    {
        /// <summary>
        ///     Whether chunk meshes cast shadows.
        /// </summary>
        public ShadowCastingMode shadows;
    }
}