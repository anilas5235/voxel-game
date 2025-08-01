using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Test
{
    public class CamController : MonoBehaviour
    {
        private Vector2 moveInput;
        private Vector2 lookInput;
        private float pitch = 0f; // vertical rotation
        
        [Range(1f,100f)] public float moveSpeed = 5f;
        [Range(1f,100f)] public float lookSpeed = 10f;
        private Camera _camera;

        private void Start()
        {
            _camera = Camera.main;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void OnMove(InputValue value)
        {
            moveInput = value.Get<Vector2>();
        }

        public void OnLook(InputValue value)
        {
            lookInput = value.Get<Vector2>();
        }

        private void Update()
        {
            // Move the camera with WASD or left stick
            Vector3 moveDirection = new(moveInput.x, 0, moveInput.y);

            if (moveDirection != Vector3.zero)
            {
                _camera.transform.Translate(moveDirection.normalized * (moveSpeed * Time.deltaTime), Space.World);
            }

            // Rotate the camera with mouse or right stick
            if (!(lookInput.sqrMagnitude > 0.0001f)) return;
            // Horizontal rotation (yaw)
            _camera.transform.Rotate(Vector3.up, lookInput.x * lookSpeed * Time.deltaTime, Space.World);

            // Vertical rotation (pitch) with clamping
            pitch -= lookInput.y * lookSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -80f, 80f);

            Vector3 currentEuler = _camera.transform.eulerAngles;
            currentEuler.x = pitch;
            _camera.transform.eulerAngles = new Vector3(pitch, currentEuler.y, 0f);
        }
    }
}