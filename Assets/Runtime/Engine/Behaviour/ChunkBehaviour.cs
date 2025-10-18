using Runtime.Engine.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace Runtime.Engine.Behaviour {

    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class ChunkBehaviour : MonoBehaviour {

        private MeshRenderer _Renderer;
        [SerializeField] private MeshCollider _Collider;

        public Mesh Mesh { get; private set; }
        public Mesh ColliderMesh { get; private set; }
        public MeshCollider Collider => _Collider;

        private void Awake() {
            Mesh = GetComponent<MeshFilter>().mesh;
            _Renderer = GetComponent<MeshRenderer>();
            // Dedicated collider mesh (not shared with renderer)
            ColliderMesh = new Mesh { name = "ChunkCollider" };
        }

        public void Init(RendererSettings settings) {
            if (!settings.CastShadows) _Renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

}