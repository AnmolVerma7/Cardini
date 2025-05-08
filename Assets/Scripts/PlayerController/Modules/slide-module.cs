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
    private float slideTimer;
    private float slideCooldownTimer;
    #endregion

    // --- Initialization & Lifecycle ---

    public override void Initialize(CardiniController controller)
    {
        base.Initialize(controller);
        slideCooldownTimer = 0f;
        if (showDebugLogs) Debug.Log("<color=orange>Slide:</color> Module initialized");
    }

    public override void Activate()
    {
        bool log = controller.ShowDebugLogs;
        if (log) Debug.Log($"<color=orange>Slide:</color> Frame {Time.frameCount} | Activated");

        if (controller.Rb != null) controller.Rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        slideTimer = maxSlideTime;
        controller.SetMovementState("sliding");
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
        bool onCooldown = slideCooldownTimer > 0;
        bool wants = controller.Input.CrouchPressed &&
                     controller.Input.SprintHeld &&
                     controller.IsGrounded &&
                     !controller.IsSliding &&
                     !onCooldown;

        if (showDebugLogs && controller.Input.CrouchPressed)
            Debug.Log($"<color=orange>Slide.Wants Check:</color> Frame {Time.frameCount} | CrouchPressed={controller.Input.CrouchPressed}, SprintHeld={controller.Input.SprintHeld}, Grounded={controller.IsGrounded}, NotSliding={!controller.IsSliding}, OnCooldown={onCooldown} >> Wants={wants}");

        return wants;
    }

    public override void Tick()
    {
        if (slideCooldownTimer > 0) { slideCooldownTimer -= Time.deltaTime; }

        if (controller.activeMovementModule == this)
        {
            if (slideTimer > 0) { slideTimer -= Time.deltaTime; }
            bool slideTimeUp = slideTimer <= 0;
            bool crouchReleased = controller.Input.CrouchReleased;
            bool fellOffGround = !controller.IsGrounded;

            if (slideTimeUp || crouchReleased || fellOffGround)
            {
                bool shouldTransitionToCrouch = slideTimeUp && !crouchReleased && controller.Input.CrouchHeld && !fellOffGround;
                controller.ForceCrouchStateOnNextBaseLocoActivation = shouldTransitionToCrouch;

                if (showDebugLogs)
                {
                    if (shouldTransitionToCrouch) Debug.Log($"<color=orange>Slide.Tick:</color> Frame {Time.frameCount} | Timer ended, Crouch Held. Requesting transition to crouch.");
                    else if (slideTimeUp) Debug.Log($"<color=orange>Slide.Tick:</color> Frame {Time.frameCount} | Timer ended, Crouch NOT Held. Requesting normal deactivation.");
                    else if (crouchReleased) Debug.Log($"<color=orange>Slide.Tick:</color> Frame {Time.frameCount} | Crouch Released. Requesting normal deactivation.");
                    else if (fellOffGround) Debug.Log($"<color=orange>Slide.Tick:</color> Frame {Time.frameCount} | Fell off ground. Requesting normal deactivation.");
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