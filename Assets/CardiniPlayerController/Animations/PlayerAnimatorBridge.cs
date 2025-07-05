// PlayerAnimatorBridge.cs
// Ensure this is in your Cardini.Motion namespace
using UnityEngine;

namespace Cardini.Motion
{
    public class PlayerAnimatorBridge : MonoBehaviour, IPlayerAnimator
    {
        [Header("Animator Reference")]
        [SerializeField] private Animator animator;

        // Animator Parameter Hashes
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

        // Vaulting Animation Parameters
        private readonly int _hashVault = Animator.StringToHash("Vault");
        private readonly int _hashVaultProgress = Animator.StringToHash("VaultProgress");
        private readonly int _hashIsVaulting = Animator.StringToHash("IsVaulting");

        [Header("Animation Smoothing")]
        [SerializeField] private float _speedTierDampTime = 0.1f; // For the overall speed/tier parameter
        [SerializeField] private float _directionalDampTime = 0.05f; // For VelocityX/Z
        [SerializeField] private float _vaultProgressDampTime = 0.05f;

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (animator == null)
            {
                Debug.LogError("PlayerAnimatorBridge: Animator component not found. Disabling bridge.", this);
                enabled = false; // Good idea to disable if animator is missing
                return;
            }
        }

        public void SetGrounded(bool isGrounded)
        {
            if (enabled) animator.SetBool(_hashGrounded, isGrounded);
        }

        public void SetCrouching(bool isCrouching)
        {
            if (enabled) animator.SetBool(_hashIsCrouching, isCrouching);
        }

        public void SetSliding(bool isSliding)
        {
            if (enabled) animator.SetBool(_hashIsSliding, isSliding);
        }

        public void SetWallRunning(bool isWallRunning, float wallDirection)
        {
            if (enabled)
            {
                animator.SetBool(_hashIsWallRunning, isWallRunning);
                animator.SetFloat(_hashWallDirection, wallDirection);
            }
        }

        public void SetLocomotionSpeeds(float normalizedSpeed, float velocityX, float velocityZ)
        {
            if (enabled)
            {
                animator.SetFloat(_hashNormalizedSpeed, normalizedSpeed, _speedTierDampTime, Time.deltaTime);
                animator.SetFloat(_hashVelocityX, velocityX, _directionalDampTime, Time.deltaTime);
                animator.SetFloat(_hashVelocityZ, velocityZ, _directionalDampTime, Time.deltaTime);
            }
        }

        public void SetMovementState(CharacterMovementState movementState)
        {
            if (enabled) animator.SetInteger(_hashMovementState, (int)movementState);
        }

        public void SetAnimatorSpeed(float speed)
        {
            if (enabled && animator != null) animator.speed = speed;
        }

        public void TriggerJump()
        {
            if (enabled && animator != null) animator.SetTrigger(_hashJump);
        }

        public void TriggerLand()
        {
            if (enabled && animator != null)
                animator.SetTrigger(_hashLand);
        }

        public void TriggerVault()
        {
            if (enabled && animator != null)
            {
                animator.SetTrigger(_hashVault);
                animator.SetBool(_hashIsVaulting, true);
                Debug.Log("[Animator] Vault animation triggered");
            }
        }

        public void SetVaultProgress(float progress)
        {
            if (enabled && animator != null)
            {
                animator.SetFloat(_hashVaultProgress, progress, _vaultProgressDampTime, Time.deltaTime);

                // Auto-end vaulting when progress complete
                if (progress >= 1f)
                {
                    animator.SetBool(_hashIsVaulting, false);
                    Debug.Log("[Animator] Vault animation completed");
                }
            }

        }
    }
}