using UnityEngine;
using UnityEngine.InputSystem;

namespace Test
{
    public class CamController : MonoBehaviour
    {
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private float _pitch; // vertical rotation
        private float _verticalInput; // for up/down movement

        [Range(1f, 100f)] public float moveSpeed = 5f;
        [Range(1f, 100f)] public float lookSpeed = 10f;
        private Camera _camera;

        private void OnEnable()
        {
            _camera = Camera.main;
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
        }

        public void OnLook(InputValue value)
        {
            _lookInput = value.Get<Vector2>();
        }

        private void Update()
        {
            // Handle vertical movement with E (up) and Q (down) keys
            _verticalInput = 0f;
            if (Keyboard.current.spaceKey.isPressed) _verticalInput += 1f;
            if (Keyboard.current.leftCtrlKey.isPressed) _verticalInput -= 1f;
            float speedModifier = 1f;
            if (Keyboard.current.shiftKey.isPressed) speedModifier = 2f; // Shift

            // Create movement vector relative to camera orientation
            Vector3 forward = _camera.transform.forward;
            Vector3 right = _camera.transform.right;

            // Zero out y component for forward/right movement
            forward.y = 0;
            right.y = 0;

            // Normalize to prevent faster diagonal movement
            if (forward.magnitude > 0.01f) forward.Normalize();
            if (right.magnitude > 0.01f) right.Normalize();

            // Create movement direction
            Vector3 moveDirection = (forward * _moveInput.y + right * _moveInput.x);

            // Add vertical movement
            moveDirection += Vector3.up * _verticalInput;

            // Apply movement
            if (moveDirection != Vector3.zero)
            {
                _camera.transform.Translate(moveDirection.normalized * (moveSpeed * speedModifier * Time.deltaTime),
                    Space.World);
            }

            // Rotate the camera with mouse or right stick
            if (!(_lookInput.sqrMagnitude > 0.0001f)) return;
            // Horizontal rotation (yaw)
            _camera.transform.Rotate(Vector3.up, _lookInput.x * lookSpeed * Time.deltaTime, Space.World);

            // Vertical rotation (pitch) with clamping
            _pitch -= _lookInput.y * lookSpeed * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch, -80f, 80f);

            Vector3 currentEuler = _camera.transform.eulerAngles;
            currentEuler.x = _pitch;
            _camera.transform.eulerAngles = new Vector3(_pitch, currentEuler.y, 0f);
        }
    }
}