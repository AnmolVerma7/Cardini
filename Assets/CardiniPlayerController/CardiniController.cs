using UnityEngine;
using KinematicCharacterController;
using System.Collections.Generic;

namespace Cardini.Motion
{
    public class CardiniController : MonoBehaviour, ICharacterController
    {
        [Header("Core References")]
        public KinematicCharacterMotor Motor;
        public InputBridge inputBridge; // Assumes InputBridge is also in Cardini.Motion or global
        public BaseLocomotionSettingsSO Settings;

        [Header("Object References")]
        public Transform MeshRoot;
        public Transform CameraFollowPoint;

        [Header("Collision Filtering")]
        public List<Collider> IgnoredColliders = new List<Collider>();

        [Header("Runtime State (Debug)")]
        [SerializeField] private CharacterState _currentMajorState = CharacterState.Locomotion; // High-level state
        [SerializeField] private CharacterMovementState _currentMovementState = CharacterMovementState.Idle; // Detailed movement state
        [SerializeField] private bool _isSprinting;
        [SerializeField] private bool _isCrouching;
        [SerializeField] private bool _shouldBeCrouching;
        [SerializeField] private float _currentSpeedTierForJump;
        [SerializeField] private float _lastGroundedSpeedTier;
        [SerializeField] private Vector3 _lastMoveDirection;

        private Collider[] _probedColliders = new Collider[8];
        private Vector3 _moveInputVector;
        private Vector3 _lookInputVector;
        private bool _jumpRequested = false;
        private bool _jumpConsumed = false;
        private bool _jumpedThisFrame = false;
        private float _timeSinceJumpRequested = Mathf.Infinity;
        private float _timeSinceLastAbleToJump = 0f;
        private Vector3 _internalVelocityAdd = Vector3.zero;

        private bool _sprintToggleActive = false;
        private bool _crouchToggleActive = false;

        private void Awake()
        {
            if (Motor == null) Motor = GetComponent<KinematicCharacterMotor>();
            Motor.CharacterController = this;

            if (inputBridge == null) inputBridge = GetComponentInParent<InputBridge>() ?? GetComponent<InputBridge>();
            if (Settings == null) Debug.LogError("CardiniController: BaseLocomotionSettingsSO not assigned!", this);
            if (inputBridge == null) Debug.LogError("CardiniController: InputBridge not found/assigned!", this);


            SetMajorState(CharacterState.Locomotion);
            SetMovementState(CharacterMovementState.Idle);
        }

        // --- Controller Inputs Struct ---
        public struct ControllerInputs
        {
            public Vector2 MoveAxes;
            public Quaternion CameraRotation;
            public bool JumpPressed;
        }

        public void SetControllerInputs(ref ControllerInputs inputs)
        {
            if (_currentMajorState != CharacterState.Locomotion)
            {
                _moveInputVector = Vector3.zero;
                _lookInputVector = Motor.CharacterForward; // Or maintain last look
                _jumpRequested = false;
                _isSprinting = false;
                _shouldBeCrouching = false;
                return;
            }

            if (Settings == null) return;

            Vector3 moveInputVectorRaw = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxes.x, 0f, inputs.MoveAxes.y), 1f);
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
            if (cameraPlanarDirection.sqrMagnitude == 0f)
            {
                cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
            }
            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);
            _moveInputVector = cameraPlanarRotation * moveInputVectorRaw;

            if (Settings.OrientationMethod == CardiniOrientationMethod.TowardsCamera)
            {
                _lookInputVector = cameraPlanarDirection;
            }
            else if (Settings.OrientationMethod == CardiniOrientationMethod.TowardsMovement)
            {
                if (_moveInputVector.sqrMagnitude > 0f)
                {
                    _lastMoveDirection = _moveInputVector.normalized;
                    _lookInputVector = _lastMoveDirection;
                }
                else
                {
                    _lookInputVector = _lastMoveDirection;  // Keep facing the last movement direction
                }
            }

            if (inputs.JumpPressed)
            {
                _timeSinceJumpRequested = 0f;
                _jumpRequested = true;
            }

            if (Settings.UseToggleSprint)
            {
                if (inputBridge.Sprint.IsPressed) _sprintToggleActive = !_sprintToggleActive;
                _isSprinting = _sprintToggleActive && _moveInputVector.sqrMagnitude > 0.01f;
            }
            else
            {
                _isSprinting = inputBridge.Sprint.IsHeld;
            }

            if (Settings.UseToggleCrouch)
            {
                if (inputBridge.Crouch.IsPressed) _crouchToggleActive = !_crouchToggleActive;
                _shouldBeCrouching = _crouchToggleActive;
            }
            else
            {
                _shouldBeCrouching = inputBridge.Crouch.IsHeld;
            }
        }

        // --- ICharacterController Callbacks ---
        // (UpdateRotation, UpdateVelocity, AfterCharacterUpdate, etc., remain largely the same
        //  but will eventually be driven by the _currentMovementState)
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (Settings == null || _currentMajorState != CharacterState.Locomotion) return;

            if (_lookInputVector.sqrMagnitude > 0f && Settings.OrientationSharpness > 0f)
            {
                // Smoothly interpolate from current to target look direction
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-Settings.OrientationSharpness * deltaTime)).normalized;

                // Set the current rotation (which will be used by the KinematicCharacterMotor)
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
            }

            // Handle bonus rotation (e.g., aligning to ground slope)
            if (Settings.BonusOrientation == CardiniBonusOrientationMethod.TowardsGravity)
            {
                Vector3 targetUpDirection = -Settings.Gravity.normalized;
                Vector3 currentUp = Motor.CharacterUp;
                Quaternion rotationFromCurrentUp = Quaternion.FromToRotation(currentUp, targetUpDirection);
                currentRotation = rotationFromCurrentUp * currentRotation;
            }
            else if (Settings.BonusOrientation == CardiniBonusOrientationMethod.TowardsGroundSlopeAndGravity)
            {
                if (Motor.GroundingStatus.IsStableOnGround)
                {
                    Vector3 targetUpDirection = Motor.GroundingStatus.GroundNormal;
                    Vector3 currentUp = Motor.CharacterUp;
                    Quaternion rotationFromCurrentUp = Quaternion.FromToRotation(currentUp, targetUpDirection);
                    currentRotation = rotationFromCurrentUp * currentRotation;
                }
                else
                {
                    Vector3 targetUpDirection = -Settings.Gravity.normalized;
                    Vector3 currentUp = Motor.CharacterUp;
                    Quaternion rotationFromCurrentUp = Quaternion.FromToRotation(currentUp, targetUpDirection);
                    currentRotation = rotationFromCurrentUp * currentRotation;
                }
            }
        }

        // Example: UpdateVelocity will start checking _currentMovementState
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (Settings == null || _currentMajorState != CharacterState.Locomotion)
            {
                // If not in locomotion, maybe apply gravity only or freeze
                if (_currentMajorState != CharacterState.Locomotion && !Motor.GroundingStatus.IsStableOnGround)
                {
                    currentVelocity += Settings.Gravity * deltaTime; // Apply gravity if airborne
                }
                return;
            }

            // Determine target movement state based on inputs
            UpdateMovementStateDetermination();

            float currentDesiredMaxSpeed;
            float inputMagnitude = _moveInputVector.magnitude;

            if (_isCrouching)
            {
                currentDesiredMaxSpeed = Settings.MaxCrouchSpeed;
            }
            else if (_isSprinting)
            {
                currentDesiredMaxSpeed = Settings.MaxSprintSpeed;
            }
            // Jog/Walk based on _currentMovementState, not directly on inputMagnitude here for speed setting
            else if (_currentMovementState == CharacterMovementState.Jogging)
            {
                currentDesiredMaxSpeed = Settings.MaxJogSpeed;
            }
            else if (_currentMovementState == CharacterMovementState.Walking || _currentMovementState == CharacterMovementState.Idle)
            {
                currentDesiredMaxSpeed = (_currentMovementState == CharacterMovementState.Walking) ? Settings.MaxWalkSpeed : 0f;
            }
            else // Default or other states like Falling, Jumping
            {
                currentDesiredMaxSpeed = Settings.MaxWalkSpeed; // Fallback, or handle per state
                if (_currentMovementState == CharacterMovementState.Falling || _currentMovementState == CharacterMovementState.Jumping)
                {
                    // Air speed is handled differently below
                }
                else if (_currentMovementState == CharacterMovementState.Idle)
                {
                    currentDesiredMaxSpeed = 0f;
                }
            }


            // --- Ground Movement ---
            if (Motor.GroundingStatus.IsStableOnGround)
            {
                // If Idle, force speed to 0 unless specific idle movement is desired
                if (_currentMovementState == CharacterMovementState.Idle)
                {
                    currentDesiredMaxSpeed = 0f;
                }

                float currentVelocityMagnitude = currentVelocity.magnitude;
                Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;
                currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;
                Vector3 reorientedInput = _moveInputVector;
                if (_moveInputVector.sqrMagnitude > 0f)
                {
                    Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
                    reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * inputMagnitude;
                }
                Vector3 targetMovementVelocity = reorientedInput * currentDesiredMaxSpeed;
                currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-Settings.StableMovementSharpness * deltaTime));
            }
            // --- Air Movement ---
            else
            {
                if (_moveInputVector.sqrMagnitude > 0f)
                {
                    Vector3 addedVelocity = _moveInputVector * Settings.AirAccelerationSpeed * deltaTime;
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
                    currentVelocity += addedVelocity;
                }
                currentVelocity += Settings.Gravity * deltaTime;
                currentVelocity *= (1f / (1f + (Settings.Drag * deltaTime)));
            }

            // --- Jumping ---
            _jumpedThisFrame = false;
            _timeSinceJumpRequested += deltaTime;

            if (_jumpRequested && _timeSinceJumpRequested > Settings.JumpPreGroundingGraceTime)
            {
                _jumpRequested = false;
            }
            if (_jumpRequested)
            {
                float actualJumpUpSpeed = Settings.JumpUpSpeed_IdleWalk;
                float actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_IdleWalk;

                // Use _currentSpeedTierForJump which is now set based on movement state
                if (_timeSinceLastAbleToJump <= Settings.JumpPostGroundingGraceTime && _timeSinceLastAbleToJump > 0) // Coyote Jump
                {
                    actualJumpUpSpeed = _lastGroundedSpeedTier >= Settings.MaxSprintSpeed * 0.9f ? 
                    Settings.JumpUpSpeed_Sprint : _lastGroundedSpeedTier >= Settings.MaxJogSpeed * 0.9f ? 
                    Settings.JumpUpSpeed_Jog : Settings.JumpUpSpeed_IdleWalk;
                
                    actualJumpForwardSpeed = _lastGroundedSpeedTier >= Settings.MaxSprintSpeed * 0.9f ? 
                    Settings.JumpScalableForwardSpeed_Sprint : _lastGroundedSpeedTier >= Settings.MaxJogSpeed * 0.9f ? 
                    Settings.JumpScalableForwardSpeed_Jog : Settings.JumpScalableForwardSpeed_IdleWalk;
                }
                else if (_currentSpeedTierForJump >= Settings.MaxSprintSpeed * 0.9f) // Check against sprint speed
                {
                    actualJumpUpSpeed = Settings.JumpUpSpeed_Sprint;
                    actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_Sprint;
                }
                else if (_currentSpeedTierForJump >= Settings.MaxJogSpeed * 0.9f) // Check against jog speed
                {
                    actualJumpUpSpeed = Settings.JumpUpSpeed_Jog;
                    actualJumpForwardSpeed = Settings.JumpScalableForwardSpeed_Jog;
                }
                // Default is IdleWalk, already set (covers idle and walk jumps)


                if (!_jumpConsumed && ((Settings.AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) || _timeSinceLastAbleToJump <= Settings.JumpPostGroundingGraceTime))
                {
                    Vector3 jumpDirection = Motor.CharacterUp;
                    if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                    {
                        jumpDirection = Motor.GroundingStatus.GroundNormal;
                    }
                    Motor.ForceUnground();
                    currentVelocity += (jumpDirection * actualJumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                    currentVelocity += (_moveInputVector.normalized * actualJumpForwardSpeed);
                    _jumpRequested = false;
                    _jumpConsumed = true;
                    _jumpedThisFrame = true;
                    SetMovementState(CharacterMovementState.Jumping); // Set jumping state
                }
            }

            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }
        
        public void AfterCharacterUpdate(float deltaTime)
        {
            if (Settings == null || _currentMajorState != CharacterState.Locomotion) return;

            if (Settings.AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround)
            {
                if (!_jumpedThisFrame) // If we didn't just jump this frame...
                {
                    _jumpConsumed = false; // ... then reset the consumed flag, allowing another jump.
                }
                _timeSinceLastAbleToJump = 0f;
            }
            else
            {
                // Add this else block for tracking coyote time
                _timeSinceLastAbleToJump += deltaTime;
            }

            // Crouching capsule and mesh scaling
            // _isCrouching is the actual physical state of the capsule
            // _shouldBeCrouching is the desired state from input
            // This logic handles transitions between them and obstacle checks
            if (_isCrouching && !_shouldBeCrouching)
            {
                Motor.SetCapsuleDimensions(Motor.Capsule.radius, Settings.DefaultCapsuleHeight, Settings.DefaultCapsuleHeight * 0.5f);
                if (Motor.CharacterOverlap(Motor.TransientPosition, Motor.TransientRotation, _probedColliders, Motor.CollidableLayers, QueryTriggerInteraction.Ignore) > 0)
                {
                    Motor.SetCapsuleDimensions(Motor.Capsule.radius, Settings.CrouchedCapsuleHeight, Settings.CrouchedCapsuleHeight * 0.5f);
                }
                else
                {
                    if (MeshRoot) MeshRoot.localScale = Vector3.one;
                    _isCrouching = false;
                    // Potentially transition out of Crouching movement state here if not already handled
                    if (_currentMovementState == CharacterMovementState.Crouching) UpdateMovementStateDetermination();
                }
            }
            else if (!_isCrouching && _shouldBeCrouching)
            {
                _isCrouching = true;
                Motor.SetCapsuleDimensions(Motor.Capsule.radius, Settings.CrouchedCapsuleHeight, Settings.CrouchedCapsuleHeight * 0.5f);
                if (MeshRoot) MeshRoot.localScale = new Vector3(MeshRoot.localScale.x, Settings.CrouchedCapsuleHeight / Settings.DefaultCapsuleHeight, MeshRoot.localScale.z);
                SetMovementState(CharacterMovementState.Crouching); // Set crouching state
            }

            // Update final movement state if grounded and not jumping/crouching
            if (Motor.GroundingStatus.IsStableOnGround && _currentMovementState != CharacterMovementState.Jumping && _currentMovementState != CharacterMovementState.Crouching)
            {
                 UpdateMovementStateDetermination(); // Recalculate based on current speed/input after physics
            }
            else if (!Motor.GroundingStatus.IsStableOnGround && _currentMovementState != CharacterMovementState.Jumping)
            {
                SetMovementState(CharacterMovementState.Falling);
            }
        }


        // Method to determine and set the detailed movement state
        private void UpdateMovementStateDetermination()
        {
            if (_currentMajorState != CharacterState.Locomotion) return;
            if (!Motor.GroundingStatus.IsStableOnGround && _currentMovementState != CharacterMovementState.Jumping) { // If airborne and not already jumping
                SetMovementState(CharacterMovementState.Falling);
                return;
            }
            if (_isCrouching) { // Use the authoritative _isCrouching state
                SetMovementState(CharacterMovementState.Crouching);
                _currentSpeedTierForJump = Settings.MaxCrouchSpeed; // Update jump tier
                return;
            }
            if (_isSprinting) {
                SetMovementState(CharacterMovementState.Sprinting);
                _currentSpeedTierForJump = Settings.MaxSprintSpeed;
                return;
            }

            float inputMag = _moveInputVector.magnitude;
            if (inputMag > Settings.JogThreshold) {
                SetMovementState(CharacterMovementState.Jogging);
                _currentSpeedTierForJump = Settings.MaxJogSpeed;
            } else if (inputMag > Settings.WalkThreshold) {
                SetMovementState(CharacterMovementState.Walking);
                _currentSpeedTierForJump = Settings.MaxWalkSpeed;
            } else {
                SetMovementState(CharacterMovementState.Idle);
                _currentSpeedTierForJump = 0f; // Or Settings.MaxWalkSpeed for idle->jump
            }
        }


        // State Management (using our new enums)
        public void SetMajorState(CharacterState newState)
        {
            if (_currentMajorState == newState) return;
            // Add OnExit/OnEnter logic for major states if needed
            _currentMajorState = newState;
            Debug.Log($"Major State changed to: {newState}");
        }

        public void SetMovementState(CharacterMovementState newMoveState)
        {
            if (_currentMovementState == newMoveState) return;
            // Add OnExit/OnEnter logic for movement states if needed for animations etc.
            // For example: PlayerAnimator.SetMovementState(newMoveState);
            _currentMovementState = newMoveState;
            // Debug.Log($"Movement State changed to: {newMoveState}");
        }

        // Public property to expose current movement state
        public CharacterMovementState CurrentMovementState => _currentMovementState;

        // ... (Rest of ICharacterController methods: PostGroundingUpdate, IsColliderValidForCollisions, etc. as before) ...
        // ... Ensure they don't have dependencies on KCC.Examples enums ...
        public void PostGroundingUpdate(float deltaTime)
        {
            if (_currentMajorState != CharacterState.Locomotion) return;

            if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround) // Just Landed
            {
                OnLanded();
                if (_currentMovementState == CharacterMovementState.Jumping || _currentMovementState == CharacterMovementState.Falling)
                {
                    UpdateMovementStateDetermination(); // This will set state to Idle/Walking/etc.
                }
            }
            else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
            {
                OnLeaveStableGround();
                // If not already jumping (e.g. walked off edge), transition to Falling
                if (_currentMovementState != CharacterMovementState.Jumping)
                {
                     SetMovementState(CharacterMovementState.Falling);
                }
            }
        }
        public bool IsColliderValidForCollisions(Collider coll) => !IgnoredColliders.Contains(coll);
        public void AddVelocity(Vector3 velocity) => _internalVelocityAdd += velocity;
        public void BeforeCharacterUpdate(float deltaTime) { }
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
        protected void OnLanded() { /* PlayerAnimator.TriggerLanded(); */ }
        protected void OnLeaveStableGround()
        { 
            _lastGroundedSpeedTier = _currentSpeedTierForJump;
        }
        public void OnDiscreteCollisionDetected(Collider hitCollider) { }
    }
}