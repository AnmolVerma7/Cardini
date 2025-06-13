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
        public AbilityManager abilityManager;

        [Header("Object References")]
        public Transform MeshRoot; // Modules might need access for scaling etc.
        public Transform CameraFollowPoint;

        [Header("Animation")]
        public IPlayerAnimator PlayerAnimator;

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
        public string ActiveModuleName => _activeMovementModule != null ? _activeMovementModule.GetType().Name : "None";

        // These are derived from input and settings, readable by modules
        public bool IsSprinting { get; private set; }
        public bool ShouldBeCrouching { get; private set; } // Desired state from input + toggle logic

        // This is the authoritative PHYSICAL crouch state, set by modules (typically GroundedModule)
        public bool IsCrouching { get; private set; }

        public float CurrentSpeedTierForJump { get; set; } // Modules can update this (Grounded primarily)
        public float LastGroundedSpeedTier { get; private set; } // Set when leaving ground

        // Processed Inputs, readable by modules
        public Vector3 MoveInputVector { get; private set; }
        public Vector3 LookInputVector { get; private set; }


        // Jump related flags managed by Controller, influenced by modules
        public bool _jumpRequestedInternal;
        public bool _jumpConsumedInternal;
        private bool _jumpExecutionIntentInternal = false;
        public bool _jumpedThisFrameInternal;// Set by AirborneModule during jump execution
        public float TimeSinceJumpRequested { get; private set; } = Mathf.Infinity;
        public float TimeSinceLastAbleToJump { get; set; } = 0f; // Modules (Airborne) update this

        // Internal KCC variables
        public Collider[] ProbedColliders_SharedBuffer { get; private set; } = new Collider[8]; // Modules might need for CharacterOverlap
        private Vector3 _internalVelocityAdd = Vector3.zero; // For AddVelocity calls

        // Toggle states for sprint/crouch (managed by controller)
        private bool _sprintToggleActive = false;
        private bool _crouchToggleActive = false;

        [Header("Abilites")]
        private AbilityType _currentWheelTypeToDisplay = AbilityType.Utility;
        private AbilityType _currentWheelTypeBeingDisplayed;

        private void Awake()
        {
            if (Motor == null) Motor = GetComponent<KinematicCharacterMotor>();
            Motor.CharacterController = this; // 'this' (CardiniController) IS the ICharacterController

            if (inputBridge == null) inputBridge = GetComponentInParent<InputBridge>() ?? GetComponent<InputBridge>();
            if (abilityManager == null) abilityManager = GetComponentInChildren<AbilityManager>() ?? GetComponent<AbilityManager>();

            if (Settings == null) Debug.LogError("CardiniController: BaseLocomotionSettingsSO not assigned!", this);
            if (inputBridge == null) Debug.LogError("CardiniController: InputBridge not found/assigned!", this);
            if (abilityManager == null) Debug.LogWarning("CardiniController: AbilityManager not found/assigned!", this);
            if (PlayerAnimator == null)
            {
                if (MeshRoot != null) // Assuming Animator/Bridge is on or under MeshRoot
                {
                    PlayerAnimator = MeshRoot.GetComponentInChildren<IPlayerAnimator>();
                }
                if (PlayerAnimator == null) // Fallback to self if MeshRoot not assigned or Bridge is elsewhere
                {
                    PlayerAnimator = GetComponentInChildren<IPlayerAnimator>();
                }
            }
            if (PlayerAnimator == null) Debug.LogWarning("CardiniController: IPlayerAnimator not found/assigned!", this);
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
                MoveInputVector = Vector3.zero;
                LookInputVector = Motor.CharacterForward;
                _jumpRequestedInternal = false;
                IsSprinting = false;
                ShouldBeCrouching = false;
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
            MoveInputVector = cameraPlanarRotation * moveInputVectorRaw;

            if (Settings.OrientationMethod == CardiniOrientationMethod.TowardsCamera)
            {
                LookInputVector = cameraPlanarDirection;
            }
            else if (Settings.OrientationMethod == CardiniOrientationMethod.TowardsMovement)
            {
                // Maintain last look direction if not moving, otherwise update
                if (MoveInputVector.sqrMagnitude > 0.001f)
                    LookInputVector = MoveInputVector.normalized;
                else if (LookInputVector.sqrMagnitude < 0.001f) // If look vector was also zero (e.g. at start)
                    LookInputVector = cameraPlanarDirection; // Default to camera direction
                // else, LookInputVector retains its previous value (last movement direction)
            }


            if (inputs.JumpPressed)
            {
                TimeSinceJumpRequested = 0f;
                _jumpRequestedInternal = true;
            }

            if (Settings.UseToggleSprint)
            {
                if (inputBridge.Sprint.IsPressed) _sprintToggleActive = !_sprintToggleActive;
                IsSprinting = _sprintToggleActive && MoveInputVector.sqrMagnitude > 0.01f;
            }
            else
            {
                IsSprinting = inputBridge.Sprint.IsHeld;
            }

            if (Settings.UseToggleCrouch)
            {
                if (inputBridge.Crouch.IsPressed) _crouchToggleActive = !_crouchToggleActive;
                ShouldBeCrouching = _crouchToggleActive;
            }
            else
            {
                ShouldBeCrouching = inputBridge.Crouch.IsHeld;
            }

            PlayerAnimator?.SetCrouching(IsCrouching);
        }

        private void HandleMajorStateInputs()
        {
            if (abilityManager == null) return; // Can't do anything without the manager

            bool wasAbilityWheelOpen = (CurrentMajorState == CharacterState.AbilitySelection);

            // --- Optional: Add logic here to switch _currentWheelTypeToDisplay ---
            // Example: Press a specific button while AbilitySelect is held to switch wheel type
            // if (inputBridge.AbilitySelect.IsHeld) {
            //     if (Input.GetKeyDown(KeyCode.Alpha1)) { // Replace with InputBridge action
            //         _currentWheelTypeToDisplay = AbilityType.Utility;
            //         // If wheel is already open, re-populate it
            //         if (wasAbilityWheelOpen) abilityManager.SetAbilityWheelVisible(true, _currentWheelTypeToDisplay);
            //         Debug.Log("Switched to Utility Wheel display");
            //     } else if (Input.GetKeyDown(KeyCode.Alpha2)) { // Replace with InputBridge action
            //         _currentWheelTypeToDisplay = AbilityType.CombatAbility;
            //         if (wasAbilityWheelOpen) abilityManager.SetAbilityWheelVisible(true, _currentWheelTypeToDisplay);
            //         Debug.Log("Switched to Combat Ability Wheel display");
            //     }
            // }
            // --- End Optional Wheel Switch Logic ---


            if (inputBridge.AbilitySelect.IsHeld)
            {
                if (!wasAbilityWheelOpen) // Just pressed AbilitySelect
                {
                    SetMajorState(CharacterState.AbilitySelection);
                    Time.timeScale = 0.1f;
                    abilityManager.SetAbilityWheelVisible(true, _currentWheelTypeToDisplay); // <<< MODIFIED
                }
                // While L1 is held, URM asset + CardiniRadialInputManager handles navigation.
            }
            else // AbilitySelect not held
            {
                if (wasAbilityWheelOpen) // Just released AbilitySelect
                {
                    // The URM asset's button click callback (-> HandleAbilitySelectedFromWheel)
                    // should have handled the actual equipping and already started closing the wheel.
                    // This block now mainly ensures state restoration if selection didn't auto-close.
                    abilityManager?.SetAbilityWheelVisible(false, _currentWheelTypeToDisplay);
                    SetMajorState(CharacterState.Locomotion);
                    Time.timeScale = 1f;

                    // The call to abilityManager.ConfirmAbilitySelection() is likely redundant
                    // if selection happens on URM button click, as that directly calls EquipAbility.
                    // It was more for a scenario where selection is confirmed *only* on L1 release.
                    // abilityManager.ConfirmAbilitySelection(); 
                    // For now, we can leave it as a log in AbilityManager to see when it's called.
                }
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

            _jumpRequestedInternal = false;
            _jumpConsumedInternal = true;
            // _jumpedThisFrameInternal will be set by AirborneModule or a new method if we centralize it.
            _jumpExecutionIntentInternal = true; // Signal that a jump was just executed

            PlayerAnimator?.TriggerJump();
            // The state transition to AirborneModule will happen in ManageModuleTransitions
            // due to Motor.ForceUnground() making IsStableOnGround false.
        }

        public bool ConsumeJumpExecutionIntent()
        {
            bool intent = _jumpExecutionIntentInternal;
            _jumpExecutionIntentInternal = false; // Consume it
            return intent;
        }

        public bool IsJumpRequested() => _jumpRequestedInternal;
        public bool IsJumpConsumed() => _jumpConsumedInternal;
        public void ConsumeJumpRequest() { _jumpRequestedInternal = false; TimeSinceJumpRequested = Mathf.Infinity; }
        public void SetJumpConsumed(bool consumed) => _jumpConsumedInternal = consumed;
        public void SetJumpedThisFrame(bool jumped) => _jumpedThisFrameInternal = jumped; // Airborne module will call this
        public void SetCrouchingState(bool isPhysicallyCrouching) => IsCrouching = isPhysicallyCrouching;
        public void SetLastGroundedSpeedTier(float speedTier) => LastGroundedSpeedTier = speedTier;

        // Shared Rotation Logic (can be called by modules)
        public void HandleStandardRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (Settings == null) return; // Should not happen if initialized

            if (LookInputVector.sqrMagnitude > 0f && Settings.OrientationSharpness > 0f)
            {
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, LookInputVector, 1 - Mathf.Exp(-Settings.OrientationSharpness * deltaTime)).normalized;
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
            ManageModuleTransitions(); // Call this once per frame

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
            _jumpedThisFrameInternal = false; // This controller flag is reset each frame. AirborneModule sets its own internal one.


            if (_activeMovementModule != null && CurrentMajorState == CharacterState.Locomotion)
            {
                _activeMovementModule.AfterCharacterUpdate(deltaTime);
            }

            // This logic for jump request timeout is controller-level
            if (_jumpRequestedInternal && TimeSinceJumpRequested > Settings.JumpPreGroundingGraceTime)
            {
                _jumpRequestedInternal = false;
            }

            // Reset TimeSinceLastAbleToJump  if we are now grounded (Module might have done this, but good to be sure)
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
            // This is where TimeSinceLastAbleToJump  is reset if grounded by a module,
            // or incremented if airborne by a module
        }

        public void BeforeCharacterUpdate(float deltaTime)
        {
            // Prime place to call ManageModuleTransitions once per KCC update cycle
            TimeSinceJumpRequested += deltaTime; // Increment this early
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
            TimeSinceLastAbleToJump = 0f;
            PlayerAnimator?.SetGrounded(true);
            PlayerAnimator?.TriggerLand();
        }

        protected void OnLeaveStableGround() { LastGroundedSpeedTier = CurrentSpeedTierForJump; /* Called by AirborneModule when it takes over after ground */ }
        public void OnDiscreteCollisionDetected(Collider hitCollider) { }

        // State Management (using our new enums) - kept public for now
        public void SetMajorState(CharacterState newState)
        {
            if (CurrentMajorState == newState) return;
            CharacterState oldMajorState = CurrentMajorState;
            CurrentMajorState = newState;

            if (newState == CharacterState.AbilitySelection)
            {
                // Option A: Tell animator to go to a specific "UI Focus" or "Neutral" animation state
                // PlayerAnimator?.SetMajorStateParameter(newState); // If you add such a param to animator

                // Option B: Freeze animator speed for locomotion layer
                if (PlayerAnimator != null && PlayerAnimator is PlayerAnimatorBridge bridge) // Need concrete type for this
                {
                    bridge.SetAnimatorSpeed(0f); // Need to add SetAnimatorSpeed to PlayerAnimatorBridge
                }
            }
            else if (oldMajorState == CharacterState.AbilitySelection)
            {
                if (PlayerAnimator != null && PlayerAnimator is PlayerAnimatorBridge bridge)
                {
                    bridge.SetAnimatorSpeed(1f);
                }
            }
            // Debug.Log($"Major State changed to: {newState}");
        }

        public void SetMovementState(CharacterMovementState newMoveState)
        {
            if (CurrentMovementState == newMoveState) return;

            CharacterMovementState oldMovementState = CurrentMovementState;
            CurrentMovementState = newMoveState;
            PlayerAnimator?.SetMovementState(newMoveState); // <<<--- ANIMATOR CALL
            // Additional specific calls based on state changes
            if (newMoveState == CharacterMovementState.Crouching)
            {
                PlayerAnimator?.SetCrouching(true);
            }
            else if (oldMovementState == CharacterMovementState.Crouching && newMoveState != CharacterMovementState.Crouching)
            {
                PlayerAnimator?.SetCrouching(false);
            }
            // Debug.Log($"Movement State changed to: {newMoveState}");
        }
        
        public void RequestCloseAbilityWheel()
        {
            if (CurrentMajorState == CharacterState.AbilitySelection)
            {
                abilityManager?.SetAbilityWheelVisible(false, _currentWheelTypeBeingDisplayed);
                SetMajorState(CharacterState.Locomotion);
                Time.timeScale = 1f;
            }
        }
    }
}