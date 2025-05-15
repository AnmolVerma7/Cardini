// StickyMovementModule.cs
using UnityEngine;
using System.Collections;
using Cardini.Motion; // Your namespace

public class StickyMovementModule : MovementModule
{
    public override int Priority => 30;

    [Header("Surface Detection")]
    [SerializeField] private LayerMask stickableLayers;
    [Tooltip("How far ahead to check for initial stick (scaled by velocity).")]
    [SerializeField] private float initialDetectionDistanceScale = 0.5f;
    [SerializeField] private float minInitialDetectionDistance = 0.5f;
    [SerializeField] private float maxInitialDetectionDistance = 2.0f;
    [Tooltip("Max angle between velocity and -surfaceNormal for initial stick.")]
    [SerializeField] private float maxEntryAngle = 75f;
    [Tooltip("Min speed to attempt sticking.")]
    [SerializeField] private float minEntrySpeed = 3f;
    [Tooltip("Offset from surface to position player capsule pivot.")]
    [SerializeField] private float playerOffsetFromSurface = 0.5f;
    [Tooltip("How far inwards from playerOffsetFromSurface to cast for continuous surface check. Total ray length will be this + a bit.")]
    [SerializeField] private float surfaceStickRayLength = 0.3f;

    [Header("Transitions")]
    [Tooltip("Duration of the snap-to-surface transition.")]
    [SerializeField] private float snapToSurfaceDuration = 0.15f;

    [Header("Movement on Surface - Speeds")]
    [Tooltip("Speed when 'walking' on a surface (lowest input magnitude or no sprint).")]
    [SerializeField] private float stickyWalkSpeed = 2f;
    [Tooltip("Speed when 'jogging' on a surface (medium input magnitude or default).")]
    [SerializeField] private float stickyJogSpeed = 4f;
    [Tooltip("Speed when 'sprinting' on a surface.")]
    [SerializeField] private float stickySprintSpeed = 7f;

    [Header("Movement on Surface - Physics & Control")]
    [Tooltip("Minimum input magnitude to register movement intent.")]
    [SerializeField] private float inputDeadZone = 0.01f;
    [Tooltip("Acceleration when moving on surfaces. HIGHER = more responsive, reaches target speed faster.")]
    [SerializeField] private float surfaceAcceleration = 30f; // Increased default
    [Tooltip("Drag/Deceleration on surfaces when no input. HIGHER = stops faster.")]
    [SerializeField] private float surfaceDrag = 20f; // Increased default
    [Tooltip("How quickly the player capsule's forward rotates to match input direction on surface.")]
    [SerializeField] private float capsuleSurfaceRotationSpeed = 15f; // Renamed for clarity
    [Tooltip("How quickly the player MODEL rotates to its target orientation on the surface.")]
    [SerializeField] private float modelSurfaceRotationSpeed = 15f;
    [Tooltip("How quickly animator parameters (VelocityX/Z, NormalizedSpeed) are damped.")]
    [SerializeField] private float stickyAnimatorDampTime = 0.1f;

    [Header("Jumping from Surface")]
    [SerializeField] private float surfaceJumpUpForce = 8f;
    [SerializeField] private float surfaceJumpForwardForce = 2f;

    // --- Configuration Read from BaseLocomotionModule (if available) ---
    private bool _useToggleSprintOnWall_Config = false; 
    private bool _resumeSprintAfterIdleOnWall_Config = false;
    private float _walkInputMagnitudeThreshold_Config = 0.25f; // Adjusted default
    private float _jogInputMagnitudeThreshold_Config = 0.75f; // Adjusted default

    // --- Private State ---
    private bool _isOnStickySurface = false;
    private Vector3 _currentSurfaceNormal;
    private Vector3 _currentSurfacePoint;
    private Vector3 _currentCapsuleTangentForward; // Capsule's "forward" (facing/movement) on the surface
    private Coroutine _snapCoroutine;
    private CollisionDetectionMode _originalCollisionMode;

    private Animator _cachedAnimator;
    private BaseLocomotionModule _cachedBaseLocoModule;

    private float _currentDesiredSpeedTierOnWall;
    private bool _isSprintingOnWall_Action = false; 
    private bool _sprintToggleStateOnWall_Mode = false; 
    private Vector2 _lastRawInputOnWall = Vector2.zero;

    public override void Initialize(CardiniController controller)
    {
        base.Initialize(controller);
        playerOffsetFromSurface = controller.PlayerRadius > 0 ? controller.PlayerRadius : playerOffsetFromSurface;
        
        if (controller.PlayerObj != null)
            _cachedAnimator = controller.PlayerObj.GetComponentInChildren<Animator>();

        _cachedBaseLocoModule = controller.GetComponentInParent<BaseLocomotionModule>(); // GetComponentInParent more robust
        if (_cachedBaseLocoModule != null)
        {
            // Ensure BaseLocomotionModule has public getters for these
            _useToggleSprintOnWall_Config = _cachedBaseLocoModule.UseToggleSprint;
            _resumeSprintAfterIdleOnWall_Config = _cachedBaseLocoModule.ResumeSprintAfterIdle;
            _walkInputMagnitudeThreshold_Config = _cachedBaseLocoModule.GetWalkInputThreshold();
            _jogInputMagnitudeThreshold_Config = _cachedBaseLocoModule.GetJogInputThreshold();
            if (controller.ShowDebugLogs) Debug.Log($"<color=lime>StickyModule:</color> Initialized with BaseLoco config. SprintToggle:{_useToggleSprintOnWall_Config}, WalkThresh:{_walkInputMagnitudeThreshold_Config:F2}, JogThresh:{_jogInputMagnitudeThreshold_Config:F2}");
        }
        else
        {
            if (controller.ShowDebugLogs) Debug.LogWarning($"<color=yellow>StickyModule:</color> BaseLocomotionModule not found. Using default configs. SprintToggle:{_useToggleSprintOnWall_Config}, WalkThresh:{_walkInputMagnitudeThreshold_Config:F2}, JogThresh:{_jogInputMagnitudeThreshold_Config:F2}");
        }
    }

    public override bool WantsToActivate()
    {
        if (_isOnStickySurface || _snapCoroutine != null || controller.Input == null || controller.Rb == null) return false;
        if (!controller.IsGrounded) return false; 

        Vector3 currentVelocity = controller.Rb.linearVelocity;
        if (currentVelocity.magnitude < minEntrySpeed) return false;

        Vector3 velocityDirection = currentVelocity.normalized;
        float dynamicDetectionDistance = Mathf.Clamp(currentVelocity.magnitude * initialDetectionDistanceScale, minInitialDetectionDistance, maxInitialDetectionDistance);
        Vector3 castOrigin = controller.transform.position + controller.transform.up * (controller.PlayerHeight * 0.25f);

        RaycastHit hit;
        if (Physics.Raycast(castOrigin, velocityDirection, out hit, dynamicDetectionDistance, stickableLayers, QueryTriggerInteraction.Ignore))
        {
            float angleToNormal = Vector3.Angle(velocityDirection, -hit.normal);
            float surfaceAngleFromHorizontal = Vector3.Angle(hit.normal, Vector3.up);
            
            // Check if it's a valid sticky surface (not too shallow an entry, and actually a wall/ceiling)
            if (angleToNormal <= maxEntryAngle && surfaceAngleFromHorizontal > controller.MaxSlopeAngle - 5f)
            {
                Vector3 worldUpProjectedOntoWallPlane = Vector3.ProjectOnPlane(Vector3.up, hit.normal).normalized;
                if (worldUpProjectedOntoWallPlane.sqrMagnitude < 0.001f) worldUpProjectedOntoWallPlane = Vector3.ProjectOnPlane(controller.Orientation.forward, hit.normal).normalized;
                if (worldUpProjectedOntoWallPlane.sqrMagnitude < 0.001f) worldUpProjectedOntoWallPlane = Vector3.ProjectOnPlane(controller.transform.forward, hit.normal).normalized;

                Vector3 pointAboveEntry = hit.point + worldUpProjectedOntoWallPlane * (controller.PlayerHeight * 0.5f);
                if (!Physics.Raycast(pointAboveEntry + hit.normal * 0.1f, -hit.normal, 0.2f, stickableLayers, QueryTriggerInteraction.Ignore))
                {
                    if (controller.ShowDebugLogs) Debug.Log($"<color=#FFA500>Sticky WantsActivate:</color> Validation ray failed for surface continuity.");
                    return false;
                }
                _currentSurfaceNormal = hit.normal;
                _currentSurfacePoint = hit.point;
                if (controller.ShowDebugLogs) Debug.Log($"<color=#00FFF0>Sticky WantsActivate:</color> Surface detected. EntryAngle:{angleToNormal:F1}, SurfAngle:{surfaceAngleFromHorizontal:F1}, Normal:{hit.normal}");
                return true;
            }
        }
        return false;
    }

    public override void Activate()
    {
        _isOnStickySurface = true;
        controller.IsPlayerModelOrientationExternallyManaged = true;
        controller.SetMovementState(CharacterMovementState.StickyMovement);
        
        _originalCollisionMode = controller.Rb.collisionDetectionMode;
        controller.Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        controller.Rb.useGravity = false;

        _isSprintingOnWall_Action = false;
        _sprintToggleStateOnWall_Mode = false;
        _currentDesiredSpeedTierOnWall = 0f; 
        _lastRawInputOnWall = Vector2.zero;

        if (_snapCoroutine != null) StopCoroutine(_snapCoroutine);
        _snapCoroutine = StartCoroutine(SnapToSurfaceCoroutine(_currentSurfacePoint, _currentSurfaceNormal));

        _cachedAnimator?.SetBool("IsOnStickySurface", true);
    }

    private IEnumerator SnapToSurfaceCoroutine(Vector3 hitPoint, Vector3 surfaceNormal)
    {
        controller.Rb.isKinematic = true;
        Quaternion initialCapsuleRotation = controller.transform.rotation;
        Quaternion initialModelRotation = controller.PlayerObj.rotation;
        Vector3 initialPosition = controller.transform.position;

        Vector3 entryForwardDirection = controller.Rb.linearVelocity.normalized;
        if (entryForwardDirection.sqrMagnitude < 0.01f) entryForwardDirection = controller.Orientation.forward;
        
        _currentCapsuleTangentForward = Vector3.ProjectOnPlane(entryForwardDirection, surfaceNormal).normalized;
        if (_currentCapsuleTangentForward.sqrMagnitude < 0.001f) 
        {
            _currentCapsuleTangentForward = Vector3.ProjectOnPlane(controller.transform.forward, surfaceNormal).normalized;
            if (_currentCapsuleTangentForward.sqrMagnitude < 0.001f)
            {
                Vector3 tempRight = Vector3.Cross(surfaceNormal, Vector3.up);
                if (tempRight.sqrMagnitude < 0.001f) tempRight = Vector3.Cross(surfaceNormal, Vector3.right);
                _currentCapsuleTangentForward = tempRight.normalized; 
            }
        }
        
        Quaternion targetCapsuleRotation = Quaternion.LookRotation(_currentCapsuleTangentForward, surfaceNormal);

        
        // Model faces INTO wall, its "up" is along capsule's tangent forward (direction of movement)
        // --- START OF NEW BLOCK TO INSERT ---
        // This new block calculates targetModelRotation to keep the model upright relative to the wall.
        Vector3 modelTargetForwardDuringSnap = -surfaceNormal; // Model faces into the wall
        Vector3 modelTargetUpDuringSnap = Vector3.ProjectOnPlane(Vector3.up, surfaceNormal).normalized;
        if (modelTargetUpDuringSnap.sqrMagnitude < 0.001f) // Wall is likely a ceiling/floor
        {
            modelTargetUpDuringSnap = _currentCapsuleTangentForward; // Model's "up" is direction of movement on surface
            if (modelTargetUpDuringSnap.sqrMagnitude < 0.001f) // Fallback if _currentCapsuleTangentForward is zero (e.g., no entry velocity)
            {
                // Use the capsule's initial actual forward projection (before snap) as a fallback for model's "up"
                modelTargetUpDuringSnap = Vector3.ProjectOnPlane(initialCapsuleRotation * Vector3.forward, surfaceNormal).normalized;
                if (modelTargetUpDuringSnap.sqrMagnitude < 0.001f) modelTargetUpDuringSnap = Vector3.up; // Absolute fallback to world up
            }
        }
        Quaternion targetModelRotation = Quaternion.LookRotation(modelTargetForwardDuringSnap, modelTargetUpDuringSnap);
        
        Vector3 targetPosition = hitPoint + surfaceNormal * playerOffsetFromSurface;

        float elapsedTime = 0f;
        while (elapsedTime < snapToSurfaceDuration)
        {
            float t = Mathf.SmoothStep(0.0f, 1.0f, elapsedTime / snapToSurfaceDuration);
            controller.transform.position = Vector3.Lerp(initialPosition, targetPosition, t);
            controller.transform.rotation = Quaternion.Slerp(initialCapsuleRotation, targetCapsuleRotation, t);
            if (controller.PlayerObj != null) // Ensure PlayerObj exists
            {
                 controller.PlayerObj.rotation = Quaternion.Slerp(initialModelRotation, targetModelRotation, t);
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        controller.transform.position = targetPosition;
        controller.transform.rotation = targetCapsuleRotation;
        if (controller.PlayerObj != null) controller.PlayerObj.rotation = targetModelRotation;
        _currentSurfaceNormal = surfaceNormal; // Ensure this is set after snap

        controller.Rb.isKinematic = false;
        float entrySpeed = controller.Rb.linearVelocity.magnitude;
        // Determine initial speed tier based on entry (or if sprint was held)
        if (_isSprintingOnWall_Action && entrySpeed > stickyJogSpeed) _currentDesiredSpeedTierOnWall = stickySprintSpeed;
        else if (entrySpeed > stickyWalkSpeed) _currentDesiredSpeedTierOnWall = stickyJogSpeed;
        else _currentDesiredSpeedTierOnWall = stickyWalkSpeed;
        
        controller.Rb.linearVelocity = _currentCapsuleTangentForward * Mathf.Min(entrySpeed, _currentDesiredSpeedTierOnWall);

        _snapCoroutine = null;
    }

    public override void Deactivate()
    {
        _isOnStickySurface = false;
        controller.IsPlayerModelOrientationExternallyManaged = false; // Give control back
        controller.Rb.useGravity = true;
        controller.Rb.collisionDetectionMode = _originalCollisionMode;
        
        if (_snapCoroutine != null)
        {
            StopCoroutine(_snapCoroutine);
            _snapCoroutine = null;
            if (controller.Rb.isKinematic) controller.Rb.isKinematic = false;
        }

        // ADD THIS SECTION for smoother visual model re-orientation:
        if(controller.PlayerObj != null && controller.transform != null) 
        {
            // The capsule (controller.transform) should ideally be reset to world upright 
            // by the next active module (e.g., BaseLocomotion).
            // We make the model quickly align to what the capsule's forward *would be* if it were upright.
            Vector3 capsuleUprightForward = controller.transform.forward; // Assuming capsule will be made upright
            capsuleUprightForward.y = 0; // Flatten it for standard ground orientation
            if (capsuleUprightForward.sqrMagnitude < 0.001f) capsuleUprightForward = controller.Orientation.forward; // Fallback

            Quaternion targetModelUprightRotation = Quaternion.LookRotation(capsuleUprightForward.normalized, Vector3.up);
            controller.PlayerObj.rotation = Quaternion.Slerp(controller.PlayerObj.rotation, targetModelUprightRotation, 0.5f); // Adjust slerp speed as needed
        }
        // END OF ADDED SECTION

        if (_cachedAnimator != null)
        {
            _cachedAnimator.SetBool("IsOnStickySurface", false);
            _cachedAnimator.SetFloat("StickyNormalizedSpeed", 0f, stickyAnimatorDampTime, Time.deltaTime); 
            _cachedAnimator.SetFloat("StickySpeed", 0f, stickyAnimatorDampTime, Time.deltaTime);
            _cachedAnimator.SetFloat("StickySideSpeed", 0f, stickyAnimatorDampTime, Time.deltaTime);
        }
        // IMPORTANT: BaseLocomotionModule.Activate() is responsible for resetting capsule to upright.
    }

    public override void Tick()
    {
        if (!_isOnStickySurface || _snapCoroutine != null)
        {
            if (_snapCoroutine == null && _cachedAnimator != null && _cachedAnimator.GetBool("IsOnStickySurface"))
            {
                 _cachedAnimator.SetBool("IsOnStickySurface", false);
                 _cachedAnimator.SetFloat("StickyNormalizedSpeed", 0f, stickyAnimatorDampTime, Time.deltaTime);
                 _cachedAnimator.SetFloat("StickySpeed", 0f, stickyAnimatorDampTime, Time.deltaTime);
                 _cachedAnimator.SetFloat("StickySideSpeed", 0f, stickyAnimatorDampTime, Time.deltaTime);
            }
            return;
        }

        _lastRawInputOnWall = new Vector2(controller.Input.HorizontalInput, controller.Input.VerticalInput);

        UpdateSprintStateOnWall(); 
        DetermineDesiredSpeedAndAnimationStateOnWall();

        if (controller.Input.JumpPressed)
        {
            PerformSurfaceJump();
        }
    }

    public override void FixedTick()
    {
        if (!_isOnStickySurface || _snapCoroutine != null) return;
        
        MaintainSurfaceContact(); 
        if (!_isOnStickySurface) return;

        ApplySurfaceMovement(); 
        OrientCapsuleAndModelToSurface(); // Renamed and updated
    }

    private void UpdateSprintStateOnWall()
    {
        if (controller.Input == null) return;
        bool isActuallyMovingOnWall = _lastRawInputOnWall.sqrMagnitude > inputDeadZone * inputDeadZone;
        // For sticky movement, canPotentiallySprint just means moving. No crouch check needed here.
        bool canPotentiallySprintOnWall = isActuallyMovingOnWall; 

        if (_useToggleSprintOnWall_Config)
        {
            if (controller.Input.SprintTogglePressed)
            {
                _sprintToggleStateOnWall_Mode = !_sprintToggleStateOnWall_Mode;
                if (!_sprintToggleStateOnWall_Mode) _isSprintingOnWall_Action = false;
            }

            if (_sprintToggleStateOnWall_Mode)
            {
                _isSprintingOnWall_Action = canPotentiallySprintOnWall;
                if (!_resumeSprintAfterIdleOnWall_Config && !isActuallyMovingOnWall && _sprintToggleStateOnWall_Mode)
                {
                    _sprintToggleStateOnWall_Mode = false; // Turn off mode if stopped and not resuming
                }
            }
            else _isSprintingOnWall_Action = false;
            
            if (_isSprintingOnWall_Action && !canPotentiallySprintOnWall) _isSprintingOnWall_Action = false;
        }
        else // Hold to sprint
        {
            _isSprintingOnWall_Action = controller.Input.SprintHeld && canPotentiallySprintOnWall;
            _sprintToggleStateOnWall_Mode = false;
        }
    }
    
    private void DetermineDesiredSpeedAndAnimationStateOnWall()
    {
        float inputMagnitude = _lastRawInputOnWall.magnitude;
        float targetAnimNormalizedSpeed = 0f; 
        
        if (_isSprintingOnWall_Action && inputMagnitude > inputDeadZone)
        {
            _currentDesiredSpeedTierOnWall = stickySprintSpeed;
            targetAnimNormalizedSpeed = 3f;
        }
        else if (inputMagnitude >= _jogInputMagnitudeThreshold_Config) // Using configured threshold
        {
            _currentDesiredSpeedTierOnWall = stickyJogSpeed;
            targetAnimNormalizedSpeed = 2f;
        }
        else if (inputMagnitude >= _walkInputMagnitudeThreshold_Config) // Using configured threshold
        {
            _currentDesiredSpeedTierOnWall = stickyWalkSpeed;
            targetAnimNormalizedSpeed = 1f;
        }
        else 
        {
            _currentDesiredSpeedTierOnWall = 0f; 
            targetAnimNormalizedSpeed = 0f;
        }

        if (_cachedAnimator != null)
        {
            _cachedAnimator.SetFloat("StickyNormalizedSpeed", targetAnimNormalizedSpeed, stickyAnimatorDampTime, Time.deltaTime);

            Vector3 velocityOnPlane = Vector3.ProjectOnPlane(controller.Rb.linearVelocity, _currentSurfaceNormal);
            // Use _currentCapsuleTangentForward as the reference for "forward" speed on the wall
            float speedAlongTangent = Vector3.Dot(velocityOnPlane, _currentCapsuleTangentForward);
            // Right vector on the wall relative to capsule's orientation
            Vector3 capsuleSurfaceRight = Vector3.Cross(_currentCapsuleTangentForward, _currentSurfaceNormal); 
            float sideSpeed = Vector3.Dot(velocityOnPlane, capsuleSurfaceRight);

            float currentMaxSpeedForAnim = _currentDesiredSpeedTierOnWall > 0.1f ? _currentDesiredSpeedTierOnWall : stickyJogSpeed; // Avoid division by zero
            _cachedAnimator.SetFloat("StickySpeed", speedAlongTangent / currentMaxSpeedForAnim , stickyAnimatorDampTime, Time.deltaTime);
            _cachedAnimator.SetFloat("StickySideSpeed", sideSpeed / currentMaxSpeedForAnim, stickyAnimatorDampTime, Time.deltaTime);
        }
        // if(controller.ShowDebugLogs && inputMagnitude > 0.01f) Debug.Log($"InputMag: {inputMagnitude:F2}, WalkThresh: {_walkInputMagnitudeThreshold_Config:F2}, JogThresh: {_jogInputMagnitudeThreshold_Config:F2}, SprintAction: {_isSprintingOnWall_Action}, TargetTierSpeed: {_currentDesiredSpeedTierOnWall:F2}, AnimNormSpeed: {targetAnimNormalizedSpeed}");
    }

    private void MaintainSurfaceContact()
    {
        Vector3 castOrigin = controller.Rb.position + _currentSurfaceNormal * (playerOffsetFromSurface * 0.1f); 
        float rayLength = playerOffsetFromSurface + surfaceStickRayLength; 

        RaycastHit hitInfo;
        if (Physics.Raycast(castOrigin, -_currentSurfaceNormal, out hitInfo, rayLength, stickableLayers, QueryTriggerInteraction.Ignore))
        {
            _currentSurfaceNormal = hitInfo.normal; 
            _currentSurfacePoint = hitInfo.point;

            Vector3 idealPositionAtPivot = _currentSurfacePoint + _currentSurfaceNormal * playerOffsetFromSurface;
            Vector3 normalCorrection = Vector3.Project(idealPositionAtPivot - controller.Rb.position, _currentSurfaceNormal);
            
            if (normalCorrection.sqrMagnitude > (0.0001f * 0.0001f)) 
            {
                controller.Rb.MovePosition(controller.Rb.position + normalCorrection);
            }
        }
        else
        {
            if (controller.ShowDebugLogs) Debug.LogWarning($"<color=red>Sticky MaintainContact:</color> Lost surface. Deactivating.");
            controller.RequestMovementModuleDeactivation(this);
        }
    }

    // Renamed from OrientToSurface
    private void OrientCapsuleAndModelToSurface()
    {
        // --- CAPSULE ORIENTATION ---
        // _currentCapsuleTangentForward is updated by ApplySurfaceMovement if there was directional input.
        if (_currentCapsuleTangentForward.sqrMagnitude < 0.001f) // Fallback if tangent forward is zero
        {
            _currentCapsuleTangentForward = Vector3.ProjectOnPlane(controller.transform.forward, _currentSurfaceNormal).normalized;
            if (_currentCapsuleTangentForward.sqrMagnitude < 0.001f) {
                 Vector3 camForwardProj = Vector3.ProjectOnPlane(controller.Orientation.forward, _currentSurfaceNormal).normalized;
                 if(camForwardProj.sqrMagnitude > 0.001f) _currentCapsuleTangentForward = camForwardProj;
                 else  _currentCapsuleTangentForward = Vector3.ProjectOnPlane(Vector3.forward, _currentSurfaceNormal).normalized; // last resort
            }
        }
        
        if (_currentCapsuleTangentForward.sqrMagnitude > 0.001f && _currentSurfaceNormal.sqrMagnitude > 0.001f)
        {
            Quaternion targetCapsuleRotation = Quaternion.LookRotation(_currentCapsuleTangentForward, _currentSurfaceNormal);
            controller.Rb.MoveRotation(Quaternion.Slerp(controller.Rb.rotation, targetCapsuleRotation, Time.fixedDeltaTime * capsuleSurfaceRotationSpeed));
        }

        // --- MODEL ORIENTATION ---
        // Model faces INTO the wall, its "up" is aligned with the capsule's tangent forward (direction of movement)
        if (controller.PlayerObj != null && _currentSurfaceNormal.sqrMagnitude > 0.001f)
        {
            // Model's "forward" is always INTO the wall
            Vector3 modelTargetForward = -_currentSurfaceNormal;

            // Model's "up" needs to be the "up" direction on the plane of the wall.
            Vector3 wallPlaneUpForModel = Vector3.ProjectOnPlane(Vector3.up, _currentSurfaceNormal).normalized;

            // Fallback if the wall is a ceiling/floor (normal is world up/down)
            if (wallPlaneUpForModel.sqrMagnitude < 0.001f)
            {
                // For ceilings/floors, model's "up" should align with its direction of travel.
                wallPlaneUpForModel = _currentCapsuleTangentForward; // Use the capsule's movement direction
                if (wallPlaneUpForModel.sqrMagnitude < 0.001f) // If no movement, fallback
                {
                    // Try capsule's current actual forward projected, or camera's projected forward
                    wallPlaneUpForModel = Vector3.ProjectOnPlane(controller.transform.forward, _currentSurfaceNormal).normalized;
                    if (wallPlaneUpForModel.sqrMagnitude < 0.001f)
                    {
                    wallPlaneUpForModel = Vector3.ProjectOnPlane(controller.Orientation.forward, _currentSurfaceNormal).normalized;
                    }
                    if (wallPlaneUpForModel.sqrMagnitude < 0.001f) wallPlaneUpForModel = Vector3.up; // Absolute fallback
                }
            }
            
            if (modelTargetForward.sqrMagnitude > 0.001f && wallPlaneUpForModel.sqrMagnitude > 0.001f)
            {
                Quaternion targetModelRotation = Quaternion.LookRotation(modelTargetForward, wallPlaneUpForModel);
                controller.PlayerObj.rotation = Quaternion.Slerp(controller.PlayerObj.rotation, targetModelRotation, Time.fixedDeltaTime * modelSurfaceRotationSpeed); // Using modelSurfaceRotationSpeed
            }
        }
    }

    private void ApplySurfaceMovement()
    {
        float horizontalInput = _lastRawInputOnWall.x;
        float verticalInput = _lastRawInputOnWall.y;

        Vector3 wallPlaneUp, wallPlaneRight;
        bool isWallLike = Mathf.Abs(Vector3.Dot(_currentSurfaceNormal, Vector3.up)) < 0.707f;

        if (isWallLike)
        {
            wallPlaneUp = Vector3.ProjectOnPlane(Vector3.up, _currentSurfaceNormal).normalized;
            if (wallPlaneUp.sqrMagnitude < 0.001f) wallPlaneUp = Vector3.ProjectOnPlane(controller.Orientation.forward, _currentSurfaceNormal).normalized;
            wallPlaneRight = Vector3.Cross(_currentSurfaceNormal, wallPlaneUp).normalized;
        }
        else 
        {
            wallPlaneUp = Vector3.ProjectOnPlane(controller.Orientation.forward, _currentSurfaceNormal).normalized;
            wallPlaneRight = Vector3.ProjectOnPlane(controller.Orientation.right, _currentSurfaceNormal).normalized;
            if (wallPlaneRight.sqrMagnitude < 0.001f && wallPlaneUp.sqrMagnitude > 0.001f)
                wallPlaneRight = Vector3.Cross(wallPlaneUp, _currentSurfaceNormal).normalized;
        }

        // Final safety nets for axes
        if (wallPlaneUp.sqrMagnitude < 0.001f) wallPlaneUp = Vector3.ProjectOnPlane(controller.transform.forward, _currentSurfaceNormal).normalized;
        if (wallPlaneRight.sqrMagnitude < 0.001f) wallPlaneRight = Vector3.Cross(wallPlaneUp, _currentSurfaceNormal).normalized;
        if (wallPlaneUp.sqrMagnitude < 0.001f) wallPlaneUp = Vector3.one; // Avoid LookRotation error if all fails
        if (wallPlaneRight.sqrMagnitude < 0.001f) wallPlaneRight = Vector3.ProjectOnPlane(controller.Orientation.right, _currentSurfaceNormal).normalized; // Try another source
         if (wallPlaneRight.sqrMagnitude < 0.001f) wallPlaneRight = Vector3.one; // Avoid LookRotation error if all fails

        Vector3 desiredMoveDirection = (wallPlaneUp * verticalInput) + (wallPlaneRight * horizontalInput);
        Vector3 desiredVelocityOnSurface = Vector3.zero;
        bool isTryingToMove = false;

        if (_lastRawInputOnWall.sqrMagnitude > inputDeadZone * inputDeadZone)
        {
            if (desiredMoveDirection.sqrMagnitude > 0.001f)
            {
                isTryingToMove = true;
                _currentCapsuleTangentForward = desiredMoveDirection.normalized; // Capsule faces/moves this way
                desiredVelocityOnSurface = _currentCapsuleTangentForward * _currentDesiredSpeedTierOnWall; 
            }
            // else: input exists, but projections resulted in no movement (e.g. into a corner)
        }
        
        Vector3 currentVelocityOnSurface = Vector3.ProjectOnPlane(controller.Rb.linearVelocity, _currentSurfaceNormal);
        Vector3 newVelocityOnSurface = Vector3.MoveTowards(
            currentVelocityOnSurface,
            desiredVelocityOnSurface,
            (isTryingToMove ? surfaceAcceleration : surfaceDrag) * Time.fixedDeltaTime
        );
        
        Vector3 perpendicularVelocity = controller.Rb.linearVelocity - currentVelocityOnSurface; 
        controller.Rb.linearVelocity = newVelocityOnSurface + perpendicularVelocity;

        if(controller.ShowDebugLogs && isTryingToMove)
        {
            // Debug.Log($"StickyMove: Vel: {controller.Rb.linearVelocity.magnitude:F2}/{_currentDesiredSpeedTierOnWall:F2}, TargetTangent: {_currentCapsuleTangentForward.ToString("F2")}");
        }
    }

    private void PerformSurfaceJump()
    {
        if (controller.ShowDebugLogs) Debug.Log($"<color=#00FFF0>Sticky Jump:</color> Normal: {_currentSurfaceNormal}, CapsuleTangent: {_currentCapsuleTangentForward}");
        
        Vector3 jumpDirection = (_currentSurfaceNormal * surfaceJumpUpForce) + (_currentCapsuleTangentForward * surfaceJumpForwardForce);
        if (jumpDirection.sqrMagnitude < 0.01f) jumpDirection = (_currentSurfaceNormal + Vector3.up).normalized * ((surfaceJumpUpForce + surfaceJumpForwardForce) * 0.5f);

        controller.Rb.linearVelocity = jumpDirection; 
        controller.RequestMovementModuleDeactivation(this);
    }
    
    public float GetCurrentStickySpeedOnSurface() => _isOnStickySurface ? controller.Rb.linearVelocity.magnitude : 0f; // Reflect actual speed

    #if UNITY_EDITOR
    // Gizmos remain largely the same, ensure _currentCapsuleTangentForward is used
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || controller == null || !controller.ShowDebugLogs) return;

        if (!_isOnStickySurface && controller.Rb != null && controller.Rb.linearVelocity.magnitude >= minEntrySpeed)
        {
            Gizmos.color = Color.yellow;
            Vector3 castOrigin = controller.transform.position + controller.transform.up * (controller.PlayerHeight * 0.25f);
            Gizmos.DrawLine(castOrigin, castOrigin + controller.Rb.linearVelocity.normalized * 
                Mathf.Clamp(controller.Rb.linearVelocity.magnitude * initialDetectionDistanceScale, minInitialDetectionDistance, maxInitialDetectionDistance));
        }

        if (_isOnStickySurface)
        {
            Vector3 pos = controller.transform.position;
            Gizmos.color = Color.green; 
            Gizmos.DrawLine(pos, pos + _currentSurfaceNormal * 1.5f);
            UnityEditor.Handles.Label(pos + _currentSurfaceNormal * 1.5f, "Surface N (Capsule Up)");

            if(_currentCapsuleTangentForward.sqrMagnitude > 0.001f)
            {
                Gizmos.color = Color.blue; 
                Gizmos.DrawLine(pos, pos + _currentCapsuleTangentForward * 1.0f);
                UnityEditor.Handles.Label(pos + _currentCapsuleTangentForward * 1.0f, $"Capsule Fwd (MoveDir)\nSpeedTier: {_currentDesiredSpeedTierOnWall:F1}\nActualSpeed: {Vector3.ProjectOnPlane(controller.Rb.linearVelocity, _currentSurfaceNormal).magnitude:F1}");
                
                Gizmos.color = Color.red; 
                Vector3 currentCapsuleSurfaceRight = Vector3.Cross(_currentCapsuleTangentForward, _currentSurfaceNormal);
                Gizmos.DrawLine(pos, pos + currentCapsuleSurfaceRight * 1.0f);
                UnityEditor.Handles.Label(pos + currentCapsuleSurfaceRight * 1.0f, "Capsule Right");
            }

            Gizmos.color = Color.cyan;
            if(controller.PlayerObj != null) {
                Gizmos.DrawLine(controller.PlayerObj.position, controller.PlayerObj.position + controller.PlayerObj.forward * 0.7f);
                UnityEditor.Handles.Label(controller.PlayerObj.position + controller.PlayerObj.forward * 0.7f, "Model Fwd");
            }


            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(_currentSurfacePoint, 0.1f);
            UnityEditor.Handles.Label(_currentSurfacePoint, $"Contact Pt");
        }
    }
    #endif
}