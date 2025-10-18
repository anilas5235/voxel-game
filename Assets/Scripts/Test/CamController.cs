using UnityEngine;
using UnityEngine.InputSystem;

namespace Test
{
    public class CamController : MonoBehaviour
    {
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _sprintInput;
        private float _pitch; // vertical rotation
        private float _verticalInput; // for up/down movement

        [Range(1f, 100f)] public float moveSpeed = 5f;
        [Range(1f, 100f)] public float lookSpeed = 10f;
        [Range(1f,89f)] public float maxLookAngle = 80f;
        private Camera _camera;
        public Rigidbody rigidbodyCam;

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

        public void OnSprint(InputValue value)
        {
            _sprintInput = value.isPressed;
        }

        private void Update()
        {
            // Rotate the camera with mouse or right stick
            if (!(_lookInput.sqrMagnitude > 0.0001f)) return;
            // Horizontal rotation (yaw)
            _camera.transform.Rotate(Vector3.up, _lookInput.x * lookSpeed * Time.fixedDeltaTime, Space.World);

            // Vertical rotation (pitch) with clamping
            _pitch -= _lookInput.y * lookSpeed * Time.fixedDeltaTime;
            _pitch = Mathf.Clamp(_pitch, -maxLookAngle, maxLookAngle);

            Vector3 currentEuler = _camera.transform.eulerAngles;
            currentEuler.x = _pitch;
            _camera.transform.eulerAngles = new Vector3(_pitch, currentEuler.y, 0f);
        }

        private void FixedUpdate()
        {
            rigidbodyCam.angularVelocity = Vector3.zero;
            // Handle vertical movement with E (up) and Q (down) keys
            _verticalInput = 0f;
            if (Keyboard.current.spaceKey.isPressed) _verticalInput += 1f;
            if (Keyboard.current.leftCtrlKey.isPressed) _verticalInput -= 1f;
            float speedModifier = 1f;
            if (_sprintInput) speedModifier = 2f; // Shift

            // Create movement vector relative to camera orientation
            Vector3 forward = _camera.transform.forward;
            Vector3 right = _camera.transform.right;

            // Normalize to prevent faster diagonal movement
            if (forward.magnitude > 0.01f) forward.Normalize();
            if (right.magnitude > 0.01f) right.Normalize();
            // Create movement direction
            Vector3 moveDirection = (forward * _moveInput.y + right * _moveInput.x);

            // Add vertical movement
            moveDirection.y = _verticalInput;
            // Apply movement using Rigidbody for collision
            rigidbodyCam.linearVelocity = moveDirection.normalized * (moveSpeed * speedModifier);
        }
    }
}