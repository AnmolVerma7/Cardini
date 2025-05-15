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

    [Header("Animation Smoothing")]
    [Tooltip("How quickly the master locomotion blend (Idle/Walk/Jog/Boost) transitions between speed tiers in the Animator. Lower = faster/snappier.")]
    [SerializeField] private float speedTierDampTime = 0.1f;
    [Tooltip("How quickly the directional VelocityX/Z parameters blend in the Animator for 2D blend trees. Lower = faster/snappier.")]
    [SerializeField] private float directionalVelocityDampTime = 0.05f;

    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 7f;
    [SerializeField] private float jogSpeed = 7f; 
    [SerializeField] private float sprintSpeed = 11f;
    [SerializeField] private float crouchSpeed = 2f;

    [Header("Input Options")]
    [SerializeField, Tooltip("Toggle crouch with single press, otherwise hold to crouch")]
    private bool useToggleCrouch = true;
    [SerializeField, Tooltip("Toggle sprint with single press, otherwise hold to sprint")]
    private bool useToggleSprint = false;
    [SerializeField, Tooltip("ONLY IF Use Toggle Sprint is TRUE: If true, sprinting automatically resumes after stopping movement if the sprint toggle is still ON. If false, stopping movement always requires another sprint press/toggle to turn sprint MODE off.")]
    private bool resumeSprintAfterIdle = false;

    // [Header("Analog Control Thresholds")] // Optional new header
    [SerializeField, Range(0.01f, 0.5f), Tooltip("Minimum analog stick input magnitude to transition from Idle to Walk.")]
    private float walkInputMagnitudeThreshold = 0.1f;
    [SerializeField, Range(0.51f, 0.99f), Tooltip("Minimum analog stick input magnitude to transition from Walk to Jog. Full stick push beyond this will aim for Jog/Sprint.")]
    private float jogInputMagnitudeThreshold = 0.6f;

    [Header("Speed Transition")]
    [SerializeField] private float speedIncreaseMultiplier = 10f;
    [SerializeField] private float slopeIncreaseMultiplier = 2.5f;

    [Header("Physics")]
    [SerializeField] private float groundDrag = 5f;
    [SerializeField] private float airMultiplier = 0.4f;
    [SerializeField] private float airControlFactor = 0.5f;

    [Header("Jumping")]
    // [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float jumpCooldown = 0.25f;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Variable Jump Settings")]
    [SerializeField, Tooltip("Force applied for a tap jump or uncharged release.")]
    private float minJumpForce = 6f;
    [SerializeField, Tooltip("Force applied for a fully charged jump.")]
    private float maxJumpForce = 12f;
    [SerializeField, Tooltip("Time (seconds) to hold jump button to reach maxJumpForce.")]
    private float jumpChargeTime = 0.3f;

     [Header("Speed-Based Jump Modifiers")]
        [Tooltip("Multiplier applied to jump force when walking. 1 = no change.")]
        [SerializeField] private float walkJumpForceMultiplier = 1.0f; // Default to 1 (no change) initially
        [Tooltip("Multiplier applied to jump force when jogging. 1 = no change.")]
        [SerializeField] private float jogJumpForceMultiplier = 1.05f; // Slight boost for jogging
        [Tooltip("Multiplier applied to jump force when sprinting. 1 = no change.")]
        [SerializeField] private float sprintJumpForceMultiplier = 1.1f; // More boost for sprinting

    [Header("High Fall & Roll Prep")]
    [SerializeField, Tooltip("Minimum time in air (seconds) to be considered a high fall, potentially triggering a roll.")]
    private float minTimeForHighFall = 0.8f;
    [SerializeField, Tooltip("Duration (seconds) the roll mechanic would affect the player (placeholder for now).")]
    private float rollDuration = 0.5f;
    [SerializeField, Tooltip("Speed multiplier during the roll placeholder. <1 to slow down.")]
    private float rollSpeedFactor = 0.7f;

    [Header("Step Handling")]
    [SerializeField] private bool enableAutoStep = true;
    [SerializeField] private float maxStepHeight = 0.4f;
    [SerializeField] private float stepSmooth = 0.1f;

    #endregion

    #region Public Properties (for external access)
    /// <summary>Gets whether toggle crouch input mode is enabled.</summary>
    public bool UseToggleCrouch => useToggleCrouch;
    /// <summary>Gets whether toggle sprint input mode is enabled.</summary>
    public bool UseToggleSprint => useToggleSprint;
    /// <summary>Gets whether sprint should resume after idle when toggle sprint is enabled.</summary>
    public bool ResumeSprintAfterIdle => resumeSprintAfterIdle;
    public float GetWalkInputThreshold() => walkInputMagnitudeThreshold;
    public float GetJogInputThreshold() => jogInputMagnitudeThreshold;
    #endregion

    #region Private State Variables
    private float moveSpeed;
    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    private Coroutine speedLerpCoroutine;
    private bool isIdle = true;
    private bool isSprinting = false;
    private bool _sprintToggleState = false; // <<< ADD THIS
    private bool crouching = false;
    private Vector3 moveDirection;
    private bool exitingSlope;

    private bool readyToJump = true;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool didJumpThisTick = false;

    // --- NEW JUMP STATE VARIABLES ---
    private bool _isJumpButtonPressedThisFrame = false; // To detect initial press for charging
    private bool _isJumpButtonHeld = false;          // True while jump button is physically held
    private float _jumpChargeTimer = 0f;             // How long jump has been charging
    private bool _jumpChargeIsMax = false;        // True if held long enough for max jump
    private bool _pendingBufferedJump = false;       // For jump buffering, true if a jump input is queued
    // --- END NEW JUMP STATE VARIABLES ---

    // --- NEW HIGH FALL STATE VARIABLES ---
    private float _timeInAir = 0f;
    private bool _isPerformingRollPlaceholder = false; // True during the roll placeholder effect
    // --- END NEW HIGH FALL STATE VARIABLES ---

    private bool isAnimatorGrounded = true;
    private float timeSinceLeftGround = 0f;
    private const float GROUNDED_BUFFER_TIME = 0.1f;

    private SlideModule cachedSlideModule;
    private Animator cachedAnimator;
    private FreeLookOrientation cachedFreeLookOrientation;
    #endregion

    #region Initialization & Lifecycle Overrides
    public override void Initialize(CardiniController controller)
    {
        base.Initialize(controller);

        cachedSlideModule = controller.GetComponent<SlideModule>();
        cachedFreeLookOrientation = controller.GetComponent<FreeLookOrientation>();
        if (cachedFreeLookOrientation == null) Debug.LogWarning($"{GetType().Name}: FreeLookOrientation component not found on Player object.", this);
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

        _sprintToggleState = false;
        isSprinting = false; 
        crouching = false; 

        readyToJump = true;
        jumpBufferCounter = 0;
        coyoteTimeCounter = 0;
        didJumpThisTick = false;

        if (controller.Rb != null) controller.Rb.useGravity = !controller.IsOnSlope;
    }

    public override void Activate()
    {
        bool log = controller.ShowDebugLogs;

        // --- CONSOLIDATED AND CORRECTED ROTATION RESET ---
        // Always ensure the capsule is world-upright when BaseLocomotion becomes active.
        // Preserve the horizontal facing direction based on the camera's orientation.
        Vector3 desiredForwardHorizontal = controller.Orientation.forward;
        desiredForwardHorizontal.y = 0; // Ensure it's horizontal

        // If camera orientation gives a zero vector (e.g., looking straight up/down),
        // try to use player's current horizontal forward, or world forward as last resort.
        if (desiredForwardHorizontal.sqrMagnitude < 0.001f)
        {
            desiredForwardHorizontal = Vector3.ProjectOnPlane(controller.transform.forward, Vector3.up);
            if (desiredForwardHorizontal.sqrMagnitude < 0.001f)
            {
                desiredForwardHorizontal = Vector3.forward;
            }
        }

        if (controller.Rb.IsSleeping())
        {
            controller.Rb.WakeUp();
        }
        controller.Rb.isKinematic = false;
        
        Quaternion targetUprightRotation = Quaternion.LookRotation(desiredForwardHorizontal.normalized, Vector3.up);
        controller.Rb.MoveRotation(targetUprightRotation); 
        
        if (log && controller.transform.up != Vector3.up) // Should ideally not happen after above line, but good for sanity check
        {
            Debug.LogWarning($"<color=orange>BaseLoco Activate:</color> Attempted to reset rotation, but transform.up is still {controller.transform.up}. This might indicate an issue.");
        }
        else if (log)
        {
            // Debug.Log($"<color=green>BaseLoco Activate:</color> Ensured player rotation is world upright. Forward: {controller.transform.forward}, Up: {controller.transform.up}");
        }
        // --- END CONSOLIDATED ROTATION RESET ---

        bool forceCrouch = controller.ForceCrouchStateOnNextBaseLocoActivation;

        if (forceCrouch)
        {
            _sprintToggleState = false;
            isSprinting = false;
            controller.ForceCrouchStateOnNextBaseLocoActivation = false;
            EnterCrouchState(skipInitialEffects: true); // This calls SetMovementState(Crouching)
            // desiredMoveSpeed is set in EnterCrouchState
            if (log) Debug.Log($"<color=#90EE90>BaseLoco Activate:</color> Forced Crouch. State: {controller.CurrentMovementState}");
        }
        else // Not forced to crouch
        {
            _sprintToggleState = false;
            isSprinting = false;
            controller.ForceCrouchStateOnNextBaseLocoActivation = false;
            crouching = false; // Ensure internal crouch flag is off

            // Determine initial movement state and desired speed
            if (controller.IsGrounded) // IsGrounded check should be more reliable now that capsule is upright
            {
                float inputMag = new Vector2(controller.Input.HorizontalInput, controller.Input.VerticalInput).magnitude;
                if (inputMag >= jogInputMagnitudeThreshold)
                {
                    controller.SetMovementState(CharacterMovementState.Jogging);
                    desiredMoveSpeed = jogSpeed;
                }
                else if (inputMag >= walkInputMagnitudeThreshold)
                {
                    controller.SetMovementState(CharacterMovementState.Walking);
                    desiredMoveSpeed = walkSpeed;
                }
                else
                {
                    controller.SetMovementState(CharacterMovementState.Idle);
                    desiredMoveSpeed = 0f;
                }
            }
            else // Not Grounded
            {
                controller.SetMovementState(CharacterMovementState.Falling);
                // Determine airborne speed (e.g., maintain some momentum or default to walkSpeed)
                if (lastDesiredMoveSpeed > walkSpeed * 0.5f) desiredMoveSpeed = lastDesiredMoveSpeed * airMultiplier; // Example momentum
                else desiredMoveSpeed = walkSpeed * airMultiplier;
            }
            
            if (log) Debug.Log($"<color=green>BaseLoco Activate:</color> Normal activation. Initial State: {controller.CurrentMovementState}, Desired Speed: {desiredMoveSpeed:F1}");
        }

        // Apply the determined speed immediately if snapping, or let HandleSpeedTransition manage it
        moveSpeed = desiredMoveSpeed; // Or: if (shouldSnapOnActivate) moveSpeed = desiredMoveSpeed;
        // lastDesiredMoveSpeed will be set by StateHandler or end of Tick. For safety, can set here:
        lastDesiredMoveSpeed = desiredMoveSpeed;


        // Rigidbody setup
        controller.Rb.useGravity = true; // BaseLocomotion always uses gravity initially.
                                        // IsOnSlope check in FixedTick might temporarily disable it for slope sticking.

        // Reset jump state variables
        readyToJump = true;
        jumpBufferCounter = 0f; // Reset from MovementHelpers call below
        coyoteTimeCounter = coyoteTimeCounter = controller.IsGrounded ? coyoteTime : 0f; // Reset from MovementHelpers call below
        didJumpThisTick = false;
        exitingSlope = false;

        _isJumpButtonPressedThisFrame = false;
        _isJumpButtonHeld = false;
        _jumpChargeTimer = 0f;
        _jumpChargeIsMax = false;
        _pendingBufferedJump = false;
        
        _timeInAir = 0f;
        _isPerformingRollPlaceholder = false;

        // Animator cleanup
        if (cachedAnimator != null)
        {
            cachedAnimator.SetBool("IsOnStickySurface", false);
            cachedAnimator.SetBool("IsSliding", false);
            cachedAnimator.SetBool("IsWallRunning", false);
            // Let StateHandler in the first Tick after activation set NormalizedSpeed, VelocityX/Z etc.
        }
    }

    public override void Deactivate()
    {
        if (speedLerpCoroutine != null) { StopCoroutine(speedLerpCoroutine); speedLerpCoroutine = null; }
        CancelInvoke(nameof(ResetJumpCooldown));
        isSprinting = false;
        _sprintToggleState = false;
        if (controller.ShowDebugLogs) Debug.Log($"<color=green>Base Locomotion:</color> Frame {Time.frameCount} | Deactivated");
    }
    #endregion

    #region MovementModule Overrides

    public override bool WantsToActivate() => true;

    public override void Tick()
    {
        if (_isPerformingRollPlaceholder) // If rolling, skip normal tick logic
        {
            // Potentially allow some very limited input or checks during roll
            return;
        }

        didJumpThisTick = false; // Reset each tick

        // --- MOVED INPUT HANDLING AND JUMP LOGIC TO BE PROCESSED EARLIER ---
        HandleJumpInputAndCharge(); // Process jump button state and charge
        UpdateJumpTimingCounters(); // Update coyote and buffer based on new input state
        CheckAndExecuteJump();      // Attempt to jump if conditions met

        if (controller.activeMovementModule == this)
        {
            UpdateSprintState();
            UpdateCrouchState();
            StateHandler(controller.IsGrounded);
        }

        CalculateMoveDirection();

        if (!controller.IsGrounded)
        {
            _timeInAir += Time.deltaTime;
        }
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

    private void HandleJumpInputAndCharge()
    {
        if (controller.Input == null) return;

        _isJumpButtonPressedThisFrame = controller.Input.JumpPressed;

        if (_isJumpButtonPressedThisFrame) // Button just pressed down
        {
            _isJumpButtonHeld = true;
            _jumpChargeTimer = 0f;
            _jumpChargeIsMax = false;
            _pendingBufferedJump = true; // Indicate an intent to jump is active
            // if(controller.ShowDebugLogs) Debug.Log("Jump Pressed: Pending ON, Held ON, Charge Reset");
        }

        if (_isJumpButtonHeld) // If we previously registered a press and are tracking hold state
        {
            if (controller.Input.JumpHeld) // Is the button *still* physically down?
            {
                _jumpChargeTimer += Time.deltaTime;
                if (_jumpChargeTimer >= jumpChargeTime)
                {
                    _jumpChargeIsMax = true;
                    // if(controller.ShowDebugLogs) Debug.Log($"Jump Charge Maxed: {_jumpChargeTimer}");
                }
            }
            else // Button was held, but is now released this frame
            {
                // if(controller.ShowDebugLogs) Debug.Log("Jump Released: Held OFF");
                _isJumpButtonHeld = false; 
                // NOW is a good time to try and execute the jump if conditions are met,
                // as this is a "release" event for a potentially charged jump.
                // CheckAndExecuteJump will see _isJumpButtonHeld is false and _pendingBufferedJump is true.
            }
        }
    }


    private void UpdateJumpTimingCounters() // Now primarily for coyote/buffer, charge is separate
    {
        if (controller.Input == null) return;

        // Jump Buffer: if _pendingBufferedJump is true (set in HandleJumpInputAndCharge),
        // the buffer effectively starts. It's consumed in CheckAndExecuteJump.
        // We can still use a timer if we want the buffer to expire.
        if (_pendingBufferedJump)
        {
            jumpBufferCounter = jumpBufferTime; // Refresh buffer time as long as jump is intended
        }
        else
        {
            jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - Time.deltaTime);
        }
        
        // Coyote Time (original logic is fine, but ensure 'didJumpThisTick' is correctly managed)
        coyoteTimeCounter = MovementHelpers.UpdateCoyoteTime(coyoteTimeCounter, coyoteTime, controller.WasGrounded, controller.IsGrounded, didJumpThisTick);
    }

    private void CheckAndExecuteJump()
    {
        // Can we physically perform a jump right now? (cooldown, grounded/coyote, not rolling)
        bool canCurrentlyExecutePhysicalJump = readyToJump && 
                                            (controller.IsGrounded || coyoteTimeCounter > 0) && 
                                            !_isPerformingRollPlaceholder;

        // Scenario 1: Jump button was *just released* AND a jump was pending AND we can physically jump.
        // This is the primary path for charged jumps.
        if (_pendingBufferedJump && !_isJumpButtonHeld && canCurrentlyExecutePhysicalJump)
        {
            // _isJumpButtonHeld became false in HandleJumpInputAndCharge because Input.JumpHeld is false.
            float forceToApply;
            if (_jumpChargeIsMax)
            {
                forceToApply = maxJumpForce;
                if(controller.ShowDebugLogs) Debug.Log($"<color=#6495ED>JUMP (Charged Release):</color> Max Force ({maxJumpForce}) applied.");
            }
            else // Released before full charge, or was a tap that's now being released.
            {
                forceToApply = minJumpForce;
                if(controller.ShowDebugLogs) Debug.Log($"<color=#6495ED>JUMP (Early Release/Tap):</color> Min Force ({minJumpForce}) applied. ChargeTimer: {_jumpChargeTimer:F2}");
            }
            ExecuteAndResetJumpLogic(forceToApply);
        }
        // Scenario 2: Jump button was *just pressed* (a potential tap) AND a jump is pending AND we can physically jump.
        // This catches quick taps where the game loop might process the jump execution *before* it sees the release in HandleJumpInputAndCharge.
        // Also handles if jump is forced by buffer/coyote on the very first press frame.
        else if (_pendingBufferedJump && _isJumpButtonPressedThisFrame && canCurrentlyExecutePhysicalJump)
        {
            // Since it's the same frame as press, it's definitely a min jump (no time to charge).
            if(controller.ShowDebugLogs) Debug.Log($"<color=#6495ED>JUMP (Immediate Tap):</color> Min Force ({minJumpForce}) applied.");
            ExecuteAndResetJumpLogic(minJumpForce);
        }
        // Scenario 3: Jump Buffer is active, conditions to jump are met, AND a jump intent is still pending.
        // This handles cases where the player pressed jump in the air, held it (or not), and then landed.
        // The jump should execute upon landing if the buffer is still active.
        else if (_pendingBufferedJump && jumpBufferCounter > 0 && canCurrentlyExecutePhysicalJump)
        {
            float forceToApply;
            // If the button was released *before* landing but buffer is still active
            if (!_isJumpButtonHeld && _jumpChargeIsMax) // Was fully charged then released in air
            {
                forceToApply = maxJumpForce;
                if(controller.ShowDebugLogs) Debug.Log($"<color=yellow>JUMP (Buffered Charged Release):</color> Max Force ({maxJumpForce}) applied.");
            }
            else if (!_isJumpButtonHeld) // Was tapped or released early in air
            {
                forceToApply = minJumpForce;
                if(controller.ShowDebugLogs) Debug.Log($"<color=yellow>JUMP (Buffered Tap/Early Release):</color> Min Force ({minJumpForce}) applied.");
            }
            else // Button is STILL HELD when buffer triggers jump (e.g., landed while holding)
            {
                if (_jumpChargeIsMax) // Was it held long enough for max charge by the time of landing?
                {
                    forceToApply = maxJumpForce;
                    if(controller.ShowDebugLogs) Debug.Log($"<color=yellow>JUMP (Buffered Hold - Charged):</color> Max Force ({maxJumpForce}) applied.");
                }
                else
                {
                    forceToApply = minJumpForce;
                    if(controller.ShowDebugLogs) Debug.Log($"<color=yellow>JUMP (Buffered Hold - Not Charged):</color> Min Force ({minJumpForce}) applied. ChargeTimer: {_jumpChargeTimer:F2}");
                }
            }
            ExecuteAndResetJumpLogic(forceToApply);
        }

        // If _pendingBufferedJump is true but no conditions were met to execute, it remains pending.
        // The jumpBufferCounter will eventually run out if conditions aren't met.
    }

    private void ExecuteAndResetJumpLogic(float baseForce)
    {
        readyToJump = false;
        didJumpThisTick = true; 

        float speedMultiplier = 1.0f;
        if (controller.IsGrounded || coyoteTimeCounter > 0) 
        {
            if (lastDesiredMoveSpeed >= sprintSpeed * 0.8f) speedMultiplier = sprintJumpForceMultiplier;
            else if (lastDesiredMoveSpeed >= jogSpeed * 0.8f) speedMultiplier = jogJumpForceMultiplier;
            else if (lastDesiredMoveSpeed >= walkSpeed * 0.8f) speedMultiplier = walkJumpForceMultiplier;
        }
        float finalJumpForce = baseForce * speedMultiplier;

        if(controller.ShowDebugLogs) 
            Debug.Log($"<color=cyan>JUMP CALC:</color> BaseF: {baseForce:F2}, SpeedTier: {GetCurrentSpeedTierNameForJumpDebug()}, SpeedMult: {speedMultiplier:F2}, FinalF: {finalJumpForce:F2}");

        ExecuteJumpForce(finalJumpForce); // Your existing method that applies physics & sets state

        // Reset all relevant flags now that the jump has been processed
        _pendingBufferedJump = false; // Consume the jump intent
        jumpBufferCounter = 0f;     
        coyoteTimeCounter = 0f;     
        
        // Only reset charge if the button is NOT currently held. 
        // If it's held, HandleJumpInputAndCharge will continue to manage the charge for a potential *next* jump.
        // However, for a typical jump, we usually want the charge to reset.
        // Let's always reset the charge after a jump executes for now.
        _jumpChargeTimer = 0f;      
        _jumpChargeIsMax = false;   
        // _isJumpButtonHeld is managed by HandleJumpInputAndCharge based on actual Input.JumpHeld for the *next* frame.

        CancelInvoke(nameof(ResetJumpCooldown));
        Invoke(nameof(ResetJumpCooldown), jumpCooldown);
    }
    
    // Optional helper for debug logging (place it somewhere in the class)
    private string GetCurrentSpeedTierNameForJumpDebug()
    {
        if (lastDesiredMoveSpeed >= sprintSpeed * 0.8f) return "Sprint";
        if (lastDesiredMoveSpeed >= jogSpeed * 0.8f) return "Jog";
        if (lastDesiredMoveSpeed >= walkSpeed * 0.8f) return "Walk";
        return "Idle/Standing";
    }

    private void ExecuteJumpForce(float force)
    {
        // Ensure capsule is upright if jumping from a non-upright state (e.g., from a future wall interaction)
        // This is a simplified version. A more robust one might be needed if modules can leave capsule non-upright.
        if (controller.transform.up != Vector3.up)
        {
            Vector3 currentForwardHorizontal = Vector3.ProjectOnPlane(controller.transform.forward, Vector3.up);
            if (currentForwardHorizontal.sqrMagnitude < 0.001f)
                currentForwardHorizontal = Vector3.ProjectOnPlane(controller.Orientation.forward, Vector3.up);
            if (currentForwardHorizontal.sqrMagnitude < 0.001f) currentForwardHorizontal = Vector3.forward;
            controller.transform.rotation = Quaternion.LookRotation(currentForwardHorizontal.normalized, Vector3.up);
        }

        exitingSlope = true; // From your original Jump method
        controller.Rb.linearVelocity = new Vector3(controller.Rb.linearVelocity.x, 0f, controller.Rb.linearVelocity.z);
        controller.Rb.AddForce(Vector3.up * force, ForceMode.Impulse);

        controller.SetMovementState(CharacterMovementState.Jumping);

        // Animation Triggers (Placeholder - will need specific animation states)
        cachedAnimator?.SetTrigger("Jump"); // Generic jump trigger
        // TODO: Set animator parameter for take-off speed/style if you have blended take-offs
        // cachedAnimator?.SetFloat("SpeedAtTakeOff", moveSpeed); 
        if (controller.ShowDebugLogs) Debug.Log($"<color=green>EXECUTE JUMP:</color> Force: {force}");
    }

    private void UpdateSprintState()
    {
        if (controller.Input == null) return;

        bool currentlyMoving = controller.Input.HorizontalInput != 0 || controller.Input.VerticalInput != 0;
        bool canPotentiallyEnterSprintState = !crouching && controller.IsGrounded; // Sprint condition: not crouching, grounded
        bool previousSprintActionState = isSprinting;

        if (useToggleSprint)
        {
            // --- TOGGLE SPRINT MODE LOGIC ---
            if (controller.Input.SprintTogglePressed) // On initial press
            {
                _sprintToggleState = !_sprintToggleState; // Flip the desired mode
                // If toggling the MODE OFF, immediately ensure the sprint ACTION also stops
                if (!_sprintToggleState) isSprinting = false;
            }

            // Determine if sprint ACTION (isSprinting) should be active this frame
            if (_sprintToggleState) // Sprint MODE is ON
            {
                // Try to activate sprint ACTION if conditions are met
                isSprinting = canPotentiallyEnterSprintState && currentlyMoving;

                // If 'resumeSprintAfterIdle' is false, turn off the toggle MODE when movement stops
                if (!resumeSprintAfterIdle && !currentlyMoving && _sprintToggleState)
                {
                    _sprintToggleState = false; // Turn off the mode itself
                    // isSprinting will become false in the next line because !currentlyMoving
                }
            }
            else // Sprint MODE is OFF
            {
                isSprinting = false; // Ensure sprint ACTION is off
            }

            // Final check: If conditions to be in sprint state are lost (e.g., crouched, in air), stop sprint ACTION
            // This is important if, for example, the player crouches while sprint mode is still toggled on.
            if (isSprinting && !canPotentiallyEnterSprintState)
            {
                isSprinting = false;
            }
            // --- END TOGGLE SPRINT LOGIC ---
        }
        else // --- HOLD SPRINT LOGIC ---
        {
            bool holdCanSprint = canPotentiallyEnterSprintState && currentlyMoving; // Hold requires movement directly
            isSprinting = controller.Input.SprintHeld && holdCanSprint;
            _sprintToggleState = false; // Ensure toggle mode is off if not using it
        }

        // Optional Logging
        //if (isSprinting != previousSprintActionState && controller.ShowDebugLogs)
        //{
        //    Debug.Log($"<color=lightblue>Sprint State Change:</color> {previousSprintActionState} -> {isSprinting} (Mode: {(useToggleSprint ? "Toggle" : "Hold")}, ToggleModeActive={_sprintToggleState})");
        //}
    }

    private void UpdateCrouchState()
    {
        if (controller.Input == null) return;

        // --- SLIDE ACTIVATION CHECK ---
        // Uses the current frame's isSprinting ACTION state determined by UpdateSprintState
        bool isCurrentlySprintingState = this.isSprinting; // Use reliable state
        bool canPotentiallySlide = cachedSlideModule != null &&
                                    controller.Input.CrouchPressed &&      // Must be the initial press OF CROUCH
                                    (controller.Input.SprintHeld || isCurrentlySprintingState) && // Sprint button held OR currently in sprint ACTION
                                    controller.IsGrounded &&
                                    !controller.IsSliding; // Use CardiniController helper

        bool slideWantsToActivate = canPotentiallySlide && cachedSlideModule.WantsToActivate(); // Let SlideModule confirm
        // ---------------------------


        // --- CROUCH LOGIC ---
        if (useToggleCrouch)
        {
            if (controller.Input.CrouchTogglePressed) // Button pressed this frame
            {
                if (crouching) // If already crouching, try to stand up
                {
                    if (!CheckForCeiling()) StopCrouch();
                }
                else // Not crouching, try to enter crouch
                {
                    // Can enter crouch if grounded, not sliding, not currently in sprint ACTION, and slide isn't trying to activate
                    bool canStartCrouch = controller.IsGrounded && !controller.IsSliding && !this.isSprinting;
                    if (canStartCrouch && !slideWantsToActivate) StartCrouch();
                }
            }
            // --- Jump from Crouch (Toggle Mode) -> Stand up ---
            else if (crouching && controller.Input.JumpPressed)
            {
                if (!CheckForCeiling())
                {
                    StopCrouch();
                    // No direct jump action from crouch; player stands, then can jump.
                    // StateHandler will set desiredMoveSpeed to normalSpeed/idle next.
                }
            }
            // --- Sprint from Crouch (Toggle Mode) -> Stand up & Attempt Sprint ---
            else if (crouching && controller.Input.SprintTogglePressed) // Check Sprint Toggle Press
            {
                if (!CheckForCeiling())
                {
                    StopCrouch(); // Stand up
                    _sprintToggleState = true; // Turn on sprint MODE
                    // isSprinting action will be set true in next UpdateSprintState if conditions met
                }
            }
        }
        else // --- HOLD CROUCH LOGIC ---
        {
            bool canStartHoldCrouch = controller.IsGrounded && !crouching && !controller.IsSliding && !this.isSprinting && !slideWantsToActivate;
            if (controller.Input.CrouchPressed && canStartHoldCrouch) StartCrouch();
            else if (controller.Input.CrouchReleased && crouching) { if (!CheckForCeiling()) StopCrouch(); }
            else if (crouching && !controller.IsGrounded) StopCrouch(); // Auto-stand if in air

            // --- Sprint from Crouch (Hold Mode) -> Stand up & Attempt Sprint ---
            else if (crouching && controller.Input.SprintHeld && controller.IsGrounded)
            {
                if(!CheckForCeiling())
                {
                    StopCrouch(); // Stand up
                    // isSprinting will be set true in UpdateSprintState if conditions met (because SprintHeld is true)
                }
            }
            // Jump from Hold Crouch is handled by normal jump logic once standing
        }
    }

    private bool CheckForCeiling()
    {
        return false;
    }

    private void StateHandler(bool isCurrentlyGrounded_Physics)
    {
        if (controller.Input == null) return;
        if (_isPerformingRollPlaceholder) return;

        // bool previouslyAnimatorGrounded = isAnimatorGrounded;

        // --- LANDING DETECTION & HIGH FALL LOGIC ---
        // Use controller.WasGrounded to detect the exact frame we land
        if (!controller.WasGrounded && isCurrentlyGrounded_Physics) // JUST LANDED THIS FRAME
        {
            if (controller.ShowDebugLogs) Debug.Log($"<color=orange>LANDED.</color> Time in Air: {_timeInAir:F2}s. Current State: {controller.CurrentMovementState}");
            
            if (_timeInAir >= minTimeForHighFall)
            {
                StartCoroutine(PerformLandingRollPlaceholder()); // This coroutine will set _isPerformingRollPlaceholder = true
                _timeInAir = 0f; // Reset after processing
                // The PerformLandingRollPlaceholder coroutine will handle setting the CharacterMovementState.Rolling
                // and then transitioning out of it. We return here to let it take full control this frame.
                return; 
            }
            else
            {
                // Normal landing (not a high fall)
                _timeInAir = 0f; // Reset fall timer
                if (cachedAnimator != null)
                {
                    cachedAnimator.ResetTrigger("Jump"); // Or a specific "Land" trigger if you have one
                    bool landingIntoRun = DetermineLandingIntoRun(); // Your helper method
                    cachedAnimator.SetBool("IsLandingIntoRun", landingIntoRun);
                    // The subsequent logic in StateHandler will set Idle/Walk/Jog based on input
                }
            }
        }
        // --- END LANDING DETECTION & HIGH FALL LOGIC ---
        // --- Animator Grounded State (Visual Delay for Animator) ---
        // This is separate from the physics-based IsGrounded check for landing detection
        bool previousAnimatorGroundedVisual = isAnimatorGrounded; // Local variable for this specific animator logic
        if (!isCurrentlyGrounded_Physics)
        {
            timeSinceLeftGround += Time.deltaTime;
            if (timeSinceLeftGround > GROUNDED_BUFFER_TIME)
            {
                isAnimatorGrounded = false;
            }
        }
        else // Physically grounded
        {
            isAnimatorGrounded = true;
            timeSinceLeftGround = 0f;
        }

        if (!previousAnimatorGroundedVisual  && isAnimatorGrounded)
        {
            if (cachedAnimator != null)
            {
                cachedAnimator.ResetTrigger("Jump");

                // --- ADD THIS LOGIC for IsLandingIntoRun ---
                bool landingIntoRun = false;
                float minSpeedForRunLand = walkSpeed * 0.7f; // Threshold to consider "running" speed for landing

                // Determine if player intends to continue moving forward with speed
                // Condition: Holding forward input AND (either their last desired speed was running OR sprint toggle mode is on)
                bool wantsToContinueMovingForward = controller.Input.VerticalInput > 0.1f &&
                                                (lastDesiredMoveSpeed >= minSpeedForRunLand || // Was moving at decent speed
                                                    (UseToggleSprint && _sprintToggleState) );    // OR sprint mode is toggled on

                if (wantsToContinueMovingForward)
                {
                    landingIntoRun = true;
                }
                cachedAnimator.SetBool("IsLandingIntoRun", landingIntoRun);
                //if (controller.ShowDebugLogs) Debug.Log($"<color=yellow>Landing:</color> IsLandingIntoRun = {landingIntoRun}, LastDesiredSpeed = {lastDesiredMoveSpeed}, CurrentSpeed = {moveSpeed}");
                // --- END ADDED LOGIC ---
            }
        }

        

        isIdle = isCurrentlyGrounded_Physics && !crouching && !isSprinting && // Added !isSprinting
        controller.Input.HorizontalInput == 0 && controller.Input.VerticalInput == 0;

        CharacterMovementState targetState;

        bool isActuallyMoving = controller.Input.HorizontalInput != 0 || controller.Input.VerticalInput != 0;
        float inputMagnitude = new Vector2(controller.Input.HorizontalInput, controller.Input.VerticalInput).magnitude;
        inputMagnitude = Mathf.Clamp01(inputMagnitude);

        if (crouching)
        {
            desiredMoveSpeed = crouchSpeed;
            targetState = CharacterMovementState.Crouching; // ENUM
            SetAnimatorNormalizedSpeed(0.5f); // (Keep this for animator tier)
        }
        else if (isSprinting && isActuallyMoving && isCurrentlyGrounded_Physics) // isSprinting is the "Boost" ACTION
        {
            desiredMoveSpeed = sprintSpeed;
            targetState = CharacterMovementState.Sprinting; // ENUM (was "boosting")
            SetAnimatorNormalizedSpeed(3f);
        }
        else if (isActuallyMoving && isCurrentlyGrounded_Physics)
        {
            if (inputMagnitude >= jogInputMagnitudeThreshold)
            {
                desiredMoveSpeed = jogSpeed;
                targetState = CharacterMovementState.Jogging; // ENUM
                SetAnimatorNormalizedSpeed(2f);
            }
            else if (inputMagnitude >= walkInputMagnitudeThreshold)
            {
                desiredMoveSpeed = walkSpeed;
                targetState = CharacterMovementState.Walking; // ENUM
                SetAnimatorNormalizedSpeed(1f);
            }
            else // No significant input magnitude (below walk threshold)
            {
                desiredMoveSpeed = 0f;
                targetState = CharacterMovementState.Idle; // ENUM (was "idle_from_low_input")
                SetAnimatorNormalizedSpeed(0f);
            }
        }
        else if ((isIdle || (!isActuallyMoving && isCurrentlyGrounded_Physics))) // Explicitly idle or stopped on ground
        {
            desiredMoveSpeed = 0f;
            targetState = CharacterMovementState.Idle; // ENUM
            SetAnimatorNormalizedSpeed(0f);
        }
        else // In Air
        {
            // Maintain momentum from last ground speed tier
            if (lastDesiredMoveSpeed >= sprintSpeed) desiredMoveSpeed = sprintSpeed;
            else if (lastDesiredMoveSpeed >= jogSpeed) desiredMoveSpeed = jogSpeed;
            else if (lastDesiredMoveSpeed >= walkSpeed) desiredMoveSpeed = walkSpeed;
            else desiredMoveSpeed = walkSpeed; 

            if (didJumpThisTick && lastDesiredMoveSpeed <= crouchSpeed)
            {
                desiredMoveSpeed = walkSpeed * 0.7f;
            }

            // If Jump() was called, it would have set the state to Jumping.
            // Otherwise, we are Falling.
            if (controller.CurrentMovementState == CharacterMovementState.Jumping) { // Check if already jumping
                targetState = CharacterMovementState.Jumping;
            } else {
                targetState = CharacterMovementState.Falling; // ENUM (was "air")
            }
            // NormalizedSpeed for air is less critical here if your main locomotion blend doesn't handle air.
            // Jumping/Falling states in animator are usually separate.
        }

        controller.SetMovementState(targetState);

        // Recalculate isIdle based on final desiredMoveSpeed for animator
        isIdle = (targetState == CharacterMovementState.Idle);

        UpdateAnimatorParameters(isAnimatorGrounded);
        HandleSpeedTransition();
        // Update lastDesiredMoveSpeed only if on a ground movement state
        if (targetState == CharacterMovementState.Idle || 
            targetState == CharacterMovementState.Walking || 
            targetState == CharacterMovementState.Jogging || 
            targetState == CharacterMovementState.Sprinting ||
            targetState == CharacterMovementState.Crouching)
        {
            lastDesiredMoveSpeed = desiredMoveSpeed;
        }
    }

    private bool DetermineLandingIntoRun()
    {
        float minSpeedForRunLand = walkSpeed * 0.7f;
        bool wantsToContinueMovingForward = controller.Input.VerticalInput > 0.1f &&
                                        (lastDesiredMoveSpeed >= minSpeedForRunLand || 
                                            (UseToggleSprint && _sprintToggleState) );
        // Also consider current velocity magnitude if you want to land into run only if already moving fast
        // bool movingFastEnough = controller.Rb.linearVelocity.magnitude > (walkSpeed * 0.5f);
        // return wantsToContinueMovingForward && movingFastEnough;
        return wantsToContinueMovingForward; // Simplified for now
    }

    private IEnumerator PerformLandingRollPlaceholder()
    {
        if(controller.ShowDebugLogs) Debug.Log($"<color=red>HIGH FALL DETECTED!</color> Starting Roll Placeholder...");
        _isPerformingRollPlaceholder = true;
        controller.SetMovementState(CharacterMovementState.Rolling); // Add Rolling to your enum
        // cachedAnimator?.SetTrigger("Roll"); // Placeholder for actual roll animation

        // Simulate roll effect: reduce speed and maybe control
        float originalDesiredSpeed = desiredMoveSpeed; // Store for restoration
        Vector3 rollDirection = new Vector3(controller.Rb.linearVelocity.x, 0, controller.Rb.linearVelocity.z).normalized;
        if (rollDirection.sqrMagnitude < 0.01f) rollDirection = controller.transform.forward;

        float timer = 0f;
        while (timer < rollDuration)
        {
            // Apply reduced speed in the direction of the roll
            // This is a simplified way to affect speed; direct velocity manipulation might be better.
            desiredMoveSpeed = originalDesiredSpeed * rollSpeedFactor; 
            // moveDirection = rollDirection; // Force movement in roll direction

            // For direct velocity control during roll:
            Vector3 currentRollVelocity = rollDirection * (originalDesiredSpeed * rollSpeedFactor);
            controller.Rb.linearVelocity = new Vector3(currentRollVelocity.x, controller.Rb.linearVelocity.y, currentRollVelocity.z);


            // Optional: Briefly reduce airControlFactor or other input influences
            timer += Time.deltaTime;
            yield return null;
        }

        desiredMoveSpeed = originalDesiredSpeed; // Attempt to restore
        _isPerformingRollPlaceholder = false;
        
        // After roll, StateHandler in the next Tick will determine appropriate Idle/Walk/Jog state based on input
        if(controller.ShowDebugLogs) Debug.Log($"<color=red>Roll Placeholder FINISHED.</color>");
        // Force a StateHandler call to immediately update to idle/walk etc.
        StateHandler(controller.IsGrounded); 
    }

    private void UpdateAnimatorParameters(bool isGrounded_Animator)
    {
        if (cachedAnimator == null) return;

        float targetVelocityX = 0f;
        float targetVelocityZ = 0f;

        bool characterShouldRotate = true; // Default if FreeLookOrientation component not found
        if (cachedFreeLookOrientation != null)
        {
            characterShouldRotate = cachedFreeLookOrientation.ShouldCharacterRotateWithMovementInput();
        }

        // Get input magnitude to determine if there's any input at all
        float currentInputMagnitude = new Vector2(controller.Input.HorizontalInput, controller.Input.VerticalInput).magnitude;

        if (currentInputMagnitude > 0.01f) // If there's any significant movement input
        {
            if (characterShouldRotate)
            {
                // MODE 1: Character model rotates to face movement direction.
                // Since the model itself is turning to face where we're going,
                // from the Animator's perspective (relative to the model's new forward),
                // it's always moving "forward" at its current speed tier.
                targetVelocityZ = 1f; // Always signal full forward animation intensity
                targetVelocityX = 0f; // Model's rotation handles the direction, not strafe animation
            }
            else // MODE 2: Character does NOT rotate with input (Strafe Mode / 8-Way)
            {
                // Animator needs local X and Z relative to character's fixed forward (camera's forward).
                // moveDirection is already calculated world-space based on input and camera.
                if (controller.Orientation != null && moveDirection.magnitude > 0.001f)
                {
                    Vector3 worldMoveDirNormalized = moveDirection.normalized;
                    Vector3 localMoveNormalized = controller.Orientation.InverseTransformDirection(worldMoveDirNormalized);
                    targetVelocityX = localMoveNormalized.x; // Use components of the NORMALIZED direction
                    targetVelocityZ = localMoveNormalized.z; // This ensures values are between -1 and 1 for full anim intensity
                }
                // If moveDirection is zero (e.g., just after input stops but before currentInputMagnitude fully drops),
                // targetVelocityX/Z will remain 0.
            }
        }
        // If currentInputMagnitude is ~0, targetVelocityX/Z remain 0 from initialization, leading to idle.

        // Apply with DAMPING for smooth directional blending
        cachedAnimator.SetFloat("VelocityX", targetVelocityX, directionalVelocityDampTime, Time.deltaTime);
        cachedAnimator.SetFloat("VelocityZ", targetVelocityZ, directionalVelocityDampTime, Time.deltaTime);

        // NormalizedSpeed (0=Idle, 1=Walk, 2=Jog, 3=Boost) is set in StateHandler via SetAnimatorNormalizedSpeed
        // This controls WHICH 2D blend tree (or Idle anim) is active in the Locomotion_MasterBlend.

        cachedAnimator.SetBool("Grounded", isGrounded_Animator);
        cachedAnimator.SetBool("IsCrouching", crouching);

        if (controller.activeMovementModule == this)
        {
            // These parameters are managed by other modules when they are active.
            // BaseLocomotion only asserts they are false when it is in control.
            cachedAnimator.SetBool("IsSliding", false);
            cachedAnimator.SetBool("IsWallRunning", false);
            cachedAnimator.SetFloat("WallRunDirection", 0f);
            // FreeFall and MotionSpeed parameters have been removed from Animator and this method.
        }
    }

    private void SetAnimatorNormalizedSpeed(float targetNormalizedSpeed)
    {
        if (cachedAnimator != null)
        {
            // You might want to add damp time here for smoother transitions
            // between the main blend tree states (Idle, Walk, Jog, Boost)
            // e.g., cachedAnimator.SetFloat("NormalizedSpeed", targetNormalizedSpeed, 0.1f, Time.deltaTime);
            // For now, direct set:
            cachedAnimator.SetFloat("NormalizedSpeed", targetNormalizedSpeed, speedTierDampTime, Time.deltaTime);
        }
    }

    private void HandleSpeedTransition()
    {
        if (Mathf.Abs(moveSpeed - desiredMoveSpeed) < 0.01f)
        {
            if (speedLerpCoroutine != null) { StopCoroutine(speedLerpCoroutine); speedLerpCoroutine = null; }
            moveSpeed = desiredMoveSpeed; return;
        }

        bool startingFromStop = moveSpeed < 0.1f && desiredMoveSpeed > 0;
        // Snap if starting from stop OR if we just started the sprint ACTION this frame
        bool justStartedSprintingAction = isSprinting && lastDesiredMoveSpeed < sprintSpeed;
        bool shouldSnap = startingFromStop || justStartedSprintingAction;

        if (shouldSnap)
        {
            if (speedLerpCoroutine != null) { StopCoroutine(speedLerpCoroutine); speedLerpCoroutine = null; }
            moveSpeed = desiredMoveSpeed;
        }
        else if (speedLerpCoroutine == null)
        {
            speedLerpCoroutine = StartCoroutine(SmoothlyLerpMoveSpeed());
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
        controller.SetMovementState(CharacterMovementState.Crouching);

        if (speedLerpCoroutine != null)
        {
            StopCoroutine(speedLerpCoroutine);
            speedLerpCoroutine = null;
        }

        cachedAnimator?.SetBool("IsCrouching", true);
    }

    private void StartCrouch() { EnterCrouchState(skipInitialEffects: false); }

    private void StopCrouch()
    {
        if (!crouching) return;
        crouching = false;
        cachedAnimator?.SetBool("IsCrouching", false);
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