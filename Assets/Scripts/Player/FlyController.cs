using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class FlyController : MonoBehaviour
    {
        private Vector2 _moveInput;
        private bool _sprintInput;
        private float _pitch; // vertical rotation
        private float _verticalInput; // for up/down movement

        [Range(1f, 100f)] public float moveSpeed = 20f;
        [SerializeField] private Camera targetCamera;
        private CharacterController _controller;
        
        private bool _jumpInput;
        private bool _crouchInput;

        private void OnEnable()
        {
            _controller = GetComponent<CharacterController>();
        }
        public void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
        }

        public void OnSprint(InputValue value)
        {
            _sprintInput = value.isPressed;
        }
        
        public void OnJump(InputValue value)
        {
            _jumpInput = value.isPressed;
        }
        
        public void OnCrouch(InputValue value)
        {
            _crouchInput = value.isPressed;
        }

        private void Update()
        {
            _verticalInput = 0f;
            if (_jumpInput) _verticalInput += 1f;
            if (_crouchInput) _verticalInput -= 1f;
            float speedModifier = 1f;
            if (_sprintInput) speedModifier = 2f; // Shift

            // Create movement vector relative to camera orientation
            Vector3 forward = targetCamera.transform.forward;
            Vector3 right = targetCamera.transform.right;

            // Normalize to prevent faster diagonal movement
            if (forward.magnitude > 0.01f) forward.Normalize();
            if (right.magnitude > 0.01f) right.Normalize();
            // Create movement direction
            Vector3 moveDirection = (forward * _moveInput.y + right * _moveInput.x);

            // Add vertical movement
            moveDirection.y = _verticalInput;
            
            _controller.Move(moveDirection.normalized * (moveSpeed * speedModifier * Time.deltaTime));
        }
    }
}