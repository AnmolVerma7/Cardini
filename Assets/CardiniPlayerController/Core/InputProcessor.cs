// Input Processor - Handles all input logic and timing
using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
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
        /// Main processing method - call this from SetControllerInputs
        /// </summary>
        public void ProcessInputs(InputBridge inputBridge, ref InputContext context, CharacterMovementState currentMovementState, float deltaTime)
        {
            // Step 1: Update raw input states
            UpdateRawInputStates(inputBridge, ref context);

            // Step 2: Update timing for all actions
            UpdateInputTiming(ref context, deltaTime);

            // Step 3: Process toggle logic
            ProcessToggleLogic(ref context);

            // Step 4: Process slide logic (the complex part!)
            ProcessSlideLogic(ref context, currentMovementState);

            // Step 5: Apply mutual exclusion rules
            ApplyMutualExclusionRules(ref context);

            // Step 6: Validate states against current conditions
            ValidateStates(ref context, currentMovementState);
        }

        private void UpdateRawInputStates(InputBridge inputBridge, ref InputContext context)
        {
            // Update raw button states - FIXED property names!
            context.Jump.Pressed = inputBridge.Jump.IsPressed;
            context.Jump.Held = inputBridge.Jump.IsHeld;
            context.Jump.Released = inputBridge.Jump.WasReleasedThisFrame; // FIXED!

            context.Sprint.Pressed = inputBridge.Sprint.IsPressed;
            context.Sprint.Held = inputBridge.Sprint.IsHeld;
            context.Sprint.Released = inputBridge.Sprint.WasReleasedThisFrame; // FIXED!

            context.Crouch.Pressed = inputBridge.Crouch.IsPressed;
            context.Crouch.Held = inputBridge.Crouch.IsHeld;
            context.Crouch.Released = inputBridge.Crouch.WasReleasedThisFrame; // FIXED!
            
            // Debug the input states
            // if (context.Sprint.Pressed || context.Crouch.Pressed)
            // {
            //     Debug.Log($"[INPUT] Sprint: P={context.Sprint.Pressed} H={context.Sprint.Held} R={context.Sprint.Released}");
            //     Debug.Log($"[INPUT] Crouch: P={context.Crouch.Pressed} H={context.Crouch.Held} R={context.Crouch.Released}");
            // }
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

            // Crouch toggle (but NOT during slide cancel!)
            bool isSlideCancel = (context.Slide.IsSlideActive && context.Crouch.Pressed);

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
                // Handle slide cancellation
                ProcessSlideCancellation(ref context);
            }
            else
            {
                // Handle slide initiation
                ProcessSlideInitiation(ref context, currentMovementState);
            }
        }

        private void ProcessSlideCancellation(ref InputContext context)
        {
            bool shouldCancel = false;

            if (_settings.UseToggleCrouch)
            {
                // Toggle mode: Cancel on crouch press
                if (context.Crouch.Pressed)
                {
                    shouldCancel = true;
                    // Debug.Log($"[INPUT] Slide cancel requested (Toggle mode)");
                }
            }
            else
            {
                // Hold mode: Cancel on crouch release
                if (context.Crouch.Released)
                {
                    shouldCancel = true;
                    // Debug.Log($"[INPUT] Slide cancel requested (Hold mode)");
                }
            }

            if (shouldCancel)
            {
                context.Slide.CancelRequested = true;
                context.Slide.InitiationRequested = false;
            }
        }

        private void ProcessSlideInitiation(ref InputContext context, CharacterMovementState currentMovementState)
{
    // Clear any stale cancel requests when not sliding
    context.Slide.CancelRequested = false;

    // Check for slide initiation conditions
    bool canInitiateSlide = CanInitiateSlide(context, currentMovementState);
    context.Slide.CanInitiateSlide = canInitiateSlide;

    // DEBUG: Log all the conditions
    // if (context.Crouch.Pressed)
    // {
    //     Debug.Log($"[SLIDE DEBUG] Crouch pressed! CanInitiate: {canInitiateSlide}");
    //     Debug.Log($"[SLIDE DEBUG] IsSprinting: {context.IsSprinting}, IsMoving: {context.IsMoving}");
    //     Debug.Log($"[SLIDE DEBUG] HoldDuration: {context.Crouch.HoldDuration:F3}, MinRequired: {context.Slide.MinHoldTimeForSlide:F3}");
    //     Debug.Log($"[SLIDE DEBUG] CurrentMovementState: {currentMovementState}");
    // }

    if (canInitiateSlide && context.Crouch.Pressed)
    {
        // Check minimum hold time requirement
        if (context.Crouch.HoldDuration >= context.Slide.MinHoldTimeForSlide || 
            context.Slide.MinHoldTimeForSlide <= 0f)
        {
            context.Slide.InitiationRequested = true;
            context.Slide.TimeSinceSlideRequest = 0f;
            Debug.Log($"[INPUT] Slide initiation requested!");
        }
        else
        {
            Debug.Log($"[SLIDE DEBUG] Hold time too short: {context.Crouch.HoldDuration:F3} < {context.Slide.MinHoldTimeForSlide:F3}");
        }
    }
}

        private bool CanInitiateSlide(InputContext context, CharacterMovementState currentMovementState)
        {
            // Must be grounded and stable
            if (!_motor.GroundingStatus.IsStableOnGround)
            {
                return false;
            }

            // Must not already be crouching
            if (currentMovementState == CharacterMovementState.Crouching)
            {
                return false;
            }

            // Must be sprinting with movement
            if (!context.IsSprinting || !context.IsMoving)
            {
                return false;
            }

            return true;
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
                // Debug.Log($"[INPUT] Slide initiation cleared - conditions no longer met");
            }
        }
    }
}