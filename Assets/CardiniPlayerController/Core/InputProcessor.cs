using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    /// <summary>
    /// Processes all input logic including timing, toggle mechanics, and slide state management.
    /// Handles complex input interactions like slide initiation/cancellation and mutual exclusion rules.
    /// </summary>
    public class InputProcessor
    {
        private BaseLocomotionSettingsSO _settings;
        private KinematicCharacterMotor _motor;

        public InputProcessor(BaseLocomotionSettingsSO settings, KinematicCharacterMotor motor)
        {
            _settings = settings;
            _motor = motor;
        }

        /// <summary>
        /// Main processing method - processes all input states and timing
        /// </summary>
        public void ProcessInputs(InputBridge inputBridge, ref InputContext context, CharacterMovementState currentMovementState, float deltaTime)
        {
            UpdateRawInputStates(inputBridge, ref context);
            UpdateInputTiming(ref context, deltaTime);
            ProcessToggleLogic(ref context);
            ProcessSlideLogic(ref context, currentMovementState);
            ApplyMutualExclusionRules(ref context);
            ValidateStates(ref context, currentMovementState);
        }

        #region Private Methods

        private void UpdateRawInputStates(InputBridge inputBridge, ref InputContext context)
        {
            // Update jump states
            context.Jump.Pressed = inputBridge.Jump.IsPressed;
            context.Jump.Held = inputBridge.Jump.IsHeld;
            context.Jump.Released = inputBridge.Jump.WasReleasedThisFrame;

            // Update sprint states
            context.Sprint.Pressed = inputBridge.Sprint.IsPressed;
            context.Sprint.Held = inputBridge.Sprint.IsHeld;
            context.Sprint.Released = inputBridge.Sprint.WasReleasedThisFrame;

            // Update crouch states
            context.Crouch.Pressed = inputBridge.Crouch.IsPressed;
            context.Crouch.Held = inputBridge.Crouch.IsHeld;
            context.Crouch.Released = inputBridge.Crouch.WasReleasedThisFrame;
        }

        private void UpdateInputTiming(ref InputContext context, float deltaTime)
        {
            context.Jump.UpdateTiming(deltaTime);
            context.Sprint.UpdateTiming(deltaTime);
            context.Crouch.UpdateTiming(deltaTime);
            context.Slide.UpdateTiming(deltaTime);
        }

        private void ProcessToggleLogic(ref InputContext context)
        {
            // Sprint toggle
            if (_settings.UseToggleSprint && context.Sprint.Pressed)
            {
                context.SprintToggleActive = !context.SprintToggleActive;
            }

            // Crouch toggle (but not during slide cancel)
            bool isSlideCancel = context.Slide.IsSlideActive && context.Crouch.Pressed;

            if (_settings.UseToggleCrouch && context.Crouch.Pressed && !isSlideCancel)
            {
                context.CrouchToggleActive = !context.CrouchToggleActive;
            }
        }

        private void ProcessSlideLogic(ref InputContext context, CharacterMovementState currentMovementState)
        {
            context.Slide.IsSlideActive = (currentMovementState == CharacterMovementState.Sliding);

            if (context.Slide.IsSlideActive)
            {
                ProcessSlideCancellation(ref context);
            }
            else
            {
                ProcessSlideInitiation(ref context, currentMovementState);
            }
        }

        private void ProcessSlideCancellation(ref InputContext context)
        {
            bool shouldCancel = false;

            if (_settings.UseToggleCrouch)
            {
                // Toggle mode: Cancel on crouch press
                shouldCancel = context.Crouch.Pressed;
            }
            else
            {
                // Hold mode: Cancel on crouch release
                shouldCancel = context.Crouch.Released;
            }

            if (shouldCancel)
            {
                context.Slide.CancelRequested = true;
                context.Slide.InitiationRequested = false;
            }
        }

        private void ProcessSlideInitiation(ref InputContext context, CharacterMovementState currentMovementState)
        {
            // Clear stale cancel requests when not sliding
            context.Slide.CancelRequested = false;

            // Check initiation conditions
            bool canInitiateSlide = CanInitiateSlide(context, currentMovementState);
            context.Slide.CanInitiateSlide = canInitiateSlide;

            if (canInitiateSlide && context.Crouch.Pressed)
            {
                // Check minimum hold time requirement
                if (context.Crouch.HoldDuration >= context.Slide.MinHoldTimeForSlide || 
                    context.Slide.MinHoldTimeForSlide <= 0f)
                {
                    context.Slide.InitiationRequested = true;
                    context.Slide.TimeSinceSlideRequest = 0f;
                }
            }
        }

        private bool CanInitiateSlide(InputContext context, CharacterMovementState currentMovementState)
        {
            // Must be grounded and stable
            if (!_motor.GroundingStatus.IsStableOnGround)
                return false;

            // Must not already be crouching
            if (currentMovementState == CharacterMovementState.Crouching)
                return false;

            // Must be sprinting with movement
            return context.IsSprinting && context.IsMoving;
        }

        private void ApplyMutualExclusionRules(ref InputContext context)
        {
            // Get desired states from input/toggles
            bool desiredSprint = _settings.UseToggleSprint ? 
                context.SprintToggleActive : 
                context.Sprint.Held;

            bool desiredCrouch = _settings.UseToggleCrouch ? 
                context.CrouchToggleActive : 
                context.Crouch.Held;

            // Apply mutual exclusion logic
            if (desiredSprint && context.IsMoving)
            {
                context.IsSprinting = true;

                // Don't override crouch during slide
                if (!context.Slide.IsSlideActive)
                {
                    context.ShouldBeCrouching = false;
                    if (_settings.UseToggleCrouch)
                    {
                        context.CrouchToggleActive = false;
                    }
                }
            }
            else if (desiredCrouch && !context.IsSprinting)
            {
                context.ShouldBeCrouching = true;
                context.IsSprinting = false;
                if (_settings.UseToggleSprint)
                {
                    context.SprintToggleActive = false;
                }
            }
            else if (desiredSprint && !context.IsMoving)
            {
                // Sprint desired but not moving - allow crouch
                context.IsSprinting = false;
                context.ShouldBeCrouching = desiredCrouch;
            }
            else
            {
                context.IsSprinting = false;
                context.ShouldBeCrouching = false;
            }

            // Sync toggle states for hold modes
            if (!_settings.UseToggleSprint)
            {
                context.SprintToggleActive = context.IsSprinting;
            }

            if (!_settings.UseToggleCrouch)
            {
                context.CrouchToggleActive = context.ShouldBeCrouching;
            }
        }

        private void ValidateStates(ref InputContext context, CharacterMovementState currentMovementState)
        {
            // Clear slide initiation if conditions are no longer met
            if (context.Slide.InitiationRequested && !context.Slide.CanInitiateSlide)
            {
                context.Slide.InitiationRequested = false;
            }
        }

        #endregion
    }
}