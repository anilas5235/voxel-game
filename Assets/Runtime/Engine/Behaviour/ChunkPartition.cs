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
        private MeshRenderer _renderer;
        [SerializeField] private MeshCollider _Collider;

        private bool _shouldBeVisible;
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
        internal PartitionOcclusionData OcclusionData { get; set; }

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

#if UNITY_EDITOR
        private static readonly Color[] FaceColors =
            { Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta };

        private static readonly Vector3[] FaceNormals =
            { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        private void OnDrawGizmosSelected()
        {
            if (Mesh.vertexCount < 3) Gizmos.color = Color.grey;
            else if (!_Collider.sharedMesh) Gizmos.color = Color.magenta;
            else Gizmos.color = Color.green;
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

            // Draw occlusion data as colored lines between faces of the partition cube same color as the face
            Vector3 center = transform.position + PartitionSize.GetVector3() * 0.5f;
            float faceSize = PartitionSize.y * 0.9f;


            for (int i = 0; i < PartitionOcclusionData.AllDirections.Length; i++)
            {
                PartitionOcclusionData.OccDirection occDirection =
                    PartitionOcclusionData.AllDirections[i];
                Color color = FaceColors[i % FaceColors.Length];
                Vector3 normal = FaceNormals[i % FaceNormals.Length];

                DrawFace(normal, color);

                for (int j = 0; j < PartitionOcclusionData.AllDirections.Length; j++)
                {
                    PartitionOcclusionData.OccDirection otherDirection = PartitionOcclusionData.AllDirections[j];

                    if (occDirection == otherDirection) continue;
                    if (!OcclusionData.ArePartitionFacesConnected(occDirection, otherDirection)) continue;

                    Vector3 otherNormal = FaceNormals[j % FaceNormals.Length];

                    Gizmos.color = color;
                    Vector3 start = center + normal * (PartitionSize.x * 0.5f);
                    Vector3 end = center + otherNormal * (PartitionSize.x * 0.5f);
                    Vector3 mid = (start + end) * 0.5f;
                    
                    Gizmos.DrawLine(start, mid);
                }
            }
            return;

            void DrawFace(Vector3 normal, Color color)
            {
                Gizmos.color = color;
                Vector3 faceCenter = center + normal * (PartitionSize.x * 0.5f);
                // Determine size based on normal
                var size = normal switch
                {
                    _ when normal == Vector3.right || normal == Vector3.left => new Vector3(0.1f, faceSize, faceSize),
                    _ when normal == Vector3.up || normal == Vector3.down => new Vector3(faceSize, 0.1f, faceSize),
                    _ when normal == Vector3.forward || normal == Vector3.back => new Vector3(faceSize, faceSize, 0.1f),
                    _ => Vector3.zero
                };
                Gizmos.DrawWireCube(faceCenter, size);
            }
        }
#endif
    }
}