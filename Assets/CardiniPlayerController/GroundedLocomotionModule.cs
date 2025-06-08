// GroundedLocomotionModule.cs
using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    public class GroundedLocomotionModule : MovementModuleBase
    {
        // Internal state for this module
        private CharacterMovementState _currentGroundedSubState;

        // Properties from CardiniController we'll manage or influence here
        // (CardiniController will still hold the "source of truth" for some of these,
        //  this module will read/request changes)
        private bool IsSprinting => Controller._isSprinting; // Read from Controller
        private bool IsCrouching // Read from Controller's authoritative _isCrouching
        {
            get => Controller._isCrouching;
            set => Controller.SetCrouchingState(value); // Request Controller to change physical crouch
        } 
        private bool ShouldBeCrouching => Controller._shouldBeCrouching; // Read from Controller

        public override int Priority => 0; // Base locomotion module

        public override CharacterMovementState AssociatedPrimaryMovementState => _currentGroundedSubState;

        public override bool CanEnterState()
        {
            // This module can enter if the character is on stable ground
            // AND the major state is Locomotion
            // AND no higher priority module (like a vault or slide starting from ground) wants to run.
            return Motor.GroundingStatus.IsStableOnGround && 
                   Controller.CurrentMajorState == CharacterState.Locomotion;
        }

        public override void OnEnterState()
        {
            // When entering grounded state, determine initial sub-state
            // (Idle, Walking, Jogging, Sprinting, Crouching)
            Controller._timeSinceLastAbleToJump = 0f; // Reset coyote timer when grounded
            if (!Controller._jumpedThisFrame) // If we didn't just land from a jump that WE initiated
            {
                 Controller.ConsumeJumpRequest(); // Clear any buffered jump request if we land without jumping
            }
            UpdateGroundedSubState(Controller._moveInputVector.magnitude);
        }

        public override void OnExitState()
        {
            // Cleanup when leaving grounded state (e.g., if player jumps or falls)
            // Controller._lastGroundedSpeedTier might be set here based on current speed
            Controller.SetLastGroundedSpeedTier(Controller._currentSpeedTierForJump);
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            // Use the Controller's shared rotation logic for now, or implement module-specific if needed
            Controller.HandleStandardRotation(ref currentRotation, deltaTime);
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (Settings == null) return;

            // --- Determine Target Ground Speed based on State ---
            float inputMagnitude = Controller._moveInputVector.magnitude; // Use processed move input from Controller
            UpdateGroundedSubState(inputMagnitude); // Update current sub-state (Idle, Walk, Jog, etc.)

            float currentDesiredMaxSpeed;
            if (IsCrouching) // Read Controller's _isCrouching
            {
                currentDesiredMaxSpeed = Settings.MaxCrouchSpeed;
            }
            else if (IsSprinting) // Read Controller's _isSprinting
            {
                currentDesiredMaxSpeed = Settings.MaxSprintSpeed;
            }
            else if (_currentGroundedSubState == CharacterMovementState.Jogging)
            {
                currentDesiredMaxSpeed = Settings.MaxJogSpeed;
            }
            else if (_currentGroundedSubState == CharacterMovementState.Walking)
            {
                currentDesiredMaxSpeed = Settings.MaxWalkSpeed;
            }
            else // Idle
            {
                currentDesiredMaxSpeed = 0f;
            }
            
            // --- Ground Movement Logic (from CardiniController) ---
            float currentVelocityMagnitude = currentVelocity.magnitude;
            Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;
            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

            Vector3 reorientedInput = Controller._moveInputVector;
            if (Controller._moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 inputRight = Vector3.Cross(Controller._moveInputVector, Motor.CharacterUp);
                reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * inputMagnitude;
            }

            Vector3 targetMovementVelocity = reorientedInput * currentDesiredMaxSpeed;
            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-Settings.StableMovementSharpness * deltaTime));

            // --- Handle Jump Initiation ---
            // This module detects if a jump *should* happen from ground/coyote.
            // It then tells the Controller, which will handle the state transition to Airborne.
            if (Controller.IsJumpRequested()) // Check jump request via Controller
            {
                // Grounded module now ONLY checks for actual ground jumps
                bool canGroundJump = (Settings.AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround);

                if (!Controller.IsJumpConsumed() && canGroundJump) // Only ground jump condition
                {
                    // Determine jump speeds based on current ground state
                    float jumpSpeedTierForThisJump = Controller._currentSpeedTierForJump;

                    float actualJumpUpSpeed = Settings.JumpUpSpeed_IdleWalk;
                    float actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_IdleWalk;

                    if (jumpSpeedTierForThisJump >= Settings.MaxSprintSpeed * 0.9f)
                    {
                        actualJumpUpSpeed = Settings.JumpUpSpeed_Sprint;
                        actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_Sprint;
                    }
                    else if (jumpSpeedTierForThisJump >= Settings.MaxJogSpeed * 0.9f)
                    {
                        actualJumpUpSpeed = Settings.JumpUpSpeed_Jog;
                        actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_Jog;
                    }

                    Controller.ExecuteJump(actualJumpUpSpeed, actualJumpForwardSpeed, Controller._moveInputVector);
                }
            }
        }

        public override void AfterCharacterUpdate(float deltaTime)
        {
            if (Settings == null) return;

            // --- Crouching Capsule Logic ---
            // Reads Controller._shouldBeCrouching, updates Controller._isCrouching (physical state)
            if (IsCrouching && !ShouldBeCrouching) // Attempting to uncrouch
            {
                // Temporarily set to standing height for overlap test
                Motor.SetCapsuleDimensions(Motor.Capsule.radius, Settings.DefaultCapsuleHeight, Settings.DefaultCapsuleHeight * 0.5f);
                if (Motor.CharacterOverlap(Motor.TransientPosition, Motor.TransientRotation, Controller._probedColliders, Motor.CollidableLayers, QueryTriggerInteraction.Ignore) > 0)
                {
                    // Obstructed, revert to crouching dimensions
                    Motor.SetCapsuleDimensions(Motor.Capsule.radius, Settings.CrouchedCapsuleHeight, Settings.CrouchedCapsuleHeight * 0.5f);
                }
                else
                {
                    // Uncrouch successful
                    if (Controller.MeshRoot) Controller.MeshRoot.localScale = Vector3.one;
                    IsCrouching = false; // This now calls Controller.SetCrouchingState(false)
                }
            }
            else if (!IsCrouching && ShouldBeCrouching) // Attempting to crouch
            {
                IsCrouching = true; // This now calls Controller.SetCrouchingState(true)
                Motor.SetCapsuleDimensions(Motor.Capsule.radius, Settings.CrouchedCapsuleHeight, Settings.CrouchedCapsuleHeight * 0.5f);
                if (Controller.MeshRoot) Controller.MeshRoot.localScale = new Vector3(Controller.MeshRoot.localScale.x, Settings.CrouchedCapsuleHeight / Settings.DefaultCapsuleHeight, Controller.MeshRoot.localScale.z);
            }
            
            // Update the sub-state after all physics and crouch changes
            // This ensures the _currentGroundedSubState reflects the true physical state (e.g. actually crouching)
            UpdateGroundedSubState(Controller._moveInputVector.magnitude); 
        }

        public override void PostGroundingUpdate(float deltaTime)
        {
            // This module is active only when grounded, so if we just landed, this is relevant.
            if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround) // Just Landed
            {
                Controller.OnLandedInternal(); // Tell controller we landed
            }
        }

        private void UpdateGroundedSubState(float inputMagnitude)
        {
            // Determine and set the specific grounded sub-state (Idle, Walking, etc.)
            // This also updates the Controller's _currentSpeedTierForJump for the next potential jump
            CharacterMovementState newSubState;
            if (IsCrouching) // Prioritize crouching
            {
                newSubState = CharacterMovementState.Crouching;
                Controller._currentSpeedTierForJump = Settings.MaxCrouchSpeed;
            }
            else if (IsSprinting) // Then sprinting
            {
                newSubState = CharacterMovementState.Sprinting;
                Controller._currentSpeedTierForJump = Settings.MaxSprintSpeed;
            }
            else if (inputMagnitude > Settings.JogThreshold)
            {
                newSubState = CharacterMovementState.Jogging;
                Controller._currentSpeedTierForJump = Settings.MaxJogSpeed;
            }
            else if (inputMagnitude > Settings.WalkThreshold)
            {
                newSubState = CharacterMovementState.Walking;
                Controller._currentSpeedTierForJump = Settings.MaxWalkSpeed;
            }
            else
            {
                newSubState = CharacterMovementState.Idle;
                Controller._currentSpeedTierForJump = 0f; // For idle jumps
            }

            if (_currentGroundedSubState != newSubState)
            {
                _currentGroundedSubState = newSubState;
                // The Controller's main _currentMovementState will be updated by CardiniController
                // based on this module's AssociatedPrimaryMovementState.
            }
        }
    }
}