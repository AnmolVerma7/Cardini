using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovementAdvanced movement;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform orientation;

    [Header("Animation Parameters")]
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int isGroundedHash = Animator.StringToHash("isGrounded");
    private readonly int isSlidingHash = Animator.StringToHash("isSliding");
    private readonly int isCrouchingHash = Animator.StringToHash("isCrouching");
    private readonly int isWallRunningHash = Animator.StringToHash("isWallRunning");
    private readonly int jumpHash = Animator.StringToHash("Jump");
    private readonly int fallHash = Animator.StringToHash("Fall");
    
    private float lastYVelocity;
    private bool wasGrounded;

    private void Start()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovementAdvanced>();
            
        if (rb == null)
            rb = GetComponent<Rigidbody>();
            
        if (animator == null && transform.childCount > 0)
            animator = GetComponentInChildren<Animator>();
            
        wasGrounded = movement.grounded;
    }

    private void Update()
    {
        if (animator == null) return;

        // Handle locomotion speed
        float flatSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        animator.SetFloat(speedHash, flatSpeed);
        
        // Handle grounded state
        animator.SetBool(isGroundedHash, movement.grounded);
        
        // Handle movement states
        animator.SetBool(isSlidingHash, movement.sliding);
        animator.SetBool(isCrouchingHash, movement.crouching);
        animator.SetBool(isWallRunningHash, movement.wallrunning);
        
        // Handle jumps
        if (!wasGrounded && movement.grounded)
        {
            // Landing
            animator.SetTrigger("Land");
        }
        else if (wasGrounded && !movement.grounded && rb.linearVelocity.y > 0.1f)
        {
            // Starting to jump (going up)
            animator.SetTrigger(jumpHash);
        }
        else if (lastYVelocity > 0.1f && rb.linearVelocity.y < -0.1f)
        {
            // Started falling
            animator.SetTrigger(fallHash);
        }
        
        // Store previous values for next frame comparison
        wasGrounded = movement.grounded;
        lastYVelocity = rb.linearVelocity.y;
    }
}