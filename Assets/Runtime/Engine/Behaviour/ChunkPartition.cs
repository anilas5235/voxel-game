using Runtime.Engine.Jobs.Meshing;
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
#if UNITY_EDITOR
        public static bool ShowPartitionGizmos = false;
#endif
        private MeshRenderer _renderer;
        [SerializeField] private MeshCollider _Collider;

        private bool _shouldBeVisible = true;
        private bool _initialized;

        internal bool ShouldBeVisible
        {
            get => _shouldBeVisible;
            set
            {
                if (_shouldBeVisible == value) return;
                _shouldBeVisible = value;
                UpdateRenderStatus();
            }
        }

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
            _initialized = true;
            UpdateRenderStatus();
        }

        public void UpdateRenderStatus()
        {
            if (!_initialized) return;
            bool isRendered = Mesh && Mesh.vertexCount > 2 && ShouldBeVisible;
            _renderer.enabled = isRendered;
        }
        
        public void ApplyColliderMesh()
        {
            Collider.sharedMesh = ColliderMesh;
        }

        public void Clear()
        {
            Mesh.Clear();
            ColliderMesh.Clear();
            Collider.sharedMesh = null;
        }
        
        public bool HasValidMesh() => Mesh && Mesh.vertexCount > 2;

#if UNITY_EDITOR
        private static readonly Color[] FaceColors =
            { Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta };

        private static readonly Vector3[] FaceNormals =
            { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        private void OnDrawGizmosSelected()
        {
            if(!ShowPartitionGizmos) return;
            
            Gizmos.color = Color.green;
            
            if (Mesh.vertexCount < 3) Gizmos.color = Color.grey;
            else if (!_Collider.sharedMesh) Gizmos.color = Color.magenta;
            
            Gizmos.DrawWireCube(
                transform.position + PartitionSize.GetVector3() * 0.5f,
                PartitionSize.GetVector3() * 0.95f
            );

            // Draw partition ID
            Gizmos.color = Color.white;
            UnityEditor.Handles.Label(
                transform.position + PartitionSize.GetVector3() * 0.5f,
                $"Id:{PartitionId}",
                new GUIStyle()
                {
                    fontSize = 20,
                    normal = new GUIStyleState() { textColor = Color.white }
                }
            );
        }
#endif
    }
}