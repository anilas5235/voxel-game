using Runtime.Engine.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Behaviour
{
    /// <summary>
    /// MonoBehaviour representation of a chunk with a dedicated render mesh and collider mesh.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class ChunkBehaviour : MonoBehaviour
    {
        private MeshRenderer _renderer;
        [SerializeField] private MeshCollider _Collider;
        /// <summary>
        /// Mesh used for visual rendering.
        /// </summary>
        public Mesh Mesh { get; private set; }
        /// <summary>
        /// Dedicated mesh for collider (not shared with render mesh).
        /// </summary>
        public Mesh ColliderMesh { get; private set; }
        /// <summary>
        /// Access to the underlying MeshCollider.
        /// </summary>
        public MeshCollider Collider => _Collider;

        private void Awake()
        {
            Mesh = GetComponent<MeshFilter>().mesh;
            _renderer = GetComponent<MeshRenderer>();
            ColliderMesh = new Mesh { name = "ChunkCollider" };
        }

        /// <summary>
        /// Initializes renderer-specific options (e.g. shadow casting) from settings.
        /// </summary>
        public void Init(RendererSettings settings)
        {
            if (!settings.CastShadows) _renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}