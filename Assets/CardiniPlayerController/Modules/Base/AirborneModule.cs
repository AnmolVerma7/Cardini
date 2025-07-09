using UnityEngine;


namespace Cardini.Motion
{
    /// <summary>
    /// Handles airborne movement including jumping, double jumping, wall jumping, and falling.
    /// Manages air control, coyote time, and state transitions between different airborne states.
    /// </summary>
    public class AirborneModule : MovementModuleBase
    {
        private bool _initiatedByJumpThisFrame = false;
        private CharacterMovementState _currentAirborneState = CharacterMovementState.Falling;
        private bool _enteredDueToWallJump = false;
        private bool _enteredDueToBulletJump = false;
        private bool _jumpInputThisFrame = false;

        public override int Priority => 3;
        public override CharacterMovementState AssociatedPrimaryMovementState => _currentAirborneState;

        public override bool CanEnterState()
        {
            return !Motor.GroundingStatus.IsStableOnGround &&
                   Controller.CurrentMajorState == CharacterState.Locomotion &&
                   CommonChecks();
        }

        public override void OnEnterState()
        {
            // Capture jump states before consuming anything
            _enteredDueToWallJump = Controller.WasWallJumpExecuted();
            _enteredDueToBulletJump = Controller.WasBulletJumpExecuted();
            _initiatedByJumpThisFrame = Controller.ConsumeJumpExecutionIntent();

            if (_initiatedByJumpThisFrame)
            {
                DetermineJumpType();
                Controller.SetJumpedThisFrame(true);
                PlayerAnimator?.TriggerJump();
            }
            else
            {
                // Fell off edge
                _currentAirborneState = CharacterMovementState.Falling;
                Controller.SetMovementState(CharacterMovementState.Falling);
                Controller.SetLastGroundedSpeedTier(Controller.CurrentSpeedTierForJump);
            }

            Controller.TimeSinceLastAbleToJump = 0.001f;
            PlayerAnimator?.SetGrounded(false);
        }

        public override void OnExitState()
        {
            // If exiting WITHOUT using a special jump, restore jump availability
            if (!_enteredDueToBulletJump && !_enteredDueToWallJump)
            {
                Controller.SetJumpConsumed(false);
                Controller.SetDoubleJumpConsumed(false);
                Controller.TimeSinceLastAbleToJump = 0.001f;
                
                if (Controller.CurrentMovementState == CharacterMovementState.WallRunning)
                {
                    Controller.SetMovementState(CharacterMovementState.Falling);
                }
            }

            ResetAirborneState();
            PlayerAnimator?.SetGrounded(true);
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            Controller.HandleStandardRotation(ref currentRotation, deltaTime);
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (Settings == null) return;

            bool executedJumpThisFrame = false;

            // Remove or modify the special case handling - bullet jumps should only allow double jump
            if (Controller.IsJumpRequested() && !Controller.IsJumpConsumed() && !Controller.IsDoubleJumpConsumed() && !Motor.GroundingStatus.FoundAnyGround)
            {
                // Only allow this for wall run exits, not bullet jumps
                bool wasFromWallRun = _currentAirborneState == CharacterMovementState.Falling &&
                                    Controller.TimeSinceLastAbleToJump < 0.5f &&
                                    !_enteredDueToBulletJump; // Add this check

                if (wasFromWallRun)
                {
                    if (Controller.TimeSinceLastAbleToJump <= Settings.JumpPostGroundingGraceTime)
                    {
                        executedJumpThisFrame = TryExecuteCoyoteJump();
                    }
                    else
                    {
                        ExecuteRegularAirborneJump();
                        executedJumpThisFrame = true;
                    }
                }
            }

            // Only check for double jump if we didn't already jump
            if (!executedJumpThisFrame && TryExecuteDoubleJump())
            {
                executedJumpThisFrame = true;
            }

            // Apply air movement
            ApplyAirMovement(ref currentVelocity, deltaTime);

            // Apply physics
            ApplyAirPhysics(ref currentVelocity, deltaTime);

            // Update airborne state transitions
            UpdateAirborneStateTransitions(executedJumpThisFrame);
        }

        private void ExecuteRegularAirborneJump()
        {
            // Get jump speeds based on last grounded speed
            float jumpUpSpeed, jumpForwardSpeed;
            float speedTier = Controller.LastGroundedSpeedTier;

            if (speedTier >= Settings.MaxSprintSpeed * 0.9f)
            {
                jumpUpSpeed = Settings.JumpUpSpeed_Sprint;
                jumpForwardSpeed = Settings.JumpScalableForwardSpeed_Sprint;
            }
            else if (speedTier >= Settings.MaxJogSpeed * 0.9f)
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
            Controller.SetJumpConsumed(true); // Only consume regular jump

            _currentAirborneState = CharacterMovementState.Jumping;
            Controller.SetMovementState(CharacterMovementState.Jumping);
        }

        public override void BeforeCharacterUpdate(float deltaTime) 
        { 
            // Track jump input directly from InputBridge
            _jumpInputThisFrame = Controller.inputBridge.Jump.IsPressed;
        }
        public override void AfterCharacterUpdate(float deltaTime)
        {
            if (Settings == null) return;
            Controller.TimeSinceLastAbleToJump += deltaTime;
        }

        public override void PostGroundingUpdate(float deltaTime)
        {
            if (Motor.GroundingStatus.IsStableOnGround)
            {
                Controller.OnLandedInternal();
            }
        }

        #region Private Methods

        private void DetermineJumpType()
        {
            if (_enteredDueToBulletJump)
            {
                _currentAirborneState = CharacterMovementState.BulletJumping;
                Controller.SetMovementState(CharacterMovementState.BulletJumping);
            }
            else if (_enteredDueToWallJump)
            {
                _currentAirborneState = CharacterMovementState.WallJumping;
                Controller.SetMovementState(CharacterMovementState.WallJumping);
            }
            else
            {
                _currentAirborneState = CharacterMovementState.Jumping;
                Controller.SetMovementState(CharacterMovementState.Jumping);
            }
        }

        private void ResetAirborneState()
        {
            _initiatedByJumpThisFrame = false;
            _enteredDueToWallJump = false;
            _enteredDueToBulletJump = false;
            _currentAirborneState = CharacterMovementState.Falling;
        }

        private bool TryExecuteDoubleJump()
        {
            if (!Settings.AllowDoubleJump) return false;

            // Use the direct input check
            bool jumpPressed = _jumpInputThisFrame || Controller.IsJumpRequested();

            // Allow double jump with direct input check
            bool canDoubleJump = jumpPressed &&  // Use jumpPressed instead of Controller.IsJumpRequested()
                                Controller.IsJumpConsumed() &&
                                !Controller.IsDoubleJumpConsumed() &&
                                !Motor.GroundingStatus.FoundAnyGround;

            if (canDoubleJump)
            {
                ExecuteDoubleJump(false);
                Controller.ConsumeJumpRequest();
                return true;
            }

            return false;
        }

        private void ExecuteDoubleJump(bool wasFalling)
        {
            var (upSpeed, forwardSpeed) = GetDoubleJumpSpeeds();

            Controller.ExecuteJump(upSpeed, forwardSpeed, Controller.MoveInputVector);
            Controller.SetDoubleJumpConsumed(true);
            // Don't consume regular jump - it should already be consumed

            _currentAirborneState = CharacterMovementState.DoubleJumping;
            Controller.SetMovementState(CharacterMovementState.DoubleJumping);
        }

        private (float upSpeed, float forwardSpeed) GetDoubleJumpSpeeds()
        {
            float speedTier = Controller.LastGroundedSpeedTier;

            if (speedTier >= Settings.MaxSprintSpeed * 0.9f)
                return (Settings.DoubleJumpUpSpeed_Sprint, Settings.DoubleJumpScalableForwardSpeed_Sprint);

            if (speedTier >= Settings.MaxJogSpeed * 0.9f)
                return (Settings.DoubleJumpUpSpeed_Jog, Settings.DoubleJumpScalableForwardSpeed_Jog);

            return (Settings.DoubleJumpUpSpeed_IdleWalk, Settings.DoubleJumpScalableForwardSpeed_IdleWalk);
        }

        private bool TryExecuteCoyoteJump()
        {
            // Coyote jump has priority over double jump when both conditions might be met
            bool canCoyoteJump = Controller.IsJumpRequested() &&
                                !Controller.IsJumpConsumed() &&
                                Controller.TimeSinceLastAbleToJump > 0f &&
                                Controller.TimeSinceLastAbleToJump <= Settings.JumpPostGroundingGraceTime &&
                                !Motor.GroundingStatus.FoundAnyGround;

            if (canCoyoteJump)
            {
                ExecuteCoyoteJump();
                return true;
            }

            return false;
        }

        private void ExecuteCoyoteJump()
        {
            var (upSpeed, forwardSpeed) = GetCoyoteJumpSpeeds();

            Controller.ExecuteJump(upSpeed, forwardSpeed, Controller.MoveInputVector);

            _currentAirborneState = CharacterMovementState.Jumping;
            Controller.SetMovementState(CharacterMovementState.Jumping);
        }

        private (float upSpeed, float forwardSpeed) GetCoyoteJumpSpeeds()
        {
            float speedTier = Controller.LastGroundedSpeedTier;

            if (speedTier >= Settings.MaxSprintSpeed * 0.9f)
                return (Settings.JumpUpSpeed_Sprint, Settings.JumpScalableForwardSpeed_Sprint);

            if (speedTier >= Settings.MaxJogSpeed * 0.9f)
                return (Settings.JumpUpSpeed_Jog, Settings.JumpScalableForwardSpeed_Jog);

            return (Settings.JumpUpSpeed_IdleWalk, Settings.JumpScalableForwardSpeed_IdleWalk);
        }

        private void ApplyAirMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            if (Controller.MoveInputVector.sqrMagnitude <= 0f) return;

            float targetAirMoveSpeed = CalculateTargetAirMoveSpeed();
            Vector3 addedVelocity = Controller.MoveInputVector * Settings.AirAccelerationSpeed * deltaTime;
            Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

            if (currentVelocityOnInputsPlane.magnitude < targetAirMoveSpeed)
            {
                Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, targetAirMoveSpeed);
                addedVelocity = newTotal - currentVelocityOnInputsPlane;
            }
            else
            {
                // Allow air strafing
                if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                {
                    addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);
                }
            }
            
            currentVelocity += addedVelocity;
        }

        private float CalculateTargetAirMoveSpeed()
        {
            float groundedSpeedTier = Controller.LastGroundedSpeedTier;
            float targetSpeed = Mathf.Min(groundedSpeedTier, Settings.MaxAirMoveSpeed);
            float minimumAirSpeed = Settings.MaxWalkSpeed * 0.5f;

            return Mathf.Max(targetSpeed, minimumAirSpeed);
        }

        private void ApplyAirPhysics(ref Vector3 currentVelocity, float deltaTime)
        {
            // Enforce speed cap
            EnforceHorizontalSpeedCap(ref currentVelocity, deltaTime);

            // Apply gravity and drag
            currentVelocity += Settings.Gravity * deltaTime;
            currentVelocity *= (1f / (1f + (Settings.Drag * deltaTime)));
        }

        private void EnforceHorizontalSpeedCap(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

            if (horizontalVelocity.magnitude > Settings.MaxAirMoveSpeed)
            {
                float decelerationRate = Settings.AirAccelerationSpeed * 2f;
                float currentHorizontalSpeed = horizontalVelocity.magnitude;
                float newHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, Settings.MaxAirMoveSpeed, decelerationRate * deltaTime);

                Vector3 verticalVelocity = Vector3.Project(currentVelocity, Motor.CharacterUp);
                currentVelocity = horizontalVelocity.normalized * newHorizontalSpeed + verticalVelocity;
            }
        }


        private void UpdateAirborneStateTransitions(bool executedJumpThisFrame)
        {
            // Handle transition TO double jumping first
            if (executedJumpThisFrame && _currentAirborneState != CharacterMovementState.DoubleJumping)
            {
                // If we just executed a double jump, make sure we're in the right state
                if (Controller.IsDoubleJumpConsumed() && Controller.IsJumpConsumed())
                {
                    _currentAirborneState = CharacterMovementState.DoubleJumping;
                    Controller.SetMovementState(CharacterMovementState.DoubleJumping);
                    return;
                }
            }
            
            // Don't transition to falling if we just executed a jump or if we're in a jumping state and still moving upward
            bool shouldStayInJumpingState = executedJumpThisFrame || 
                                        ((_currentAirborneState == CharacterMovementState.WallJumping || 
                                            _currentAirborneState == CharacterMovementState.BulletJumping) && 
                                        Motor.Velocity.y > -0.5f);
                                        
            bool isInJumpingState = _currentAirborneState == CharacterMovementState.Jumping || 
                                _currentAirborneState == CharacterMovementState.DoubleJumping || 
                                _currentAirborneState == CharacterMovementState.WallJumping ||
                                _currentAirborneState == CharacterMovementState.BulletJumping;

            if (!shouldStayInJumpingState && isInJumpingState && Motor.Velocity.y < -0.5f)
            {
                _currentAirborneState = CharacterMovementState.Falling;
                Controller.SetMovementState(CharacterMovementState.Falling);
            }
        }

        #endregion
    }
}

