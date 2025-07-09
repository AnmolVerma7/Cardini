using UnityEngine;

namespace Cardini.Motion
{
    /// <summary>
    /// Bridge between the character controller and Unity's Animator system.
    /// Handles all animation parameter updates with proper smoothing and state management.
    /// </summary>
    public class PlayerAnimatorBridge : MonoBehaviour, IPlayerAnimator
    {
        [Header("Animator Reference")]
        [SerializeField] private Animator animator;

        [Header("Animation Smoothing")]
        [SerializeField] private float _speedTierDampTime = 0.1f;
        [SerializeField] private float _directionalDampTime = 0.05f;
        [SerializeField] private float _vaultProgressDampTime = 0.05f;

        // Cached animator parameter hashes for performance
        private readonly int _hashGrounded = Animator.StringToHash("Grounded");
        private readonly int _hashIsCrouching = Animator.StringToHash("IsCrouching");
        private readonly int _hashVelocityX = Animator.StringToHash("VelocityX");
        private readonly int _hashVelocityZ = Animator.StringToHash("VelocityZ");
        private readonly int _hashNormalizedSpeed = Animator.StringToHash("NormalizedSpeed");
        private readonly int _hashJump = Animator.StringToHash("Jump");
        private readonly int _hashMovementState = Animator.StringToHash("MovementState");
        private readonly int _hashLand = Animator.StringToHash("Land");
        private readonly int _hashIsSliding = Animator.StringToHash("IsSliding");
        private readonly int _hashIsWallRunning = Animator.StringToHash("IsWallRunning");
        private readonly int _hashWallDirection = Animator.StringToHash("WallRunDirection");
        private readonly int _hashVault = Animator.StringToHash("Vault");
        private readonly int _hashVaultProgress = Animator.StringToHash("VaultProgress");
        private readonly int _hashIsVaulting = Animator.StringToHash("IsVaulting");

        void Awake()
        {
            InitializeAnimator();
        }

        #region IPlayerAnimator Implementation

        public void SetGrounded(bool isGrounded)
        {
            if (IsAnimatorReady())
                animator.SetBool(_hashGrounded, isGrounded);
        }

        public void SetCrouching(bool isCrouching)
        {
            if (IsAnimatorReady())
                animator.SetBool(_hashIsCrouching, isCrouching);
        }

        public void SetSliding(bool isSliding)
        {
            if (IsAnimatorReady())
                animator.SetBool(_hashIsSliding, isSliding);
        }

        public void SetWallRunning(bool isWallRunning, float wallDirection)
        {
            if (IsAnimatorReady())
            {
                animator.SetBool(_hashIsWallRunning, isWallRunning);
                animator.SetFloat(_hashWallDirection, wallDirection);
            }
        }

        public void SetLocomotionSpeeds(float normalizedSpeed, float velocityX, float velocityZ)
        {
            if (IsAnimatorReady())
            {
                animator.SetFloat(_hashNormalizedSpeed, normalizedSpeed, _speedTierDampTime, Time.deltaTime);
                animator.SetFloat(_hashVelocityX, velocityX, _directionalDampTime, Time.deltaTime);
                animator.SetFloat(_hashVelocityZ, velocityZ, _directionalDampTime, Time.deltaTime);
            }
        }

        public void SetMovementState(CharacterMovementState movementState)
        {
            if (IsAnimatorReady())
                animator.SetInteger(_hashMovementState, (int)movementState);
        }

        public void TriggerJump()
        {
            if (IsAnimatorReady())
                animator.SetTrigger(_hashJump);
        }

        public void TriggerLand()
        {
            if (IsAnimatorReady())
                animator.SetTrigger(_hashLand);
        }

        public void TriggerVault()
        {
            if (IsAnimatorReady())
            {
                animator.SetTrigger(_hashVault);
                animator.SetBool(_hashIsVaulting, true);
            }
        }

        public void SetVaultProgress(float progress)
        {
            if (IsAnimatorReady())
            {
                animator.SetFloat(_hashVaultProgress, progress, _vaultProgressDampTime, Time.deltaTime);

                // Auto-complete vaulting when progress reaches 1
                if (progress >= 1f)
                {
                    animator.SetBool(_hashIsVaulting, false);
                }
            }
        }

        #endregion

        #region Public Utility Methods

        /// <summary>
        /// Sets the animator's playback speed (useful for ability selection time scaling)
        /// </summary>
        public void SetAnimatorSpeed(float speed)
        {
            if (IsAnimatorReady())
                animator.speed = speed;
        }

        #endregion

        #region Private Methods

        private void InitializeAnimator()
        {
            // Find animator if not assigned
            if (animator == null) 
                animator = GetComponent<Animator>();
            
            if (animator == null) 
                animator = GetComponentInChildren<Animator>();

            // Disable component if animator is missing
            if (animator == null)
            {
                Debug.LogError("PlayerAnimatorBridge: Animator component not found. Disabling bridge.", this);
                enabled = false;
            }
        }

        private bool IsAnimatorReady()
        {
            return enabled && animator != null;
        }

        #endregion
    }
}