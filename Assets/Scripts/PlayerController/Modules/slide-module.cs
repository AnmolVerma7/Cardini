using UnityEngine;
using Cardini.Motion;

/// <summary>
/// Handles sliding movement mechanics, activating during sprint-to-crouch transitions.
/// Controls slide physics, duration, and state management.
/// </summary>
public class SlideModule : MovementModule
{
    public override int Priority => 10;

    #region Settings
    [Header("Sliding Behavior")]
    [SerializeField] private float slideForce = 200f;
    [SerializeField] private float maxSlideTime = 1f;
    [SerializeField] private float slideCooldown = 0.2f;

    [Header("Sliding Speed")]
    [SerializeField] private float slideSpeed = 10f;
    [SerializeField] private float maxSlideSpeed = 20f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    #endregion

    #region State
    private BaseLocomotionModule _baseLocoModule;
    private float slideTimer;
    private float slideCooldownTimer;
    #endregion

    // --- Initialization & Lifecycle ---

    public override void Initialize(CardiniController controller)
    {
        base.Initialize(controller);
        slideCooldownTimer = 0f;
        _baseLocoModule = controller.GetComponent<BaseLocomotionModule>();
        if (_baseLocoModule == null) Debug.LogError($"{GetType().Name}: Could not find BaseLocomotionModule on the Controller!", this);
        if (showDebugLogs) Debug.Log("<color=orange>Slide:</color> Module initialized");
    }

    public override void Activate()
    {
        bool log = controller.ShowDebugLogs;
        if (log) Debug.Log($"<color=orange>Slide:</color> Frame {Time.frameCount} | Activated");

        if (controller.Rb != null) controller.Rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        slideTimer = maxSlideTime;
        controller.SetMovementState(CharacterMovementState.Sliding);
        slideCooldownTimer = slideCooldown;

        var animator = controller.PlayerObj?.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetBool("IsSliding", true);
            animator.SetBool("IsCrouching", false);
            animator.SetBool("IsWallRunning", false);
        }
    }

    public override void Deactivate()
    {
        bool log = controller.ShowDebugLogs;
        if (log) Debug.Log($"<color=orange>Slide:</color> Frame {Time.frameCount} | Deactivated.");

        var animator = controller.PlayerObj?.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetBool("IsSliding", false);
        }
    }

    // --- Module Overrides ---

    public override bool WantsToActivate()
    {
        if (controller == null || controller.Input == null || _baseLocoModule == null) return false;

        bool onCooldown = slideCooldownTimer > 0f;
        if (onCooldown) return false;

        bool grounded = controller.IsGrounded;
        bool currentlySliding = controller.IsSliding;
        bool crouchPressedThisFrame = controller.Input.CrouchPressed;

        // --- CRITICAL CHANGE: Check actual sprinting STATE from BaseLoco ---
        bool isCurrentlySprintingState = _baseLocoModule.IsCurrentlySprinting();
        // --------------------------------------------------------------

        // Activation requires: Crouch PRESSED THIS FRAME + ACTUAL Sprinting State + Grounded + Not Already Sliding
        bool wants = crouchPressedThisFrame &&
                    isCurrentlySprintingState && // Player must BE in the sprinting ACTION state
                    grounded &&
                    !currentlySliding;

        // Optional Logging
        //if (showDebugLogs && crouchPressedThisFrame)
        //    Debug.Log($"<color=orange>Slide.Wants Check:</color> Wants={wants} (IsSprintingState={isCurrentlySprintingState}, Grounded={grounded}, Sliding={currentlySliding})");
        return wants;
    }

    public override void Tick()
    {
        if (slideCooldownTimer > 0f) { slideCooldownTimer -= Time.deltaTime; }

        if (controller.activeMovementModule == this)
        {
            if (slideTimer > 0f) { slideTimer -= Time.deltaTime; }
            bool slideTimeUp = slideTimer <= 0f;
            // bool crouchReleased = controller.Input.CrouchReleased; // We use shouldReleaseStop now
            bool fellOffGround = !controller.IsGrounded;

            // Check toggle setting via BaseLocoModule's public property
            // Ensure _baseLocoModule is not null (it should be cached in Initialize)
            bool isToggleCrouchActive = _baseLocoModule != null && _baseLocoModule.UseToggleCrouch;
            // Determine if slide should stop based on release (only relevant if NOT using toggle crouch)
            bool shouldReleaseStop = !isToggleCrouchActive && controller.Input.CrouchReleased;

            if (slideTimeUp || shouldReleaseStop || fellOffGround)
            {
                // Force crouch state on exit only if slide timer ended while grounded.
                // BaseLoco will handle whether the player *stays* crouched based on its own logic.
                bool shouldForceCrouchOnExit = slideTimeUp && !fellOffGround;
                controller.ForceCrouchStateOnNextBaseLocoActivation = shouldForceCrouchOnExit;

                // Using controller.ShowDebugLogs if you want consistent debug control
                if (controller.ShowDebugLogs) // Or keep your local 'showDebugLogs' if preferred
                {
                    string reason = "Unknown";
                    if (slideTimeUp) reason = shouldForceCrouchOnExit ? "Timer (to crouch)" : "Timer (to stand)";
                    else if (shouldReleaseStop) reason = "Crouch Released";
                    else if (fellOffGround) reason = "Fell Off Ground";
                    Debug.Log($"<color=orange>Slide.Tick:</color> Stopping slide ({reason}). ForceCrouchOnExit={shouldForceCrouchOnExit}");
                }
                controller.RequestMovementModuleDeactivation(this);
            }
        }
    }

    public override void FixedTick()
    {
        ApplySlideForces();
    }

    // --- Public Getters ---

    public float GetCurrentMoveSpeed()
    {
        return controller.IsOnSlope ? maxSlideSpeed : slideSpeed;
    }

    // --- Core Logic Methods ---

    private void ApplySlideForces()
    {
        Vector3 inputDirection = MovementHelpers.CalculateMoveDirection(
            controller.Orientation,
            controller.Input.HorizontalInput,
            controller.Input.VerticalInput
        );

        if (controller.Rb != null)
        {
            if (!controller.IsOnSlope || controller.Rb.linearVelocity.y > -0.1f)
            {
                controller.Rb.AddForce(inputDirection.normalized * slideForce, ForceMode.Force);
            }
            else
            {
                Vector3 slopeDirection = MovementHelpers.GetSlopeMoveDirection(inputDirection, controller.SlopeHit);
                controller.Rb.AddForce(slopeDirection * slideForce, ForceMode.Force);
            }
        }
    }
}