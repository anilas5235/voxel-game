using Runtime.Engine.Settings;
using Runtime.Engine.Utils.Extensions;
using UnityEngine;
using UnityEngine.Rendering;
using static Runtime.Engine.Utils.VoxelConstants;

namespace Runtime.Engine.Behaviour
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class ChunkPartition : MonoBehaviour
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

        public short PartitionId { get; private set; }

        private void Awake()
        {
            Mesh = GetComponent<MeshFilter>().mesh;
            _renderer = GetComponent<MeshRenderer>();
            ColliderMesh = new Mesh { name = "PCollider" };
        }

        public void Init(RendererSettings settings, int pId)
        {
            PartitionId = (short)pId;
            transform.localPosition = new Vector3(0, PartitionHeight * pId, 0);
            _renderer.shadowCastingMode = settings.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            _renderer.allowOcclusionWhenDynamic = false;
            UpdateRenderStatus();
        }

        public void UpdateRenderStatus()
        {
            gameObject.SetActive(Mesh.vertexCount > 2);
        }

        public void Clear()
        {
            Mesh.Clear();
            ColliderMesh.Clear();
            Collider.sharedMesh = null;
            UpdateRenderStatus();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Mesh.vertexCount < 3) Gizmos.color = Color.grey;
            else if (_Collider.sharedMesh == null) Gizmos.color = Color.magenta;
            else Gizmos.color = Color.green;
            Gizmos.DrawWireCube(
                transform.position + PartitionSize.GetVector3() * 0.5f,
                PartitionSize.GetVector3() * 0.95f
            );
        }
#endif
    }
}