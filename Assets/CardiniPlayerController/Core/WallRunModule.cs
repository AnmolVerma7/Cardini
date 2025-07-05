using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    /// <summary>
    /// Clean, focused wall running module that maintains momentum and provides directional wall jumping.
    /// </summary>
    public class WallRunModule : MovementModuleBase
    {
        [Header("Wall Running Settings")]
        [Tooltip("Force applied forward along the wall")]
        public float wallRunForce = 200f;
        
        [Tooltip("Maximum wall running speed (prevents infinite acceleration)")]
        public float maxWallRunSpeed = 15f;
        
        [Tooltip("Minimum speed required to initiate wall running")]
        public float minWallRunSpeed = 8f;
        
        [Tooltip("Maximum time allowed on a single wall")]
        public float maxWallRunTime = 1.5f;
        
        [Tooltip("Cooldown before you can wall run on the same wall again")]
        public float wallRunCooldown = 0.5f;
        
        [Tooltip("Force that counteracts gravity while wall running")]
        public float gravityCounterForce = 50f;
        
        [Tooltip("Force that keeps player attached to the wall")]
        public float wallAttachForce = 100f;
        
        [Tooltip("How smoothly the player transitions between wall directions")]
        public float wallDirectionSmoothing = 8f;

        [Header("Wall Jumping")]
        [Tooltip("Upward force when jumping off a wall")]
        public float wallJumpUpForce = 12f;
        
        [Tooltip("Force pushing away from the wall when jumping")]
        public float wallJumpSideForce = 15f;
        
        [Tooltip("Forward force when jumping (in look direction)")]
        public float wallJumpForwardForce = 8f;
        
        [Tooltip("Maximum wall jumps allowed per wall")]
        public int maxWallJumpsPerWall = 1;

        [Header("References")]
        [SerializeField] private WallDetector wallDetector;

        // Runtime state
        private float _wallRunTimer;
        private int _wallJumpsUsed;
        private Transform _currentWall;
        private float _lastWallExitTime;
        private Vector3 _smoothedWallForward;
        private Vector3 _smoothedWallNormal;
        private bool _wasWallRunning;
        private float _lastWallDirection; // Store last direction for proper exit animation

        public override int Priority => 3; // Higher than airborne (2) and grounded (0)
        public override CharacterMovementState AssociatedPrimaryMovementState => CharacterMovementState.WallRunning;

        public override void Initialize(CardiniController controller)
        {
            base.Initialize(controller);
            
            // Find WallDetector if not assigned
            if (wallDetector == null)
                wallDetector = GetComponentInChildren<WallDetector>();
            
            if (wallDetector == null)
                Debug.LogError($"WallRunModule: WallDetector component not found!", this);
        }

        public override bool CanEnterState()
        {
            if (wallDetector == null || !CommonChecks()) return false;
            
            var wallData = wallDetector.CurrentWallData;
            
            // Must have a valid wall to run on
            if (!wallData.canWallRun) return false;
            
            // Must have sufficient speed
            float currentSpeed = Motor.BaseVelocity.magnitude;
            if (currentSpeed < minWallRunSpeed) return false;
            
            // Check cooldown for same wall
            if (_currentWall == wallData.wallTransform && 
                Time.time - _lastWallExitTime < wallRunCooldown)
                return false;
            
            // Must be airborne or just leaving ground (seamless transition)
            bool isAirborneOrJustLeft = !Motor.GroundingStatus.IsStableOnGround || 
                                       Motor.LastGroundingStatus.IsStableOnGround;
            
            return isAirborneOrJustLeft;
        }

        public override void OnEnterState()
        {
            var wallData = wallDetector.CurrentWallData;
            
            // Initialize wall run state
            if (IsNewWall(wallData.wallTransform))
            {
                _currentWall = wallData.wallTransform;
                _wallRunTimer = maxWallRunTime;
                _wallJumpsUsed = 0;
            }

            // IMPORTANT: Clear jump consumed flags when entering wall run
            // This allows the player to jump off the wall even if they jumped to reach it
            Controller.SetJumpConsumed(false);
            Controller.SetDoubleJumpConsumed(false);
            
            // Initialize smoothed directions
            _smoothedWallForward = wallData.wallForward;
            _smoothedWallNormal = wallData.wallNormal;
            
            // Store wall direction for proper exit animation
            _lastWallDirection = wallData.primaryWallType == WallDetector.WallType.Right ? 1f : 0f;
            
            // Prevent grounded module from interfering
            Motor.ForceUnground(0.1f);
            
            // Set animation states
            PlayerAnimator?.SetGrounded(false);
            if (PlayerAnimator != null)
            {
                PlayerAnimator.SetWallRunning(true, _lastWallDirection);
            }
            
            _wasWallRunning = true;
            
            Debug.Log($"[WallRun] Started wall running on {wallData.primaryWallType} wall, direction: {_lastWallDirection}");
        }

        public override void OnExitState()
        {
            _lastWallExitTime = Time.time;
            _wasWallRunning = false;
            
            // Turn off wall running animation using LAST direction (not 0f)
            if (PlayerAnimator != null)
            {
                PlayerAnimator.SetWallRunning(false, _lastWallDirection);
                Debug.Log($"[WallRun] Exiting with last direction: {_lastWallDirection}");
            }
            
            Debug.Log("[WallRun] Exited wall running");
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            // Standard rotation - capsule and mesh rotate together with input
            Controller.HandleStandardRotation(ref currentRotation, deltaTime);
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (wallDetector == null) return;
            
            var wallData = wallDetector.CurrentWallData;
            
            // Update timer
            _wallRunTimer -= deltaTime;
            
            // Check if we should exit
            if (_wallRunTimer <= 0f || !wallData.canWallRun)
            {
                return; // Exit gracefully
            }
            
            // Smooth wall direction transitions for curved surfaces
            UpdateWallDirections(wallData, deltaTime);
            
            // Apply wall running forces
            ApplyWallRunMovement(ref currentVelocity, deltaTime);
            
            // Handle wall jumping
            HandleWallJumpInput(ref currentVelocity);
        }

        private void UpdateWallDirections(WallDetector.WallData wallData, float deltaTime)
        {
            // Smoothly interpolate wall directions for curved surfaces and corners
            _smoothedWallForward = Vector3.Slerp(_smoothedWallForward, wallData.wallForward, 
                                               wallDirectionSmoothing * deltaTime);
            
            _smoothedWallNormal = Vector3.Slerp(_smoothedWallNormal, wallData.wallNormal, 
                                              wallDirectionSmoothing * deltaTime);
            
            // Ensure directions stay normalized
            _smoothedWallForward.Normalize();
            _smoothedWallNormal.Normalize();
        }

        private void ApplyWallRunMovement(ref Vector3 currentVelocity, float deltaTime)
        {
            // 1. Forward momentum along wall (with speed limit to prevent ramping)
            Vector3 currentWallVelocity = Vector3.Project(currentVelocity, _smoothedWallForward);
            float currentWallSpeed = currentWallVelocity.magnitude;
            
            if (currentWallSpeed < maxWallRunSpeed)
            {
                Vector3 forwardForce = _smoothedWallForward * wallRunForce * deltaTime;
                currentVelocity += forwardForce;
                
                // Clamp wall speed to prevent infinite acceleration
                Vector3 newWallVelocity = Vector3.Project(currentVelocity, _smoothedWallForward);
                if (newWallVelocity.magnitude > maxWallRunSpeed)
                {
                    Vector3 otherVelocity = currentVelocity - newWallVelocity;
                    currentVelocity = otherVelocity + _smoothedWallForward * maxWallRunSpeed;
                }
            }
            
            // 2. Counter gravity to maintain height
            Vector3 gravityCounter = Vector3.up * gravityCounterForce * deltaTime;
            currentVelocity += gravityCounter;
            
            // 3. Attach to wall (but don't override player input away from wall)
            Vector3 wallAttach = -_smoothedWallNormal * wallAttachForce * deltaTime;
            
            // Only apply wall attach if player isn't actively moving away from wall
            Vector3 inputDirection = Controller.MoveInputVector;
            float inputAwayFromWall = Vector3.Dot(inputDirection, _smoothedWallNormal);
            
            if (inputAwayFromWall <= 0f) // Not moving away from wall
            {
                currentVelocity += wallAttach;
            }
            
            // 4. Apply standard gravity (will be countered by gravityCounterForce)
            currentVelocity += Settings.Gravity * deltaTime;
            
            // 5. Project velocity to be tangent to wall surface for smooth curved following
            Vector3 velocityOnWallPlane = Vector3.ProjectOnPlane(currentVelocity, _smoothedWallNormal);
            Vector3 wallNormalComponent = Vector3.Project(currentVelocity, _smoothedWallNormal);
            
            // Keep some normal component for wall attachment, but limit it
            wallNormalComponent = Vector3.ClampMagnitude(wallNormalComponent, 5f);
            currentVelocity = velocityOnWallPlane + wallNormalComponent;
        }
        private void HandleWallJumpInput(ref Vector3 currentVelocity)
        {
            if (!Controller.IsJumpRequested() || Controller.IsJumpConsumed()) 
            {
                return;
            }
            
            Debug.Log("[WallRun] Jump input detected while wall running!");
            
            // Check if we have wall jumps remaining
            if (_wallJumpsUsed >= maxWallJumpsPerWall)
            {
                Controller.ConsumeJumpRequest();
                Debug.Log("[WallRun] Wall jump blocked - no jumps remaining");
                return;
            }
            
            // Since we're already wall running, we know we have a valid wall
            Vector3 wallNormal = _smoothedWallNormal;
            Vector3 wallForward = _smoothedWallForward;
            
            // === QUALITY OF LIFE DIRECTIONAL LOGIC ===
            
            // Get input in world space
            Vector3 inputDirection = Controller.MoveInputVector;
            float inputMagnitude = inputDirection.magnitude;
            
            // Calculate input relative to wall forward
            float forwardDot = Vector3.Dot(inputDirection.normalized, wallForward);
            float sidewaysDot = Vector3.Dot(inputDirection.normalized, wallNormal);
            
            Vector3 jumpDirection;
            string jumpType = "";
            
            // Case 1: No Input - Pure sideways jump
            if (inputMagnitude < 0.1f)
            {
                jumpDirection = wallNormal;
                jumpType = "No-Input Sideways";
                Debug.Log($"[WallRun] {jumpType} jump - jumping directly away from wall");
            }
            // Case 2: Mostly Forward Input - 30° angled jump
            else if (forwardDot > 0.7f && Mathf.Abs(sidewaysDot) < 0.5f)
            {
                // Jump at 30 degrees away from wall while maintaining forward momentum
                float angleInRadians = 35f * Mathf.Deg2Rad;
                jumpDirection = wallForward + wallNormal * Mathf.Tan(angleInRadians);
                jumpDirection.Normalize();
                jumpType = "Forward-Angled";
                Debug.Log($"[WallRun] {jumpType} jump - 30° angle away from wall");
            }
            // Case 3: Mostly Backward Input - Reverse momentum jump
            else if (forwardDot < -0.7f && Mathf.Abs(sidewaysDot) < 0.5f)
            {
                jumpDirection = -wallForward * 0.5f + wallNormal;
                jumpDirection.Normalize();
                jumpType = "Backward-Cancel";
                Debug.Log($"[WallRun] {jumpType} jump - canceling forward momentum");
            }
            // Case 4: Input Away from Wall - Boost away
            else if (sidewaysDot > 0.7f)
            {
                jumpDirection = wallNormal * 1.2f + wallForward * 0.3f;
                jumpDirection.Normalize();
                jumpType = "Push-Away";
                Debug.Log($"[WallRun] {jumpType} jump - boosted push away from wall");
            }
            // Case 5: Input Toward Wall - Shallow angle jump
            else if (sidewaysDot < -0.7f)
            {
                jumpDirection = wallNormal * 0.5f + wallForward;
                jumpDirection.Normalize();
                jumpType = "Wall-Hug";
                Debug.Log($"[WallRun] {jumpType} jump - shallow angle, staying close to wall");
            }
            // Default: Use original directional blending for diagonal inputs
            else
            {
                // Your original system - blend based on look direction
                Vector3 playerLookDirection = Controller.LookInputVector;
                if (playerLookDirection.sqrMagnitude < 0.1f)
                {
                    playerLookDirection = Motor.CharacterForward;
                }
                playerLookDirection = Vector3.ProjectOnPlane(playerLookDirection, Motor.CharacterUp).normalized;
                
                float lookAngleToWall = Vector3.Angle(playerLookDirection, -wallNormal);
                float blendFactor = Mathf.InverseLerp(0f, 180f, lookAngleToWall) * 0.5f;
                jumpDirection = Vector3.Slerp(wallNormal, playerLookDirection, blendFactor).normalized;
                jumpType = "Directional-Blend";
                Debug.Log($"[WallRun] {jumpType} jump - using look direction blend");
            }
            
            // === APPLY JUMP FORCES ===
            
            // Use ExecuteJump for state management
            Controller.ExecuteJump(wallJumpUpForce, 0f, Vector3.zero);
            Controller.SetWallJumpedThisFrame(true);
            
            // Apply horizontal forces based on jump type
            float horizontalForce = wallJumpSideForce;
            
            // Adjust force magnitude based on jump type
            if (jumpType == "Forward-Angled")
            {
                horizontalForce *= 0.8f; // Slightly less side force when jumping forward
            }
            else if (jumpType == "Push-Away")
            {
                horizontalForce *= 1.2f; // Extra force when actively pushing away
            }
            else if (jumpType == "Wall-Hug")
            {
                horizontalForce *= 0.6f; // Less force when trying to stay close
            }
            
            // Add the horizontal jump velocity
            Vector3 horizontalJump = jumpDirection * horizontalForce;
            Controller.AddVelocity(horizontalJump);
            
            // Update tracking
            _wallJumpsUsed++;
            
            Debug.Log($"[WallRun] Wall jump executed! Type: {jumpType}, Direction: {jumpDirection}, Force: {horizontalForce:F1}");
            Debug.Log($"[WallRun] Wall jumps used: ({_wallJumpsUsed}/{maxWallJumpsPerWall})");
        }


        private bool IsNewWall(Transform wallTransform)
        {
            return _currentWall != wallTransform;
        }

        public override void BeforeCharacterUpdate(float deltaTime) { }

        public override void AfterCharacterUpdate(float deltaTime) 
        {
            // Keep animation states updated
            if (PlayerAnimator != null && _wasWallRunning && wallDetector != null)
            {
                var wallData = wallDetector.CurrentWallData;
                float wallDirection = wallData.primaryWallType == WallDetector.WallType.Right ? 1f : 0f;
                
                // Update stored direction for smooth transitions
                _lastWallDirection = wallDirection;
                
                // Keep overriding other modules
                PlayerAnimator.SetGrounded(false);
                PlayerAnimator.SetWallRunning(true, wallDirection);
            }
        }

        public override void PostGroundingUpdate(float deltaTime) { }

        // Public methods for debugging/UI
        public float GetWallRunTimeRemaining() => Mathf.Max(0f, _wallRunTimer);
        public float GetWallRunProgress() => 1f - (_wallRunTimer / maxWallRunTime);
        public int GetWallJumpsRemaining() => Mathf.Max(0, maxWallJumpsPerWall - _wallJumpsUsed);
    }
}