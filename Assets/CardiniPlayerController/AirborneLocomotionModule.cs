// AirborneLocomotionModule.cs
using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    public class AirborneLocomotionModule : MovementModuleBase
    {
        private bool _initiatedByJumpThisFrame = false; // Was this airborne state started by a jump this activation?

        public override int Priority => 0; // Same base priority as Grounded for now

        public override CharacterMovementState AssociatedPrimaryMovementState
        {
            get
            {
                // If we still have upward velocity from a jump or were recently jumped
                if (Controller._jumpedThisFrame || Motor.Velocity.y > 0.1f && Controller.CurrentMovementState == CharacterMovementState.Jumping)
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
                   Controller.CurrentMajorState == CharacterState.Locomotion;
        }

        public override void OnEnterState()
        {
            // Check if the Controller flagged that this airborne state was due to a jump execution
            // This flag would be set by CardiniController right before activating this module
            // if ExecuteJump() was called.
            _initiatedByJumpThisFrame = Controller.ConsumeJumpExecutionIntent(); // New method in CardiniController

            if (_initiatedByJumpThisFrame)
            {
                Controller.SetMovementState(CharacterMovementState.Jumping);
                Controller.SetJumpedThisFrame(true); // Let controller know a jump was physically executed
            }
            else
            {
                // If not initiated by jump, means we fell off an edge
                Controller.SetMovementState(CharacterMovementState.Falling);
                Controller.SetLastGroundedSpeedTier(Controller._currentSpeedTierForJump); // Store speed before falling
            }
             Controller._timeSinceLastAbleToJump = 0.001f; // Start counting time since last able to jump (now airborne)
        }

        public override void OnExitState()
        {
            _initiatedByJumpThisFrame = false;
            // Controller._jumpedThisFrame will be reset by CardiniController in its AfterCharacterUpdate
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            // Typically, air rotation is similar to ground rotation logic
            Controller.HandleStandardRotation(ref currentRotation, deltaTime);
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (Settings == null) return;

            // --- Handle Coyote Jump Initiation (if requested and conditions met) ---
            if (Controller.IsJumpRequested() && 
                !Controller.IsJumpConsumed() && // Haven't already jumped in this airtime
                Controller._timeSinceLastAbleToJump > 0f && // Must have been airborne for at least a tick
                Controller._timeSinceLastAbleToJump <= Settings.JumpPostGroundingGraceTime &&
                !Motor.GroundingStatus.FoundAnyGround) // Double check we are truly off ground
            {
                // This is a Coyote Jump
                float jumpSpeedTierForCoyote = Controller._lastGroundedSpeedTier; // Use speed before falling

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
                Controller.ExecuteJump(actualJumpUpSpeed, actualJumpForwardSpeed, Controller._moveInputVector);
                // ExecuteJump already sets _jumpConsumed, _jumpedThisFrame, _jumpRequested = false

                // Set current movement state directly here, as ExecuteJump might not know the context
                Controller.SetMovementState(CharacterMovementState.Jumping); 
            }

            // --- Jump Execution (if flagged by OnEnterState) ---
            // This part is now handled by CardiniController.ExecuteJump() which adds to _internalVelocityAdd
            // This module now focuses on maintaining air physics *after* that initial impulse.
            // So, _initiatedByJumpThisFrame is more for setting the initial CharacterMovementState.Jumping.

            // --- Air Movement Logic (from CardiniController) ---
            if (Controller._moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 addedVelocity = Controller._moveInputVector * Settings.AirAccelerationSpeed * deltaTime;
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

            // --- Handle buffered jump request (if any) ---
            // This is where a double jump or similar could be processed if _jumpRequested is true
            // For now, we assume single jump from ground.
        }

        public override void AfterCharacterUpdate(float deltaTime)
        {
            if (Settings == null) return;

            // --- Jump State Flag Resets (from CardiniController) ---
            // Note: Controller._jumpedThisFrame is reset by CardiniController itself.
            // _jumpConsumed is reset by CardiniController when grounded and not _jumpedThisFrame.

            // Increment time since last able to jump (since we are airborne)
            Controller._timeSinceLastAbleToJump += deltaTime;
            
            // If a jump was requested but not consumed (e.g., buffered jump, or failed double jump)
            // and the buffer time has passed, clear the request.
            // This specific line for JumpPreGroundingGraceTime is more for jumps *before* landing,
            // which is now handled by CardiniController's main AfterCharacterUpdate.
            // if (Controller.IsJumpRequested() && Controller._timeSinceJumpRequested > Settings.JumpPreGroundingGraceTime)
            // {
            //    Controller.ConsumeJumpRequest();
            // }
        }

        public override void PostGroundingUpdate(float deltaTime)
        {
            // This module is active only when airborne.
            // If we JUST LANDED, CardiniController's ManageModuleTransitions should have already
            // switched to GroundedLocomotionModule before this module's PostGroundingUpdate is called.
            // However, if there's a frame delay, we might catch it here.
            if (Motor.GroundingStatus.IsStableOnGround)
            {
                Controller.OnLandedInternal(); 
                // Controller will handle transition back to GroundedModule.
            }
        }
    }
}