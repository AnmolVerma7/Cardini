using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    /// <summary>
    /// Handles vault execution using sophisticated trajectory following or simple arc interpolation.
    /// Integrates with VaultDetector for obstacle detection and trajectory calculation.
    /// </summary>
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
        private static float _lastVaultTime = -999f; // Static cooldown across instances

        public override int Priority => 6; // High priority - takes precedence over grounded locomotion
        public override CharacterMovementState AssociatedPrimaryMovementState => CharacterMovementState.Vaulting;

        public override void Initialize(CardiniController controller)
        {
            base.Initialize(controller);
            
            if (vaultDetector == null)
            {
                vaultDetector = GetComponent<VaultDetector>() ?? GetComponentInChildren<VaultDetector>();
            }
            
            if (vaultDetector == null)
            {
                Debug.LogError($"VaultModule on {gameObject.name}: VaultDetector not found! Please assign or add VaultDetector component.", this);
            }
        }

        public override bool CanEnterState()
        {
            return _isVaulting ? CanContinueVault() : CanEnterVault();
        }

        public override void OnEnterState()
        {
            InitializeVault();
        }

        public override void OnExitState()
        {
            CompleteVault();
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (!_isVaulting) return;
            
            // Face the vault direction during vault
            var vaultData = vaultDetector.CurrentVaultData;
            if (vaultData.vaultDirection != Vector3.zero)
            {
                Vector3 targetDirection = Vector3.ProjectOnPlane(vaultData.vaultDirection, Vector3.up);
                
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
            
            UpdateVaultProgress();
            Vector3 targetPosition = CalculateTargetPosition();
            ApplyVaultMovement(ref currentVelocity, targetPosition, deltaTime);
            
            CheckVaultCompletion();
        }

        public override void BeforeCharacterUpdate(float deltaTime) { }
        public override void AfterCharacterUpdate(float deltaTime) { }

        public override void PostGroundingUpdate(float deltaTime)
        {
            // Allow early completion if landed during vault
            if (_isVaulting && _vaultProgress >= 0.8f && Motor.GroundingStatus.IsStableOnGround)
            {
                _vaultProgress = 1f;
            }
        }

        #region Private Methods - State Checking

        private bool CanContinueVault()
        {
            // Stay active until vault completes or lands early
            bool vaultComplete = _vaultProgress >= 1f;
            bool landedEarly = _vaultProgress >= 0.8f && Motor.GroundingStatus.IsStableOnGround;
            
            return !(vaultComplete || landedEarly);
        }

        private bool CanEnterVault()
        {
            if (!ValidateBasicConditions()) return false;
            if (!ValidateVaultConditions()) return false;
            if (!ValidateButtonRequirement()) return false;
            if (!ValidateTrajectory()) return false;
            
            return true;
        }

        private bool ValidateBasicConditions()
        {
            if (Conditions.RequireGrounded && !Motor.GroundingStatus.IsStableOnGround) return false;
            if (Conditions.RequireAirborne && Motor.GroundingStatus.IsStableOnGround) return false;
            if (Conditions.MinSpeed > 0f && Controller.Motor.BaseVelocity.magnitude < Conditions.MinSpeed) return false;
            if (Conditions.BlockIfCrouching && Controller.IsCrouching) return false;
            
            return Controller.IsSprinting && Motor.GroundingStatus.IsStableOnGround;
        }

        private bool ValidateVaultConditions()
        {
            if (vaultDetector == null) return false;
            if (Time.time - _lastVaultTime < Settings.VaultCooldown) return false;
            
            var vaultData = vaultDetector.CurrentVaultData;
            return vaultData.canVault && vaultData.inInitiationZone;
        }

        private bool ValidateButtonRequirement()
        {
            return !Settings.RequireButtonForVault || Controller.IsJumpRequested();
        }

        private bool ValidateTrajectory()
        {
            if (!Settings.UseDetectedTrajectory) return true;
            
            var vaultData = vaultDetector.CurrentVaultData;
            return vaultData.fullTrajectoryPoints != null && vaultData.fullTrajectoryPoints.Length >= 2;
        }

        #endregion

        #region Private Methods - Vault Execution

        private void InitializeVault()
        {
            _isVaulting = true;
            _vaultStartTime = Time.time;
            _vaultProgress = 0f;
            _lastVaultTime = Time.time;
            
            StoreVaultData();
            ConsumeInputs();
            PreparePhysics();
            UpdateAnimations();
        }

        private void StoreVaultData()
        {
            var vaultData = vaultDetector.CurrentVaultData;
            _currentVaultTrajectory = vaultData.fullTrajectoryPoints;
            _vaultStartPosition = Controller.transform.position;
            _vaultTargetPosition = vaultData.landingPoint;
        }

        private void ConsumeInputs()
        {
            if (Controller.IsJumpRequested())
            {
                Controller.ConsumeJumpRequest();
            }
        }

        private void PreparePhysics()
        {
            Motor.ForceUnground();
        }

        private void UpdateAnimations()
        {
            PlayerAnimator?.TriggerVault();
        }

        private void CompleteVault()
        {
            _isVaulting = false;
            _vaultProgress = 0f;
            _currentVaultTrajectory = null;

            PlayerAnimator?.SetVaultProgress(1f);
        }

        private void UpdateVaultProgress()
        {
            float vaultDuration = Settings.VaultDuration;
            _vaultProgress = Mathf.Clamp01((Time.time - _vaultStartTime) / vaultDuration);
            PlayerAnimator?.SetVaultProgress(_vaultProgress);
        }

        private Vector3 CalculateTargetPosition()
        {
            if (Settings.UseDetectedTrajectory && _currentVaultTrajectory != null && _currentVaultTrajectory.Length > 1)
            {
                return GetPositionOnTrajectory(_vaultProgress);
            }
            else
            {
                return CalculateSimpleVaultPosition(_vaultProgress);
            }
        }

        private void ApplyVaultMovement(ref Vector3 currentVelocity, Vector3 targetPosition, float deltaTime)
        {
            Vector3 currentPosition = Motor.TransientPosition;
            Vector3 positionDelta = targetPosition - currentPosition;
            
            // Set velocity to reach target in this frame
            currentVelocity = positionDelta / deltaTime;
            
            // Clamp for stability
            float maxVaultSpeed = Settings.VaultSpeed * 2f;
            if (currentVelocity.magnitude > maxVaultSpeed)
            {
                currentVelocity = currentVelocity.normalized * maxVaultSpeed;
            }
        }

        private void CheckVaultCompletion()
        {
            if (_vaultProgress >= 1f)
            {
                // Vault will be transitioned out by module system
            }
        }

        private Vector3 GetPositionOnTrajectory(float t)
        {
            if (_currentVaultTrajectory == null || _currentVaultTrajectory.Length < 2)
            {
                return Controller.transform.position;
            }
            
            // Convert normalized progress to trajectory point index
            float floatIndex = t * (_currentVaultTrajectory.Length - 1);
            int lowerIndex = Mathf.FloorToInt(floatIndex);
            int upperIndex = Mathf.Min(lowerIndex + 1, _currentVaultTrajectory.Length - 1);
            
            if (lowerIndex == upperIndex)
            {
                return _currentVaultTrajectory[lowerIndex];
            }
            
            // Interpolate between points
            float localT = floatIndex - lowerIndex;
            return Vector3.Lerp(_currentVaultTrajectory[lowerIndex], _currentVaultTrajectory[upperIndex], localT);
        }

        private Vector3 CalculateSimpleVaultPosition(float t)
        {
            Vector3 start = _vaultStartPosition;
            Vector3 end = _vaultTargetPosition;
            
            // Simple parabolic arc
            Vector3 linearPosition = Vector3.Lerp(start, end, t);
            float verticalOffset = Mathf.Sin(t * Mathf.PI) * 1.5f; // Fixed arc height
            
            return linearPosition + Vector3.up * verticalOffset;
        }

        #endregion

        #region Gizmos

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

        #endregion
    }
}