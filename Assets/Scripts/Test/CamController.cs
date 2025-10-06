using UnityEngine;
using UnityEngine.InputSystem;
using Voxels;

namespace Test
{
    public class CamController : MonoBehaviour
    {
        private Vector2 moveInput;
        private Vector2 lookInput;
        private float pitch = 0f; // vertical rotation
        private float verticalInput = 0f; // for up/down movement

        [Range(1f, 100f)] public float moveSpeed = 5f;
        [Range(1f, 100f)] public float lookSpeed = 10f;
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

        public void OnAttack(InputValue value)
        {
            bool interaction = value.isPressed;
            if (!interaction) return;
            bool res = Physics.Raycast(_camera.transform.position, _camera.transform.forward, out RaycastHit hitInfo,
                5f);
            if (res)
            {
                Vector3 worldPos = hitInfo.point + _camera.transform.forward * .001f;
                Vector3Int voxelWorldPos = Vector3Int.FloorToInt(worldPos);
                //VoxelWorld.Instance.SetVoxelFromWorldVoxPos(voxelWorldPos, 0);
                Debug.DrawLine(_camera.transform.position, hitInfo.point, Color.red, 30f);
            }
        }

        private void Update()
        {
            // Handle vertical movement with E (up) and Q (down) keys
            verticalInput = 0f;
            if (Keyboard.current.spaceKey.isPressed) verticalInput += 1f;
            if (Keyboard.current.leftCtrlKey.isPressed) verticalInput -= 1f;
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
            Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x);

            // Add vertical movement
            moveDirection += Vector3.up * verticalInput;

            // Apply movement
            if (moveDirection != Vector3.zero)
            {
                _camera.transform.Translate(moveDirection.normalized * (moveSpeed * speedModifier * Time.deltaTime),
                    Space.World);
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