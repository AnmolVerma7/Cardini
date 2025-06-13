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
        private readonly int _hashMovementState = Animator.StringToHash("MovementState"); // You added this
        private readonly int _hashLand = Animator.StringToHash("Land"); // <<< ADD THIS HASH

        [Header("Animation Smoothing")]
        [SerializeField] private float _speedTierDampTime = 0.1f; // For the overall speed/tier parameter
        [SerializeField] private float _directionalDampTime = 0.05f; // For VelocityX/Z

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
    }
}