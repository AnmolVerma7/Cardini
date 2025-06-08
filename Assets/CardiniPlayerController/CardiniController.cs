// CardiniController.cs
using UnityEngine;
using KinematicCharacterController;
using System.Collections.Generic;
using System.Linq; // For OrderByDescending

namespace Cardini.Motion
{
    public class CardiniController : MonoBehaviour, ICharacterController
    {
        [Header("Core References")]
        public KinematicCharacterMotor Motor;
        public InputBridge inputBridge;
        public BaseLocomotionSettingsSO Settings;

        [Header("Object References")]
        public Transform MeshRoot; // Modules might need access for scaling etc.
        public Transform CameraFollowPoint;

        [Header("Collision Filtering")]
        public List<Collider> IgnoredColliders = new List<Collider>();

        [Header("Movement Modules")] // Assign GroundedLocomotionModule (and Airborne later) here
        public List<MovementModuleBase> movementModules = new List<MovementModuleBase>();
        private MovementModuleBase _activeMovementModule;

        // --- Publicly Readable States (for Modules, UI, etc.) ---
        // These are now mostly managed by this controller, informed by inputs and module actions
        [Header("Runtime State (Debug)")]
        [SerializeField] public CharacterState CurrentMajorState { get; private set; } = CharacterState.Locomotion;
        [SerializeField] public CharacterMovementState CurrentMovementState { get; private set; } = CharacterMovementState.Idle;
        
        // These are derived from input and settings, readable by modules
        public bool _isSprinting { get; private set; }
        public bool _shouldBeCrouching { get; private set; } // Desired state from input + toggle logic
        
        // This is the authoritative PHYSICAL crouch state, set by modules (typically GroundedModule)
        public bool _isCrouching { get; private set; } 

        public float _currentSpeedTierForJump { get; set; } // Modules can update this (Grounded primarily)
        public float _lastGroundedSpeedTier { get; private set; } // Set when leaving ground

        // Processed Inputs, readable by modules
        public Vector3 _moveInputVector { get; private set; }
        public Vector3 _lookInputVector { get; private set; }


        // Jump related flags managed by Controller, influenced by modules
        public bool _jumpRequested { get; private set; }
        public bool _jumpConsumed { get; private set; }
        private bool _jumpExecutionIntent = false;
        public bool _jumpedThisFrame { get; private set; } // Set by AirborneModule during jump execution
        public float _timeSinceJumpRequested { get; private set; } = Mathf.Infinity;
        public float _timeSinceLastAbleToJump { get; set; } = 0f; // Modules (Airborne) update this

        // Internal KCC variables
        public Collider[] _probedColliders { get; private set; } = new Collider[8]; // Modules might need for CharacterOverlap
        private Vector3 _internalVelocityAdd = Vector3.zero; // For AddVelocity calls

        // Toggle states for sprint/crouch (managed by controller)
        private bool _sprintToggleActive = false;
        private bool _crouchToggleActive = false;


        private void Awake()
        {
            if (Motor == null) Motor = GetComponent<KinematicCharacterMotor>();
            Motor.CharacterController = this; // 'this' (CardiniController) IS the ICharacterController

            if (inputBridge == null) inputBridge = GetComponentInParent<InputBridge>() ?? GetComponent<InputBridge>();
            if (Settings == null) Debug.LogError("CardiniController: BaseLocomotionSettingsSO not assigned!", this);
            if (inputBridge == null) Debug.LogError("CardiniController: InputBridge not found/assigned!", this);

            // Initialize all assigned movement modules
            foreach (var module in movementModules)
            {
                if (module != null) module.Initialize(this);
            }
            // Sort modules by priority (higher priority first)
            movementModules = movementModules.OrderByDescending(m => m.Priority).ToList();


            SetMajorState(CharacterState.Locomotion);
            // Initial movement state will be determined by module activation
        }

        // Internal struct, used by CardiniPlayer
        public struct ControllerInputs
        {
            public Vector2 MoveAxes;
            public Quaternion CameraRotation;
            public bool JumpPressed;
        }

        public void SetControllerInputs(ref ControllerInputs inputs) // Called by CardiniPlayer
        {
            // Process major state input (e.g., ability wheel) first
            HandleMajorStateInputs(); // New method to handle ability wheel, etc.

            if (CurrentMajorState != CharacterState.Locomotion)
            {
                _moveInputVector = Vector3.zero;
                _lookInputVector = Motor.CharacterForward;
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
                // Maintain last look direction if not moving, otherwise update
                if (_moveInputVector.sqrMagnitude > 0.001f) 
                    _lookInputVector = _moveInputVector.normalized;
                else if (_lookInputVector.sqrMagnitude < 0.001f) // If look vector was also zero (e.g. at start)
                    _lookInputVector = cameraPlanarDirection; // Default to camera direction
                // else, _lookInputVector retains its previous value (last movement direction)
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
        
        private void HandleMajorStateInputs()
        {
            if (inputBridge.AbilitySelect.IsHeld && CurrentMajorState != CharacterState.AbilitySelection)
            {
                SetMajorState(CharacterState.AbilitySelection);
                Time.timeScale = 0.1f; // Example
                // TODO: Show UI
            }
            else if (!inputBridge.AbilitySelect.IsHeld && CurrentMajorState == CharacterState.AbilitySelection)
            {
                SetMajorState(CharacterState.Locomotion); // Or previous state
                Time.timeScale = 1f;
                // TODO: Hide UI, process selection
            }
        }


        private void ManageModuleTransitions()
        {
            if (CurrentMajorState != CharacterState.Locomotion && CurrentMajorState != CharacterState.Combat) // Only allow movement modules in these states for now
            {
                if (_activeMovementModule != null)
                {
                    _activeMovementModule.OnExitState();
                    _activeMovementModule = null;
                    SetMovementState(CharacterMovementState.None); // Or a specific "non-locomotion" state
                }
                return;
            }

            MovementModuleBase newActiveModule = null;
            int highestPriority = -1;

            foreach (var module in movementModules)
            {
                if (module.CanEnterState()) // Module determines its own viability
                {
                    if (module.Priority > highestPriority)
                    {
                        newActiveModule = module;
                        highestPriority = module.Priority;
                    }
                    // Basic conflict: if same priority, first one in list wins (can refine later)
                    else if (module.Priority == highestPriority && newActiveModule == null) 
                    {
                        newActiveModule = module;
                    }
                }
            }

            if (newActiveModule != _activeMovementModule)
            {
                if (_activeMovementModule != null)
                {
                    _activeMovementModule.OnExitState();
                }
                _activeMovementModule = newActiveModule;
                if (_activeMovementModule != null)
                {
                    _activeMovementModule.OnEnterState();
                }
            }
            
            // Update the controller's overall movement state based on the active module
            if (_activeMovementModule != null)
            {
                SetMovementState(_activeMovementModule.AssociatedPrimaryMovementState);
            }
            else
            {
                SetMovementState(CharacterMovementState.None); // Or Idle if grounded, Falling if not
            }
        }

        // --- Helper Methods for Modules ---
        public void ExecuteJump(float jumpUpSpeed, float jumpForwardSpeed, Vector3 moveInputForJump)
        {
            Vector3 jumpDirection = Motor.CharacterUp;
            // If jumping from an unstable slope, use ground normal for jump direction
            if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround) 
            {
                jumpDirection = Motor.GroundingStatus.GroundNormal;
            }

            Motor.ForceUnground();
            _internalVelocityAdd += (jumpDirection * jumpUpSpeed) - Vector3.Project(Motor.BaseVelocity, Motor.CharacterUp);
            _internalVelocityAdd += (moveInputForJump.normalized * jumpForwardSpeed);
            
            _jumpRequested = false; 
            _jumpConsumed = true; 
            // _jumpedThisFrame will be set by AirborneModule or a new method if we centralize it.
            _jumpExecutionIntent = true; // Signal that a jump was just executed

            // The state transition to AirborneModule will happen in ManageModuleTransitions
            // due to Motor.ForceUnground() making IsStableOnGround false.
        }

        public bool ConsumeJumpExecutionIntent()
        {
            bool intent = _jumpExecutionIntent;
            _jumpExecutionIntent = false; // Consume it
            return intent;
        }

        public bool IsJumpRequested() => _jumpRequested;
        public bool IsJumpConsumed() => _jumpConsumed;
        public void ConsumeJumpRequest() { _jumpRequested = false; _timeSinceJumpRequested = Mathf.Infinity; }
        public void SetJumpConsumed(bool consumed) => _jumpConsumed = consumed;
        public void SetJumpedThisFrame(bool jumped) => _jumpedThisFrame = jumped; // Airborne module will call this
        public void SetCrouchingState(bool isPhysicallyCrouching) => _isCrouching = isPhysicallyCrouching;
        public void SetLastGroundedSpeedTier(float speedTier) => _lastGroundedSpeedTier = speedTier;

        // Shared Rotation Logic (can be called by modules)
        public void HandleStandardRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (Settings == null) return; // Should not happen if initialized

            if (_lookInputVector.sqrMagnitude > 0f && Settings.OrientationSharpness > 0f)
            {
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-Settings.OrientationSharpness * deltaTime)).normalized;
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
            }
            // Removed Bonus Orientation logic as per your request.
            // Add simple reorient to world up if desired:
            else if (Settings.OrientationSharpness > 0f) // If not actively looking, gently reorient to world up
            {
                 Vector3 currentUp = (currentRotation * Vector3.up);
                 Vector3 smoothedUp = Vector3.Slerp(currentUp, Vector3.up, 1 - Mathf.Exp(-Settings.OrientationSharpness * deltaTime * 0.5f)); // Slower reorient
                 currentRotation = Quaternion.FromToRotation(currentUp, smoothedUp) * currentRotation;
            }
        }

        // --- ICharacterController Implementation (Delegation to Active Module) ---
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (_activeMovementModule != null && CurrentMajorState == CharacterState.Locomotion)
            {
                _activeMovementModule.UpdateRotation(ref currentRotation, deltaTime);
            }
            else 
            {
                HandleStandardRotation(ref currentRotation, deltaTime); // Fallback standard rotation
            }
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            // ManageModuleTransitions(); // Call this once per frame

            if (_activeMovementModule != null && CurrentMajorState == CharacterState.Locomotion)
            {
                _activeMovementModule.UpdateVelocity(ref currentVelocity, deltaTime);
            }
            else // Handle non-locomotion states (e.g. just apply gravity if airborne)
            {
                if (!Motor.GroundingStatus.IsStableOnGround)
                {
                    if (Settings != null) currentVelocity += Settings.Gravity * deltaTime;
                    else currentVelocity += Physics.gravity * deltaTime; // Absolute fallback
                }
            }
            
            // Apply any internal velocity additions (like from jump execution)
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            if (Settings == null) return; // Safety first

            // Reset one-frame jump execution flag (the one set by ExecuteJump)
             _jumpedThisFrame = false; // This controller flag is reset each frame. AirborneModule sets its own internal one.


            if (_activeMovementModule != null && CurrentMajorState == CharacterState.Locomotion)
            {
                _activeMovementModule.AfterCharacterUpdate(deltaTime);
            }
            
            // This logic for jump request timeout is controller-level
            if (_jumpRequested && _timeSinceJumpRequested > Settings.JumpPreGroundingGraceTime)
            {
                _jumpRequested = false;
            }

            // Reset _timeSinceLastAbleToJump if we are now grounded (Module might have done this, but good to be sure)
            // This is more accurately handled by the modules now based on their active state.
            // The GroundedModule sets it to 0 in OnEnterState.
            // The AirborneModule increments it in its AfterCharacterUpdate.
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            ManageModuleTransitions(); // Good place to check for transitions based on new ground state

            if (_activeMovementModule != null && CurrentMajorState == CharacterState.Locomotion)
            {
                _activeMovementModule.PostGroundingUpdate(deltaTime);
            }
             // This is where _timeSinceLastAbleToJump is reset if grounded by a module,
             // or incremented if airborne by a module
        }
        
        public void BeforeCharacterUpdate(float deltaTime) 
        {
            // Prime place to call ManageModuleTransitions once per KCC update cycle
            _timeSinceJumpRequested += deltaTime; // Increment this early
            ManageModuleTransitions();

            if (_activeMovementModule != null && CurrentMajorState == CharacterState.Locomotion)
            {
                _activeMovementModule.BeforeCharacterUpdate(deltaTime);
            }
        }


        // Pass-through or controller-level logic for other ICharacterController methods
        public bool IsColliderValidForCollisions(Collider coll) => !IgnoredColliders.Contains(coll);
        public void AddVelocity(Vector3 velocity) => _internalVelocityAdd += velocity; // Queues external velocity
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { /* Delegate if module needs it */ }
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { /* Delegate if module needs it */ }
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { /* Delegate if module needs it */ }
        public void OnLandedInternal() 
        { 
            // Called by modules upon landing
            SetJumpConsumed(false); // Allow new jump after landing
            _timeSinceLastAbleToJump = 0f;
            // Potentially: PlayerAnimator.TriggerLanded();
        }

        protected void OnLeaveStableGround() { _lastGroundedSpeedTier = _currentSpeedTierForJump; /* Called by AirborneModule when it takes over after ground */ }
        public void OnDiscreteCollisionDetected(Collider hitCollider) { }

        // State Management (using our new enums) - kept public for now
        public void SetMajorState(CharacterState newState)
        {
            if (CurrentMajorState == newState) return;
            CurrentMajorState = newState;
            // Debug.Log($"Major State changed to: {newState}");
        }

        public void SetMovementState(CharacterMovementState newMoveState)
        {
            if (CurrentMovementState == newMoveState) return;
            CurrentMovementState = newMoveState;
            // Debug.Log($"Movement State changed to: {newMoveState}");
        }
    }
}