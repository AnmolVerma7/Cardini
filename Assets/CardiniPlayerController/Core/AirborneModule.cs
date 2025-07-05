using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    public class AirborneLocomotionModule : MovementModuleBase
    {
        private bool _initiatedByJumpThisFrame = false; // Was this airborne state started by a jump this activation?

        public override int Priority => 2; // Same base priority as Grounded for now

        public override CharacterMovementState AssociatedPrimaryMovementState
        {
            get
            {
                // If we still have upward velocity from a jump or were recently jumped
                if (Controller._jumpedThisFrameInternal || Motor.Velocity.y > 0.1f && Controller.CurrentMovementState == CharacterMovementState.Jumping)
                {
                    return CharacterMovementState.Jumping;
                }
                return CharacterMovementState.Falling;
            }
        }

        public override bool CanEnterState()
        {
            // This module can enter if the character is NOT on stable ground
            // AND the major state is Locomotion
            return !Motor.GroundingStatus.IsStableOnGround &&
                   Controller.CurrentMajorState == CharacterState.Locomotion &&
                   CommonChecks();
        }

        public override void OnEnterState()
        {
            // Check if the Controller flagged that this airborne state was due to a jump execution
            // This flag would be set by CardiniController right before activating this module
            // if ExecuteJump() was called.
            _initiatedByJumpThisFrame = Controller.ConsumeJumpExecutionIntent(); // New method in CardiniController

            if (_initiatedByJumpThisFrame)
            {
                // Controller.SetMovementState(CharacterMovementState.Jumping);
                // Controller.SetJumpedThisFrame(true); // Let controller know a jump was physically executed
                // PlayerAnimator?.TriggerJump();

                // **NEW CHECK FOR WALL JUMP FIRST - But it still changes to "Jumping" state**
                if (Controller.WasWallJumpExecuted())
                {
                    Controller.SetMovementState(CharacterMovementState.WallJumping);
                }
                else
                {
                    Controller.SetMovementState(CharacterMovementState.Jumping);
                }
                
                Controller.SetJumpedThisFrame(true);
                PlayerAnimator?.TriggerJump();
            }
            else
            {
                // If not initiated by jump, means we fell off an edge
                Controller.SetMovementState(CharacterMovementState.Falling);
                Controller.SetLastGroundedSpeedTier(Controller.CurrentSpeedTierForJump); // Store speed before falling
            }
            Controller.TimeSinceLastAbleToJump = 0.001f; // Start counting time since last able to jump (now airborne)
            PlayerAnimator?.SetGrounded(false);
        }

        public override void OnExitState()
        {
            _initiatedByJumpThisFrame = false;
            // Make sure we clear any lingering jump states in the animator
            PlayerAnimator?.SetGrounded(true);
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            // Typically, air rotation is similar to ground rotation logic
            Controller.HandleStandardRotation(ref currentRotation, deltaTime);
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        
        {
            if (Settings == null) return;
            // Double Jump Logic Handle double jump
            if (Settings.AllowDoubleJump)
            {
                // Conditions for double jump: jump requested, first jump consumed, double jump not yet consumed, and character is airborne
                if (Controller.IsJumpRequested() && Controller.IsJumpConsumed() && !Controller.IsDoubleJumpConsumed() && !Motor.GroundingStatus.FoundAnyGround)
                {
                    Debug.Log($"[Airborne] DOUBLE JUMP EXECUTION ALLOWED! Current SpeedTier: {Controller.CurrentSpeedTierForJump}");

                    float actualJumpUpSpeed;
                    float actualJumpForwardSpeed;

                    // Tiered double jump speeds based on speed before first jump (LastGroundedSpeedTier)
                    if (Controller.LastGroundedSpeedTier >= Settings.MaxSprintSpeed * 0.9f)
                    {
                        actualJumpUpSpeed = Settings.DoubleJumpUpSpeed_Sprint;
                        actualJumpForwardSpeed = Settings.DoubleJumpScalableForwardSpeed_Sprint;
                    }
                    else if (Controller.LastGroundedSpeedTier >= Settings.MaxJogSpeed * 0.9f)
                    {
                        actualJumpUpSpeed = Settings.DoubleJumpUpSpeed_Jog;
                        actualJumpForwardSpeed = Settings.DoubleJumpScalableForwardSpeed_Jog;
                    }
                    else // Idle/Walk speed for double jump
                    {
                        actualJumpUpSpeed = Settings.DoubleJumpUpSpeed_IdleWalk;
                        actualJumpForwardSpeed = Settings.DoubleJumpScalableForwardSpeed_IdleWalk;
                    }

                    // Tell controller to execute the jump mechanics (this will ForceUnground, add velocity, consume jump request)
                    Controller.ExecuteJump(actualJumpUpSpeed, actualJumpForwardSpeed, Controller.MoveInputVector);
                    
                    Controller.SetDoubleJumpConsumed(true); // Mark double jump as consumed
                    // Controller.SetJumpedThisFrame(true); // ExecuteJump already sets _jumpExecutionIntentInternal which leads to this.

                    Controller.SetMovementState(CharacterMovementState.Jumping); // Explicitly set to Jumping state
                }
            }

            // --- Handle Coyote Jump Initiation (if requested and conditions met) ---
            if (Controller.IsJumpRequested() &&
                !Controller.IsJumpConsumed() && 
                Controller.TimeSinceLastAbleToJump > 0f && // Must have been airborne for at least a tick
                Controller.TimeSinceLastAbleToJump <= Settings.JumpPostGroundingGraceTime &&
                !Motor.GroundingStatus.FoundAnyGround)
            {
                // This is a Coyote Jump
                float jumpSpeedTierForCoyote = Controller.LastGroundedSpeedTier; // Use speed before falling

                float actualJumpUpSpeed = Settings.JumpUpSpeed_IdleWalk; // Default for safety
                float actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_IdleWalk;

                if (jumpSpeedTierForCoyote >= Settings.MaxSprintSpeed * 0.9f)
                {
                    actualJumpUpSpeed = Settings.JumpUpSpeed_Sprint;
                    actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_Sprint;
                }
                else if (jumpSpeedTierForCoyote >= Settings.MaxJogSpeed * 0.9f)
                {
                    actualJumpUpSpeed = Settings.JumpUpSpeed_Jog;
                    actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_Jog;
                }

                // Tell controller to execute the jump mechanics
                Controller.ExecuteJump(actualJumpUpSpeed, actualJumpForwardSpeed, Controller.MoveInputVector);
                // ExecuteJump already sets _jumpConsumed, _jumpedThisFrame, _jumpRequested = false

                // Set current movement state directly here, as ExecuteJump might not know the context
                Controller.SetMovementState(CharacterMovementState.Jumping);
            }

            // --- Jump Execution (if flagged by OnEnterState) ---
            // This part is now handled by CardiniController.ExecuteJump() which adds to _internalVelocityAdd
            // This module now focuses on maintaining air physics *after* that initial impulse.
            // So, _initiatedByJumpThisFrame is more for setting the initial CharacterMovementState.Jumping.

            // --- Air Movement Logic (from CardiniController) ---
            if (Controller.MoveInputVector.sqrMagnitude > 0f)
            {
                Vector3 addedVelocity = Controller.MoveInputVector * Settings.AirAccelerationSpeed * deltaTime;
                Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                if (currentVelocityOnInputsPlane.magnitude < Settings.MaxAirMoveSpeed)
                {
                    Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, Settings.MaxAirMoveSpeed);
                    addedVelocity = newTotal - currentVelocityOnInputsPlane;
                }
                else
                {
                    if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                    {
                        addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);
                    }
                }
                // KCC Example prevents air-climbing sloped walls. We might want this or not.
                // For now, let's keep it simpler. If we add it back, it uses Motor.GroundingStatus.FoundAnyGround
                // and Motor.GroundingStatus.GroundNormal.
                currentVelocity += addedVelocity;
            }

            // --- Apply Gravity ---
            currentVelocity += Settings.Gravity * deltaTime;

            // --- Apply Drag ---
            currentVelocity *= (1f / (1f + (Settings.Drag * deltaTime)));
        }

        public override void AfterCharacterUpdate(float deltaTime)
        {
            if (Settings == null) return;
            Controller.TimeSinceLastAbleToJump += deltaTime;
        }

        public override void PostGroundingUpdate(float deltaTime)
        {
            // This module is active only when airborne.
            if (Motor.GroundingStatus.IsStableOnGround)
            {
                Controller.OnLandedInternal(); 
            }
        }
    }
}