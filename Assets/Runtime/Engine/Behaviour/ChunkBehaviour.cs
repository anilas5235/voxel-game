using Runtime.Engine.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Behaviour
{
    /// <summary>
    /// MonoBehaviour Repräsentation eines Chunks mit separatem Mesh für Rendering und Collider.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class ChunkBehaviour : MonoBehaviour
    {
        private MeshRenderer _renderer;
        [SerializeField] private MeshCollider _Collider;

        /// <summary>
        /// Mesh für visuelle Darstellung.
        /// </summary>
        public Mesh Mesh { get; private set; }
        /// <summary>
        /// Separates Mesh für Collider (nicht geteilt mit Render Mesh).
        /// </summary>
        public Mesh ColliderMesh { get; private set; }
        /// <summary>
        /// Zugriff auf zugehörigen MeshCollider.
        /// </summary>
        public MeshCollider Collider => _Collider;

        private void Awake()
        {
            Mesh = GetComponent<MeshFilter>().mesh;
            _renderer = GetComponent<MeshRenderer>();
            // Dedicated collider mesh (not shared with renderer)
            ColliderMesh = new Mesh { name = "ChunkCollider" };
        }

        /// <summary>
        /// Initialisiert Renderer-spezifische Optionen (z.B. Schattenwurf) laut Settings.
        /// </summary>
        public void Init(RendererSettings settings)
        {
            if (!settings.CastShadows) _renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}