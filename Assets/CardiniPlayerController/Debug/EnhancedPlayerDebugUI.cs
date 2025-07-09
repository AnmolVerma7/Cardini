using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Cardini.Motion;
namespace Cardini.UI
{
    public class EnhancedPlayerDebugUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CardiniController playerController;
        [SerializeField] private AbilityManager abilityManager;
        [SerializeField] private CardiniDebugManager debugManager; // ADDED THIS
        
        [Header("UI Containers")]
        [SerializeField] private Transform debugUIParent;
        [SerializeField] private GameObject sectionPrefab; // For creating toggleable sections
        
        [Header("Always Visible Core")]
        [SerializeField] private TextMeshProUGUI coreStatusText;
        
        [Header("Toggleable Sections")]
        [SerializeField] private DebugSection movementSection;
        [SerializeField] private DebugSection jumpSection;
        [SerializeField] private DebugSection inputSection;
        [SerializeField] private DebugSection moduleSection;
        [SerializeField] private DebugSection transitionSection;
        
        // Transition tracking
        private List<TransitionRecord> moduleTransitions = new List<TransitionRecord>();
        private List<TransitionRecord> stateTransitions = new List<TransitionRecord>();
        private const int MAX_TRANSITION_HISTORY = 5;
        
        // Previous states for transition detection
        private string lastActiveModule = "";
        private CharacterMovementState lastMovementState = CharacterMovementState.None;
        
        void Start()
        {
            InitializeDebugUI();
            SetupTransitionTracking();
        }
        
        void Update()
        {
            if (playerController == null) return;
            
            UpdateCoreStatus();
            UpdateTransitionTracking();
            
            if (movementSection.isActive) UpdateMovementSection();
            if (jumpSection.isActive) UpdateJumpSection();
            if (inputSection.isActive) UpdateInputSection();
            if (moduleSection.isActive) UpdateModuleSection();
            if (transitionSection.isActive) UpdateTransitionSection();
        }
        
        #region Initialization
        
        void InitializeDebugUI()
        {
            // Auto-find debug manager if not assigned
            if (debugManager == null)
            {
                debugManager = playerController?.GetComponent<CardiniDebugManager>();
            }
            
            // Initialize all sections
            movementSection.Initialize("🏃 Movement System", this);
            jumpSection.Initialize("🦘 Jump System", this);
            inputSection.Initialize("🎮 Input System", this);
            moduleSection.Initialize("⚙️ Active Module", this);
            transitionSection.Initialize("📈 Transitions", this);
        }
        
        void SetupTransitionTracking()
        {
            if (playerController != null)
            {
                lastActiveModule = playerController.ActiveModuleName;
                lastMovementState = playerController.CurrentMovementState;
            }
        }
        
        #endregion
        
        #region Core Status (Always Visible)
        
        void UpdateCoreStatus()
        {
            string groundInfo = GetGroundingInfo();
            Vector3 velocity = playerController.Motor.Velocity;
            float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            
            string coreStatus = $"<size=14><b>CORE STATUS</b></size>\n" +
                              $"<color=#87CEEB>Major:</color> {playerController.CurrentMajorState}\n" +
                              $"<color=#98FB98>Module:</color> {playerController.ActiveModuleName}\n" +
                              $"<color=#FFB6C1>State:</color> {playerController.CurrentMovementState}\n" +
                              $"<color=#DDA0DD>Ground:</color> {groundInfo}\n" +
                              $"<color=#F0E68C>Speed:</color> {horizontalSpeed:F1} m/s";
            
            // Add ability info if available
            if (abilityManager != null && abilityManager.CurrentlyEquippedAbility != null)
            {
                var ability = abilityManager.CurrentlyEquippedAbility;
                float cooldownProgress = abilityManager.GetAbilityCooldownProgress(ability);
                string cooldownStatus = cooldownProgress > 0 ? $"<color=#FF6B6B>({(cooldownProgress * 100):F0}%)</color>" : "<color=#90EE90>(Ready)</color>";
                coreStatus += $"\n<color=#FFA07A>Ability:</color> {ability.AbilityName} {cooldownStatus}";
            }
            
            coreStatusText.text = coreStatus;
        }
        
        string GetGroundingInfo()
        {
            var status = playerController.Motor.GroundingStatus;
            
            if (status.IsStableOnGround)
            {
                float groundAngle = Vector3.Angle(Vector3.up, status.GroundNormal);
                return groundAngle > 0.1f ? $"<color=#90EE90>Slope ({groundAngle:F1}°)</color>" : "<color=#90EE90>Flat</color>";
            }
            else if (status.FoundAnyGround)
            {
                return "<color=#FFD700>Sliding</color>";
            }
            else
            {
                float verticalSpeed = playerController.Motor.Velocity.y;
                string verticalIndicator = verticalSpeed > 0.1f ? "↑" : verticalSpeed < -0.1f ? "↓" : "→";
                return $"<color=#87CEEB>Air {verticalIndicator} ({verticalSpeed:F1})</color>";
            }
        }
        
        #endregion
        
        #region Movement Section
        
        void UpdateMovementSection()
        {
            string content = $"<b>MOVEMENT SYSTEM</b>\n\n" +
                           $"<color=#FFB6C1>Current Speed Tier:</color> {playerController.CurrentSpeedTierForJump:F1}\n" +
                           $"<color=#DDA0DD>Last Grounded Tier:</color> {playerController.LastGroundedSpeedTier:F1}\n" +
                           $"<color=#98FB98>Move Input:</color> {FormatVector3(playerController.MoveInputVector)}\n" +
                           $"<color=#87CEEB>Look Input:</color> {FormatVector3(playerController.LookInputVector)}\n" +
                           $"<color=#F0E68C>Sprint Toggle:</color> {GetToggleStatus(playerController.IsSprinting)}\n" +
                           $"<color=#FFA07A>Crouch Toggle:</color> {GetToggleStatus(playerController.ShouldBeCrouching)}";
            
            movementSection.UpdateContent(content);
        }
        
        #endregion
        
        #region Jump Section
        
        void UpdateJumpSection()
        {
            var wallRunModule = playerController.GetComponentInChildren<WallRunModule>();
            bool wallJumpAvailable = wallRunModule.GetWallJumpsRemaining() > 0;
            string content = $"<b>JUMP SYSTEM</b>\n\n" +
                           $"<color=#FF6B6B>Jump Consumed:</color> {GetBoolStatus(playerController.IsJumpConsumed())}\n" +
                           $"<color=#FF8C00>Double Jump Consumed:</color> {GetBoolStatus(playerController.IsDoubleJumpConsumed())}\n" +
                           $"<color=#32CD32>Jump Requested:</color> {GetBoolStatus(playerController.IsJumpRequested())}\n" +
                           $"<color=#4169E1>Time Since Able to Jump:</color> {playerController.TimeSinceLastAbleToJump:F2}s\n" +
                           $"<color=#9370DB>Wall Jump Available:</color> {GetBoolStatus(wallJumpAvailable)}";
            


            // Add coyote time info (even if not working yet)
            float coyoteTimeRemaining = Mathf.Max(0, playerController.Settings.JumpPostGroundingGraceTime - playerController.TimeSinceLastAbleToJump);
            content += $"\n<color=#FFD700>Coyote Time:</color> {coyoteTimeRemaining:F2}s";
            
            jumpSection.UpdateContent(content);
        }
        
        #endregion
        
        #region Input Section
        
        void UpdateInputSection()
        {
            var inputContext = playerController.InputContext;
            
            string content = $"<b>INPUT SYSTEM</b>\n\n" +
                           $"<color=#FF69B4>Raw Move Axes:</color> {FormatVector2(inputContext.MoveAxes)}\n" +
                           $"<color=#FFB6C1>Processed Move:</color> {FormatVector3(inputContext.MoveInputVector)}\n" +
                           $"<color=#FF1493>Raw Look Input:</color> {FormatVector3(inputContext.RawLookInput)}\n" +
                           $"<color=#DDA0DD>Character Look Dir:</color> {FormatVector3(inputContext.LookInputVector)}\n" +
                           $"<color=#DDA0DD>Sprint Mode:</color> {GetInputMode(playerController.Settings.UseToggleSprint)}\n" +
                           $"<color=#98FB98>Crouch Mode:</color> {GetInputMode(playerController.Settings.UseToggleCrouch)}\n" +
                           $"<color=#87CEEB>Jump Held:</color> {GetBoolStatus(inputContext.Jump.Held)}\n" +
                           $"<color=#F0E68C>Jump Hold Duration:</color> {inputContext.Jump.HoldDuration:F2}s";
            
            inputSection.UpdateContent(content);
        }
        
        #endregion
        
        #region Module Section
        
        void UpdateModuleSection()
        {
            string activeModule = playerController.ActiveModuleName;
            string content = $"<b>ACTIVE MODULE: {activeModule}</b>\n\n";
            
            // ONLY CHANGE: Use debug manager if available, otherwise use the original switch
            if (debugManager != null)
            {
                content += debugManager.GetModuleDebugInfo(activeModule);
            }
            else
            {
                // Original switch code as fallback
                switch (activeModule)
                {
                    case "SlideModule":
                        content += GetSlideModuleInfo();
                        break;
                    case "WallRunModule":
                        content += GetWallRunModuleInfo();
                        break;
                    case "VaultModule":
                        content += GetVaultModuleInfo();
                        break;
                    case "MantleModule":
                        content += GetMantleModuleInfo();
                        break;
                    case "AirborneModule":
                        content += GetAirborneModuleInfo();
                        break;
                    case "BaseLocomotionModule":
                        content += GetGroundedModuleInfo();
                        break;
                    default:
                        content += "<color=#808080>No specific debug info for this module</color>";
                        break;
                }
            }
            
            moduleSection.UpdateContent(content);
        }
        
        string GetSlideModuleInfo()
        {
            var slideModule = playerController.GetComponent<SlideModule>();
            if (slideModule == null) return "<color=#FF6B6B>SlideModule not found</color>";
            
            return $"<color=#4CAF50>Slide Cooldown:</color> Ready\n" +
                   $"<color=#2196F3>Slide Input:</color> {GetBoolStatus(playerController.IsSlideInitiationRequested)}\n" +
                   $"<color=#FF9800>Cancel Input:</color> {GetBoolStatus(playerController.IsSlideCancelRequested)}";
        }
        
        string GetWallRunModuleInfo()
        {
            var wallRunModule = playerController.GetComponent<WallRunModule>();
            if (wallRunModule == null) return "<color=#FF6B6B>WallRunModule not found</color>";
            
            return $"<color=#E91E63>Time Remaining:</color> {wallRunModule.GetWallRunTimeRemaining():F1}s\n" +
                   $"<color=#9C27B0>Jumps Remaining:</color> {wallRunModule.GetWallJumpsRemaining()}\n" +
                   $"<color=#673AB7>Is Wall Running:</color> {GetBoolStatus(wallRunModule.IsWallRunning)}";
        }
        
        string GetVaultModuleInfo()
        {
            var vaultModule = playerController.GetComponent<VaultModule>();
            if (vaultModule == null) return "<color=#FF6B6B>VaultModule not found</color>";
            
            var vaultDetector = playerController.GetComponentInChildren<VaultDetector>();
            if (vaultDetector != null)
            {
                var vaultData = vaultDetector.CurrentVaultData;
                return $"<color=#4CAF50>Can Vault:</color> {GetBoolStatus(vaultData.canVault)}\n" +
                       $"<color=#2196F3>In Initiation Zone:</color> {GetBoolStatus(vaultData.inInitiationZone)}\n" +
                       $"<color=#FF9800>Vault Distance:</color> {vaultData.vaultDistance:F2}m";
            }
            
            return "<color=#FF6B6B>VaultDetector not found</color>";
        }
        
        string GetMantleModuleInfo()
        {
            var mantleModule = playerController.GetComponent<MantleModule>();
            if (mantleModule == null) return "<color=#FF6B6B>MantleModule not found</color>";
            
            return $"<color=#795548>Is Mantling:</color> {GetBoolStatus(mantleModule.IsMantling)}\n" +
                   $"<color=#607D8B>Phase:</color> {mantleModule.CurrentMantlePhase}\n" +
                   $"<color=#9E9E9E>Progress:</color> {(mantleModule.MantleProgress * 100):F0}%\n" +
                   $"<color=#FF5722>Requires Button:</color> {GetBoolStatus(mantleModule.RequiresButton)}";
        }
        
        string GetAirborneModuleInfo()
        {
            return $"<color=#03A9F4>Gravity Applied:</color> {GetBoolStatus(!playerController.Motor.GroundingStatus.IsStableOnGround)}\n" +
                   $"<color=#00BCD4>Air Control:</color> Active\n" +
                   $"<color=#009688>Vertical Velocity:</color> {playerController.Motor.Velocity.y:F2}";
        }
        
        string GetGroundedModuleInfo()
        {
            return $"<color=#8BC34A>Ground Stable:</color> {GetBoolStatus(playerController.Motor.GroundingStatus.IsStableOnGround)}\n" +
                   $"<color=#CDDC39>Crouching:</color> {GetBoolStatus(playerController.IsCrouching)}";
        }
        
        #endregion
        
        #region Transition Tracking
        
        void UpdateTransitionTracking()
        {
            // Check for module transitions
            string currentModule = playerController.ActiveModuleName;
            if (currentModule != lastActiveModule && !string.IsNullOrEmpty(lastActiveModule))
            {
                AddTransition(moduleTransitions, lastActiveModule, currentModule, "Module");
            }
            lastActiveModule = currentModule;
            
            // Check for state transitions  
            CharacterMovementState currentState = playerController.CurrentMovementState;
            if (currentState != lastMovementState)
            {
                AddTransition(stateTransitions, lastMovementState.ToString(), currentState.ToString(), "State");
            }
            lastMovementState = currentState;
        }
        
        void AddTransition(List<TransitionRecord> list, string from, string to, string type)
        {
            var transition = new TransitionRecord
            {
                fromState = from,
                toState = to,
                timestamp = Time.time,
                conditions = CaptureTransitionConditions(from, to, type)
            };
            
            list.Insert(0, transition);
            
            if (list.Count > MAX_TRANSITION_HISTORY)
            {
                list.RemoveAt(list.Count - 1);
            }
        }
        
        string CaptureTransitionConditions(string from, string to, string type)
        {
            return $"Speed: {playerController.Motor.Velocity.magnitude:F1}, Grounded: {playerController.Motor.GroundingStatus.IsStableOnGround}";
        }

        void UpdateTransitionSection()
        {
            string content = $"<b>TRANSITION HISTORY</b>\n\n";

            content += "<b><color=#FF6B6B>MODULE TRANSITIONS</color></b>\n";
            if (moduleTransitions.Count == 0)
            {
                content += "<color=#808080>No transitions yet</color>\n";
            }
            else
            {
                for (int i = 0; i < moduleTransitions.Count; i++)
                {
                    var trans = moduleTransitions[i];
                    string prefix = i == 0 ? "Last:" : $"{i + 1}.";
                    content += $"<size=20>{prefix} <color=#FFD700>{trans.fromState}</color> → <color=#90EE90>{trans.toState}</color></size>\n";
                    content += $"<size=15><color=#B0B0B0>  {trans.conditions}</color></size>\n";
                }
            }

            content += "\n<b><color=#4169E1>STATE TRANSITIONS</color></b>\n";
            if (stateTransitions.Count == 0)
            {
                content += "<color=#808080>No transitions yet</color>";
            }
            else
            {
                for (int i = 0; i < stateTransitions.Count; i++)
                {
                    var trans = stateTransitions[i];
                    string prefix = i == 0 ? "Last:" : $"{i + 1}.";
                    content += $"<size=20>{prefix} <color=#FFB6C1>{trans.fromState}</color> → <color=#98FB98>{trans.toState}</color></size>\n";
                    content += $"<size=15><color=#B0B0B0>  {trans.conditions}</color></size>\n";
                }
            }

            transitionSection.UpdateContent(content);
        }
        
        #endregion

        #region Helper Methods

        string FormatVector3(Vector3 vector)
        {
            return $"({vector.x:F2}, {vector.y:F2}, {vector.z:F2})";
        }
        
        string FormatVector2(Vector2 vector)
        {
            return $"({vector.x:F2}, {vector.y:F2})";
        }
        
        string GetBoolStatus(bool value)
        {
            return value ? "<color=#90EE90>✓</color>" : "<color=#FF6B6B>✗</color>";
        }
        
        string GetToggleStatus(bool isActive)
        {
            return isActive ? "<color=#90EE90>ON</color>" : "<color=#808080>OFF</color>";
        }
        
        string GetInputMode(bool isToggle)
        {
            return isToggle ? "<color=#FFD700>Toggle</color>" : "<color=#87CEEB>Hold</color>";
        }
        
        #endregion
    }
    
    [System.Serializable]
    public class DebugSection
    {
        public string title;
        public bool isActive = true;
        public TextMeshProUGUI contentText;
        // public Button toggleButton;
        public GameObject sectionContainer;
        
        public void Initialize(string sectionTitle, EnhancedPlayerDebugUI parent)
        {
            title = sectionTitle;
            
            UpdateSectionVisibility();
        }
        
        public void UpdateSectionVisibility()
        {
            if (sectionContainer != null)
                sectionContainer.SetActive(isActive);
        }

        
        public void UpdateContent(string content)
        {
            UpdateSectionVisibility();
            if (contentText != null && isActive)
                contentText.text = content;
        }
    
    }
    
    [System.Serializable]
    public struct TransitionRecord
    {
        public string fromState;
        public string toState;
        public float timestamp;
        public string conditions;
    }
}