using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    /// <summary>
    /// Manages switching between walking and flying controllers based on input
    /// and current grounded state.
    /// </summary>
    public class PlayerControllerManager : MonoBehaviour
    {
        /// <summary>
        /// Fly controller used when the player is in flying mode.
        /// </summary>
        [SerializeField] private FlyController flyController;

        /// <summary>
        /// Look controller responsible for camera rotation.
        /// </summary>
        [SerializeField] private LookController lookController;

        /// <summary>
        /// Walking controller used when the player is in walking mode.
        /// </summary>
        [SerializeField] private WalkingController walkingController;

        private CharacterController _characterController;

        private enum PlayerMode
        {
            Walking,
            Flying
        }

        private PlayerMode _mode = PlayerMode.Walking;

        private InputAction _jumpInput;

        private bool _crouchPressed;

        private void OnEnable()
        {
            _characterController = GetComponent<CharacterController>();
            ApplyMode();
        }

        private void Update()
        {
            if (_mode == PlayerMode.Flying && _crouchPressed && _characterController.isGrounded)
            {
                SwitchToWalking();
            }
        }

        /// <summary>
        /// Input System callback for the "double jump" action which toggles flying mode
        /// when currently walking.
        /// </summary>
        /// <param name="value">Button state for the double jump action.</param>
        public void OnDoubleJump(InputValue value)
        {
            if (_mode == PlayerMode.Walking && !value.isPressed)
            {
                SwitchToFly();
            }
        }

        /// <summary>
        /// Input System callback for crouch, used to return to walking mode when grounded in fly mode.
        /// </summary>
        /// <param name="value">Button state for crouch.</param>
        public void OnCrouch(InputValue value)
        {
            _crouchPressed = value.isPressed;
        }

        private void SwitchToFly()
        {
            if (_mode == PlayerMode.Flying) return;
            _mode = PlayerMode.Flying;
            ApplyMode();
        }

        private void SwitchToWalking()
        {
            if (_mode == PlayerMode.Walking) return;
            _mode = PlayerMode.Walking;
            ApplyMode();
        }

        private void ApplyMode()
        {
            if (flyController) flyController.enabled = _mode == PlayerMode.Flying;
            if (walkingController) walkingController.enabled = _mode == PlayerMode.Walking;
        }
    }
}