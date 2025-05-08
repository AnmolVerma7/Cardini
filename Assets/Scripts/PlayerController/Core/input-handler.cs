using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardini.Motion // Added namespace
{
    /// <summary>
    /// Processes player input using Unity's Input System and provides simple boolean flags.
    /// Resets press/release flags each frame in LateUpdate. Attached to the main Player object.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        #region Public Properties (Input State Flags)
        // --- Public Properties ---
        [Header("Movement Input")]
        public float HorizontalInput { get; private set; }
        public float VerticalInput { get; private set; }

        [Header("Action Input States")]
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool JumpReleased { get; private set; }

        public bool CrouchPressed { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool CrouchReleased { get; private set; }
        public bool CrouchTogglePressed { get; private set; } 

        public bool SprintPressed { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool SprintReleased { get; private set; }
        public bool SprintTogglePressed { get; private set; }

        public bool WallRunPressed { get; private set; } // Check if actually used by WallRunModule
        public bool WallRunHeld { get; private set; }
        public bool WallRunReleased { get; private set; }

        [Header("Teleport Input States")]
        public bool TeleportAimHeld { get; private set; }
        public bool TeleportExecutePressed { get; private set; }
        public bool TeleportCancelPressed { get; private set; }
        #endregion

        #region Input System Callbacks
        // --- Input System Callbacks ---
        // Methods called by the PlayerInput component based on Action names

        public void OnMove(InputAction.CallbackContext context)
        {
            if (context.performed) { Vector2 move = context.ReadValue<Vector2>(); HorizontalInput = move.x; VerticalInput = move.y; }
            else if (context.canceled) { HorizontalInput = 0; VerticalInput = 0; }
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.started) { JumpPressed = true; JumpHeld = true; }
            else if (context.canceled) { JumpReleased = true; JumpHeld = false; }
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                CrouchPressed = true;
                CrouchHeld = true;
                CrouchTogglePressed = true; // Set toggle flag ONCE on press
            }
            else if (context.canceled)
            {
                CrouchReleased = true;
                CrouchHeld = false;
                // Toggle flag is NOT reset on release
            }
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                SprintPressed = true;
                SprintHeld = true;
                SprintTogglePressed = true; // ADD THIS
            }
            else if (context.canceled)
            {
                SprintReleased = true;
                SprintHeld = false;
                // SprintTogglePressed = false; // DO NOT reset here
            }
        }

        public void OnWallRun(InputAction.CallbackContext context)
        {
            // Assuming WallRun might also be a hold action? Adjust if it's just a press.
            if (context.started) { WallRunPressed = true; WallRunHeld = true; }
            else if (context.canceled) { WallRunReleased = true; WallRunHeld = false; }
        }

        public void OnTeleportAim(InputAction.CallbackContext context)
        {
            // Hold actions use performed/canceled
            if (context.performed) { TeleportAimHeld = true; }
            else if (context.canceled) { TeleportAimHeld = false; }
        }

        public void OnTeleportExecute(InputAction.CallbackContext context)
        {
            // Press actions typically use performed or started
            if (context.performed) { TeleportExecutePressed = true; }
        }

        public void OnTeleportCancel(InputAction.CallbackContext context)
        {
            if (context.performed) { TeleportCancelPressed = true; }
        }
        #endregion

        #region Frame Cleanup
        // --- Frame Cleanup ---
        private void LateUpdate()
        {
            // Reset all single-frame "Pressed" and "Released" flags after all Updates ran.
            // "Held" flags persist until explicitly set false by the 'canceled' phase callback.
            JumpPressed = false; JumpReleased = false;
            CrouchPressed = false; CrouchReleased = false;
            SprintPressed = false; SprintReleased = false;
            SprintTogglePressed = false;
            WallRunPressed = false; WallRunReleased = false;
            TeleportExecutePressed = false; TeleportCancelPressed = false;
            CrouchTogglePressed = false;
        }
        #endregion
    }
}