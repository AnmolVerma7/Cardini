using System.Collections;
using UnityEngine;
using Cardini.Motion;

/// <summary>
/// Core movement controller that handles basic locomotion including walking, sprinting,
/// crouching, and jumping. Serves as the default movement state.
/// </summary>
public class BaseLocomotionModule : MovementModule
{
    public override int Priority => 0;

    #region Movement Settings
    [Header("Movement Speeds")]
    [SerializeField] private float normalSpeed = 7f;
    [SerializeField] private float sprintSpeed = 11f;
    [SerializeField] private float crouchSpeed = 2f;

    [Header("Input Options")]
    [SerializeField, Tooltip("Toggle crouch with single press, otherwise hold to crouch")]
    private bool useToggleCrouch = true;
    [SerializeField, Tooltip("Toggle sprint with single press, otherwise hold to sprint")]
    private bool useToggleSprint = false;

    [Header("Speed Transition")]
    [SerializeField] private float speedIncreaseMultiplier = 10f;
    [SerializeField] private float slopeIncreaseMultiplier = 2.5f;

    [Header("Physics")]
    [SerializeField] private float groundDrag = 5f;
    [SerializeField] private float airMultiplier = 0.4f;
    [SerializeField] private float airControlFactor = 0.5f;

    [Header("Jumping")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float jumpCooldown = 0.25f;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Step Handling")]
    [SerializeField] private bool enableAutoStep = true;
    [SerializeField] private float maxStepHeight = 0.4f;
    [SerializeField] private float stepSmooth = 0.1f;

    #endregion

    #region Private State Variables
    private float moveSpeed;
    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    private Coroutine speedLerpCoroutine;
    private bool isIdle = true;
    private bool isSprinting = false;
    private bool crouching = false;
    private Vector3 moveDirection;
    private bool exitingSlope;

    private bool readyToJump = true;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool didJumpThisTick = false;

    private bool isAnimatorGrounded = true;
    private float timeSinceLeftGround = 0f;
    private const float GROUNDED_BUFFER_TIME = 0.1f;

    private SlideModule cachedSlideModule;
    private Animator cachedAnimator;
    #endregion

    #region Initialization & Lifecycle Overrides
    public override void Initialize(CardiniController controller)
    {
        base.Initialize(controller);

        cachedSlideModule = controller.GetComponent<SlideModule>();

        if (controller.PlayerObj != null)
        {
            cachedAnimator = controller.PlayerObj.GetComponentInChildren<Animator>();
            if (cachedAnimator == null)
            {
                Debug.LogError("BaseLocomotionModule: Animator component not found on PlayerObj or its children!", controller.PlayerObj);
            }
        }
        else
        {
            Debug.LogError("BaseLocomotionModule: CardiniController.PlayerObj is null during Initialize!");
        }

        desiredMoveSpeed = 0f;
        moveSpeed = 0f;
        isIdle = true;

        readyToJump = true;
        jumpBufferCounter = 0;
        coyoteTimeCounter = 0;
        didJumpThisTick = false;

        if (controller.Rb != null) controller.Rb.useGravity = !controller.IsOnSlope;
    }

    public override void Activate()
    {
        bool log = controller.ShowDebugLogs;
        bool forceCrouch = controller.ForceCrouchStateOnNextBaseLocoActivation;

        if (forceCrouch)
        {
            controller.ForceCrouchStateOnNextBaseLocoActivation = false;
            EnterCrouchState(skipInitialEffects: true);
            if (log) Debug.Log($"<color=#90EE90>Base Locomotion:</color> Frame {Time.frameCount} | Activated (Forced Crouch)");
        }
        else
        {
            controller.ForceCrouchStateOnNextBaseLocoActivation = false;
            crouching = false;
            StateHandler(controller.IsGrounded);
            moveSpeed = desiredMoveSpeed;
            lastDesiredMoveSpeed = desiredMoveSpeed;
            if (log) Debug.Log($"<color=green>Base Locomotion:</color> Frame {Time.frameCount} | Activated normally, Target Speed: {moveSpeed:F1}");
        }

        readyToJump = true;
        jumpBufferCounter = 0;
        coyoteTimeCounter = controller.IsGrounded ? coyoteTime : 0;
        didJumpThisTick = false;
        exitingSlope = false;
        if (controller.Rb != null) controller.Rb.useGravity = !controller.IsOnSlope;

        if (cachedAnimator != null)
        {
        }
    }

    public override void Deactivate()
    {
        if (speedLerpCoroutine != null) { StopCoroutine(speedLerpCoroutine); speedLerpCoroutine = null; }
        CancelInvoke(nameof(ResetJumpCooldown));
        if (controller.ShowDebugLogs) Debug.Log($"<color=green>Base Locomotion:</color> Frame {Time.frameCount} | Deactivated");
    }
    #endregion

    #region MovementModule Overrides

    public override bool WantsToActivate() => true;

    public override void Tick()
    {
        didJumpThisTick = false;

        UpdateJumpTimingCounters();
        CheckAndExecuteJump();

        if (controller.activeMovementModule == this)
        {
            UpdateSprintState();
            UpdateCrouchState();
            StateHandler(controller.IsGrounded);
        }

        CalculateMoveDirection();
    }

    public override void FixedTick()
    {
        if (controller.Rb == null) return;

        bool grounded = controller.IsGrounded;
        ApplyMovementForces(grounded);

        controller.Rb.linearDamping = (grounded && !isIdle) ? groundDrag : 0;

        if (enableAutoStep && grounded && !crouching && moveDirection.magnitude > 0.1f)
        {
            MovementHelpers.AutoStep(controller.Rb, moveDirection, maxStepHeight, stepSmooth);
        }
    }
    #endregion

    #region Public Getters
    public float GetCurrentMoveSpeed() => moveSpeed;
    public bool IsCurrentlyCrouching => crouching;
    public bool IsCurrentlySprinting() => isSprinting;
    #endregion

    #region Core Logic Methods
    private void CalculateMoveDirection()
    {
        if (controller.Orientation != null && controller.Input != null)
        {
            moveDirection = MovementHelpers.CalculateMoveDirection(controller.Orientation, controller.Input.HorizontalInput, controller.Input.VerticalInput);
        }
        else
        {
            moveDirection = Vector3.zero;
        }
    }

    private void UpdateJumpTimingCounters()
    {
        if (controller.Input == null) return;
        jumpBufferCounter = MovementHelpers.UpdateJumpBuffer(jumpBufferCounter, jumpBufferTime, controller.Input.JumpPressed);
        if (controller.IsGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            if (controller.WasGrounded && !didJumpThisTick)
            {
                coyoteTimeCounter = coyoteTime;
            }
            else
            {
                coyoteTimeCounter = Mathf.Max(0f, coyoteTimeCounter - Time.deltaTime);
            }
        }
    }

    private void CheckAndExecuteJump()
    {
        if (jumpBufferCounter > 0 && readyToJump && (controller.IsGrounded || coyoteTimeCounter > 0))
        {
            bool wasOnGround = controller.IsGrounded;
            readyToJump = false;
            didJumpThisTick = true;
            jumpBufferCounter = 0;
            coyoteTimeCounter = 0;
            Jump(wasOnGround);
            CancelInvoke(nameof(ResetJumpCooldown));
            Invoke(nameof(ResetJumpCooldown), jumpCooldown);
        }
    }

    private void UpdateSprintState()
    {
        if (controller.Input == null) return;

        bool currentlyMoving = controller.Input.HorizontalInput != 0 || controller.Input.VerticalInput != 0;
        bool canPotentiallySprint = !crouching && controller.IsGrounded && currentlyMoving;

        if (useToggleSprint)
        {
            if (controller.Input.SprintTogglePressed)
            {
                if (isSprinting)
                {
                    isSprinting = false;
                }
                else if (canPotentiallySprint)
                {
                    isSprinting = true;
                }
            }
            else if (isSprinting && (!canPotentiallySprint || !currentlyMoving))
            {
                isSprinting = false;
            }
        }
        else
        {
            isSprinting = controller.Input.SprintHeld && canPotentiallySprint;
        }
    }

    private void UpdateCrouchState()
    {
        if (controller.Input == null) return;

        bool slideWantsToActivate = cachedSlideModule != null &&
                                    controller.Input.CrouchPressed &&
                                    controller.Input.SprintHeld &&
                                    controller.IsGrounded &&
                                    !controller.IsSliding;

        if (useToggleCrouch)
        {
            if (controller.Input.CrouchTogglePressed)
            {
                if (crouching)
                {
                    bool clearToStand = !CheckForCeiling();
                    if (clearToStand)
                    {
                        StopCrouch();
                    }
                }
                else
                {
                    bool canStartCrouch = controller.IsGrounded && !controller.IsSliding && !isSprinting;
                    if (canStartCrouch && !slideWantsToActivate)
                    {
                        StartCrouch();
                    }
                }
            }
        }
        else
        {
            bool canStartBasicCrouch = controller.IsGrounded && !crouching && !controller.IsSliding && !isSprinting && !slideWantsToActivate;

            if (controller.Input.CrouchPressed && canStartBasicCrouch)
            {
                StartCrouch();
            }
            else if (controller.Input.CrouchReleased && crouching)
            {
                bool clearToStand = !CheckForCeiling();
                if (clearToStand)
                {
                    StopCrouch();
                }
            }
            else if (crouching && !controller.IsGrounded)
            {
                StopCrouch();
            }
        }
    }

    private bool CheckForCeiling()
    {
        return false;
    }

    private void StateHandler(bool isCurrentlyGrounded_Physics)
    {
        if (controller.Input == null) return;

        bool previouslyAnimatorGrounded = isAnimatorGrounded;

        if (!isCurrentlyGrounded_Physics)
        {
            timeSinceLeftGround += Time.deltaTime;
            if (timeSinceLeftGround > GROUNDED_BUFFER_TIME)
            {
                isAnimatorGrounded = false;
            }
        }
        else
        {
            isAnimatorGrounded = true;
            timeSinceLeftGround = 0f;
        }

        if (!previouslyAnimatorGrounded && isAnimatorGrounded)
        {
            if (cachedAnimator != null)
            {
                cachedAnimator.ResetTrigger("Jump");
            }
        }

        isIdle = isCurrentlyGrounded_Physics && !crouching && 
                controller.Input.HorizontalInput == 0 && controller.Input.VerticalInput == 0;

        if (crouching)
        {
            desiredMoveSpeed = crouchSpeed;
            controller.SetMovementState("crouching");
            isIdle = false;
        }
        else if (isSprinting && isCurrentlyGrounded_Physics)
        {
            desiredMoveSpeed = sprintSpeed;
            controller.SetMovementState("sprinting");
            isIdle = false;
        }
        else if (isIdle)
        {
            desiredMoveSpeed = 0f;
            controller.SetMovementState("idle");
        }
        else if (isCurrentlyGrounded_Physics)
        {
            desiredMoveSpeed = normalSpeed;
            controller.SetMovementState("moving");
            isIdle = false;
        }
        else
        {
            desiredMoveSpeed = (lastDesiredMoveSpeed > 0f && lastDesiredMoveSpeed != crouchSpeed) 
                ? lastDesiredMoveSpeed 
                : normalSpeed;
            
            if (didJumpThisTick && (lastDesiredMoveSpeed == 0f || lastDesiredMoveSpeed == crouchSpeed))
            {
                desiredMoveSpeed = normalSpeed;
            }
            controller.SetMovementState("air");
            isIdle = false;
        }

        UpdateAnimatorParameters(isAnimatorGrounded);
        HandleSpeedTransition();
        lastDesiredMoveSpeed = desiredMoveSpeed;
    }

    private void UpdateAnimatorParameters(bool isGrounded_Animator)
    {
        if (cachedAnimator == null) return;

        if (isIdle)
        {
            cachedAnimator.SetFloat("Speed", 0f);
        }
        else
        {
            float animatorSpeed = 0f;
            if (isGrounded_Animator && !crouching) animatorSpeed = isSprinting ? 2.0f : 1.0f;

            cachedAnimator.SetFloat("Speed", animatorSpeed, 0.1f, Time.deltaTime);
        }

        cachedAnimator.SetBool("Grounded", isGrounded_Animator);
        cachedAnimator.SetBool("FreeFall", !isGrounded_Animator);
        cachedAnimator.SetBool("IsCrouching", crouching);

        float motionSpeedMultiplier = 1f;
        if (!isIdle && isGrounded_Animator && desiredMoveSpeed > 0.1f && moveSpeed > 0.1f)
        {
            motionSpeedMultiplier = Mathf.Clamp(moveSpeed / desiredMoveSpeed, 0.8f, 1.2f);
        }
        cachedAnimator.SetFloat("MotionSpeed", motionSpeedMultiplier);

        if (controller.activeMovementModule == this)
        {
            cachedAnimator.SetBool("IsSliding", false);
            cachedAnimator.SetBool("IsWallRunning", false);
            cachedAnimator.SetFloat("WallRunDirection", 0f);
        }
    }

    private void HandleSpeedTransition()
    {
        if (Mathf.Approximately(moveSpeed, desiredMoveSpeed))
        {
            if (speedLerpCoroutine != null)
            {
                StopCoroutine(speedLerpCoroutine);
                speedLerpCoroutine = null;
            }
            return;
        }

        bool accelerating = desiredMoveSpeed > moveSpeed && desiredMoveSpeed > 0;
        bool startingFromIdle = isIdle && desiredMoveSpeed > 0;
        bool startingSprint = isSprinting && !Mathf.Approximately(lastDesiredMoveSpeed, sprintSpeed);
        bool shouldSnap = startingFromIdle || startingSprint || (controller.IsGrounded && accelerating);

        if (shouldSnap)
        {
            if (speedLerpCoroutine != null)
            {
                StopCoroutine(speedLerpCoroutine);
                speedLerpCoroutine = null;
            }
            moveSpeed = desiredMoveSpeed;
        }
        else
        {
            if (speedLerpCoroutine == null)
            {
                speedLerpCoroutine = StartCoroutine(SmoothlyLerpMoveSpeed());
            }
        }
    }

    private void ApplyMovementForces(bool isCurrentlyGrounded)
    {
        if (controller.Rb == null) return;
        if (isIdle && isCurrentlyGrounded) return;

        Vector3 effectiveMoveDir = moveDirection.normalized;

        if (controller.IsOnSlope && !exitingSlope)
        {
            Vector3 slopeMoveDir = MovementHelpers.GetSlopeMoveDirection(effectiveMoveDir, controller.SlopeHit);
            controller.Rb.AddForce(slopeMoveDir * moveSpeed * 20f, ForceMode.Force);
            if (controller.Rb.linearVelocity.y > 0 && effectiveMoveDir.magnitude > 0.1f)
            {
                controller.Rb.AddForce(Vector3.down * 80f, ForceMode.Force);
            }
        }
        else if (isCurrentlyGrounded)
        {
            controller.Rb.AddForce(effectiveMoveDir * moveSpeed * 10f, ForceMode.Force);
        }
        else
        {
            controller.Rb.AddForce(effectiveMoveDir * moveSpeed * airMultiplier * airControlFactor * 10f, ForceMode.Force);
        }

        controller.Rb.useGravity = !controller.IsOnSlope || exitingSlope;
    }
    #endregion

    #region Actions (Jump, Crouch)
    private void Jump(bool wasGrounded)
    {
        exitingSlope = true;
        if (controller.Rb != null)
        {
            controller.Rb.linearVelocity = new Vector3(controller.Rb.linearVelocity.x, 0f, controller.Rb.linearVelocity.z);
            controller.Rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        if (cachedAnimator != null)
        {
            cachedAnimator.SetTrigger("Jump");
        }
    }

    private void ResetJumpCooldown()
    {
        readyToJump = true;
        exitingSlope = false;
    }

    private void EnterCrouchState(bool skipInitialEffects = false)
    {
        if (crouching) return;
        crouching = true;
        isSprinting = false;
        isIdle = false;

        if (!skipInitialEffects)
        {
            if (controller.Rb != null) controller.Rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }

        desiredMoveSpeed = crouchSpeed;
        moveSpeed = crouchSpeed;
        lastDesiredMoveSpeed = crouchSpeed;
        controller.SetMovementState("crouching");

        if (speedLerpCoroutine != null)
        {
            StopCoroutine(speedLerpCoroutine);
            speedLerpCoroutine = null;
        }
    }

    private void StartCrouch() { EnterCrouchState(skipInitialEffects: false); }

    private void StopCrouch()
    {
        if (!crouching) return;
        crouching = false;
    }
    #endregion

    #region Coroutines
    private IEnumerator SmoothlyLerpMoveSpeed()
    {
        float time = 0;
        float startValue = moveSpeed;
        float initialTarget = desiredMoveSpeed;
        float journey = Mathf.Abs(initialTarget - startValue);

        if (journey < 0.01f)
        {
            moveSpeed = initialTarget;
            speedLerpCoroutine = null;
            yield break;
        }

        while (time < journey && Mathf.Abs(moveSpeed - initialTarget) > 0.01f)
        {
            if (!Mathf.Approximately(desiredMoveSpeed, initialTarget))
            {
                speedLerpCoroutine = null;
                yield break;
            }

            float deltaTime = Time.unscaledDeltaTime;
            float rateMultiplier = speedIncreaseMultiplier;

            if (controller.IsOnSlope)
            {
                rateMultiplier *= slopeIncreaseMultiplier * (1 + (Vector3.Angle(Vector3.up, controller.SlopeHit.normal) / 90f));
            }

            rateMultiplier = Mathf.Max(1f, rateMultiplier);
            moveSpeed = Mathf.MoveTowards(moveSpeed, initialTarget, rateMultiplier * deltaTime);
            time += deltaTime * rateMultiplier;
            yield return null;
        }

        if (Mathf.Approximately(desiredMoveSpeed, initialTarget))
        {
            moveSpeed = initialTarget;
        }

        speedLerpCoroutine = null;
    }
    #endregion
}