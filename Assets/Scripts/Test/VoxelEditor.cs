using Runtime.Engine.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Test
{
    public class VoxelEditor : MonoBehaviour
    {
        private Camera _camera;
        public bool destruct;
        public bool place;
        public ushort voxelId = 1;

        private void OnEnable()
        {
            _camera = Camera.main;
        }

        public void OnAttack(InputValue value)
        {
            destruct = value.isPressed;
        }

        public void OnSelectVoxel(InputValue value)
        {
            if (!value.isPressed) return;
            if (!GetLookAtVoxelPos(out Vector3Int voxelWorldPos)) return;
            ushort voxId = VoxelWorld.Instance.GetVoxel(voxelWorldPos);
            if (voxId > 0) voxelId = VoxelWorld.Instance.GetVoxel(voxelWorldPos);
        }

        public void OnPlaceVoxel(InputValue value)
        {
            place = value.isPressed;
        }

        private void FixedUpdate()
        {
            if (destruct)
            {
                if (!GetLookAtVoxelPos(out Vector3Int voxelWorldPos)) return;
                VoxelWorld.Instance.SetVoxel(0, voxelWorldPos);
            }
            else if (place)
            {
                if (!GetLookAtVoxelPos(out Vector3Int voxelWorldPos, true)) return;
                VoxelWorld.Instance.SetVoxel(voxelId, voxelWorldPos);
            }
        }

        private bool GetLookAtVoxelPos(out Vector3Int voxelWorldPos, bool placeMode = false)
        {
            voxelWorldPos = Vector3Int.zero;
            bool res = Physics.Raycast(_camera.transform.position, _camera.transform.forward, out RaycastHit hitInfo,
                5f);
            if (!res) return false;

            Vector3 worldPos = hitInfo.point + _camera.transform.forward * (.001f * (placeMode ? -1 : 1));
            voxelWorldPos = Vector3Int.FloorToInt(worldPos);
            return true;
        }
    }
}