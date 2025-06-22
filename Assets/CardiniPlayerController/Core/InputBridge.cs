// InputBridge.cs (Revised based on YOUR structure and project plan)
using UnityEngine;
using UnityEngine.InputSystem;
namespace Cardini.Motion
{
    public class InputBridge : MonoBehaviour
    {
        public struct ButtonInputState
        {
            public bool IsPressed { get; private set; }
            public bool IsHeld { get; private set; }
            public bool WasReleasedThisFrame { get; private set; } 

            private bool _isPressedInternalNextFrame;
            private bool _wasReleasedInternalNextFrame;

            public void SetPressed()
            {
                _isPressedInternalNextFrame = true;
                IsHeld = true;
            }

            public void SetReleased()
            {
                _wasReleasedInternalNextFrame = true;
                IsHeld = false;
            }

            public void ProcessFrameEnd()
            {
                IsPressed = _isPressedInternalNextFrame;
                WasReleasedThisFrame = _wasReleasedInternalNextFrame;

                _isPressedInternalNextFrame = false;
                _wasReleasedInternalNextFrame = false;
            }

            // Optional: If an action needs to be externally cancelled (e.g., ability interrupted by damage)
            public void ForceReset()
            {
                IsPressed = false;
                IsHeld = false;
                WasReleasedThisFrame = false;
                _isPressedInternalNextFrame = false;
                _wasReleasedInternalNextFrame = false;
            }
        }

        // --- Movement Inputs ---
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; } // Primarily for Cinemachine's Input Provider

        // --- Action Button States ---
        private ButtonInputState _jumpButton;
        private ButtonInputState _crouchButton;
        private ButtonInputState _sprintButton;
        private ButtonInputState _abilitySelectButton;
        private ButtonInputState _useEquippedAbilityButton;
        private ButtonInputState _cancelEquippedAbilityButton; // As per your project plan

        // Public read-only accessors (returning a copy of the struct is fine for reading state)
        public ButtonInputState Jump => _jumpButton;
        public ButtonInputState Crouch => _crouchButton;
        public ButtonInputState Sprint => _sprintButton;
        public ButtonInputState AbilitySelect => _abilitySelectButton; // Was AbilitySelect_Hold
        public ButtonInputState UseEquippedAbility => _useEquippedAbilityButton;
        public ButtonInputState CancelEquippedAbility => _cancelEquippedAbilityButton;


        // --- Unity Input System Callback Methods (to be called by PlayerInput component) ---
        public void OnMove(InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            // CinemachineInputProvider will likely handle this directly for camera.
            // Storing it here is useful if CardiniController needs direct look input (e.g., for aiming).
            LookInput = context.ReadValue<Vector2>();
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.started) _jumpButton.SetPressed(); // Corresponds to "KeyDown"
            else if (context.canceled) _jumpButton.SetReleased(); // Corresponds to "KeyUp"
            // IsHeld is managed internally by SetPressed/SetReleased
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {

            // This implies Crouch and CancelEquippedAbility might share an input or context.
            // For now, InputBridge will report the raw Crouch button state.
            // CardiniController will decide if a Crouch press means "Crouch" or "Cancel"
            // based on game state (e.g., if an ability is being aimed).
            // Anmol update: need to fix this, as it is not working properly
            if (context.started) _crouchButton.SetPressed();
            else if (context.canceled) _crouchButton.SetReleased();
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (context.started) _sprintButton.SetPressed();
            else if (context.canceled) _sprintButton.SetReleased();
        }

        // Callback for "AbilitySelect" Action from your screenshot
        public void OnAbilitySelect(InputAction.CallbackContext context)
        {
            if (context.started) _abilitySelectButton.SetPressed();
            else if (context.canceled) _abilitySelectButton.SetReleased();
        }

        // Callback for "UseEquippedAbility" Action from your screenshot
        public void OnUseEquippedAbility(InputAction.CallbackContext context)
        {
            if (context.started) _useEquippedAbilityButton.SetPressed();
            else if (context.canceled) _useEquippedAbilityButton.SetReleased();
        }

        // Callback for a potential "CancelEquippedAbility" Action
        // If this is a distinct button, you'll need to add it to your Input Actions asset.
        // If it shares the Crouch button, this specific callback isn't needed; CardiniController handles context.
        public void OnCancelEquippedAbility(InputAction.CallbackContext context)
        {
            if (context.started) _cancelEquippedAbilityButton.SetPressed();
            else if (context.canceled) _cancelEquippedAbilityButton.SetReleased();
        }


        // --- Frame End Processing ---
        public void ProcessButtonStatesFrameEnd()
        {
            _jumpButton.ProcessFrameEnd();
            _crouchButton.ProcessFrameEnd();
            _sprintButton.ProcessFrameEnd();
            _abilitySelectButton.ProcessFrameEnd();
            _useEquippedAbilityButton.ProcessFrameEnd();
            _cancelEquippedAbilityButton.ProcessFrameEnd(); // Process even if not directly mapped yet
        }
    }
}