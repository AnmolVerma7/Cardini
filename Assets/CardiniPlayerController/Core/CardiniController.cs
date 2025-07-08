using UnityEngine;
using KinematicCharacterController;
using System.Collections.Generic;
using System.Linq;


namespace Cardini.Motion
{
    public class CardiniController : MonoBehaviour, ICharacterController
    {
        #region Core References
        [Header("Core References")]
        public KinematicCharacterMotor Motor;
        public InputBridge inputBridge;
        public AbilityManager abilityManager;
        public BaseLocomotionSettingsSO Settings;
        public StateTransitionSO transitionMatrix;

        [Header("Object References")]
        public Transform MeshRoot;
        public Transform CameraFollowPoint;

        [Header("Animation")]
        public IPlayerAnimator PlayerAnimator;

        [Header("Collision Filtering")]
        public List<Collider> IgnoredColliders = new List<Collider>();
        #endregion

        #region Movement Modules
        [Header("Movement Modules")]
        public List<MovementModuleBase> movementModules = new List<MovementModuleBase>();
        private MovementModuleBase _activeMovementModule;
        public string ActiveModuleName => _activeMovementModule != null ? _activeMovementModule.GetType().Name : "None";
        #endregion

        #region State Management
        [Header("Runtime State (Debug)")]
        [SerializeField] public CharacterState CurrentMajorState { get; private set; } = CharacterState.Locomotion;
        [SerializeField] public CharacterMovementState CurrentMovementState { get; private set; } = CharacterMovementState.Idle;
        
        // Physical states
        public bool IsCrouching { get; private set; }
        
        // Speed tracking
        public float CurrentSpeedTierForJump { get; set; }
        public float LastGroundedSpeedTier { get; private set; }
        #endregion

        #region Input System
        [Header("Enhanced Input System")]
        private InputContext _inputContext = new InputContext();
        private InputProcessor _inputProcessor;
        
        // Input states
        public bool IsSprinting { get; private set; }
        public bool ShouldBeCrouching { get; private set; }
        
        // Processed inputs
        public Vector3 MoveInputVector { get; private set; }
        public Vector3 LookInputVector { get; private set; }
        
        // Toggle states
        private bool _sprintToggleActive = false;
        private bool _crouchToggleActive = false;
        
        // Slide input access
        public InputContext InputContext => _inputContext;
        public bool IsSlideInitiationRequested => _inputContext.Slide.InitiationRequested;
        public bool IsSlideCancelRequested => _inputContext.Slide.CancelRequested;
        #endregion

        #region Jump System
        // Core jump flags
        public bool _jumpRequestedInternal;
        public bool _jumpConsumedInternal;
        public bool _jumpedThisFrameInternal;
        private bool _jumpExecutionIntentInternal = false;
        
        // Double jump
        public bool _doubleJumpConsumedInternal = false;
        
        // Jump timing
        public float TimeSinceJumpRequested { get; private set; } = Mathf.Infinity;
        public float TimeSinceLastAbleToJump { get; set; } = 0f;
        
        // Wall jump detection (currently unused by WallRunModule)
        private bool _canWallJump = false;
        private Vector3 _wallJumpNormal;
        
        // Wall jump state tracking (attempted fix that didn't work)
        private bool _wallJumpedThisFrame = false;
        #endregion

        #region KCC Internal
        public Collider[] ProbedColliders_SharedBuffer { get; private set; } = new Collider[8];
        private Vector3 _internalVelocityAdd = Vector3.zero;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Get motor reference
            if (Motor == null) Motor = GetComponent<KinematicCharacterMotor>();
            Motor.CharacterController = this;

            // Get component references
            if (inputBridge == null) inputBridge = GetComponentInParent<InputBridge>() ?? GetComponent<InputBridge>();
            if (abilityManager == null) abilityManager = GetComponentInChildren<AbilityManager>() ?? GetComponent<AbilityManager>();

            // Initialize input processor
            if (Settings == null) Debug.LogError("CardiniController: BaseLocomotionSettingsSO not assigned!", this);
            _inputProcessor = new InputProcessor(Settings, Motor);

            // Find animator
            if (PlayerAnimator == null)
            {
                if (MeshRoot != null)
                    PlayerAnimator = MeshRoot.GetComponentInChildren<IPlayerAnimator>();
                if (PlayerAnimator == null)
                    PlayerAnimator = GetComponentInChildren<IPlayerAnimator>();
            }

            // Log warnings for missing components
            if (inputBridge == null) Debug.LogError("CardiniController: InputBridge not found/assigned!", this);
            if (abilityManager == null) Debug.LogWarning("CardiniController: AbilityManager not found/assigned!", this);
            if (PlayerAnimator == null) Debug.LogWarning("CardiniController: IPlayerAnimator not found/assigned!", this);

            // Initialize movement modules
            foreach (var module in movementModules)
            {
                if (module != null) module.Initialize(this);
            }
            movementModules = movementModules.OrderByDescending(m => m.Priority).ToList();

            // Initialize state transition matrix
            if (transitionMatrix != null)
            {
                transitionMatrix.Initialize();
            }
            else
            {
                Debug.LogWarning("CardiniController: Transition Matrix SO not assigned! All state transitions will be allowed by default.", this);
            }

            // Set initial state
            SetMajorState(CharacterState.Locomotion);
        }
        #endregion

        #region Input Processing
        public struct ControllerInputs
        {
            public Vector2 MoveAxes;
            public Quaternion CameraRotation;
            public bool JumpPressed;
        }

        public void SetControllerInputs(ref ControllerInputs inputs)
        {
            HandleMajorStateInputs();

            if (CurrentMajorState != CharacterState.Locomotion)
            {
                ResetLocomotionInputs();
                return;
            }

            if (Settings == null) return;

            ProcessMovementInput(inputs, ref _inputContext);
            ProcessJumpInput(inputs, ref _inputContext);
            _inputProcessor.ProcessInputs(inputBridge, ref _inputContext, CurrentMovementState, Time.deltaTime);
            ApplyInputResults();
        }

        // private void ProcessMovementInput(ControllerInputs inputs, ref InputContext context)
        // {
        //     context.MoveAxes = inputs.MoveAxes;

        //     Vector3 moveInputVectorRaw = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxes.x, 0f, inputs.MoveAxes.y), 1f);
        //     Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;

        //     if (cameraPlanarDirection.sqrMagnitude == 0f)
        //     {
        //         cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
        //     }

        //     Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);
        //     context.MoveInputVector = cameraPlanarRotation * moveInputVectorRaw;
        //     MoveInputVector = context.MoveInputVector;

        //     if (Settings.OrientationMethod == CardiniOrientationMethod.TowardsCamera)
        //     {
        //         context.LookInputVector = cameraPlanarDirection;
        //     }
        //     else if (Settings.OrientationMethod == CardiniOrientationMethod.TowardsMovement)
        //     {
        //         if (context.MoveInputVector.sqrMagnitude > 0.001f)
        //             context.LookInputVector = context.MoveInputVector.normalized;
        //         else if (context.LookInputVector.sqrMagnitude < 0.001f)
        //             context.LookInputVector = cameraPlanarDirection;
        //     }

        //     LookInputVector = context.LookInputVector;
        // }
        private void ProcessMovementInput(ControllerInputs inputs, ref InputContext context)
        {
            context.MoveAxes = inputs.MoveAxes;
            
            // NEW: Capture raw look input for debug (doesn't affect gameplay)
            Vector2 rawLookInput = inputBridge.LookInput;
            context.RawLookInput = new Vector3(rawLookInput.x, 0f, rawLookInput.y);
            
            // EXISTING CODE - Keep all your current logic unchanged!
            Vector3 moveInputVectorRaw = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxes.x, 0f, inputs.MoveAxes.y), 1f);
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
            
            if (cameraPlanarDirection.sqrMagnitude == 0f)
            {
                cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
            }
            
            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);
            context.MoveInputVector = cameraPlanarRotation * moveInputVectorRaw;
            MoveInputVector = context.MoveInputVector;

            // EXISTING LOOK LOGIC - Keep exactly as it was!
            if (Settings.OrientationMethod == CardiniOrientationMethod.TowardsCamera)
            {
                context.LookInputVector = cameraPlanarDirection;
            }
            else if (Settings.OrientationMethod == CardiniOrientationMethod.TowardsMovement)
            {
                if (context.MoveInputVector.sqrMagnitude > 0.001f)
                    context.LookInputVector = context.MoveInputVector.normalized;
                else if (context.LookInputVector.sqrMagnitude < 0.001f)
                    context.LookInputVector = cameraPlanarDirection;
            }
            
            LookInputVector = context.LookInputVector;
        }

        private void ProcessJumpInput(ControllerInputs inputs, ref InputContext context)
        {
            if (inputs.JumpPressed)
            {
                TimeSinceJumpRequested = 0f;
                _jumpRequestedInternal = true;
            }
        }

        private void ResetLocomotionInputs()
        {
            MoveInputVector = Vector3.zero;
            LookInputVector = Motor.CharacterForward;
            _jumpRequestedInternal = false;
            IsSprinting = false;
            ShouldBeCrouching = false;
            _inputContext.Reset();
        }

        private void ApplyInputResults()
        {
            IsSprinting = _inputContext.IsSprinting;
            ShouldBeCrouching = _inputContext.ShouldBeCrouching;
            _sprintToggleActive = _inputContext.SprintToggleActive;
            _crouchToggleActive = _inputContext.CrouchToggleActive;
        }

        private void HandleMajorStateInputs()
        {
            if (abilityManager == null) return;

            bool wasAbilityWheelOpen = (CurrentMajorState == CharacterState.AbilitySelection);

            if (inputBridge.AbilitySelect.IsHeld)
            {
                if (!wasAbilityWheelOpen)
                {
                    SetMajorState(CharacterState.AbilitySelection);
                    Time.timeScale = 0.1f;
                    abilityManager.SetAbilityWheelVisible(true);
                }
            }
            else
            {
                if (wasAbilityWheelOpen)
                {
                    abilityManager.SetAbilityWheelVisible(false);
                    SetMajorState(CharacterState.Locomotion);
                    Time.timeScale = 1f;
                }
            }
        }
        #endregion

        #region Module Management
        private void ManageModuleTransitions()
        {
            if (CurrentMajorState != CharacterState.Locomotion && CurrentMajorState != CharacterState.Combat)
            {
                if (_activeMovementModule != null)
                {
                    _activeMovementModule.OnExitState();
                    _activeMovementModule = null;
                    SetMovementState(CharacterMovementState.None);
                }
                return;
            }

            MovementModuleBase newActiveModule = null;
            int highestPriority = -1;

            foreach (var module in movementModules)
            {
                if (module.CanEnterState())
                {
                    if (transitionMatrix != null &&
                        !transitionMatrix.IsAllowed(CurrentMovementState, module.AssociatedPrimaryMovementState))
                    {
                        continue;
                    }

                    if (module.Priority > highestPriority)
                    {
                        newActiveModule = module;
                        highestPriority = module.Priority;
                    }
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

            if (_activeMovementModule != null)
            {
                SetMovementState(_activeMovementModule.AssociatedPrimaryMovementState);
            }
            else
            {
                SetMovementState(CharacterMovementState.None);
            }
        }
        #endregion

        #region Public API for Modules
        // Jump System
        public void ExecuteJump(float jumpUpSpeed, float jumpForwardSpeed, Vector3 moveInputForJump)
        {
            Vector3 jumpDirection = Motor.CharacterUp;
            if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
            {
                jumpDirection = Motor.GroundingStatus.GroundNormal;
            }

            Motor.ForceUnground();
            _internalVelocityAdd += (jumpDirection * jumpUpSpeed) - Vector3.Project(Motor.BaseVelocity, Motor.CharacterUp);
            _internalVelocityAdd += (moveInputForJump.normalized * jumpForwardSpeed);

            _jumpRequestedInternal = false;
            _jumpConsumedInternal = true;
            _jumpExecutionIntentInternal = true;

            PlayerAnimator?.TriggerJump();
        }

        public bool ConsumeJumpExecutionIntent()
        {
            bool intent = _jumpExecutionIntentInternal;
            _jumpExecutionIntentInternal = false;
            _wallJumpedThisFrame = false; // Also reset wall jump flag
            return intent;
        }

        public bool IsJumpRequested() => _jumpRequestedInternal;
        public bool IsJumpConsumed() => _jumpConsumedInternal;
        public void ConsumeJumpRequest() 
        { 
            _jumpRequestedInternal = false; 
            TimeSinceJumpRequested = Mathf.Infinity; 
        }
        public void SetJumpConsumed(bool consumed) => _jumpConsumedInternal = consumed;
        public void SetJumpedThisFrame(bool jumped) => _jumpedThisFrameInternal = jumped;
        public bool IsDoubleJumpConsumed() => _doubleJumpConsumedInternal;
        public void SetDoubleJumpConsumed(bool consumed) => _doubleJumpConsumedInternal = consumed;

        // Wall Jump (currently unused by modules)
        public bool CanWallJump() => _canWallJump;
        public Vector3 GetWallJumpNormal() => _wallJumpNormal;
        public void SetWallJumpedThisFrame(bool wallJumped) => _wallJumpedThisFrame = wallJumped;
        public bool WasWallJumpExecuted() => _wallJumpedThisFrame;

        // State Management
        public void SetCrouchingState(bool isPhysicallyCrouching) => IsCrouching = isPhysicallyCrouching;
        public void SetLastGroundedSpeedTier(float speedTier) => LastGroundedSpeedTier = speedTier;
        public bool CanTransitionToState(CharacterMovementState targetState)
        {
            if (transitionMatrix == null) return true;
            return transitionMatrix.IsAllowed(CurrentMovementState, targetState);
        }

        // Slide Management
        public void ConsumeSlideInitiation() => _inputContext.Slide.InitiationRequested = false;
        public void ConsumeSlideCancellation() => _inputContext.Slide.CancelRequested = false;

        // Velocity Management
        public void AddVelocity(Vector3 velocity) => _internalVelocityAdd += velocity;

        // Shared Utilities
        public void HandleStandardRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (Settings == null) return;

            if (LookInputVector.sqrMagnitude > 0f && Settings.OrientationSharpness > 0f)
            {
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, LookInputVector, 
                    1 - Mathf.Exp(-Settings.OrientationSharpness * deltaTime)).normalized;
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
            }
            else if (Settings.OrientationSharpness > 0f)
            {
                Vector3 currentUp = (currentRotation * Vector3.up);
                Vector3 smoothedUp = Vector3.Slerp(currentUp, Vector3.up, 
                    1 - Mathf.Exp(-Settings.OrientationSharpness * deltaTime * 0.5f));
                currentRotation = Quaternion.FromToRotation(currentUp, smoothedUp) * currentRotation;
            }
        }

        public void OnLandedInternal()
        {
            SetJumpConsumed(false);
            SetDoubleJumpConsumed(false);
            TimeSinceLastAbleToJump = 0f;
            PlayerAnimator?.SetGrounded(true);
            PlayerAnimator?.TriggerLand();
        }
        #endregion

        #region State Management
        public void SetMajorState(CharacterState newState)
        {
            if (CurrentMajorState == newState) return;
            
            CharacterState oldMajorState = CurrentMajorState;
            CurrentMajorState = newState;

            if (newState == CharacterState.AbilitySelection)
            {
                if (PlayerAnimator != null && PlayerAnimator is PlayerAnimatorBridge bridge)
                {
                    bridge.SetAnimatorSpeed(0f);
                }
            }
            else if (oldMajorState == CharacterState.AbilitySelection)
            {
                if (PlayerAnimator != null && PlayerAnimator is PlayerAnimatorBridge bridge)
                {
                    bridge.SetAnimatorSpeed(1f);
                }
            }
        }

        public void SetMovementState(CharacterMovementState newMoveState)
        {
            if (CurrentMovementState == newMoveState) return;

            CharacterMovementState oldMovementState = CurrentMovementState;
            CurrentMovementState = newMoveState;
            PlayerAnimator?.SetMovementState(newMoveState);
            
            if (newMoveState == CharacterMovementState.Crouching)
            {
                PlayerAnimator?.SetCrouching(true);
            }
            else if (oldMovementState == CharacterMovementState.Crouching && newMoveState != CharacterMovementState.Crouching)
            {
                PlayerAnimator?.SetCrouching(false);
            }
        }

        public void RequestCloseAbilityWheel()
        {
            if (CurrentMajorState == CharacterState.AbilitySelection)
            {
                abilityManager?.SetAbilityWheelVisible(false);
                SetMajorState(CharacterState.Locomotion);
                Time.timeScale = 1f;
            }
        }
        #endregion

        #region ICharacterController Implementation
        public void BeforeCharacterUpdate(float deltaTime)
        {
            TimeSinceJumpRequested += deltaTime;

            if (_activeMovementModule != null && CurrentMajorState == CharacterState.Locomotion)
            {
                _activeMovementModule.BeforeCharacterUpdate(deltaTime);
            }
            
            ManageModuleTransitions();
        }

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (_activeMovementModule != null && _activeMovementModule.LocksRotation)
            {
                return;
            }
            
            if (_activeMovementModule != null && CurrentMajorState == CharacterState.Locomotion)
            {
                _activeMovementModule.UpdateRotation(ref currentRotation, deltaTime);
            }
            else
            {
                HandleStandardRotation(ref currentRotation, deltaTime);
            }
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (_activeMovementModule != null && _activeMovementModule.LocksVelocity)
            {
                // Module handles everything
            }
            else if (_activeMovementModule != null && CurrentMajorState == CharacterState.Locomotion)
            {
                _activeMovementModule.UpdateVelocity(ref currentVelocity, deltaTime);
            }
            else
            {
                if (!Motor.GroundingStatus.IsStableOnGround)
                {
                    if (Settings != null) currentVelocity += Settings.Gravity * deltaTime;
                    else currentVelocity += Physics.gravity * deltaTime;
                }
            }

            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            ManageModuleTransitions();

            if (_activeMovementModule != null && CurrentMajorState == CharacterState.Locomotion)
            {
                _activeMovementModule.PostGroundingUpdate(deltaTime);
            }
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            if (Settings == null) return;

            _jumpedThisFrameInternal = false;
            _canWallJump = false;

            if (_activeMovementModule != null && CurrentMajorState == CharacterState.Locomotion)
            {
                _activeMovementModule.AfterCharacterUpdate(deltaTime);
            }
            
            if (_jumpRequestedInternal && TimeSinceJumpRequested > Settings.JumpPreGroundingGraceTime)
            {
                _jumpRequestedInternal = false;
            }
        }

        public bool IsColliderValidForCollisions(Collider coll) => !IgnoredColliders.Contains(coll);
        
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) 
        { 
            // Modules can override if needed
        }
        
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            // Wall jump detection (currently unused by WallRunModule)
            if (!Motor.GroundingStatus.IsStableOnGround && !hitStabilityReport.IsStable)
            {
                _canWallJump = true;
                _wallJumpNormal = hitNormal;
                Debug.Log($"[Controller] Wall jump opportunity detected! Normal: {hitNormal}");
            }
        }
        
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, 
            Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) 
        { 
            // Modules can override if needed
        }
        
        public void OnDiscreteCollisionDetected(Collider hitCollider) 
        { 
            // Modules can override if needed
        }
        #endregion

        #region Unused/Deprecated
        // This method is never called
        // protected void OnLeaveStableGround() 
        // { 
        //     LastGroundedSpeedTier = CurrentSpeedTierForJump; 
        // }
        #endregion
    }
}