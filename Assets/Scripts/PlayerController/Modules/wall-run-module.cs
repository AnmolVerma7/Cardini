using UnityEngine;
using System.Collections;
using Cardini.Motion;

public class WallRunModule : MovementModule
{
    public override int Priority => 20;

    [Header("Wall Running Physics")]
    [SerializeField, Tooltip("How strongly the player sticks to the wall")] 
    private float wallAdhesionForce = 100f;

    [Header("Wall Detection")]
    [SerializeField] private LayerMask whatIsWall;
    [SerializeField] private float wallCheckDistance = 0.6f;
    [SerializeField] private float wallRunHeightOffset = 1.0f;
    [SerializeField] private float minWallRunSpeed = 5f;
    [SerializeField] private float maxWallEntryAngle = 60f;

    [Header("Wall Running")]
    [SerializeField] private float wallRunSpeed = 10f;
    [SerializeField] private float maxWallRunTime = 1f;

    [Header("Wall Jumping")]
    [SerializeField] private float wallJumpUpForce = 7f;
    [SerializeField] private float wallJumpSideForce = 12f;

    [Header("Exiting Wall")]
    [SerializeField] private float exitWallTime = 0.2f;

    [Header("Gravity Control")]
    [SerializeField] private bool useStandardGravity = false;
    [SerializeField, Range(0f, 2f)] private float gravityCompensationFactor = 1.0f;

    private Animator cachedAnimator;
    private float wallRunTimer;
    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;
    private bool wallLeft;
    private bool wallRight;
    private bool exitingWall;
    private float exitWallTimer;
    private Vector3 _lockedWallForward;
    private Vector3 _lockedWallNormal;

    #if UNITY_EDITOR
    private Vector3 _debugLastWallNormal;
    private Vector3 _debugLastWallForward;
    private Vector3 _debugLastHorizontalVelocity;
    private float _debugLastEntryAngle;
    private bool _debugLastAngleCheck;
    private bool _debugLastSpeedCheck;
    private bool _debugLastFwdCheck;
    private bool _debugFoundWall;
    #endif

    public override void Initialize(CardiniController controller)
    {
        base.Initialize(controller);
        if (controller.PlayerObj != null)
        {
            cachedAnimator = controller.PlayerObj.GetComponentInChildren<Animator>();
            if (cachedAnimator == null) Debug.LogWarning($"{GetType().Name}: Animator not found on PlayerObj's children.", this);
        }
        exitingWall = false;
        if (controller.ShowDebugLogs) Debug.Log("<color=blue>Wall Run:</color> Module initialized");
    }

    public override void Activate()
    {
        wallRunTimer = maxWallRunTime;
        exitingWall = false;
        exitWallTimer = 0f;

        // Fix: Properly set the movement state using controller.SetMovementState
        controller.SetMovementState(CharacterMovementState.WallRunning);

        if (cachedAnimator != null)
        {
            cachedAnimator.SetBool("IsWallRunning", true);
            cachedAnimator.SetFloat("WallRunDirection", wallLeft ? -1f : 1f);
            cachedAnimator.SetBool("Grounded", false);
            cachedAnimator.SetBool("FreeFall", false);
            cachedAnimator.SetBool("IsSliding", false);
            cachedAnimator.SetBool("IsCrouching", false);
        }

        if (controller.Rb != null && !useStandardGravity)
        {
            controller.Rb.useGravity = false;
        }
    }

    public override void Deactivate()
    {
        if (cachedAnimator != null)
        {
            cachedAnimator.SetBool("IsWallRunning", false);
            cachedAnimator.SetFloat("WallRunDirection", 0f);
        }

        if (controller.Rb != null)
        {
            controller.Rb.useGravity = true;
        }
    }

    /// <summary>
    /// Determines if the wall run can be initiated based on speed, angle, and wall detection.
    /// </summary>
    public override bool WantsToActivate()
    {
        // Quick validation of required components
        if (controller == null || controller.Input == null || controller.Rb == null || exitingWall) return false;

        // Step 1: Wall Detection
        CheckForWall();
        bool hasWall = wallLeft || wallRight;
        _debugFoundWall = hasWall;
        if (!hasWall) return false;

        // Step 2: Speed Check - Ensure minimum velocity for wall run
        Vector3 horizontalVelocity = new Vector3(controller.Rb.linearVelocity.x, 0f, controller.Rb.linearVelocity.z);
        float currentHorizontalSpeed = horizontalVelocity.magnitude;
        bool speedCheckPassed = currentHorizontalSpeed > minWallRunSpeed;
        _debugLastSpeedCheck = speedCheckPassed;
        _debugLastHorizontalVelocity = horizontalVelocity;
        if (!speedCheckPassed) return false;

        // Step 3: Angle Check - Validate entry angle for wall run
        Vector3 wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;
        Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up);
        if (horizontalVelocity.magnitude > 0.1f && Vector3.Dot(wallForward, horizontalVelocity) < 0f) { wallForward *= -1f; }
        _debugLastWallForward = wallForward;
        float entryAngle = Vector3.Angle(horizontalVelocity, wallForward);
        bool angleCheckPassed = entryAngle < maxWallEntryAngle;
        _debugLastEntryAngle = entryAngle;
        _debugLastAngleCheck = angleCheckPassed;
        if (!angleCheckPassed) return false;

        // Step 4: Forward Movement Check - Ensure player is moving forward
        bool movingForwardCheck = Vector3.Dot(horizontalVelocity.normalized, controller.Orientation.forward) > 0.1f;
        _debugLastFwdCheck = movingForwardCheck;
        if (!movingForwardCheck) return false;

        // Final Check: Grounded or Jump Pressed
        if (controller.IsGrounded)
        {
            return true;
        }
        else
        {
            return controller.Input.JumpPressed;
        }
    }

    /// <summary>
    /// Updates wall running state, handles timers and checks for exit conditions.
    /// </summary>
    public override void Tick()
    {
        // Handle exit state cooldown
        if (exitingWall)
        {
            exitWallTimer -= Time.deltaTime;

            if (exitWallTimer <= 0)
            {
                exitingWall = false;
            }
        }

        // Process active wall running
        if (controller.activeMovementModule == this)
        {
            if (controller.Input == null) return;
            CheckForWall();
            if (wallRunTimer > 0)
            {
                wallRunTimer -= Time.deltaTime;
                if (wallRunTimer <= 0 && !exitingWall)
                {
                    StartExitState("Wall Run Time Expired");
                    controller.RequestMovementModuleDeactivation(this);
                    return;
                }
            }
            if (controller.Input.JumpPressed && !exitingWall)
            {
                WallJump();
                controller.RequestMovementModuleDeactivation(this);
                return;
            }
            bool lostWall = !wallLeft && !wallRight;
            Vector3 currentHorizontalVelocity = new Vector3(controller.Rb.linearVelocity.x, 0f, controller.Rb.linearVelocity.z);
            bool stoppedMovingForward = Vector3.Dot(currentHorizontalVelocity.normalized, controller.Orientation.forward) < 0.1f || currentHorizontalVelocity.magnitude < (minWallRunSpeed * 0.5f);
            bool hitGround = controller.IsGrounded;
            if (!exitingWall && (lostWall || stoppedMovingForward || hitGround))
            {
                string reason = hitGround ? "Hit Ground" : (lostWall ? "Lost Wall" : "Stopped");
                if (!hitGround)
                {
                    StartExitState("Fell/Stopped");
                }
                controller.RequestMovementModuleDeactivation(this);
            }
        }
    }

    public override void FixedTick()
    {
        ApplyWallRunPhysics();
    }

    public float GetCurrentMoveSpeed() => wallRunSpeed;

    /// <summary>
    /// Performs wall detection using raycasts and updates wall status flags.
    /// </summary>
    private void CheckForWall()
    {
        wallLeft = false;
        wallRight = false;
        _debugFoundWall = false;

        if (controller.Orientation == null || controller.Rb == null) return;

        Vector3 origin = transform.position + Vector3.up * wallRunHeightOffset;
        Vector3 rightDir = controller.Orientation.right;
        Vector3 leftDir = -rightDir;

        bool foundR = Physics.Raycast(origin, rightDir, out rightWallHit, wallCheckDistance, whatIsWall);
        bool foundL = Physics.Raycast(origin, leftDir, out leftWallHit, wallCheckDistance, whatIsWall);

        if (foundR)
        {
            wallRight = true;
            _debugLastWallNormal = rightWallHit.normal;
            _debugFoundWall = true;
        }
        if (foundL)
        {
            wallLeft = true;
            wallRight = !wallLeft;
            _debugLastWallNormal = leftWallHit.normal;
            _debugFoundWall = true;
        }
    }

    /// <summary>
    /// Core movement logic for wall running. Handles both horizontal and vertical motion while on walls.
    /// </summary>
    private void ApplyWallRunPhysics()
    {
        if (controller.Rb == null || controller.Orientation == null || (!wallLeft && !wallRight)) return;

        // Handle gravity based on configuration
        controller.Rb.useGravity = useStandardGravity;
        float targetVerticalVelocity = controller.Rb.linearVelocity.y;

        if (!useStandardGravity)
        {
            // Custom gravity compensation to maintain height during wall run
            float gravityCompensation = Physics.gravity.magnitude * gravityCompensationFactor;
            targetVerticalVelocity += gravityCompensation * Time.fixedDeltaTime;
        }

        // Calculate wall-relative movement direction
        Vector3 wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;
        Vector3 wallForward = Vector3.Cross(wallNormal, transform.up); // Direction along wall
        Vector3 currentHorizontalVelocity = new Vector3(controller.Rb.linearVelocity.x, 0f, controller.Rb.linearVelocity.z);

        if (currentHorizontalVelocity.magnitude > 0.1f && Vector3.Dot(wallForward, currentHorizontalVelocity) < 0f)
        {
            wallForward = -wallForward;
        }

        Vector3 targetVelocity = new Vector3(
            wallForward.x * wallRunSpeed,
            targetVerticalVelocity,
            wallForward.z * wallRunSpeed
        );

        controller.Rb.linearVelocity = targetVelocity;

        // Apply adhesion force to stick to the wall
        bool pushingAway = (wallLeft && controller.Input.HorizontalInput > 0.1f) || 
                          (wallRight && controller.Input.HorizontalInput < -0.1f);

        if (!pushingAway)
        {
            controller.Rb.AddForce(-wallNormal * wallAdhesionForce, ForceMode.Force);
        }
    }

    /// <summary>
    /// Executes wall jump logic, applying outward and upward forces.
    /// </summary>
    private void WallJump()
    {
        if (controller.Rb == null) return;
        if (!wallLeft && !wallRight) return;

        exitingWall = true;

        Vector3 wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;

        float outwardSpeed = wallJumpSideForce;
        float upwardSpeed = wallJumpUpForce;

        Vector3 velocityChange = wallNormal * outwardSpeed + Vector3.up * upwardSpeed;

        controller.Rb.linearVelocity = new Vector3(controller.Rb.linearVelocity.x, 0f, controller.Rb.linearVelocity.z);
        controller.Rb.AddForce(velocityChange, ForceMode.VelocityChange);

        cachedAnimator?.SetTrigger("WallJump");

        StartExitState("Wall Jump");
    }

    /// <summary>
    /// Initiates the exit state to prevent immediate re-attachment to walls.
    /// </summary>
    private void StartExitState(string reason = "Unknown")
    {
        if (!exitingWall)
        {
            exitingWall = true;
            exitWallTimer = exitWallTime;
        }
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (controller == null || !controller.ShowDebugLogs) return;

        Vector3 origin = transform.position + Vector3.up * wallRunHeightOffset;
        Vector3 rightDir = controller.Orientation != null ? controller.Orientation.right : transform.right;
        Vector3 leftDir = -rightDir;

        Gizmos.color = wallRight ? Color.green : Color.red;
        Gizmos.DrawLine(origin, origin + rightDir * wallCheckDistance);

        Gizmos.color = wallLeft ? Color.green : Color.red;
        Gizmos.DrawLine(origin, origin + leftDir * wallCheckDistance);

        if (_debugFoundWall)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(origin, origin + _debugLastWallNormal * 1.5f);
            UnityEditor.Handles.Label(origin + _debugLastWallNormal * 1.6f, "Wall N");

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + _debugLastWallForward * 1.5f);
            UnityEditor.Handles.Label(origin + _debugLastWallForward * 1.6f, "Wall Fwd");

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin, origin + _debugLastHorizontalVelocity.normalized * 2f);
            UnityEditor.Handles.Label(origin + _debugLastHorizontalVelocity.normalized * 2.1f, "Vel");

            Vector3 textPos = origin + Vector3.up * 0.5f;
            string debugText = $"Wall Found: {_debugFoundWall}\n" +
                             $"Speed OK: {_debugLastSpeedCheck} ({_debugLastHorizontalVelocity.magnitude:F1}/{minWallRunSpeed:F1})\n" +
                             $"Angle OK: {_debugLastAngleCheck} ({_debugLastEntryAngle:F1}°/{maxWallEntryAngle}°)\n" +
                             $"Fwd OK: {_debugLastFwdCheck}";
            UnityEditor.Handles.Label(textPos, debugText);
        }
    }
    #endif
}