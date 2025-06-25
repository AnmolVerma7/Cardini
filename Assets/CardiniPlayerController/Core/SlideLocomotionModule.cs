using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    /// <summary>
    /// Handles sliding locomotion with physics-based movement, steering, and bullet jump mechanics.
    /// Integrates with the modular character controller system.
    /// </summary>
    public class SlideLocomotionModule : MovementModuleBase
    {
        [Header("Slide Settings")]
        [Tooltip("Multiplier for crouched capsule height during slide (e.g. 0.6 = 60% of crouched height)")]
        public float slideCapsuleHeight = 0.5f;
        [Tooltip("Minimum speed required to initiate a slide")]
        public float minSlideSpeed = 1f;

        [Tooltip("How long the slide lasts at maximum")]
        public float maxSlideDuration = 1.5f;

        [Tooltip("Speed multiplier when slide starts (boost effect)")]
        public float slideSpeedBoost = 1.2f;

        [Tooltip("Curve controlling slide speed over time (0=start, 1=end)")]
        public AnimationCurve slideFrictionCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f);

        [Header("Slide Physics")]
        [Tooltip("How much slope affects slide speed (1 = full gravity effect)")]
        public float slopeInfluence = 0.5f;

        [Tooltip("Maximum slope angle that allows sliding (degrees)")]
        public float maxSlideSlope = 45f;

        [Header("Slide Control")]
        [Tooltip("How much directional control player has during slide")]
        public float slideSteerStrength = 2f;

        [Tooltip("Minimum speed before slide auto-exits")]
        public float minExitSpeed = 2f;


        [Header("Bullet Jump Settings")]
        [Tooltip("Speed threshold for bullet jump boost")]
        public float bulletJumpSpeedThreshold = 8f; // Customize this!

        [Tooltip("Upward jump boost multiplier for bullet jumps")]
        public float bulletJumpUpBoost = 1.2f;

        [Tooltip("Forward jump boost multiplier for bullet jumps")]
        public float bulletJumpForwardBoost = 1.3f;

        // Private variables for slide state
        private float _slideTimer;
        private Vector3 _slideDirection;
        private float _initialSlideSpeed;
        private bool _canChainIntoOtherMoves;
        private bool _shouldExitSlide;
        private bool _isCurrentlySliding = false; // Track if we're actively sliding

        public override int Priority => 1; // Higher than grounded/airborne
        public override CharacterMovementState AssociatedPrimaryMovementState => CharacterMovementState.Sliding;

        // IMPORTANT: Lock velocity during slide to prevent grounded module from interfering
        // Update: DOESNT FUCKING WORK IT SLIDES FOREVER
        // public override bool LocksVelocity => true;

        public override bool CanEnterState()
        {

            // If we're already sliding, check if we should continue
            if (_isCurrentlySliding)
            {
                // If we should exit, this module can't enter anymore
                // This will force the module system to find a new module
                if (_shouldExitSlide)
                {
                    // Debug.Log("[SLIDE] CanEnterState returning FALSE - should exit slide");
                    return false;
                }
                // Otherwise, we can continue sliding
                return true;
            }

            // Otherwise, check if we can start a new slide
            if (!CommonChecks())
            {
                // Debug.Log("Slide: CommonChecks failed");
                return false;
            }

            bool slideInitiated = Controller.IsSlideInitiationRequested;
            // Debug.Log($"Slide: SlideInitiated = {slideInitiated}, Current State: {Controller.CurrentMovementState}");

            if (!slideInitiated)
            {
                return false;
            }


            // Speed check
            bool hasEnoughSpeed = false;
            float currentSpeed = Motor.BaseVelocity.magnitude;

            if (Controller.IsSprinting && Controller.MoveInputVector.magnitude > 0.1f)
            {
                hasEnoughSpeed = Settings.MaxSprintSpeed > minSlideSpeed;
                // Debug.Log($"Slide: Sprint speed check - MaxSprintSpeed: {Settings.MaxSprintSpeed:F2} vs minSlideSpeed: {minSlideSpeed:F2}");
            }
            else
            {
                hasEnoughSpeed = currentSpeed > minSlideSpeed;
                // Debug.Log($"Slide: Current speed check - {currentSpeed:F2} vs {minSlideSpeed:F2}");
            }

            if (!hasEnoughSpeed)
            {
                // Debug.Log("Slide: BLOCKED - Not enough speed");
                return false;
            }

            bool onGround = Motor.GroundingStatus.IsStableOnGround;
            if (!onGround)
            {
                // Debug.Log("Slide: BLOCKED - Not on ground");
                return false;
            }

            bool slopeOk = true;
            if (Motor.GroundingStatus.FoundAnyGround)
            {
                float slopeAngle = Vector3.Angle(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal);
                slopeOk = slopeAngle <= maxSlideSlope;
                if (!slopeOk)
                {
                    // Debug.Log($"Slide: BLOCKED - Slope too steep: {slopeAngle:F2}° > {maxSlideSlope}°");
                    return false;
                }
            }

            // Debug.Log("Slide: ALL CHECKS PASSED - Can enter slide!");
            return true;
        }

        public override void OnEnterState()
        {
            // Debug.Log($"[SLIDE] ENTERING Slide State! Current Speed: {Motor.BaseVelocity.magnitude:F2}");

            _isCurrentlySliding = true;
            _slideTimer = 0f;
            _shouldExitSlide = false;

            // Use movement direction if velocity is too low
            if (Motor.BaseVelocity.magnitude > 0.5f)
            {
                _slideDirection = Motor.BaseVelocity.normalized;
                _initialSlideSpeed = Motor.BaseVelocity.magnitude * slideSpeedBoost;
            }
            else if (Controller.MoveInputVector.magnitude > 0.1f)
            {
                // Use intended direction and speed
                _slideDirection = Controller.MoveInputVector.normalized;
                _initialSlideSpeed = Settings.MaxSprintSpeed * slideSpeedBoost;
                // Debug.Log($"Using intended slide direction: {_slideDirection}, speed: {_initialSlideSpeed:F2}");
            }
            else
            {
                // Fallback to forward direction
                _slideDirection = Motor.CharacterForward;
                _initialSlideSpeed = Settings.MaxSprintSpeed * slideSpeedBoost;
                // Debug.Log($"Using forward slide direction, speed: {_initialSlideSpeed:F2}");
            }

            // Set slide capsule dimensions
            float slideHeight = Settings.CrouchedCapsuleHeight * slideCapsuleHeight;
            float slideYOffset = slideCapsuleHeight * 0.5f;

            // Debug.Log($"[SLIDE] Setting capsule - Height: {slideHeight:F2}, YOffset: {slideYOffset:F2}");
            Motor.SetCapsuleDimensions(Motor.Capsule.radius, slideHeight, slideYOffset);

            // IMPORTANT: Tell the controller we're in a special crouch state
            Controller.SetCrouchingState(true);

            // Clear the initiation flag AFTER we've successfully entered
            Controller.ConsumeSlideInitiation();

            // Tell animator we're sliding
            PlayerAnimator?.SetSliding(true);

            // Determine if we can chain into other moves based on entry speed
            _canChainIntoOtherMoves = _initialSlideSpeed > minSlideSpeed * 1.5f;

            // Debug.Log($"[SLIDE] Slide initialized - Direction: {_slideDirection}, Speed: {_initialSlideSpeed:F2}");
        }

        public override void OnExitState()
        {
            // Debug.Log("[SLIDE] EXITING Slide State!");

            _isCurrentlySliding = false;
            _shouldExitSlide = false;

            // Reset character capsule to normal size
            Motor.SetCapsuleDimensions(
                Motor.Capsule.radius,
                Settings.DefaultCapsuleHeight,
                Settings.DefaultCapsuleHeight * 0.5f
            );

            // Reset crouch state
            Controller.SetCrouchingState(false);
            PlayerAnimator?.SetSliding(false);

            // Debug.Log($"[SLIDE] Reset capsule height to {Settings.DefaultCapsuleHeight:F2}");
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            _slideTimer += deltaTime;


            if (_slideTimer >= maxSlideDuration)
            {
                // Debug.Log("[SLIDE] Duration exceeded");
                _shouldExitSlide = true;
            }

            if (!Motor.GroundingStatus.FoundAnyGround)
            {
                // Debug.Log("[SLIDE] Lost ground contact");
                _shouldExitSlide = true;
            }
            // If we should exit, still calculate velocity for this frame
            // but maybe reduce it slightly for smoother transition
            float exitMultiplier = _shouldExitSlide ? 0.8f : 1.0f;

            // Calculate slide physics
            float slideProgress = Mathf.Clamp01(_slideTimer / maxSlideDuration);
            float frictionFactor = slideFrictionCurve.Evaluate(slideProgress);
            float currentSlideSpeed = _initialSlideSpeed * frictionFactor * exitMultiplier;

            // Check minimum speed
            if (currentSlideSpeed < minExitSpeed && !_shouldExitSlide)
            {
                // Debug.Log($"[SLIDE] Speed too low: {currentSlideSpeed:F2} < {minExitSpeed:F2}");
                _shouldExitSlide = true;
            }

            // --- Handle Jump from Slide ---
            if (Controller.IsJumpRequested())
            {
                bool isJumpAllowedByMatrix = Controller.CanTransitionToState(CharacterMovementState.Jumping);
                bool canSlideJump = Motor.GroundingStatus.FoundAnyGround; // Can jump from slide if touching ground

                if (!Controller.IsJumpConsumed() && canSlideJump && isJumpAllowedByMatrix)
                {
                    // Debug.Log("[SLIDE] Executing jump from slide!");

                    // Use slide speed for jump power calculation
                    float slideSpeed = currentSlideSpeed;
                    float actualJumpUpSpeed = Settings.JumpUpSpeed_Sprint; // Use sprint jump power
                    float actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_Sprint;

                    // Optional: Boost jump if sliding fast (Warframe-style bullet jump)
                    if (slideSpeed > bulletJumpSpeedThreshold)
                    {
                        actualJumpUpSpeed *= bulletJumpUpBoost;
                        actualJumpForwardSpeed *= bulletJumpForwardBoost;
                        // Debug.Log($"[SLIDE] BULLET JUMP! Speed: {slideSpeed:F2}, Boosts: {bulletJumpUpBoost}x up, {bulletJumpForwardBoost}x forward");
                    }

                    Controller.ExecuteJump(actualJumpUpSpeed, actualJumpForwardSpeed, _slideDirection);

                    // Exit slide after jump
                    _shouldExitSlide = true;
                }
                else
                {
                    // Debug.Log($"[SLIDE] Jump blocked - Consumed: {Controller.IsJumpConsumed()}, CanJump: {canSlideJump}, Matrix: {isJumpAllowedByMatrix}");
                    Controller.ConsumeJumpRequest();
                }
            }

            // Apply slope influence
            if (Motor.GroundingStatus.FoundAnyGround)
            {
                Vector3 groundNormal = Motor.GroundingStatus.GroundNormal;
                Vector3 gravityDirection = Settings.Gravity.normalized;
                Vector3 slopeDirection = Vector3.ProjectOnPlane(gravityDirection, groundNormal);

                if (slopeDirection.magnitude > 0.1f)
                {
                    float slopeFactor = Vector3.Dot(_slideDirection, slopeDirection.normalized);
                    currentSlideSpeed += slopeFactor * slopeInfluence * 5f;
                    currentSlideSpeed = Mathf.Max(currentSlideSpeed, minExitSpeed * exitMultiplier);
                }
            }

            // Calculate base slide velocity
            Vector3 slideVelocity = _slideDirection * currentSlideSpeed;

            // Add player steering input
            if (Controller.MoveInputVector.magnitude > 0.1f)
            {
                Vector3 steerInput = Controller.MoveInputVector * slideSteerStrength;
                Vector3 lateralSteer = Vector3.Project(steerInput, Vector3.Cross(Motor.CharacterUp, _slideDirection));
                slideVelocity += lateralSteer * deltaTime;

                // Update slide direction based on new velocity
                if (slideVelocity.magnitude > 0.1f)
                {
                    _slideDirection = slideVelocity.normalized;
                }
            }

            // Project velocity onto ground plane
            if (Motor.GroundingStatus.FoundAnyGround)
            {
                slideVelocity = Motor.GetDirectionTangentToSurface(slideVelocity, Motor.GroundingStatus.GroundNormal) * slideVelocity.magnitude;
            }

            // Apply gravity
            currentVelocity = slideVelocity + (Settings.Gravity * deltaTime);

            // Debug info
            if (Time.frameCount % 30 == 0)
            {
                // Debug.Log($"[SLIDE] Timer: {_slideTimer:F2}s, Speed: {currentSlideSpeed:F2}, Progress: {slideProgress:F2}, ShouldExit: {_shouldExitSlide}");
            }
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            // Smoothly rotate to face slide direction
            if (_slideDirection.magnitude > 0.1f)
            {
                Vector3 flatDirection = Vector3.ProjectOnPlane(_slideDirection, Motor.CharacterUp);
                if (flatDirection.magnitude > 0.1f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(flatDirection, Motor.CharacterUp);
                    currentRotation = Quaternion.Slerp(currentRotation, targetRotation, deltaTime * 8f);
                }
            }
        }

        public override void BeforeCharacterUpdate(float deltaTime)
        {
            // If we're sliding and should exit, force a module transition check
            if (_isCurrentlySliding && _shouldExitSlide)
            {
                // Debug.Log("[SLIDE] Forcing transition check in BeforeCharacterUpdate");
                // The controller will call ManageModuleTransitions in its BeforeCharacterUpdate
                Controller.ConsumeSlideInitiation();
            }

            if (Controller.IsSlideCancelRequested)
            {
                // Debug.Log("[SLIDE] Cancelled by player input");
                _shouldExitSlide = true;
                Controller.ConsumeSlideCancellation();
            }
        }

        public override void AfterCharacterUpdate(float deltaTime)
        {
            // This is called after character update - can be empty for slide
        }

        public override void PostGroundingUpdate(float deltaTime)
        {
            // This is called after grounding update - can be empty for slide
        }

        // Custom method to check if slide can chain into other moves
        public bool CanChainToOtherMoves()
        {
            return _canChainIntoOtherMoves && _slideTimer > 0.2f; // Small delay before chaining
        }
    }
}