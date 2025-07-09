using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    /// <summary>
    /// Wall running module with Ghostrunner-style physics and directional wall jumping.
    /// Features proper jump state management and wall run cooldowns.
    /// </summary>
    public class WallRunModule : MovementModuleBase
    {
        [Header("Wall Running")]
        public float wallRunSpeed = 8f;
        public float wallRunGravity = 15f;
        public float maxWallRunTime = 3f;
        public float minEntrySpeed = 5f;

        [Header("Wall Jumping")]
        public float wallJumpUpForce = 12f;
        public float wallJumpForwardForce = 8f;
        public float wallJumpSideForce = 15f;
        public int maxJumpsPerWall = 2;
        [Tooltip("A small delay after starting a wallrun before you can jump, to prevent accidental jumps.")]
        public float wallJumpInputGracePeriod = 0.1f;

        [Header("Detection")]
        public float wallStickDistance = 0.6f;
        public float wallRunCooldown = 1f;
        [Tooltip("Minimum height above ground required to start wall running.")]
        public float minHeightAboveGround = 2f;

        [Header("References")]
        [SerializeField] private WallDetector wallDetector;

        // Internal State
        private bool _isWallRunning = false;
        private float _wallRunTimer;
        private int _wallJumpsUsed;
        private Vector3 _wallNormal;
        private Vector3 _wallForward;
        private Transform _currentWall;
        private float _lastWallExitTime;
        private float _timeEnteredState;
        private bool _justWallJumped;

        public override int Priority => 4;
        public override CharacterMovementState AssociatedPrimaryMovementState => CharacterMovementState.WallRunning;

        public override void Initialize(CardiniController controller)
        {
            base.Initialize(controller);
            if (wallDetector == null)
                wallDetector = GetComponentInChildren<WallDetector>();
        }

        public override bool CanEnterState()
        {
            if (wallDetector == null || !wallDetector.CurrentWall.canWallRun) return false;
            
            var wall = wallDetector.CurrentWall;

            // If already wall running, continue if timer and wall are valid
            if (_isWallRunning)
            {
                return _wallRunTimer > 0f && wall.hasWall;
            }

            // Entry conditions
            if (!wall.hasWall || !CommonChecks()) return false;
            if (Motor.BaseVelocity.magnitude < minEntrySpeed) return false;

            // Cooldown check - prevent immediate re-entry to same wall
            if (_currentWall == wall.wallTransform && Time.time - _lastWallExitTime < wallRunCooldown)
            {
                return false;
            }
            
            // Height check - must be high enough off ground
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, minHeightAboveGround + 2f))
            {
                if (Vector3.Distance(transform.position, groundHit.point) < minHeightAboveGround)
                {
                    return false;
                }
            }

            return !Motor.GroundingStatus.IsStableOnGround;
        }

        public override void OnEnterState()
        {
            var wall = wallDetector.CurrentWall;

            _isWallRunning = true;
            _timeEnteredState = Time.time;
            _wallRunTimer = maxWallRunTime;
            _wallNormal = wall.wallNormal;
            _wallForward = wall.wallForward;
            _wallJumpsUsed = 0;
            _currentWall = wall.wallTransform;
            
            // Grant fresh jumps during wall run
            Controller.SetJumpConsumed(false);
            Controller.SetDoubleJumpConsumed(false);
            Controller.ConsumeJumpRequest();

            _justWallJumped = false;
            Motor.ForceUnground();
            
            // Update animations
            float wallDirection = wall.isLeftWall ? 0f : 1f;
            PlayerAnimator?.SetGrounded(false);
            PlayerAnimator?.SetWallRunning(true, wallDirection);
        }

        public override void OnExitState()
        {
            _lastWallExitTime = Time.time;
            _isWallRunning = false;

            // KEY FIX: If we exited WITHOUT wall jumping, ensure both jumps are available
            if (!_justWallJumped)
            {
                // Wall run refreshes your jumps
                Controller.SetJumpConsumed(false);
                Controller.SetDoubleJumpConsumed(false);
                
                // Give coyote time to make first jump use regular jump path
                Controller.TimeSinceLastAbleToJump = 0.001f;
                
                // Ensure we're in Falling state (not DoubleJumping)
                if (Controller.CurrentMovementState == CharacterMovementState.WallRunning)
                {
                    Controller.SetMovementState(CharacterMovementState.Falling);
                }
            }
            // If we DID wall jump, jump consumption is already handled by ExecuteJump

            // Update animations
            var wall = wallDetector.CurrentWall;
            float wallDirection = wall.isLeftWall ? 0f : 1f;
            PlayerAnimator?.SetWallRunning(false, wallDirection);

            _justWallJumped = false;
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            Controller.HandleStandardRotation(ref currentRotation, deltaTime);
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            _wallRunTimer -= deltaTime;

            HandleWallJumpInput();

            if (_isWallRunning)
            {
                ApplyWallRunPhysics(ref currentVelocity, deltaTime);
            }
        }

        private void ApplyWallRunPhysics(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 forwardVelocity = _wallForward * wallRunSpeed;
            Vector3 gravityVelocity = Vector3.down * wallRunGravity * deltaTime;
            Vector3 stickToWallVelocity = -_wallNormal * wallStickDistance;
            
            currentVelocity = forwardVelocity + gravityVelocity + stickToWallVelocity;
        }
        
        private void HandleWallJumpInput()
        {
            if (!Controller.IsJumpRequested() || Controller.IsJumpConsumed())
                return;
                
            if (Time.time < _timeEnteredState + wallJumpInputGracePeriod)
                return;

            if (_wallJumpsUsed >= maxJumpsPerWall)
            {
                Controller.ConsumeJumpRequest();
                return;
            }
            
            // Execute the wall jump
            Controller.ExecuteJump(wallJumpUpForce, 0f, Vector3.zero);
            
            // Add directional forces
            Vector3 sideForce = _wallNormal * wallJumpSideForce;
            Vector3 directionalForce = Vector3.zero;
            if (Controller.MoveInputVector.sqrMagnitude > 0.01f)
            {
                Vector3 directionAlongWall = Vector3.ProjectOnPlane(Controller.MoveInputVector, _wallNormal).normalized;
                directionalForce = directionAlongWall * wallJumpForwardForce;
            }

            Controller.AddVelocity(sideForce + directionalForce);
            
            // Update state
            _wallJumpsUsed++;
            _justWallJumped = true;
            Controller.SetWallJumpedThisFrame(true);
            Controller.ConsumeJumpRequest();
            
            _isWallRunning = false; // Exit wall run on jump
        }

        public override void BeforeCharacterUpdate(float deltaTime)
        {
            // Landing resets wall state and cooldown
            if (Motor.GroundingStatus.IsStableOnGround && _currentWall != null)
            {
                _currentWall = null;
                _wallJumpsUsed = 0;
            }
        }
        
        public override void AfterCharacterUpdate(float deltaTime) { }
        public override void PostGroundingUpdate(float deltaTime) { }

        #region Public API
        public float GetWallRunTimeRemaining() => Mathf.Max(0f, _wallRunTimer);
        public int GetWallJumpsRemaining() => Mathf.Max(0, maxJumpsPerWall - _wallJumpsUsed);
        public bool IsWallRunning => _isWallRunning;
        #endregion
    }
}