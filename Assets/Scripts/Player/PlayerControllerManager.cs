using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

namespace Player
{
    public class PlayerControllerManager : MonoBehaviour    
    {
        [SerializeField] private FlyController flyController;
        [SerializeField] private LookController lookController;
        [SerializeField] private WalkingController walkingController;
        
        private CharacterController _characterController;

        private enum PlayerMode { Walking, Flying }
        private PlayerMode _mode = PlayerMode.Walking;
        
        private InputAction _jumpInput;

        private bool _crouchPressed;

        public void OnDoubleJump(InputValue value)
        {
            if (_mode == PlayerMode.Walking && !value.isPressed)
            {
                SwitchToFly();
            }
        }

        public void OnCrouch(InputValue value)
        {
            _crouchPressed = value.isPressed;
        }
        
        private void OnEnable()
        {
            _characterController = GetComponent<CharacterController>();
            // Ensure initial mode state
            ApplyMode();
        }


        private void Update()
        {
            if (_mode == PlayerMode.Flying && _crouchPressed && _characterController.isGrounded)
            {
                SwitchToWalking();
            }
        }
        
        private void SwitchToFly()
        {
            if(_mode == PlayerMode.Flying) return;
            _mode = PlayerMode.Flying;
            ApplyMode();
        }

        private void SwitchToWalking()
        {
            if(_mode == PlayerMode.Walking) return;
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