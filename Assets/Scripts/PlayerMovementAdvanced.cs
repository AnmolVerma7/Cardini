using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerMovementAdvanced : MonoBehaviour
{
    [Header("Movement")]
    private float moveSpeed;
    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    public float normalSpeed; // Renamed from sprintSpeed, now the only regular movement speed
    public float slideSpeed;
    public float maxSlideSpeed; // Added for slope sliding
    public float wallrunSpeed;

    public float speedIncreaseMultiplier;
    public float slopeIncreaseMultiplier;

    public float groundDrag;

    [Header("Jumping")]
    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    bool readyToJump;

    [Header("Coyote Time")]
    public float coyoteTime = 0.2f; // Time window after leaving ground where jump is still allowed
    private float coyoteTimeCounter;
    
    [Header("Jump Buffering")]
    public float jumpBufferTime = 0.2f; // Time window where jump input is remembered
    private float jumpBufferCounter;
    
    [Header("Advanced Movement")]
    public bool enableAirControl = true;
    public float airControlFactor = 0.5f; // How responsive air control is compared to ground movement
    
    [Header("Movement Feedback")]
    public bool enableJumpSquash = true;
    public float jumpSquashAmount = 0.2f;
    public float jumpStretchAmount = 0.2f;
    public float squashRecoverySpeed = 8f;
    private Vector3 originalScale;
    
    [Header("Step Handling")]
    public float maxStepHeight = 0.4f;
    public float stepSmooth = 0.1f;
    public bool enableAutoStep = true;

    [Header("Crouching")]
    public float crouchSpeed;
    public float crouchYScale;
    private float startYScale;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask whatIsGround;
    public bool grounded;
    private bool wasGrounded; // To track when we just left the ground

    [Header("Slope Handling")]
    public float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;


    public Transform orientation;

    float horizontalInput;
    float verticalInput;

    Vector3 moveDirection;

    Rigidbody rb;

    public MovementState state;
    public enum MovementState
    {
        moving,
        wallrunning,
        crouching,
        sliding,
        air
    }

    public bool sliding;
    public bool crouching;
    public bool wallrunning;

    public TextMeshProUGUI text_speed;
    public TextMeshProUGUI text_mode;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        readyToJump = true;

        startYScale = transform.localScale.y;
        originalScale = transform.localScale;
    }

    private void Update()
    {
        // Store previous grounded state before update
        wasGrounded = grounded;
        
        // ground check
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        // Coyote time logic
        if (wasGrounded && !grounded)
        {
            // Just left the ground, start coyote time
            coyoteTimeCounter = coyoteTime;
        }
        else if (grounded)
        {
            // On ground, reset coyote time
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            // In air, count down coyote time
            coyoteTimeCounter -= Time.deltaTime;
        }

        MyInput();
        SpeedControl();
        StateHandler();
        TextStuff();

        // handle drag
        if (grounded)
            rb.linearDamping = groundDrag;
        else
            rb.linearDamping = 0;
            
        // Auto step handling
        if (enableAutoStep && grounded && !sliding && !crouching)
        {
            AutoStep();
        }
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // Handle jump buffer timer
        if (Input.GetKeyDown(jumpKey))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // when to jump - now using coyote time
        if (jumpBufferCounter > 0 && readyToJump && (grounded || coyoteTimeCounter > 0))
        {
            readyToJump = false;
            jumpBufferCounter = 0; // Reset buffer once jump is triggered
            
            Jump();

            Invoke(nameof(ResetJump), jumpCooldown);
        }

        // start crouch
        if (Input.GetKeyDown(crouchKey) && horizontalInput == 0 && verticalInput == 0)
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);

            crouching = true;
        }

        // stop crouch
        if (Input.GetKeyUp(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);

            crouching = false;
        }
    }

    private void StateHandler()
    {
        // Mode - Wallrunning
        if (wallrunning)
        {
            state = MovementState.wallrunning;
            desiredMoveSpeed = wallrunSpeed;
        }

        // Mode - Sliding
        else if (sliding)
        {
            state = MovementState.sliding;

            // Use maxSlideSpeed on slopes, regular slideSpeed otherwise
            if (OnSlope() && rb.linearVelocity.y < 0.1f)
                desiredMoveSpeed = maxSlideSpeed;
            else
                desiredMoveSpeed = slideSpeed;
        }

        // Mode - Crouching
        else if (crouching)
        {
            state = MovementState.crouching;
            desiredMoveSpeed = crouchSpeed;
        }

        // Mode - Moving (combined walking/sprinting)
        else if (grounded)
        {
            state = MovementState.moving;
            desiredMoveSpeed = normalSpeed;
        }

        // Mode - Air
        else
        {
            state = MovementState.air;
        }

        // check if desired move speed has changed drastically
        if (Mathf.Abs(desiredMoveSpeed - lastDesiredMoveSpeed) > 4f && moveSpeed != 0)
        {
            StopAllCoroutines();
            StartCoroutine(SmoothlyLerpMoveSpeed());
        }
        else
        {
            moveSpeed = desiredMoveSpeed;
        }

        lastDesiredMoveSpeed = desiredMoveSpeed;
    }

    private IEnumerator SmoothlyLerpMoveSpeed()
    {
        // smoothly lerp movementSpeed to desired value
        float time = 0;
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
        float startValue = moveSpeed;

        while (time < difference)
        {
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / difference);

            if (OnSlope())
            {
                float slopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
                float slopeAngleIncrease = 1 + (slopeAngle / 90f);

                time += Time.deltaTime * speedIncreaseMultiplier * slopeIncreaseMultiplier * slopeAngleIncrease;
            }
            else
                time += Time.deltaTime * speedIncreaseMultiplier;

            yield return null;
        }

        moveSpeed = desiredMoveSpeed;
    }

    private void MovePlayer()
    {
        // calculate movement direction
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // on slope
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection(moveDirection) * moveSpeed * 20f, ForceMode.Force);

            if (rb.linearVelocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }

        // on ground
        else if (grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);

        // in air with air control
        else if (!grounded && enableAirControl)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier * airControlFactor, ForceMode.Force);

        // turn gravity off while on slope
        if(!wallrunning) rb.useGravity = !OnSlope();
    }

    private void SpeedControl()
    {
        // limiting speed on slope
        if (OnSlope() && !exitingSlope)
        {
            if (rb.linearVelocity.magnitude > moveSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * moveSpeed;
        }

        // limiting speed on ground or in air
        else
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

            // limit velocity if needed
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
            }
        }
    }

    private void Jump()
    {
        exitingSlope = true;

        // reset y velocity
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
        
        // Apply jump squash and stretch if enabled
        if (enableJumpSquash)
        {
            StartCoroutine(JumpSquashAndStretch());
        }
    }
    
    private IEnumerator JumpSquashAndStretch()
    {
        // Initial squash before jump
        transform.localScale = new Vector3(
            originalScale.x * (1f + jumpSquashAmount),
            originalScale.y * (1f - jumpSquashAmount),
            originalScale.z * (1f + jumpSquashAmount)
        );
        
        yield return new WaitForSeconds(0.1f);
        
        // Stretch during jump
        transform.localScale = new Vector3(
            originalScale.x * (1f - jumpStretchAmount),
            originalScale.y * (1f + jumpStretchAmount),
            originalScale.z * (1f - jumpStretchAmount)
        );
        
        // Gradually return to original scale
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                originalScale,
                Time.deltaTime * squashRecoverySpeed
            );
            yield return null;
        }
        
        // Ensure we end at exactly the original scale
        transform.localScale = originalScale;
    }
    
    private void ResetJump()
    {
        readyToJump = true;

        exitingSlope = false;
    }

    public bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }
    
    private void AutoStep()
    {
        // Check if there's an obstacle ahead
        Vector3 moveDir = moveDirection.normalized;
        if (moveDir.magnitude < 0.1f) return; // Skip if not moving

        // Cast a ray forward to check for obstacles
        RaycastHit hit;
        if (Physics.Raycast(transform.position + new Vector3(0, 0.05f, 0), moveDir, out hit, 0.6f))
        {
            // Check if there's space above the obstacle
            if (!Physics.Raycast(transform.position + new Vector3(0, maxStepHeight, 0), moveDir, 0.7f))
            {
                // Cast a ray down to check if we can stand on the step
                RaycastHit hitTop;
                if (Physics.Raycast(transform.position + new Vector3(0, maxStepHeight, 0) + moveDir * 0.6f, 
                                    Vector3.down, out hitTop, maxStepHeight * 2))
                {
                    // Found a valid step, move the player up
                    rb.position = Vector3.Lerp(rb.position, 
                                              new Vector3(rb.position.x, hitTop.point.y + 0.10f, rb.position.z), 
                                              stepSmooth);
                }
            }
        }
    }

    private void TextStuff()
    {
        if (!text_speed || !text_mode) return;
        
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (OnSlope())
            text_speed.SetText("Speed: " + Round(rb.linearVelocity.magnitude, 1));
        else
            text_speed.SetText("Speed: " + Round(flatVel.magnitude, 1));

        text_mode.SetText(state.ToString());
    }

    public static float Round(float value, int digits)
    {
        float mult = Mathf.Pow(10.0f, (float)digits);
        return Mathf.Round(value * mult) / mult;
    }
}