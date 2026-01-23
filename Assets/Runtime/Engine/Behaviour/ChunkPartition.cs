using Runtime.Engine.Settings;
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
        
        public short PartitionId{get; private set;}

        private void Awake()
        {
            Mesh = GetComponent<MeshFilter>().mesh;
            _renderer = GetComponent<MeshRenderer>();
            ColliderMesh = new Mesh { name = "ChunkCollider" };
        }
        
        public void Init(RendererSettings settings, int pId)
        {
            PartitionId = (short)pId;
            transform.localPosition = new Vector3(0, PartitionHeight * pId, 0);
            if (!settings.CastShadows) _renderer.shadowCastingMode = ShadowCastingMode.Off;
        } 
    }
}