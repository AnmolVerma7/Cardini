
using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    /// <summary>
    /// Handles ground-based movement including walking, jogging, sprinting, and crouching.
    /// Manages speed tiers, capsule resizing, and jump execution from ground.
    /// </summary>
    public class BaseLocomotionModule : MovementModuleBase
    {
        private CharacterMovementState _currentGroundedSubState;

        // Properties for accessing controller state
        private bool IsSprinting => Controller.IsSprinting;
        private bool IsCrouching
        {
            get => Controller.IsCrouching;
            set => Controller.SetCrouchingState(value);
        }
        private bool ShouldBeCrouching => Controller.ShouldBeCrouching;

        public override int Priority => 0; // Base locomotion module
        public override CharacterMovementState AssociatedPrimaryMovementState => _currentGroundedSubState;

        public override bool CanEnterState()
        {
            bool isExitingSlide = Controller.CurrentMovementState == CharacterMovementState.Sliding;
            
            return Motor.GroundingStatus.IsStableOnGround &&
                   Controller.CurrentMajorState == CharacterState.Locomotion &&
                   CommonChecks() &&
                   (isExitingSlide || true);
        }

        public override void OnEnterState()
        {
            Controller.TimeSinceLastAbleToJump = 0f;
            
            if (!Controller._jumpedThisFrameInternal)
            {
                Controller.ConsumeJumpRequest();
                Controller.SetJumpConsumed(false);
                Controller.SetDoubleJumpConsumed(false);
            }
            
            UpdateGroundedSubState(Controller.MoveInputVector.magnitude);
        }

        public override void OnExitState()
        {
            Controller.SetLastGroundedSpeedTier(Controller.CurrentSpeedTierForJump);
            PlayerAnimator?.SetGrounded(false);
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            Controller.HandleStandardRotation(ref currentRotation, deltaTime);
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (Settings == null) return;

            float inputMagnitude = Controller.MoveInputVector.magnitude;
            UpdateGroundedSubState(inputMagnitude);

            // Determine speed based on current sub-state
            float currentDesiredMaxSpeed = GetDesiredSpeedForState();

            // Calculate animator parameters
            CalculateAnimatorParameters(out float normalizedSpeedTier, out float velocityX, out float velocityZ);
            PlayerAnimator?.SetLocomotionSpeeds(normalizedSpeedTier, velocityX, velocityZ);

            // Apply ground movement
            ApplyGroundMovement(ref currentVelocity, currentDesiredMaxSpeed, deltaTime);

            // Handle jump requests
            HandleJumpRequests();
        }

        public override void AfterCharacterUpdate(float deltaTime)
        {
            if (Settings == null) return;
            
            // Skip capsule management if we're sliding (slide module handles it)
            if (Controller.CurrentMovementState == CharacterMovementState.Sliding)
                return;

            HandleCrouchingCapsule();
            UpdateGroundedSubState(Controller.MoveInputVector.magnitude);
        }

        public override void PostGroundingUpdate(float deltaTime)
        {
            // Handle landing detection
            if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
            {
                Controller.OnLandedInternal();
            }
        }

        #region Private Methods

        private float GetDesiredSpeedForState()
        {
            return _currentGroundedSubState switch
            {
                CharacterMovementState.Crouching => Settings.MaxCrouchSpeed,
                CharacterMovementState.Sprinting => Settings.MaxSprintSpeed,
                CharacterMovementState.Jogging => Settings.MaxJogSpeed,
                CharacterMovementState.Walking => Settings.MaxWalkSpeed,
                _ => 0f // Idle
            };
        }

        private void CalculateAnimatorParameters(out float normalizedSpeedTier, out float velocityX, out float velocityZ)
        {
            normalizedSpeedTier = _currentGroundedSubState switch
            {
                CharacterMovementState.Sprinting => 3f,
                CharacterMovementState.Jogging => 2f,
                CharacterMovementState.Walking => 1f,
                _ => 0f
            };

            // Calculate local velocities for 2D blend tree
            Vector3 localMoveInput = Motor.transform.InverseTransformDirection(Controller.MoveInputVector);
            velocityX = localMoveInput.x;
            velocityZ = localMoveInput.z;

            // Handle orientation-specific adjustments
            if (Settings.OrientationMethod == CardiniOrientationMethod.TowardsMovement && 
                Controller.MoveInputVector.sqrMagnitude > 0.01f)
            {
                velocityX = 0f;
                velocityZ = 1f; // Always moving "forward" relative to self
            }
            else if (Controller.MoveInputVector.sqrMagnitude < 0.01f)
            {
                velocityX = 0f;
                velocityZ = 0f;
            }
        }

        private void ApplyGroundMovement(ref Vector3 currentVelocity, float targetSpeed, float deltaTime)
        {
            float currentVelocityMagnitude = currentVelocity.magnitude;
            Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;
            
            // Project velocity onto ground plane
            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

            // Calculate target movement direction
            Vector3 targetDirection = CalculateTargetDirection(effectiveGroundNormal);
            Vector3 targetMovementVelocity = targetDirection * targetSpeed;

            // Apply movement with smoothing
            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 
                1f - Mathf.Exp(-Settings.StableMovementSharpness * deltaTime));
        }

        private Vector3 CalculateTargetDirection(Vector3 groundNormal)
        {
            Vector3 inputDirection = Controller.MoveInputVector.normalized;

            if (Controller.MoveInputVector.sqrMagnitude <= 0.01f)
                return Vector3.zero;

            // Reorient input direction onto ground plane
            Vector3 inputRight = Vector3.Cross(inputDirection, Motor.CharacterUp);
            return Vector3.Cross(groundNormal, inputRight).normalized;
        }

        private void HandleJumpRequests()
        {
            if (!Controller.IsJumpRequested()) return;

            // Check if higher priority module wants the jump input
            if (IsJumpClaimedByHigherPriorityModule()) return;

            bool isJumpAllowedByMatrix = Controller.CanTransitionToState(CharacterMovementState.Jumping);
            bool canGroundJump = Settings.AllowJumpingWhenSliding ? 
                Motor.GroundingStatus.FoundAnyGround : 
                Motor.GroundingStatus.IsStableOnGround;

            if (!Controller.IsJumpConsumed() && canGroundJump && isJumpAllowedByMatrix)
            {
                ExecuteGroundJump();
            }
            else
            {
                Controller.ConsumeJumpRequest();
            }
        }

        private bool IsJumpClaimedByHigherPriorityModule()
        {
            foreach (var module in Controller.movementModules)
            {
                if (module.Priority > this.Priority && module.CanEnterState())
                    return true;
            }
            return false;
        }

        private void ExecuteGroundJump()
        {
            float jumpSpeedTier = Controller.CurrentSpeedTierForJump;
            
            // Determine jump parameters based on speed
            float jumpUpSpeed, jumpForwardSpeed;
            if (jumpSpeedTier >= Settings.MaxSprintSpeed * 0.9f)
            {
                jumpUpSpeed = Settings.JumpUpSpeed_Sprint;
                jumpForwardSpeed = Settings.JumpScalableForwardSpeed_Sprint;
            }
            else if (jumpSpeedTier >= Settings.MaxJogSpeed * 0.9f)
            {
                jumpUpSpeed = Settings.JumpUpSpeed_Jog;
                jumpForwardSpeed = Settings.JumpScalableForwardSpeed_Jog;
            }
            else
            {
                jumpUpSpeed = Settings.JumpUpSpeed_IdleWalk;
                jumpForwardSpeed = Settings.JumpScalableForwardSpeed_IdleWalk;
            }

            Controller.ExecuteJump(jumpUpSpeed, jumpForwardSpeed, Controller.MoveInputVector);
        }

        private void HandleCrouchingCapsule()
        {
            if (IsCrouching && !ShouldBeCrouching) // Attempting to uncrouch
            {
                // Test if we can stand up
                Motor.SetCapsuleDimensions(Motor.Capsule.radius, Settings.DefaultCapsuleHeight, Settings.DefaultCapsuleHeight * 0.5f);
                
                if (Motor.CharacterOverlap(Motor.TransientPosition, Motor.TransientRotation, 
                    Controller.ProbedColliders_SharedBuffer, Motor.CollidableLayers, QueryTriggerInteraction.Ignore) > 0)
                {
                    // Obstructed, revert to crouching
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
                IsCrouching = true;
                Motor.SetCapsuleDimensions(Motor.Capsule.radius, Settings.CrouchedCapsuleHeight, Settings.CrouchedCapsuleHeight * 0.5f);
            }
        }

        private void UpdateGroundedSubState(float inputMagnitude)
        {
            CharacterMovementState newSubState;
            
            bool wantsToSprint = Controller.IsSprinting;
            bool wantsToCrouch = Controller.ShouldBeCrouching;

            // Determine desired state based on input priority
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

            // Update speed tier for jumping
            Controller.CurrentSpeedTierForJump = GetDesiredSpeedForState();

            // Update state if changed
            if (_currentGroundedSubState != newSubState)
            {
                _currentGroundedSubState = newSubState;
            }
        }

        #endregion
    }
}