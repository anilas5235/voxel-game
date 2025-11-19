using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class WalkingController : MonoBehaviour
    {
        private Vector2 _moveInput;
        private bool _sprintInput;

        [Range(1f, 100f)] public float moveSpeed = 20f;
        [Range(1f, 20f)] public float jumpHeight = 1.5f;
        public float gravity = -9.81f;
        private CharacterController _controller;
        [SerializeField] private Camera targetCamera;

        private bool _jumpInput;

        private Vector3 _velocity;

        public bool isGrounded;

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

        private void Update()
        {
            float speedModifier = _sprintInput ? 1.5f : 1.0f;
            isGrounded = _controller.isGrounded;

            // Create movement vector relative to camera orientation
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // Normalize to prevent faster diagonal movement
            if (forward.magnitude > 0.01f) forward.Normalize();
            if (right.magnitude > 0.01f) right.Normalize();
            // Create movement direction
            Vector3 moveDirection = forward * _moveInput.y + right * _moveInput.x;

            _controller.Move(moveDirection.normalized * (moveSpeed * speedModifier * Time.deltaTime));

            // Handle jumping
            if (isGrounded)
            {
                // Apply gravity
                if (_velocity.y < 0)
                {
                    _velocity.y = -2f; // small downward force to keep grounded
                }

                if (_jumpInput)
                {
                    _velocity.y = Mathf.Sqrt(2f * -gravity * jumpHeight);
                }
            }

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }
    }
}