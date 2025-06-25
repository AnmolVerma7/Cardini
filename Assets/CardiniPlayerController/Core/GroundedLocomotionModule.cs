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
        private bool IsSprinting => Controller.IsSprinting; // Read from Controller
        private bool IsCrouching // Read from Controller's authoritative _isCrouching
        {
            get => Controller.IsCrouching;
            set => Controller.SetCrouchingState(value); // Request Controller to change physical crouch
        }
        private bool ShouldBeCrouching => Controller.ShouldBeCrouching; // Read from Controller

        public override int Priority => 0; // Base locomotion module

        public override CharacterMovementState AssociatedPrimaryMovementState => _currentGroundedSubState;

        public override bool CanEnterState()
        {
            // This module can enter if the character is on stable ground
            // AND the major state is Locomotion
            // AND no higher priority module (like a vault or slide starting from ground) wants to run.
            bool isExitingSlide = Controller.CurrentMovementState == CharacterMovementState.Sliding;
    
            return Motor.GroundingStatus.IsStableOnGround &&
                Controller.CurrentMajorState == CharacterState.Locomotion &&
                CommonChecks() &&
                (isExitingSlide || true);
        }

        public override void OnEnterState()
        {
            // When entering grounded state, determine initial sub-state
            // (Idle, Walking, Jogging, Sprinting, Crouching)
            Controller.TimeSinceLastAbleToJump = 0f; // Reset coyote timer when grounded
            if (!Controller._jumpedThisFrameInternal) // If we didn't just land from a jump that WE initiated
            {
                Controller.ConsumeJumpRequest();
                Controller.SetJumpConsumed(false); // Clear any buffered jump request if we land without jumping
                Controller.SetDoubleJumpConsumed(false); // Clear any buffered jump request if we land without jumping
            }
            UpdateGroundedSubState(Controller.MoveInputVector.magnitude);
        }

        public override void OnExitState()
        {
            // Cleanup when leaving grounded state (e.g., if player jumps or falls)
            // Controller._lastGroundedSpeedTier might be set here based on current speed
            Controller.SetLastGroundedSpeedTier(Controller.CurrentSpeedTierForJump);
            PlayerAnimator?.SetGrounded(false);
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            // Use the Controller's shared rotation logic for now, or implement module-specific if needed
            Controller.HandleStandardRotation(ref currentRotation, deltaTime);
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (Settings == null) return;

            // Use processed move input from Controller
            float inputMagnitude = Controller.MoveInputVector.magnitude;
            UpdateGroundedSubState(inputMagnitude); // Update current sub-state (Idle, Walk, Jog, etc.)

            float currentDesiredMaxSpeed;
            // Determine speed based on the module's sub-state, which was just updated
            if (_currentGroundedSubState == CharacterMovementState.Crouching)
            {
                currentDesiredMaxSpeed = Settings.MaxCrouchSpeed;
            }
            else if (_currentGroundedSubState == CharacterMovementState.Sprinting)
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

            // Calculate animator parameters
            float normalizedSpeedTier = 0f;
            if (_currentGroundedSubState == CharacterMovementState.Sprinting) normalizedSpeedTier = 3f;
            else if (_currentGroundedSubState == CharacterMovementState.Jogging) normalizedSpeedTier = 2f;
            else if (_currentGroundedSubState == CharacterMovementState.Walking) normalizedSpeedTier = 1f;

            // Calculate local velocities for 2D blend tree
            // MoveInputVector is world-space camera-relative input
            // We need it in character's local space for X/Z animation parameters
            Vector3 localMoveInput = Motor.transform.InverseTransformDirection(Controller.MoveInputVector);
            float velocityX = localMoveInput.x;
            float velocityZ = localMoveInput.z;

            // If OrientationMethod is TowardsMovement, character always faces move direction.
            // So, local X velocity for animation should be 0, and Z is forward speed.
            if (Settings.OrientationMethod == CardiniOrientationMethod.TowardsMovement && Controller.MoveInputVector.sqrMagnitude > 0.01f)
            {
                velocityX = 0f;
                velocityZ = 1f; // Always moving "forward" relative to self
                // normalizedSpeedTier already represents the speed magnitude (0,1,2,3)
            }
            else if (Controller.MoveInputVector.sqrMagnitude < 0.01f) // No movement input
            {
                velocityX = 0f;
                velocityZ = 0f;
            }


            PlayerAnimator?.SetLocomotionSpeeds(normalizedSpeedTier, velocityX, velocityZ);

            // --- Ground Movement Logic ---
            float currentVelocityMagnitude = currentVelocity.magnitude;
            Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;
            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

            // Get the PURE DIRECTION of input from the controller
            Vector3 inputDirection = Controller.MoveInputVector.normalized;

            // Reorient the desired input direction onto the ground plane
            Vector3 reorientedInputDirection = inputDirection;
            if (Controller.MoveInputVector.sqrMagnitude > 0.01f)
            {
                Vector3 inputRight = Vector3.Cross(inputDirection, Motor.CharacterUp);
                reorientedInputDirection = Vector3.Cross(effectiveGroundNormal, inputRight).normalized;
            }
            else // No significant input, reoriented direction should also be zero to prevent drift
            {
                reorientedInputDirection = Vector3.zero;
            }

            // Target velocity is now the full desired speed IN THE PURE, REORIENTED DIRECTION.
            // This creates the speed plateaus.

            Vector3 targetMovementVelocity = reorientedInputDirection * currentDesiredMaxSpeed;

            // If _currentGroundedSubState is Idle, currentDesiredMaxSpeed is 0, so targetMovementVelocity is zero.
            // The Lerp will then smoothly bring currentVelocity to zero.

            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-Settings.StableMovementSharpness * deltaTime));

            // // --- Handle Jump Initiation ---
            // if (Controller.IsJumpRequested())
            // {
            //     // Debug.Log($"[Grounded] Jump Requested! Current SubState: {_currentGroundedSubState}, CurrentControllerState: {Controller.CurrentMovementState}");
            //     bool isJumpAllowedByMatrix = Controller.CanTransitionToState(CharacterMovementState.Jumping);
            //     bool canGroundJump = (Settings.AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround);
            //     if (!Controller.IsJumpConsumed() && canGroundJump && isJumpAllowedByMatrix)
            //     {
            //         // Debug.Log($"[Grounded] Jump Execution ALLOWED! Matrix allowed: {isJumpAllowedByMatrix}. Consumed: {Controller.IsJumpConsumed()}.");
            //         float jumpSpeedTierForThisJump = Controller.CurrentSpeedTierForJump;
            //         float actualJumpUpSpeed = Settings.JumpUpSpeed_IdleWalk;
            //         float actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_IdleWalk;

            //         if (jumpSpeedTierForThisJump >= Settings.MaxSprintSpeed * 0.9f)
            //         {
            //             actualJumpUpSpeed = Settings.JumpUpSpeed_Sprint;
            //             actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_Sprint;
            //         }
            //         else if (jumpSpeedTierForThisJump >= Settings.MaxJogSpeed * 0.9f)
            //         {
            //             actualJumpUpSpeed = Settings.JumpUpSpeed_Jog;
            //             actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_Jog;
            //         }

            //         Controller.ExecuteJump(actualJumpUpSpeed, actualJumpForwardSpeed, Controller.MoveInputVector);
            //     }
            //     else // If jump was requested but blocked by matrix/conditions or already consumed
            //     {
            //         // Debug.Log($"[Grounded] Jump Execution BLOCKED! Consumed: {Controller.IsJumpConsumed()}, GroundJump: {canGroundJump}, Matrix Allowed: {isJumpAllowedByMatrix}. Consuming request.");
            //         Controller.ConsumeJumpRequest(); // Consume it immediately, whether it's matrix, conditions, or already consumed.
            //     }
            // }
            // --- Handle Jump Initiation ---
if (Controller.IsJumpRequested())
{
    Debug.Log($"[Grounded] Jump Requested! Current SubState: {_currentGroundedSubState}, CurrentControllerState: {Controller.CurrentMovementState}");
    
    // NEW: Check if any higher priority module (like VaultModule) wants this jump input
    bool higherPriorityModuleWantsJump = false;
    foreach (var module in Controller.movementModules)
    {
        if (module.Priority > this.Priority && module.CanEnterState())
        {
            higherPriorityModuleWantsJump = true;
            Debug.Log($"[Grounded] Higher priority module {module.GetType().Name} wants jump input");
            break;
        }
    }
    
    // Only process jump if no higher priority module wants it
    if (!higherPriorityModuleWantsJump)
    {
        bool isJumpAllowedByMatrix = Controller.CanTransitionToState(CharacterMovementState.Jumping);
        bool canGroundJump = (Settings.AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround);
        
        if (!Controller.IsJumpConsumed() && canGroundJump && isJumpAllowedByMatrix)
        {
            Debug.Log($"[Grounded] Jump Execution ALLOWED! Matrix allowed: {isJumpAllowedByMatrix}. Consumed: {Controller.IsJumpConsumed()}.");
            
            float jumpSpeedTierForThisJump = Controller.CurrentSpeedTierForJump;
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

            Controller.ExecuteJump(actualJumpUpSpeed, actualJumpForwardSpeed, Controller.MoveInputVector);
        }
        else
        {
            Debug.Log($"[Grounded] Jump Execution BLOCKED! Consumed: {Controller.IsJumpConsumed()}, GroundJump: {canGroundJump}, Matrix Allowed: {isJumpAllowedByMatrix}. Consuming request.");
            Controller.ConsumeJumpRequest();
        }
    }
    else
    {
        Debug.Log("[Grounded] Deferring jump to higher priority module");
        // Don't consume the jump request - let the higher priority module handle it
    }
}
        }

        public override void AfterCharacterUpdate(float deltaTime)
        {
            if (Settings == null) return;
            // IMPORTANT: Skip capsule management if we're sliding
            // The slide module handles its own capsule dimensions
            if (Controller.CurrentMovementState == CharacterMovementState.Sliding)
            {
                Debug.Log("[Grounded] Skipping capsule update - currently sliding");
                return;
            }

            // --- Crouching Capsule Logic ---
            // Reads Controller.ShouldBeCrouching, updates Controller._isCrouching (physical state)
            if (IsCrouching && !ShouldBeCrouching) // Attempting to uncrouch
            {
                // Temporarily set to standing height for overlap test
                Motor.SetCapsuleDimensions(Motor.Capsule.radius, Settings.DefaultCapsuleHeight, Settings.DefaultCapsuleHeight * 0.5f);
                if (Motor.CharacterOverlap(Motor.TransientPosition, Motor.TransientRotation, Controller.ProbedColliders_SharedBuffer, Motor.CollidableLayers, QueryTriggerInteraction.Ignore) > 0)
                {
                    // Obstructed, revert to crouching dimensions
                    Motor.SetCapsuleDimensions(Motor.Capsule.radius, Settings.CrouchedCapsuleHeight, Settings.CrouchedCapsuleHeight * 0.5f);
                }
                else
                {
                    // Uncrouch successful
                    IsCrouching = false;
                }
            }
            else if (!IsCrouching && ShouldBeCrouching) // Attempting to crouch
            {
                IsCrouching = true; // This now calls Controller.SetCrouchingState(true)
                Motor.SetCapsuleDimensions(Motor.Capsule.radius, Settings.CrouchedCapsuleHeight, Settings.CrouchedCapsuleHeight * 0.5f);
            }

            // Update the sub-state after all physics and crouch changes
            // This ensures the _currentGroundedSubState reflects the true physical state (e.g. actually crouching)
            UpdateGroundedSubState(Controller.MoveInputVector.magnitude);
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
            CharacterMovementState newSubState;
            CharacterMovementState oldSubState = _currentGroundedSubState;

            // Use the desired input states from CardiniController
            bool wantsToSprint = Controller.IsSprinting;
            bool wantsToCrouch = Controller.ShouldBeCrouching;
            bool isPhysicallyCrouching = Controller.IsCrouching; // The current physical state of the capsule

            // --- Determine the new desired CharacterMovementState based on input priority ---
            // (This now reflects what the player *intends* to do, overriding crouch with sprint)
            if (wantsToSprint)
            {
                newSubState = CharacterMovementState.Sprinting;
            }
            else if (wantsToCrouch)
            {
                newSubState = CharacterMovementState.Crouching;
            }
            else if (inputMagnitude > Settings.JogThreshold)
            {
                newSubState = CharacterMovementState.Jogging;
            }
            else if (inputMagnitude > Settings.WalkThreshold)
            {
                newSubState = CharacterMovementState.Walking;
            }
            else
            {
                newSubState = CharacterMovementState.Idle;
            }

            // --- Apply speed tier based on this new desired state ---
            // The physical capsule resizing will happen in AfterCharacterUpdate
            if (newSubState == CharacterMovementState.Crouching)
            {
                Controller.CurrentSpeedTierForJump = Settings.MaxCrouchSpeed;
            }
            else if (newSubState == CharacterMovementState.Sprinting)
            {
                Controller.CurrentSpeedTierForJump = Settings.MaxSprintSpeed;
            }
            else if (newSubState == CharacterMovementState.Jogging)
            {
                Controller.CurrentSpeedTierForJump = Settings.MaxJogSpeed;
            }
            else if (newSubState == CharacterMovementState.Walking)
            {
                Controller.CurrentSpeedTierForJump = Settings.MaxWalkSpeed;
            }
            else // Idle
            {
                Controller.CurrentSpeedTierForJump = 0f;
            }

            // --- Final Assignment of _currentGroundedSubState ---
            if (_currentGroundedSubState != newSubState)
            {
                // Debug.Log($"[Grounded] SubState Change: {oldSubState} -> {newSubState}. IsSprinting: {wantsToSprint}, ShouldBeCrouching: {wantsToCrouch}, IsPhysicallyCrouching: {isPhysicallyCrouching}, InputMagnitude: {inputMagnitude}");
                _currentGroundedSubState = newSubState;
            }
        }
    }
}