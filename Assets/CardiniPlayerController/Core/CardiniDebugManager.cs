// CardiniDebugManager.cs - Separate component, keeps CardiniController clean!
using UnityEngine;
using System.Collections.Generic;

namespace Cardini.Motion
{
    /// <summary>
    /// Handles all debug tracking without bloating CardiniController
    /// </summary>
    public class CardiniDebugManager : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool enableTransitionDebug = true;
        [SerializeField] private bool logTransitionsToConsole = false;
        
        [Header("References")]
        [SerializeField] private CardiniController controller;
        
        // State tracking
        private string _lastActiveModule = "";
        private CharacterMovementState _lastMovementState = CharacterMovementState.None;
        private bool _initialized = false;
        
        void Start()
        {
            InitializeDebugManager();
        }
        
        void Update()
        {
            if (!_initialized || !enableTransitionDebug) return;
            
            TrackTransitions();
        }
        
        #region Initialization
        
        void InitializeDebugManager()
        {
            if (controller == null)
                controller = GetComponent<CardiniController>();
                
            if (controller == null)
            {
                Debug.LogError("CardiniDebugManager: No CardiniController found!");
                enabled = false;
                return;
            }
            
            _lastActiveModule = controller.ActiveModuleName;
            _lastMovementState = controller.CurrentMovementState;
            _initialized = true;
            
            Debug.Log("🔧 CardiniDebugManager initialized - tracking enabled!");
        }
        
        #endregion
        
        #region Transition Tracking
        
        void TrackTransitions()
        {
            // Track module transitions
            string currentModule = controller.ActiveModuleName;
            if (currentModule != _lastActiveModule && !string.IsNullOrEmpty(_lastActiveModule))
            {
                LogModuleTransition(_lastActiveModule, currentModule);
            }
            _lastActiveModule = currentModule;
            
            // Track state transitions
            CharacterMovementState currentState = controller.CurrentMovementState;
            if (currentState != _lastMovementState)
            {
                LogStateTransition(_lastMovementState, currentState);
            }
            _lastMovementState = currentState;
        }
        
        void LogModuleTransition(string fromModule, string toModule)
        {
            var conditions = CaptureCurrentConditions();
            
            // Add module-specific conditions
            AddModuleSpecificConditions(toModule, conditions);
            
            // Log competing modules (simplified - just show what's available)
            List<string> availableModules = GetAvailableModules();
            foreach (string available in availableModules)
            {
                if (available != toModule)
                {
                    conditions.AddCompetingModule(available);
                }
            }
            
            conditions.LogTransition(fromModule, toModule, TransitionType.Module);
        }
        
        void LogStateTransition(CharacterMovementState fromState, CharacterMovementState toState)
        {
            var conditions = CaptureCurrentConditions();
            conditions.LogTransition(fromState.ToString(), toState.ToString(), TransitionType.State);
        }
        
        #endregion
        
        #region Condition Capture
        
        TransitionConditionBuilder CaptureCurrentConditions()
        {
            var motor = controller.Motor;
            
            return TransitionConditionBuilder.Create()
                .AddBool("IsGrounded", motor.GroundingStatus.IsStableOnGround)
                .AddBool("FoundAnyGround", motor.GroundingStatus.FoundAnyGround)
                .AddFloat("GroundAngle", motor.GroundingStatus.IsStableOnGround ? 
                    Vector3.Angle(Vector3.up, motor.GroundingStatus.GroundNormal) : 0f)
                .AddFloat("HorizontalSpeed", new Vector3(motor.BaseVelocity.x, 0, motor.BaseVelocity.z).magnitude)
                .AddFloat("VerticalSpeed", motor.BaseVelocity.y)
                .AddFloat("TotalSpeed", motor.BaseVelocity.magnitude)
                .AddBool("IsSprinting", controller.IsSprinting)
                .AddBool("ShouldBeCrouching", controller.ShouldBeCrouching)
                .AddBool("IsCrouching", controller.IsCrouching)
                .AddBool("JumpRequested", controller.IsJumpRequested())
                .AddBool("JumpConsumed", controller.IsJumpConsumed())
                .AddBool("DoubleJumpConsumed", controller.IsDoubleJumpConsumed())
                .AddFloat("TimeSinceJump", controller.TimeSinceLastAbleToJump)
                .AddVector("MoveInput", controller.MoveInputVector)
                .AddFloat("MoveInputMagnitude", controller.MoveInputVector.magnitude)
                .AddFloat("CurrentSpeedTier", controller.CurrentSpeedTierForJump)
                .AddFloat("LastGroundedTier", controller.LastGroundedSpeedTier);
        }
        
        void AddModuleSpecificConditions(string moduleName, TransitionConditionBuilder conditions)
        {
            switch (moduleName)
            {
                case "SlideModule":
                    conditions
                        .AddBool("SlideInitRequested", controller.IsSlideInitiationRequested)
                        .AddBool("SlideCancelRequested", controller.IsSlideCancelRequested)
                        .AddThreshold("SpeedForSlide", controller.Motor.BaseVelocity.magnitude, 5f);
                    break;
                    
                case "WallRunModule":
                    var wallDetector = controller.GetComponentInChildren<WallDetector>();
                    if (wallDetector != null)
                    {
                        var wallInfo = wallDetector.CurrentWall;
                        conditions
                            .AddBool("WallDetected", wallInfo.hasWall)
                            .AddBool("CanWallRun", wallInfo.canWallRun)
                            .AddFloat("WallQuality", wallInfo.wallQuality)
                            .AddBool("IsLeftWall", wallInfo.isLeftWall);
                    }
                    break;
                    
                case "VaultModule":
                    var vaultDetector = controller.GetComponentInChildren<VaultDetector>();
                    if (vaultDetector != null)
                    {
                        var vaultData = vaultDetector.CurrentVaultData;
                        conditions
                            .AddBool("CanVault", vaultData.canVault)
                            .AddBool("InInitiationZone", vaultData.inInitiationZone)
                            .AddFloat("VaultDistance", vaultData.vaultDistance);
                    }
                    break;
                    
                case "MantleModule":
                    var mantleDetector = controller.GetComponentInChildren<MantleDetector>();
                    if (mantleDetector != null)
                    {
                        var mantleData = mantleDetector.CurrentMantleData;
                        conditions
                            .AddBool("CanMantle", mantleData.isValid)
                            .AddFloat("ObjectHeight", mantleData.objectHeight);
                    }
                    break;
                    
                case "AirborneLocomotionModule":
                    float coyoteTimeRemaining = Mathf.Max(0, controller.Settings.JumpPostGroundingGraceTime - controller.TimeSinceLastAbleToJump);
                    conditions
                        .AddFloat("CoyoteTimeRemaining", coyoteTimeRemaining)
                        .AddBool("AllowDoubleJump", controller.Settings.AllowDoubleJump);
                    break;
                    
                case "GroundedLocomotionModule":
                    conditions
                        .AddFloat("StableMovementSharpness", controller.Settings.StableMovementSharpness)
                        .AddBool("AllowJumpingWhenSliding", controller.Settings.AllowJumpingWhenSliding);
                    break;
            }
        }
        
        List<string> GetAvailableModules()
        {
            List<string> available = new List<string>();
            
            // Check each module type that might be available
            if (controller.GetComponentInChildren<SlideModule>() != null) available.Add("SlideModule");
            if (controller.GetComponentInChildren<WallRunModule>() != null) available.Add("WallRunModule");
            if (controller.GetComponentInChildren<VaultModule>() != null) available.Add("VaultModule");
            if (controller.GetComponentInChildren<MantleModule>() != null) available.Add("MantleModule");
            if (controller.GetComponent<AirborneModule>() != null) available.Add("AirborneModule");
            if (controller.GetComponent<BaseLocomotionModule>() != null) available.Add("BaseLocomotionModule");
            
            return available;
        }
        
        #endregion
        
        #region Public Interface for UI
        
        public List<DetailedTransitionRecord> GetRecentTransitions()
        {
            return TransitionDebugSystem.GetRecentTransitions();
        }
        
        public void SetDebugEnabled(bool enabled)
        {
            enableTransitionDebug = enabled;
        }
        
        public bool IsDebugEnabled()
        {
            return enableTransitionDebug;
        }
        
        // Helper method for UI to get module-specific debug info
        public string GetModuleDebugInfo(string moduleName)
        {
            switch (moduleName)
            {
                case "SlideModule":
                    return GetSlideModuleInfo();
                case "WallRunModule":
                    return GetWallRunModuleInfo();
                case "VaultModule":
                    return GetVaultModuleInfo();
                case "MantleModule":
                    return GetMantleModuleInfo();
                case "AirborneModule":
                    return GetAirborneModuleInfo();
                case "BaseLocomotionModule":
                    return GetGroundedModuleInfo();
                default:
                    return "<color=#808080>No specific debug info available</color>";
            }
        }
        
        #endregion
        
        #region Module Debug Info
        
        string GetSlideModuleInfo()
        {
            var slideModule = controller.GetComponentInChildren<SlideModule>();
            if (slideModule == null) return "<color=#FF6B6B>SlideModule not found</color>";
            
            return $"<color=#4CAF50>Slide Available:</color> {GetBoolIcon(!controller.IsSlideInitiationRequested)}\n" +
                   $"<color=#2196F3>Slide Input:</color> {GetBoolIcon(controller.IsSlideInitiationRequested)}\n" +
                   $"<color=#FF9800>Cancel Input:</color> {GetBoolIcon(controller.IsSlideCancelRequested)}\n" +
                   $"<color=#9C27B0>Sprint Required:</color> {GetBoolIcon(controller.IsSprinting)}";
        }
        
        string GetWallRunModuleInfo()
        {
            var wallRunModule = controller.GetComponentInChildren<WallRunModule>();
            if (wallRunModule == null) return "<color=#FF6B6B>WallRunModule not found</color>";
            
            var wallDetector = controller.GetComponentInChildren<WallDetector>();
            if (wallDetector != null)
            {
                var wallInfo = wallDetector.CurrentWall;
                return $"<color=#E91E63>Wall Detected:</color> {GetBoolIcon(wallInfo.hasWall)}\n" +
                       $"<color=#9C27B0>Can Wall Run:</color> {GetBoolIcon(wallInfo.canWallRun)}\n" +
                       $"<color=#673AB7>Wall Quality:</color> {wallInfo.wallQuality:F2}\n" +
                       $"<color=#3F51B5>Is Running:</color> {GetBoolIcon(wallRunModule.IsWallRunning)}";
            }
            
            return $"<color=#3F51B5>Is Running:</color> {GetBoolIcon(wallRunModule.IsWallRunning)}\n" +
                   $"<color=#FF6B6B>WallDetector not found</color>";
        }
        
        string GetVaultModuleInfo()
        {
            var vaultDetector = controller.GetComponentInChildren<VaultDetector>();
            if (vaultDetector != null)
            {
                var vaultData = vaultDetector.CurrentVaultData;
                return $"<color=#4CAF50>Can Vault:</color> {GetBoolIcon(vaultData.canVault)}\n" +
                       $"<color=#2196F3>In Zone:</color> {GetBoolIcon(vaultData.inInitiationZone)}\n" +
                       $"<color=#FF9800>Distance:</color> {vaultData.vaultDistance:F2}m\n" +
                       $"<color=#9C27B0>Button Required:</color> {GetBoolIcon(controller.Settings.RequireButtonForVault)}";
            }
            
            return "<color=#FF6B6B>VaultDetector not found</color>";
        }
        
        string GetMantleModuleInfo()
        {
            var mantleModule = controller.GetComponentInChildren<MantleModule>();
            if (mantleModule == null) return "<color=#FF6B6B>MantleModule not found</color>";
            
            var mantleDetector = controller.GetComponentInChildren<MantleDetector>();
            if (mantleDetector != null)
            {
                var mantleData = mantleDetector.CurrentMantleData;
                return $"<color=#795548>Can Mantle:</color> {GetBoolIcon(mantleData.isValid)}\n" +
                       $"<color=#607D8B>Is Mantling:</color> {GetBoolIcon(mantleModule.IsMantling)}\n" +
                       $"<color=#9E9E9E>Height:</color> {mantleData.objectHeight:F2}m\n" +
                       $"<color=#FF5722>Phase:</color> {mantleModule.CurrentMantlePhase}";
            }
            
            return $"<color=#607D8B>Is Mantling:</color> {GetBoolIcon(mantleModule.IsMantling)}\n" +
                   $"<color=#FF6B6B>MantleDetector not found</color>";
        }
        
        string GetAirborneModuleInfo()
        {
            return $"<color=#03A9F4>In Air:</color> {GetBoolIcon(!controller.Motor.GroundingStatus.IsStableOnGround)}\n" +
                   $"<color=#00BCD4>Vertical Speed:</color> {controller.Motor.Velocity.y:F2}\n" +
                   $"<color=#009688>Double Jump:</color> {GetBoolIcon(controller.Settings.AllowDoubleJump)}\n" +
                   $"<color=#4CAF50>Coyote Time:</color> {(controller.Settings.JumpPostGroundingGraceTime - controller.TimeSinceLastAbleToJump):F2}s";
        }
        
        string GetGroundedModuleInfo()
        {
            return $"<color=#8BC34A>Stable Ground:</color> {GetBoolIcon(controller.Motor.GroundingStatus.IsStableOnGround)}\n" +
                   $"<color=#CDDC39>Crouching:</color> {GetBoolIcon(controller.IsCrouching)}\n" +
                   $"<color=#FFC107>Sprint Toggle:</color> {GetBoolIcon(controller.IsSprinting)}\n" +
                   $"<color=#FF9800>Can Jump:</color> {GetBoolIcon(!controller.IsJumpConsumed())}";
        }
        
        string GetBoolIcon(bool value)
        {
            return value ? "<color=#90EE90>✓</color>" : "<color=#FF6B6B>✗</color>";
        }
        
        #endregion
    }
}