using UnityEngine;


namespace Cardini.Motion
{
    /// <summary>
    /// Handles sliding locomotion with physics-based movement, steering, and bullet jump mechanics.
    /// Provides momentum-based sliding with slope influence and smooth player control.
    /// </summary>
    public class SlideModule : MovementModuleBase
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
        [Header("Slide Cooldown")]
        [Tooltip("Cooldown time (seconds) after a slide before you can slide again")]
        public float slideCooldown = 1.0f;

        [Header("Bullet Jump Settings")]
        [Tooltip("Speed threshold for bullet jump boost")]
        public float bulletJumpSpeedThreshold = 8f;

        [Tooltip("Upward jump boost multiplier for bullet jumps")]
        public float bulletJumpUpBoost = 1.2f;

        [Tooltip("Forward jump boost multiplier for bullet jumps")]
        public float bulletJumpForwardBoost = 1.3f;

        // Private state variables
        private float _slideTimer;
        private Vector3 _slideDirection;
        private float _initialSlideSpeed;
        private bool _canChainIntoOtherMoves;
        private bool _shouldExitSlide;
        private bool _isCurrentlySliding = false;
        private float _lastSlideEndTime = -Mathf.Infinity;

        public override int Priority => 1; // Higher than grounded/airborne
        public override CharacterMovementState AssociatedPrimaryMovementState => CharacterMovementState.Sliding;

        public override bool CanEnterState()
        {
            // If already sliding, check if we should continue
            if (_isCurrentlySliding)
            {
                return !_shouldExitSlide;
            }

            return CanInitiateSlide();
        }

        public override void OnEnterState()
        {
            InitializeSlide();
        }

        public override void OnExitState()
        {
            TerminateSlide();
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

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            _slideTimer += deltaTime;

            CheckExitConditions();
            
            float exitMultiplier = _shouldExitSlide ? 0.8f : 1.0f;
            float currentSlideSpeed = CalculateSlideSpeed(exitMultiplier);
            
            HandleSlideJump();
            ApplySlidePhysics(ref currentVelocity, currentSlideSpeed, deltaTime);
        }

        public override void BeforeCharacterUpdate(float deltaTime)
        {
            HandleSlideControls();
        }

        public override void AfterCharacterUpdate(float deltaTime) { }
        public override void PostGroundingUpdate(float deltaTime) { }

        #region Private Methods - State Management

        private bool CanInitiateSlide()
        {
            if (!CommonChecks()) return false;
            if (!Controller.IsSlideInitiationRequested) return false;
            if (!HasSufficientSpeed()) return false;
            if (!IsOnValidSurface()) return false;

            // Consume request if on cooldown to prevent "arming"
            if (Time.time < _lastSlideEndTime + slideCooldown)
            {
                Controller.ConsumeSlideInitiation();
                return false;
            }

            return true;
        }

        private bool HasSufficientSpeed()
        {
            if (Controller.IsSprinting && Controller.MoveInputVector.magnitude > 0.1f)
            {
                return Settings.MaxSprintSpeed > minSlideSpeed;
            }
            else
            {
                return Motor.BaseVelocity.magnitude > minSlideSpeed;
            }
        }

        private bool IsOnValidSurface()
        {
            if (!Motor.GroundingStatus.IsStableOnGround) return false;
            
            if (Motor.GroundingStatus.FoundAnyGround)
            {
                float slopeAngle = Vector3.Angle(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal);
                return slopeAngle <= maxSlideSlope;
            }
            
            return true;
        }

        private void InitializeSlide()
        {
            _isCurrentlySliding = true;
            _slideTimer = 0f;
            _shouldExitSlide = false;

            DetermineSlideDirection();
            SetupSlideCapsule();
            UpdateControllerState();
            UpdateAnimations();
            
            Controller.ConsumeSlideInitiation();
        }

        private void DetermineSlideDirection()
        {
            if (Motor.BaseVelocity.magnitude > 0.5f)
            {
                _slideDirection = Motor.BaseVelocity.normalized;
                _initialSlideSpeed = Motor.BaseVelocity.magnitude * slideSpeedBoost;
            }
            else if (Controller.MoveInputVector.magnitude > 0.1f)
            {
                _slideDirection = Controller.MoveInputVector.normalized;
                _initialSlideSpeed = Settings.MaxSprintSpeed * slideSpeedBoost;
            }
            else
            {
                _slideDirection = Motor.CharacterForward;
                _initialSlideSpeed = Settings.MaxSprintSpeed * slideSpeedBoost;
            }
        }

        private void SetupSlideCapsule()
        {
            float slideHeight = Settings.CrouchedCapsuleHeight * slideCapsuleHeight;
            float slideYOffset = slideCapsuleHeight * 0.5f;
            Motor.SetCapsuleDimensions(Motor.Capsule.radius, slideHeight, slideYOffset);
        }

        private void UpdateControllerState()
        {
            Controller.SetCrouchingState(true);
            _canChainIntoOtherMoves = _initialSlideSpeed > minSlideSpeed * 1.5f;
        }

        private void UpdateAnimations()
        {
            PlayerAnimator?.SetSliding(true);
        }

        private void TerminateSlide()
        {
            _isCurrentlySliding = false;
            _shouldExitSlide = false;
            _lastSlideEndTime = Time.time;

            ResetCapsule();
            ResetControllerState();
            ResetAnimations();
        }

        private void ResetCapsule()
        {
            Motor.SetCapsuleDimensions(
                Motor.Capsule.radius,
                Settings.DefaultCapsuleHeight,
                Settings.DefaultCapsuleHeight * 0.5f
            );
        }

        private void ResetControllerState()
        {
            Controller.SetCrouchingState(false);
        }

        private void ResetAnimations()
        {
            PlayerAnimator?.SetSliding(false);
        }

        #endregion

        #region Private Methods - Slide Mechanics

        private void CheckExitConditions()
        {
            if (_slideTimer >= maxSlideDuration)
                _shouldExitSlide = true;

            if (!Motor.GroundingStatus.FoundAnyGround)
                _shouldExitSlide = true;
        }

        private float CalculateSlideSpeed(float exitMultiplier)
        {
            float slideProgress = Mathf.Clamp01(_slideTimer / maxSlideDuration);
            float frictionFactor = slideFrictionCurve.Evaluate(slideProgress);
            float currentSlideSpeed = _initialSlideSpeed * frictionFactor * exitMultiplier;

            if (currentSlideSpeed < minExitSpeed && !_shouldExitSlide)
            {
                _shouldExitSlide = true;
            }

            return currentSlideSpeed;
        }

        private void HandleSlideJump()
        {
            if (!Controller.IsJumpRequested()) return;

            bool isJumpAllowedByMatrix = Controller.CanTransitionToState(CharacterMovementState.Jumping);
            bool canSlideJump = Motor.GroundingStatus.FoundAnyGround;

            if (!Controller.IsJumpConsumed() && canSlideJump && isJumpAllowedByMatrix)
            {
                ExecuteSlideJump();
            }
            else
            {
                Controller.ConsumeJumpRequest();
            }
        }

        private void ExecuteSlideJump()
        {
            float slideSpeed = CalculateSlideSpeed(1.0f);
            float actualJumpUpSpeed = Settings.JumpUpSpeed_Sprint;
            float actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_Sprint;

            // Apply bullet jump boost if fast enough
            if (slideSpeed > bulletJumpSpeedThreshold)
            {
                actualJumpUpSpeed *= bulletJumpUpBoost;
                actualJumpForwardSpeed *= bulletJumpForwardBoost;
            }

            // Mark as bullet jump before execution (consistent with wall jump)
            Controller.SetBulletJumpedThisFrame(true);
            Controller.SetJumpedThisFrame(true);
            
            Controller.ExecuteJump(actualJumpUpSpeed, actualJumpForwardSpeed, _slideDirection);
            _shouldExitSlide = true;
        }

        private void ApplySlidePhysics(ref Vector3 currentVelocity, float currentSlideSpeed, float deltaTime)
        {
            // Apply slope influence
            ApplySlopeInfluence(ref currentSlideSpeed);

            // Calculate base slide velocity
            Vector3 slideVelocity = _slideDirection * currentSlideSpeed;

            // Add player steering
            ApplyPlayerSteering(ref slideVelocity, deltaTime);

            // Project onto ground plane
            ProjectToGroundPlane(ref slideVelocity);

            // Apply gravity
            currentVelocity = slideVelocity + (Settings.Gravity * deltaTime);
        }

        private void ApplySlopeInfluence(ref float currentSlideSpeed)
        {
            if (!Motor.GroundingStatus.FoundAnyGround) return;

            Vector3 groundNormal = Motor.GroundingStatus.GroundNormal;
            Vector3 gravityDirection = Settings.Gravity.normalized;
            Vector3 slopeDirection = Vector3.ProjectOnPlane(gravityDirection, groundNormal);

            if (slopeDirection.magnitude > 0.1f)
            {
                float slopeFactor = Vector3.Dot(_slideDirection, slopeDirection.normalized);
                currentSlideSpeed += slopeFactor * slopeInfluence * 5f;
                currentSlideSpeed = Mathf.Max(currentSlideSpeed, minExitSpeed * 0.8f);
            }
        }

        private void ApplyPlayerSteering(ref Vector3 slideVelocity, float deltaTime)
        {
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
        }

        private void ProjectToGroundPlane(ref Vector3 slideVelocity)
        {
            if (Motor.GroundingStatus.FoundAnyGround)
            {
                slideVelocity = Motor.GetDirectionTangentToSurface(slideVelocity, Motor.GroundingStatus.GroundNormal) * slideVelocity.magnitude;
            }
        }

        private void HandleSlideControls()
        {
            if (_isCurrentlySliding && _shouldExitSlide)
            {
                Controller.ConsumeSlideInitiation();
            }

            if (Controller.IsSlideCancelRequested)
            {
                _shouldExitSlide = true;
                Controller.ConsumeSlideCancellation();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Check if slide can chain into other moves based on entry conditions
        /// </summary>
        public bool CanChainToOtherMoves()
        {
            return _canChainIntoOtherMoves && _slideTimer > 0.1f;
        }

        #endregion
    }
}