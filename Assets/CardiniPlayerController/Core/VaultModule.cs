// VaultModule.cs
using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    public class VaultModule : MovementModuleBase
    {
        [Header("Vault Module")]
        [SerializeField] private VaultDetector vaultDetector;
        
        // Vault execution state
        private bool _isVaulting = false;
        private float _vaultStartTime;
        private float _vaultProgress;
        private Vector3[] _currentVaultTrajectory;
        private Vector3 _vaultStartPosition;
        private Vector3 _vaultTargetPosition;
        private static float _lastVaultTime = -999f; // Static for cooldown across instances

        public override int Priority => 10; // High priority - takes precedence over grounded locomotion

        public override CharacterMovementState AssociatedPrimaryMovementState => CharacterMovementState.Vaulting;

        // Lock both velocity and rotation during vault - we handle everything
        // public override bool LocksVelocity => _isVaulting;
        // public override bool LocksRotation => _isVaulting;

        public override void Initialize(CardiniController controller)
        {
            base.Initialize(controller);
            
            // Find VaultDetector if not assigned
            if (vaultDetector == null)
            {
                vaultDetector = GetComponent<VaultDetector>();
                if (vaultDetector == null)
                {
                    vaultDetector = GetComponentInChildren<VaultDetector>();
                }
            }
            
            if (vaultDetector == null)
            {
                // Debug.LogError($"VaultModule on {gameObject.name}: VaultDetector not found! Please assign or add VaultDetector component.", this);
            }
        }

        public override bool CanEnterState()
        {
            // DEBUG: Always log when we're checking vault conditions
            bool result = CheckVaultConditions();
            if (Controller.IsSprinting || Controller.IsJumpRequested())
            {
                // Debug.Log($"[VaultModule] CanEnterState: {result} - Sprint:{Controller.IsSprinting}, Jump:{Controller.IsJumpRequested()}, InZone:{vaultDetector?.CurrentVaultData.inInitiationZone ?? false}");
            }
            return result;
        }

        private bool CheckVaultConditions()
        {
            // If we're already vaulting, we can stay active until vault completes
            if (_isVaulting)
            {
                // Stay active until vault progress is complete OR we've landed
                bool vaultComplete = _vaultProgress >= 1f;
                bool landedEarly = _vaultProgress >= 0.8f && Motor.GroundingStatus.IsStableOnGround;
                
                if (vaultComplete || landedEarly)
                {
                    // Debug.Log($"[VaultModule] Vault ending - Complete:{vaultComplete}, LandedEarly:{landedEarly}");
                    return false;
                }
                return true; // Stay active while vaulting
            }
            
            // === ENTERING VAULT CONDITIONS ===
            
            // Basic requirements from base class (but skip sprint requirement during vault)
            if (Conditions.RequireGrounded && !Motor.GroundingStatus.IsStableOnGround) 
            {
                // Debug.Log("[VaultModule] Not grounded");
                return false;
            }
            if (Conditions.RequireAirborne && Motor.GroundingStatus.IsStableOnGround) return false;
            if (Conditions.MinSpeed > 0f && Controller.Motor.BaseVelocity.magnitude < Conditions.MinSpeed) return false;
            if (Conditions.BlockIfCrouching && Controller.IsCrouching) return false;
            // NOTE: Skip RequireSprint and BlockIfSprinting here - we'll check manually below
            
            // VaultModule specific requirements:
            // 1. Must be sprinting (for flow) - but only when entering, not while vaulting
            if (!Controller.IsSprinting) 
            {
                return false; // Don't spam this log
            }
            
            // 2. Must be on stable ground (vaulting from ground only for now)
            if (!Motor.GroundingStatus.IsStableOnGround) 
            {
                // Debug.Log("[VaultModule] Not on stable ground");
                return false;
            }
            
            // 3. Check cooldown
            if (Time.time - _lastVaultTime < Settings.VaultCooldown) 
            {
                // Debug.Log($"[VaultModule] Cooldown active: {Time.time - _lastVaultTime:F2}s < {Settings.VaultCooldown:F2}s");
                return false;
            }
            
            // 4. VaultDetector must be available and detecting a vault
            if (vaultDetector == null) 
            {
                // Debug.LogError("[VaultModule] VaultDetector is null!");
                return false;
            }
            
            if (!vaultDetector.CurrentVaultData.canVault) 
            {
                // Debug.Log("[VaultModule] VaultDetector says canVault = false");
                return false;
            }
            
            // 5. Must be in initiation zone
            if (!vaultDetector.CurrentVaultData.inInitiationZone) 
            {
                // Debug.Log("[VaultModule] Not in initiation zone");
                return false;
            }
            
            // 6. Check button requirement (ONLY when entering vault)
            if (Settings.RequireButtonForVault)
            {
                if (!Controller.IsJumpRequested()) 
                {
                    // Debug.Log("[VaultModule] Jump button required but not pressed");
                    return false;
                }
            }
            
            // 7. Must have valid trajectory
            var vaultData = vaultDetector.CurrentVaultData;
            if (Settings.UseDetectedTrajectory && (vaultData.fullTrajectoryPoints == null || vaultData.fullTrajectoryPoints.Length < 2))
            {
                // Debug.LogWarning("[VaultModule] No valid trajectory detected");
                return false;
            }
            
            // Debug.Log("[VaultModule] ALL CONDITIONS MET - Should vault!");
            return true;
        }

        public override void OnEnterState()
        {
            // Debug.Log("[VaultModule] Starting vault!");
            
            _isVaulting = true;
            _vaultStartTime = Time.time;
            _vaultProgress = 0f;
            _lastVaultTime = Time.time; // Update cooldown timer
            
            // Store vault data
            var vaultData = vaultDetector.CurrentVaultData;
            _currentVaultTrajectory = vaultData.fullTrajectoryPoints;
            _vaultStartPosition = Controller.transform.position;
            _vaultTargetPosition = vaultData.landingPoint;
            
            // Consume the jump request if it was used to trigger vault
            if (Controller.IsJumpRequested())
            {
                Controller.ConsumeJumpRequest();
            }
            
            // Force unground to prevent ground snapping during vault
            Motor.ForceUnground();
            
            // Animation call (commented for now as requested)
            PlayerAnimator?.TriggerVault();
            
            // Debug.Log($"[VaultModule] Vault trajectory has {_currentVaultTrajectory?.Length ?? 0} points");
        }

        public override void OnExitState()
        {
            // Debug.Log("[VaultModule] Vault completed!");

            _isVaulting = false;
            _vaultProgress = 0f;
            _currentVaultTrajectory = null;

            // Animation call (commented for now as requested)
            // PlayerAnimator?.TriggerLand();
            PlayerAnimator?.SetVaultProgress(1f);
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (!_isVaulting) return;
            
            // During vault, face the vault direction
            var vaultData = vaultDetector.CurrentVaultData;
            if (vaultData.vaultDirection != Vector3.zero)
            {
                Vector3 targetDirection = vaultData.vaultDirection;
                targetDirection.y = 0; // Keep horizontal
                
                if (targetDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
                    currentRotation = Quaternion.Slerp(currentRotation, targetRotation, deltaTime * 10f);
                }
            }
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (!_isVaulting) return;
            
            // Calculate vault progress (0 to 1)
            float vaultDuration = Settings.VaultDuration;
            _vaultProgress = Mathf.Clamp01((Time.time - _vaultStartTime) / vaultDuration);
            PlayerAnimator?.SetVaultProgress(_vaultProgress);
            Vector3 targetPosition;
            
            if (Settings.UseDetectedTrajectory && _currentVaultTrajectory != null && _currentVaultTrajectory.Length > 1)
            {
                // Use the sophisticated trajectory from VaultDetector
                targetPosition = GetPositionOnTrajectory(_vaultProgress);
            }
            else
            {
                // Fallback: simple arc interpolation
                targetPosition = CalculateSimpleVaultPosition(_vaultProgress);
            }
            
            // Calculate velocity needed to reach target position
            Vector3 currentPosition = Motor.TransientPosition;
            Vector3 positionDelta = targetPosition - currentPosition;
            
            // Set velocity to reach target in this frame
            currentVelocity = positionDelta / deltaTime;
            
            // Clamp velocity magnitude for stability
            float maxVaultSpeed = Settings.VaultSpeed * 2f; // Allow some overshoot for smoothness
            if (currentVelocity.magnitude > maxVaultSpeed)
            {
                currentVelocity = currentVelocity.normalized * maxVaultSpeed;
            }
            
            // Check if vault is complete
            if (_vaultProgress >= 1f)
            {
                // Vault finished - the module system will transition us out
                // Debug.Log("[VaultModule] Vault progress complete");
            }
        }

        public override void AfterCharacterUpdate(float deltaTime)
        {
            // Nothing special needed here for vault
        }

        public override void PostGroundingUpdate(float deltaTime)
        {
            // If we've landed and vault is complete, allow transition out
            if (_isVaulting && _vaultProgress >= 0.8f && Motor.GroundingStatus.IsStableOnGround)
            {
                // Debug.Log("[VaultModule] Landed during vault - completing early");
                _vaultProgress = 1f;
            }
        }

        private Vector3 GetPositionOnTrajectory(float t)
        {
            if (_currentVaultTrajectory == null || _currentVaultTrajectory.Length < 2)
            {
                return Controller.transform.position;
            }
            
            // Convert normalized progress (0-1) to trajectory point index
            float floatIndex = t * (_currentVaultTrajectory.Length - 1);
            int lowerIndex = Mathf.FloorToInt(floatIndex);
            int upperIndex = Mathf.Min(lowerIndex + 1, _currentVaultTrajectory.Length - 1);
            
            if (lowerIndex == upperIndex)
            {
                return _currentVaultTrajectory[lowerIndex];
            }
            
            // Interpolate between the two points
            float localT = floatIndex - lowerIndex;
            return Vector3.Lerp(_currentVaultTrajectory[lowerIndex], _currentVaultTrajectory[upperIndex], localT);
        }

        private Vector3 CalculateSimpleVaultPosition(float t)
        {
            // Simple parabolic arc from start to landing point
            Vector3 start = _vaultStartPosition;
            Vector3 end = _vaultTargetPosition;
            
            // Create a simple arc
            Vector3 linearPosition = Vector3.Lerp(start, end, t);
            
            // Add vertical arc
            float arcHeight = 1.5f; // Simple fixed arc height
            float verticalOffset = Mathf.Sin(t * Mathf.PI) * arcHeight;
            
            return linearPosition + Vector3.up * verticalOffset;
        }

        // Optional: Gizmo visualization for debugging
        private void OnDrawGizmosSelected()
        {
            if (_isVaulting && _currentVaultTrajectory != null)
            {
                Gizmos.color = Color.red;
                for (int i = 0; i < _currentVaultTrajectory.Length - 1; i++)
                {
                    Gizmos.DrawLine(_currentVaultTrajectory[i], _currentVaultTrajectory[i + 1]);
                }
                
                // Show current target position
                Vector3 currentTarget = GetPositionOnTrajectory(_vaultProgress);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentTarget, 0.2f);
            }
        }
    }
}