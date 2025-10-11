using System;
using Runtime.Engine.Data;
using Runtime.Engine.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Test
{
    public class VoxelEditor: MonoBehaviour
    {
        private Camera _camera;
        private bool interaction;

        private void OnEnable()
        {
            _camera = Camera.main;
        }
        
        public void OnAttack(InputValue value)
        {
            interaction = value.isPressed;
        }

        private void FixedUpdate()
        {
            if (!interaction) return;
            bool res = Physics.Raycast(_camera.transform.position, _camera.transform.forward, out RaycastHit hitInfo,
                5f);
            if (!res) return;
            
            Vector3 worldPos = hitInfo.point + _camera.transform.forward * .001f;
            Vector3Int voxelWorldPos = Vector3Int.FloorToInt(worldPos);
            VoxelWorld.Instance.SetVoxel(Block.Air, voxelWorldPos);
            Debug.DrawLine(_camera.transform.position, hitInfo.point, Color.red, 30f);
        }
    }
}