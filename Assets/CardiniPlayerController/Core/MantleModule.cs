// using UnityEngine;
// using KinematicCharacterController;

// namespace Cardini.Motion
// {
//     /// <summary>
//     /// Mantle execution module following "FIRST UP, THEN FORWARD" approach.
//     /// Uses precise two-phase movement with no thresholds - just height-based transitions.
//     /// </summary>
//     public class MantleModule : MovementModuleBase
//     {
//         [Header("Mantle Settings")]
//         [Tooltip("Speed of upward movement during first phase")]
//         public float mantleUpSpeed = 8f;
        
//         [Tooltip("Speed of forward movement during second phase")]
//         public float mantleForwardSpeed = 6f;
        
//         [Tooltip("If true, player must press jump to mantle")]
//         public bool requireButtonPress = true;
        
//         [Tooltip("Cooldown between mantles")]
//         public float mantleCooldown = 0.5f;
        
//         [Tooltip("How close to target height before switching to forward phase")]
//         public float heightThreshold = 0.2f;
        
//         [Tooltip("How close to final position to complete mantle")]
//         public float completionThreshold = 0.3f;
        
//         [Tooltip("Maximum mantle duration before auto-exit")]
//         public float maxMantleDuration = 2f;
        
//         [Header("References")]
//         [SerializeField] private MantleDetector mantleDetector;
        
//         // Internal state
//         private bool _isMantling = false;
//         private float _mantleTimer = 0f;
//         private float _lastMantleTime = -999f;
//         private float _lastJumpRequestTime = -999f; // Track when jump was last pressed
        
//         // Mantle phases: 0 = Up, 1 = Forward, 2 = Complete
//         private int _mantlePhase = 0;
        
//         // Path and target data
//         private Vector3 _startPosition;
//         private Vector3 _ledgeClearancePoint;    // The "corner" where the capsule is clear
//         private Vector3 _targetMantlePosition;   // The final, safe landing spot ON the surface
//         private Vector3 _mantleForwardDirection; // Direction to move forward
//         private float _forwardDistanceNeeded;    // How far forward we need to go (V11 logic)
//         private Rigidbody _attachedRigidbody;
        
//         public override int Priority => 5; // High priority - takes precedence over grounded/airborne
//         public override CharacterMovementState AssociatedPrimaryMovementState => CharacterMovementState.Mantling;
        
//         public override void Initialize(CardiniController controller)
//         {
//             base.Initialize(controller);
            
//             if (mantleDetector == null)
//             {
//                 mantleDetector = GetComponent<MantleDetector>() ?? GetComponentInChildren<MantleDetector>();
//             }
            
//             if (mantleDetector == null)
//             {
//                 Debug.LogError($"MantleModule on {gameObject.name}: MantleDetector not found!", this);
//             }
//         }
        
//         public override bool CanEnterState()
//         {
//             // If already mantling, continue until complete
//             if (_isMantling) return !ShouldExitMantle();
            
//             // Track jump input timing for better button mode detection
//             if (Controller.IsJumpRequested())
//             {
//                 _lastJumpRequestTime = Time.time;
//             }
            
//             // Basic state checks
//             if (Controller.CurrentMajorState != CharacterState.Locomotion) return false;
//             if (mantleDetector == null || !mantleDetector.CurrentMantleData.isValid) return false;
//             if (Time.time - _lastMantleTime < mantleCooldown) return false;
            
//             // Don't mantle if we're already at or above the target height
//             var mantleData = mantleDetector.CurrentMantleData;
//             if (Motor.TransientPosition.y >= mantleData.surfacePoint.y - 0.1f) return false;
            
//             // Button requirement check - scan for ANY recent jump press (within 0.2 seconds)
//             if (requireButtonPress)
//             {
//                 bool recentJumpPress = (Time.time - _lastJumpRequestTime) <= 0.2f;
//                 return recentJumpPress;
//             }
            
//             // Auto mantle mode - no button required
//             return true;
//         }
        
//         public override void OnEnterState()
//         {
//             _isMantling = true;
//             _mantlePhase = 0; // Start with clearance phase
//             _mantleTimer = 0f;
//             _lastMantleTime = Time.time;
            
//             // Store mantle data
//             var mantleData = mantleDetector.CurrentMantleData;
//             _startPosition = Motor.TransientPosition;
//             _attachedRigidbody = mantleData.physicsBody;
            
//             // Calculate forward direction (player's current facing direction)
//             _mantleForwardDirection = transform.forward;
            
//             // Calculate precise positions (keeping current precise positioning)
//             float capsuleRadius = Motor.Capsule.radius;
            
//             // 1. FINAL target position - ON the surface, slightly forward from edge
//             _targetMantlePosition = mantleData.surfacePoint + _mantleForwardDirection * (capsuleRadius + 0.05f);
//             _targetMantlePosition.y = mantleData.surfacePoint.y; // ON the surface level
            
//             // 2. CLEARANCE point - where we reach before moving forward (SLIGHTLY HIGHER to avoid rubbing)
//             _ledgeClearancePoint = mantleData.surfacePoint - _mantleForwardDirection * capsuleRadius;
//             _ledgeClearancePoint.y = mantleData.surfacePoint.y + (capsuleRadius * 0.3f); // Slightly above to prevent ledge rubbing
            
//             // 3. Calculate forward distance needed (V11 logic for smooth movement)
//             Vector3 horizontalToTarget = _targetMantlePosition - new Vector3(_startPosition.x, _targetMantlePosition.y, _startPosition.z);
//             _forwardDistanceNeeded = Vector3.Dot(horizontalToTarget, _mantleForwardDirection);
            
//             // Consume jump input ONLY if button was required and pressed
//             if (requireButtonPress && Controller.IsJumpRequested())
//             {
//                 Controller.ConsumeJumpRequest();
//             }
            
//             // Force unground for smooth movement
//             Motor.ForceUnground();
            
//             Debug.Log($"🧗 MANTLE STARTED - {(requireButtonPress ? "Button triggered" : "Auto triggered")}");
//         }
        
//         public override void OnExitState()
//         {
//             _isMantling = false;
//             _mantlePhase = 0;
//             _attachedRigidbody = null;
            
//             Debug.Log("Mantle completed");
//         }
        
//         public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
//         {
//             // Smoothly rotate toward the mantle direction
//             if (_mantleForwardDirection.magnitude > 0.1f)
//             {
//                 Quaternion targetRotation = Quaternion.LookRotation(_mantleForwardDirection, Vector3.up);
//                 currentRotation = Quaternion.Slerp(currentRotation, targetRotation, deltaTime * 10f);
//             }
//         }
        
//         public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
//         {
//             if (!_isMantling) return;
            
//             _mantleTimer += deltaTime;
            
//             // Adjust targets for moving platforms
//             UpdateMovingPlatformTargets(deltaTime);
            
//             // Execute mantle movement based on current phase (V11 approach)
//             switch (_mantlePhase)
//             {
//                 case 0: // UP PHASE
//                     ExecuteUpPhase(ref currentVelocity, deltaTime);
//                     break;
                    
//                 case 1: // FORWARD PHASE
//                     ExecuteForwardPhase(ref currentVelocity, deltaTime);
//                     break;
                    
//                 case 2: // COMPLETE
//                     _isMantling = false;
//                     break;
//             }
            
//             // Safety timeout
//             if (_mantleTimer >= maxMantleDuration)
//             {
//                 _isMantling = false;
//             }
//         }
        
//         private void ExecuteClearancePhase(ref Vector3 currentVelocity)
//         {
//             // PHASE 0: CLEARANCE - Move up and toward clearance point simultaneously
            
//             // Move purely vertically at constant speed
//             Vector3 verticalVelocity = Vector3.up * mantleUpSpeed;
            
//             // Move horizontally toward clearance point
//             Vector3 horizontalTarget = new Vector3(_ledgeClearancePoint.x, Motor.TransientPosition.y, _ledgeClearancePoint.z);
//             Vector3 horizontalDirection = (horizontalTarget - Motor.TransientPosition).normalized;
//             Vector3 horizontalVelocity = horizontalDirection * mantleForwardSpeed;
            
//             // Combine vertical and horizontal movement
//             currentVelocity = verticalVelocity + horizontalVelocity;
            
//             // Include platform velocity if attached
//             if (_attachedRigidbody != null)
//             {
//                 Vector3 platformVelocity = _attachedRigidbody.linearVelocity;
//                 currentVelocity += platformVelocity;
//             }
            
//             // Transition condition: reached clearance height
//             if (Motor.TransientPosition.y >= _ledgeClearancePoint.y)
//             {
//                 _mantlePhase = 1;
//                 // Snap to clearance point for precise forward phase start
//                 Motor.SetPosition(_ledgeClearancePoint);
//                 Debug.Log("Phase 1: Moving forward to surface");
//             }
//         }
        
//         private void ExecuteCommitPhase(ref Vector3 currentVelocity)
//         {
//             // PHASE 1: COMMIT - Move from clearance point to final landing spot
            
//             Vector3 directionToTarget = (_targetMantlePosition - Motor.TransientPosition).normalized;
//             currentVelocity = directionToTarget * mantleForwardSpeed;
            
//             // Small downward force to ensure proper grounding detection
//             currentVelocity.y += -2f;
            
//             // Include platform velocity if attached
//             if (_attachedRigidbody != null)
//             {
//                 currentVelocity += _attachedRigidbody.linearVelocity;
//             }
            
//             // Check for completion based on horizontal distance only
//             Vector3 currentHorizontal = new Vector3(Motor.TransientPosition.x, 0, Motor.TransientPosition.z);
//             Vector3 targetHorizontal = new Vector3(_targetMantlePosition.x, 0, _targetMantlePosition.z);
            
//             if (Vector3.Distance(currentHorizontal, targetHorizontal) < 0.1f)
//             {
//                 // Complete mantle - place character exactly on surface
//                 Motor.SetPosition(_targetMantlePosition);
//                 currentVelocity = Vector3.zero;
//                 _isMantling = false;
//                 Debug.Log("Mantle complete: On surface");
//             }
//         }
        
//         private void UpdateMovingPlatformTargets(float deltaTime)
//         {
//             if (_attachedRigidbody != null)
//             {
//                 Vector3 platformMovement = _attachedRigidbody.linearVelocity * deltaTime;
//                 _ledgeClearancePoint += platformMovement;
//                 _targetMantlePosition += platformMovement;
//             }
//         }

//         private void ExecuteUpPhase(ref Vector3 currentVelocity, float deltaTime)
//         {
//             Vector3 currentPos = Motor.TransientPosition;
//             float currentHeight = currentPos.y;
//             float targetHeight = _ledgeClearancePoint.y;
            
//             // Check if we've reached the target height (V11 smooth threshold logic)
//             if (currentHeight >= targetHeight - heightThreshold)
//             {
//                 _mantlePhase = 1; // Switch to forward phase
//                 Debug.Log("Phase 1: Moving forward to surface");
//                 return;
//             }
            
//             // Move upward (V11 style - pure vertical)
//             Vector3 upwardVelocity = Vector3.up * mantleUpSpeed;
            
//             // Maintain some horizontal position relative to moving platform
//             if (_attachedRigidbody != null)
//             {
//                 Vector3 platformVelocity = _attachedRigidbody.linearVelocity;
//                 platformVelocity.y = 0; // Only horizontal component
//                 upwardVelocity += platformVelocity;
//             }
            
//             currentVelocity = upwardVelocity;
//         }
        
//         private void ExecuteForwardPhase(ref Vector3 currentVelocity, float deltaTime)
//         {
//             Vector3 currentPos = Motor.TransientPosition;
            
//             // Calculate how far we've moved forward from the start of forward phase (V11 logic)
//             Vector3 upPhaseEndPosition = new Vector3(_startPosition.x, _ledgeClearancePoint.y, _startPosition.z);
//             Vector3 forwardMovement = currentPos - upPhaseEndPosition;
//             float currentForwardDistance = Vector3.Dot(forwardMovement, _mantleForwardDirection);
            
//             // Check if we've moved far enough forward (V11 smooth completion)
//             if (currentForwardDistance >= _forwardDistanceNeeded - completionThreshold)
//             {
//                 _mantlePhase = 2; // Complete
//                 return;
//             }
            
//             // Move straight forward in the stored direction (V11 approach)
//             Vector3 forwardVelocity = _mantleForwardDirection * mantleForwardSpeed;
            
//             // Include platform velocity if attached
//             if (_attachedRigidbody != null)
//             {
//                 forwardVelocity += _attachedRigidbody.linearVelocity;
//             }
            
//             currentVelocity = forwardVelocity;
//         }
        
//         private bool ShouldExitMantle()
//         {
//             // Exit if timed out
//             if (_mantleTimer >= maxMantleDuration) return true;
            
//             // Exit if completed (V11 style)
//             if (_mantlePhase >= 2) 
//             {
//                 // Complete mantle - place character exactly on surface (correct positioning)
//                 Motor.SetPosition(_targetMantlePosition);
//                 Debug.Log("Mantle complete: On surface");
//                 return true;
//             }
            
//             return false;
//         }
        
//         public override void BeforeCharacterUpdate(float deltaTime) { }
//         public override void AfterCharacterUpdate(float deltaTime) { }
//         public override void PostGroundingUpdate(float deltaTime) { }
        
//         #region Public API
//         public bool IsMantling => _isMantling;
//         public int CurrentMantlePhase => _mantlePhase;
//         public float MantleProgress => _mantleTimer / maxMantleDuration;
//         public Vector3 MantleForwardDirection => _mantleForwardDirection;
//         public float ForwardDistanceNeeded => _forwardDistanceNeeded;
//         public bool RequiresButton => requireButtonPress;
//         #endregion
        
//         #region Gizmos
//         private void OnDrawGizmosSelected()
//         {
//             if (_isMantling)
//             {
//                 // Start position
//                 Gizmos.color = Color.blue;
//                 Gizmos.DrawWireSphere(_startPosition, 0.1f);
                
//                 // Clearance point
//                 Gizmos.color = Color.yellow;
//                 Gizmos.DrawWireSphere(_ledgeClearancePoint, 0.15f);
                
//                 // Final target position
//                 Gizmos.color = Color.green;
//                 Gizmos.DrawWireSphere(_targetMantlePosition, 0.2f);
                
//                 // Current position
//                 Gizmos.color = Color.white;
//                 Gizmos.DrawWireSphere(Motor.TransientPosition, 0.25f);
                
//                 // Phase visualization (V11 style with proper phases)
//                 if (_mantlePhase == 0) // UP phase
//                 {
//                     Gizmos.color = Color.red;
//                     Gizmos.DrawLine(Motor.TransientPosition, new Vector3(Motor.TransientPosition.x, _ledgeClearancePoint.y, Motor.TransientPosition.z));
//                 }
//                 else if (_mantlePhase == 1) // FORWARD phase
//                 {
//                     Gizmos.color = Color.magenta;
//                     Vector3 forwardTarget = Motor.TransientPosition + _mantleForwardDirection * (_forwardDistanceNeeded * 0.5f);
//                     Gizmos.DrawLine(Motor.TransientPosition, forwardTarget);
                    
//                     // Show forward direction arrow
//                     Gizmos.color = Color.cyan;
//                     Gizmos.DrawRay(Motor.TransientPosition, _mantleForwardDirection * 0.5f);
//                 }
//                 else if (_mantlePhase == 2) // COMPLETE phase
//                 {
//                     Gizmos.color = Color.green;
//                     Gizmos.DrawLine(Motor.TransientPosition, _targetMantlePosition);
//                 }
                
//                 // Movement path visualization (V11 straight lines)
//                 Gizmos.color = Color.cyan;
//                 Vector3 upTarget = new Vector3(_startPosition.x, _ledgeClearancePoint.y, _startPosition.z);
//                 Vector3 forwardEndTarget = upTarget + _mantleForwardDirection * _forwardDistanceNeeded;
//                 Gizmos.DrawLine(_startPosition, upTarget);
//                 Gizmos.DrawLine(upTarget, forwardEndTarget);
//             }
//         }
//         #endregion
//     }
// }
// using UnityEngine;
// using KinematicCharacterController;

// namespace Cardini.Motion
// {
//     /// <summary>
//     /// Mantle execution module following "FIRST UP, THEN FORWARD" approach.
//     /// Uses precise two-phase movement with no thresholds - just height-based transitions.
//     /// </summary>
//     public class MantleModule : MovementModuleBase
//     {
//         [Header("Mantle Settings")]
//         [Tooltip("Speed of upward movement during first phase")]
//         public float mantleUpSpeed = 8f;
        
//         [Tooltip("Speed of forward movement during second phase")]
//         public float mantleForwardSpeed = 6f;
        
//         [Tooltip("If true, player must press jump to mantle")]
//         public bool requireButtonPress = true;
        
//         [Tooltip("Cooldown between mantles")]
//         public float mantleCooldown = 0.5f;
        
//         [Tooltip("How close to target height before switching to forward phase")]
//         public float heightThreshold = 0.2f;
        
//         [Tooltip("How close to final position to complete mantle")]
//         public float completionThreshold = 0.3f;
        
//         [Tooltip("Maximum mantle duration before auto-exit")]
//         public float maxMantleDuration = 2f;
        
//         [Header("References")]
//         [SerializeField] private MantleDetector mantleDetector;
        
//         // Internal state
//         private bool _isMantling = false;
//         private float _mantleTimer = 0f;
//         private float _lastMantleTime = -999f;
//         private float _lastJumpRequestTime = -999f; // Track when jump was last pressed
        
//         // Mantle phases: 0 = Up, 1 = Forward, 2 = Complete
//         private int _mantlePhase = 0;
        
//         // Path and target data
//         private Vector3 _startPosition;
//         private Vector3 _ledgeClearancePoint;    // The "corner" where the capsule is clear
//         private Vector3 _targetMantlePosition;   // The final, safe landing spot ON the surface
//         private Vector3 _mantleForwardDirection; // Direction to move forward
//         private float _forwardDistanceNeeded;    // How far forward we need to go (V11 logic)
//         private Rigidbody _attachedRigidbody;
        
//         public override int Priority => 5; // High priority - takes precedence over grounded/airborne
//         public override CharacterMovementState AssociatedPrimaryMovementState => CharacterMovementState.Mantling;
        
//         public override void Initialize(CardiniController controller)
//         {
//             base.Initialize(controller);
            
//             if (mantleDetector == null)
//             {
//                 mantleDetector = GetComponent<MantleDetector>() ?? GetComponentInChildren<MantleDetector>();
//             }
            
//             if (mantleDetector == null)
//             {
//                 Debug.LogError($"MantleModule on {gameObject.name}: MantleDetector not found!", this);
//             }
//         }
        
//         public override bool CanEnterState()
//         {
//             // If already mantling, continue until complete
//             if (_isMantling) return !ShouldExitMantle();
            
//             // Track jump input timing for better button mode detection
//             if (Controller.IsJumpRequested())
//             {
//                 _lastJumpRequestTime = Time.time;
//             }
            
//             // Basic state checks
//             if (Controller.CurrentMajorState != CharacterState.Locomotion) return false;
//             if (mantleDetector == null || !mantleDetector.CurrentMantleData.isValid) return false;
//             if (Time.time - _lastMantleTime < mantleCooldown) return false;
            
//             // Don't mantle if we're already at or above the target height
//             var mantleData = mantleDetector.CurrentMantleData;
//             if (Motor.TransientPosition.y >= mantleData.surfacePoint.y - 0.1f) return false;
            
//             // Button requirement check - scan for ANY recent jump press (within 0.2 seconds)
//             if (requireButtonPress)
//             {
//                 bool recentJumpPress = (Time.time - _lastJumpRequestTime) <= 0.2f;
//                 return recentJumpPress;
//             }
            
//             // Auto mantle mode - no button required
//             return true;
//         }
        
//         public override void OnEnterState()
//         {
//             _isMantling = true;
//             _mantlePhase = 0; // Start with clearance phase
//             _mantleTimer = 0f;
//             _lastMantleTime = Time.time;
            
//             // Store mantle data
//             var mantleData = mantleDetector.CurrentMantleData;
//             _startPosition = Motor.TransientPosition;
//             _attachedRigidbody = mantleData.physicsBody;
            
//             // Calculate forward direction (player's current facing direction)
//             _mantleForwardDirection = transform.forward;
            
//             // Calculate precise positions (keeping current precise positioning)
//             float capsuleRadius = Motor.Capsule.radius;
            
//             // 1. FINAL target position - ON the surface, slightly forward from edge
//             _targetMantlePosition = mantleData.surfacePoint + _mantleForwardDirection * (capsuleRadius + 0.05f);
//             _targetMantlePosition.y = mantleData.surfacePoint.y; // ON the surface level
            
//             // 2. CLEARANCE point - where we reach before moving forward (SLIGHTLY HIGHER to avoid rubbing)
//             _ledgeClearancePoint = mantleData.surfacePoint - _mantleForwardDirection * capsuleRadius;
//             _ledgeClearancePoint.y = mantleData.surfacePoint.y + (capsuleRadius * 0.3f); // Slightly above to prevent ledge rubbing
            
//             // 3. Calculate forward distance needed (V11 logic for smooth movement)
//             Vector3 horizontalToTarget = _targetMantlePosition - new Vector3(_startPosition.x, _targetMantlePosition.y, _startPosition.z);
//             _forwardDistanceNeeded = Vector3.Dot(horizontalToTarget, _mantleForwardDirection);
            
//             // Consume jump input ONLY if button was required and pressed
//             if (requireButtonPress && Controller.IsJumpRequested())
//             {
//                 Controller.ConsumeJumpRequest();
//             }
            
//             // Force unground for smooth movement
//             Motor.ForceUnground();
            
//             Debug.Log($"🧗 MANTLE STARTED - {(requireButtonPress ? "Button triggered" : "Auto triggered")}");
//         }
        
//         public override void OnExitState()
//         {
//             _isMantling = false;
//             _mantlePhase = 0;
//             _attachedRigidbody = null;
            
//             Debug.Log("Mantle completed");
//         }
        
//         public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
//         {
//             // Smoothly rotate toward the mantle direction
//             if (_mantleForwardDirection.magnitude > 0.1f)
//             {
//                 Quaternion targetRotation = Quaternion.LookRotation(_mantleForwardDirection, Vector3.up);
//                 currentRotation = Quaternion.Slerp(currentRotation, targetRotation, deltaTime * 10f);
//             }
//         }
        
//         public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
//         {
//             if (!_isMantling) return;
            
//             _mantleTimer += deltaTime;
            
//             // Adjust targets for moving platforms
//             UpdateMovingPlatformTargets(deltaTime);
            
//             // Execute mantle movement based on current phase (V11 approach)
//             switch (_mantlePhase)
//             {
//                 case 0: // UP PHASE
//                     ExecuteUpPhase(ref currentVelocity, deltaTime);
//                     break;
                    
//                 case 1: // FORWARD PHASE
//                     ExecuteForwardPhase(ref currentVelocity, deltaTime);
//                     break;
                    
//                 case 2: // COMPLETE
//                     _isMantling = false;
//                     break;
//             }
            
//             // Safety timeout
//             if (_mantleTimer >= maxMantleDuration)
//             {
//                 _isMantling = false;
//             }
//         }
        
//         private void ExecuteClearancePhase(ref Vector3 currentVelocity)
//         {
//             // PHASE 0: CLEARANCE - Move up and toward clearance point simultaneously
            
//             // Move purely vertically at constant speed
//             Vector3 verticalVelocity = Vector3.up * mantleUpSpeed;
            
//             // Move horizontally toward clearance point
//             Vector3 horizontalTarget = new Vector3(_ledgeClearancePoint.x, Motor.TransientPosition.y, _ledgeClearancePoint.z);
//             Vector3 horizontalDirection = (horizontalTarget - Motor.TransientPosition).normalized;
//             Vector3 horizontalVelocity = horizontalDirection * mantleForwardSpeed;
            
//             // Combine vertical and horizontal movement
//             currentVelocity = verticalVelocity + horizontalVelocity;
            
//             // Include platform velocity if attached
//             if (_attachedRigidbody != null)
//             {
//                 Vector3 platformVelocity = _attachedRigidbody.linearVelocity;
//                 currentVelocity += platformVelocity;
//             }
            
//             // Transition condition: reached clearance height
//             if (Motor.TransientPosition.y >= _ledgeClearancePoint.y)
//             {
//                 _mantlePhase = 1;
//                 // Snap to clearance point for precise forward phase start
//                 Motor.SetPosition(_ledgeClearancePoint);
//                 Debug.Log("Phase 1: Moving forward to surface");
//             }
//         }
        
//         private void ExecuteCommitPhase(ref Vector3 currentVelocity)
//         {
//             // PHASE 1: COMMIT - Move from clearance point to final landing spot
            
//             Vector3 directionToTarget = (_targetMantlePosition - Motor.TransientPosition).normalized;
//             currentVelocity = directionToTarget * mantleForwardSpeed;
            
//             // Small downward force to ensure proper grounding detection
//             currentVelocity.y += -2f;
            
//             // Include platform velocity if attached
//             if (_attachedRigidbody != null)
//             {
//                 currentVelocity += _attachedRigidbody.linearVelocity;
//             }
            
//             // Check for completion based on horizontal distance only
//             Vector3 currentHorizontal = new Vector3(Motor.TransientPosition.x, 0, Motor.TransientPosition.z);
//             Vector3 targetHorizontal = new Vector3(_targetMantlePosition.x, 0, _targetMantlePosition.z);
            
//             if (Vector3.Distance(currentHorizontal, targetHorizontal) < 0.1f)
//             {
//                 // Complete mantle - place character exactly on surface
//                 Motor.SetPosition(_targetMantlePosition);
//                 currentVelocity = Vector3.zero;
//                 _isMantling = false;
//                 Debug.Log("Mantle complete: On surface");
//             }
//         }
        
//         private void UpdateMovingPlatformTargets(float deltaTime)
//         {
//             if (_attachedRigidbody != null)
//             {
//                 Vector3 platformMovement = _attachedRigidbody.linearVelocity * deltaTime;
//                 _ledgeClearancePoint += platformMovement;
//                 _targetMantlePosition += platformMovement;
//             }
//         }

//         private void ExecuteUpPhase(ref Vector3 currentVelocity, float deltaTime)
//         {
//             Vector3 currentPos = Motor.TransientPosition;
//             float currentHeight = currentPos.y;
//             float targetHeight = _ledgeClearancePoint.y;
            
//             // Check if we've reached the target height (V11 smooth threshold logic)
//             if (currentHeight >= targetHeight - heightThreshold)
//             {
//                 _mantlePhase = 1; // Switch to forward phase
//                 Debug.Log("Phase 1: Moving forward to surface");
//                 return;
//             }
            
//             // Move upward (V11 style - pure vertical)
//             Vector3 upwardVelocity = Vector3.up * mantleUpSpeed;
            
//             // Maintain some horizontal position relative to moving platform
//             if (_attachedRigidbody != null)
//             {
//                 Vector3 platformVelocity = _attachedRigidbody.linearVelocity;
//                 platformVelocity.y = 0; // Only horizontal component
//                 upwardVelocity += platformVelocity;
//             }
            
//             currentVelocity = upwardVelocity;
//         }
        
//         private void ExecuteForwardPhase(ref Vector3 currentVelocity, float deltaTime)
//         {
//             Vector3 currentPos = Motor.TransientPosition;
            
//             // Calculate how far we've moved forward from the start of forward phase (V11 logic)
//             Vector3 upPhaseEndPosition = new Vector3(_startPosition.x, _ledgeClearancePoint.y, _startPosition.z);
//             Vector3 forwardMovement = currentPos - upPhaseEndPosition;
//             float currentForwardDistance = Vector3.Dot(forwardMovement, _mantleForwardDirection);
            
//             // Check if we've moved far enough forward (V11 smooth completion)
//             if (currentForwardDistance >= _forwardDistanceNeeded - completionThreshold)
//             {
//                 _mantlePhase = 2; // Complete
//                 return;
//             }
            
//             // Move straight forward in the stored direction (V11 approach)
//             Vector3 forwardVelocity = _mantleForwardDirection * mantleForwardSpeed;
            
//             // Add gentle downward movement to settle onto surface during forward movement
//             float heightDifference = currentPos.y - _targetMantlePosition.y;
//             if (heightDifference > 0.1f) // Only if we're above the target
//             {
//                 float downwardSpeed = heightDifference * 3f; // Proportional descent
//                 forwardVelocity.y = -downwardSpeed;
//             }
            
//             // Include platform velocity if attached
//             if (_attachedRigidbody != null)
//             {
//                 forwardVelocity += _attachedRigidbody.linearVelocity;
//             }
            
//             currentVelocity = forwardVelocity;
//         }
        
//         private bool ShouldExitMantle()
//         {
//             // Exit if timed out
//             if (_mantleTimer >= maxMantleDuration) return true;
            
//             // Exit if completed (V11 style)
//             if (_mantlePhase >= 2) 
//             {
//                 // Complete mantle - place character exactly on surface (correct positioning)
//                 Motor.SetPosition(_targetMantlePosition);
//                 Debug.Log("Mantle complete: On surface");
//                 return true;
//             }
            
//             return false;
//         }
        
//         public override void BeforeCharacterUpdate(float deltaTime) { }
//         public override void AfterCharacterUpdate(float deltaTime) { }
//         public override void PostGroundingUpdate(float deltaTime) { }
        
//         #region Public API
//         public bool IsMantling => _isMantling;
//         public int CurrentMantlePhase => _mantlePhase;
//         public float MantleProgress => _mantleTimer / maxMantleDuration;
//         public Vector3 MantleForwardDirection => _mantleForwardDirection;
//         public float ForwardDistanceNeeded => _forwardDistanceNeeded;
//         public bool RequiresButton => requireButtonPress;
//         #endregion
        
//         #region Gizmos
//         private void OnDrawGizmosSelected()
//         {
//             if (_isMantling)
//             {
//                 // Start position
//                 Gizmos.color = Color.blue;
//                 Gizmos.DrawWireSphere(_startPosition, 0.1f);
                
//                 // Clearance point
//                 Gizmos.color = Color.yellow;
//                 Gizmos.DrawWireSphere(_ledgeClearancePoint, 0.15f);
                
//                 // Final target position
//                 Gizmos.color = Color.green;
//                 Gizmos.DrawWireSphere(_targetMantlePosition, 0.2f);
                
//                 // Current position
//                 Gizmos.color = Color.white;
//                 Gizmos.DrawWireSphere(Motor.TransientPosition, 0.25f);
                
//                 // Phase visualization (V11 style with proper phases)
//                 if (_mantlePhase == 0) // UP phase
//                 {
//                     Gizmos.color = Color.red;
//                     Gizmos.DrawLine(Motor.TransientPosition, new Vector3(Motor.TransientPosition.x, _ledgeClearancePoint.y, Motor.TransientPosition.z));
//                 }
//                 else if (_mantlePhase == 1) // FORWARD phase
//                 {
//                     Gizmos.color = Color.magenta;
//                     Vector3 forwardTarget = Motor.TransientPosition + _mantleForwardDirection * (_forwardDistanceNeeded * 0.5f);
//                     Gizmos.DrawLine(Motor.TransientPosition, forwardTarget);
                    
//                     // Show forward direction arrow
//                     Gizmos.color = Color.cyan;
//                     Gizmos.DrawRay(Motor.TransientPosition, _mantleForwardDirection * 0.5f);
//                 }
//                 else if (_mantlePhase == 2) // COMPLETE phase
//                 {
//                     Gizmos.color = Color.green;
//                     Gizmos.DrawLine(Motor.TransientPosition, _targetMantlePosition);
//                 }
                
//                 // Movement path visualization (V11 straight lines)
//                 Gizmos.color = Color.cyan;
//                 Vector3 upTarget = new Vector3(_startPosition.x, _ledgeClearancePoint.y, _startPosition.z);
//                 Vector3 forwardEndTarget = upTarget + _mantleForwardDirection * _forwardDistanceNeeded;
//                 Gizmos.DrawLine(_startPosition, upTarget);
//                 Gizmos.DrawLine(upTarget, forwardEndTarget);
//             }
//         }
//         #endregion
//     }
// }
using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    /// <summary>
    /// Mantle execution module following "FIRST UP, THEN FORWARD" approach.
    /// Uses precise two-phase movement with no thresholds - just height-based transitions.
    /// </summary>
    public class MantleModule : MovementModuleBase
    {
        [Header("Mantle Settings")]
        [Tooltip("Speed of upward movement during first phase")]
        public float mantleUpSpeed = 8f;
        
        [Tooltip("Speed of forward movement during second phase")]
        public float mantleForwardSpeed = 6f;
        
        [Tooltip("If true, player must press jump to mantle")]
        public bool requireButtonPress = true;
        
        [Tooltip("Cooldown between mantles")]
        public float mantleCooldown = 0.5f;
        
        [Tooltip("How close to target height before switching to forward phase")]
        public float heightThreshold = 0.2f;
        
        [Tooltip("How close to final position to complete mantle")]
        public float completionThreshold = 0.3f;
        
        [Tooltip("Maximum mantle duration before auto-exit")]
        public float maxMantleDuration = 2f;
        
        [Header("References")]
        [SerializeField] private MantleDetector mantleDetector;
        
        // Internal state
        private bool _isMantling = false;
        private float _mantleTimer = 0f;
        private float _lastMantleTime = -999f;
        private float _lastJumpRequestTime = -999f; // Track when jump was last pressed
        
        // Mantle phases: 0 = Up, 1 = Forward, 2 = Complete
        private int _mantlePhase = 0;
        
        // Path and target data
        private Vector3 _startPosition;
        private Vector3 _ledgeClearancePoint;    // The "corner" where the capsule is clear
        private Vector3 _targetMantlePosition;   // The final, safe landing spot ON the surface
        private Vector3 _mantleForwardDirection; // Direction to move forward
        private float _forwardDistanceNeeded;    // How far forward we need to go (V11 logic)
        private Rigidbody _attachedRigidbody;
        
        public override int Priority => 5; // High priority - takes precedence over grounded/airborne
        public override CharacterMovementState AssociatedPrimaryMovementState => CharacterMovementState.Mantling;
        
        public override void Initialize(CardiniController controller)
        {
            base.Initialize(controller);
            
            if (mantleDetector == null)
            {
                mantleDetector = GetComponent<MantleDetector>() ?? GetComponentInChildren<MantleDetector>();
            }
            
            if (mantleDetector == null)
            {
                Debug.LogError($"MantleModule on {gameObject.name}: MantleDetector not found!", this);
            }
        }
        
        public override bool CanEnterState()
        {
            // If already mantling, continue until complete
            if (_isMantling) return !ShouldExitMantle();
            
            // Track jump input timing for better button mode detection
            if (Controller.IsJumpRequested())
            {
                _lastJumpRequestTime = Time.time;
            }
            
            // Basic state checks
            if (Controller.CurrentMajorState != CharacterState.Locomotion) return false;
            if (mantleDetector == null || !mantleDetector.CurrentMantleData.isValid) return false;
            if (Time.time - _lastMantleTime < mantleCooldown) return false;
            
            // Don't mantle if we're already at or above the target height
            var mantleData = mantleDetector.CurrentMantleData;
            if (Motor.TransientPosition.y >= mantleData.surfacePoint.y - 0.1f) return false;
            
            // IMPROVED AIRBORNE MANTLE DETECTION
            if (requireButtonPress)
            {
                // Button mode: Scan on ANY jump input during ANY airborne state
                // This works for: jumping, double jumping, falling, wall running->falling, etc.
                bool hasJumpInput = Controller.IsJumpRequested() || (Time.time - _lastJumpRequestTime) <= 0.5f; // Increased window
                bool isAirborneOrCanMantle = !Motor.GroundingStatus.IsStableOnGround || 
                                           (Motor.GroundingStatus.IsStableOnGround && Motor.BaseVelocity.magnitude > 0.1f);
                
                return hasJumpInput && isAirborneOrCanMantle;
            }
            else
            {
                // Auto mode: Always scan when airborne OR when moving on ground near mantleable surface
                // This enables mantling from ANY airborne state (falling, double jumping, etc.)
                bool canAutoMantle = !Motor.GroundingStatus.IsStableOnGround || 
                                   (Motor.GroundingStatus.IsStableOnGround && Motor.BaseVelocity.magnitude > 0.1f);
                
                return canAutoMantle;
            }
        }
        
        public override void OnEnterState()
        {
            _isMantling = true;
            _mantlePhase = 0; // Start with clearance phase
            _mantleTimer = 0f;
            _lastMantleTime = Time.time;
            
            // Store mantle data
            var mantleData = mantleDetector.CurrentMantleData;
            _startPosition = Motor.TransientPosition;
            _attachedRigidbody = mantleData.physicsBody;
            
            // Calculate forward direction (player's current facing direction)
            _mantleForwardDirection = transform.forward;
            
            // Calculate precise positions (keeping current precise positioning)
            float capsuleRadius = Motor.Capsule.radius;
            
            // 1. FINAL target position - ON the surface, slightly forward from edge
            _targetMantlePosition = mantleData.surfacePoint + _mantleForwardDirection * (capsuleRadius + 0.05f);
            _targetMantlePosition.y = mantleData.surfacePoint.y; // ON the surface level
            
            // 2. CLEARANCE point - where we reach before moving forward (SLIGHTLY HIGHER to avoid rubbing)
            _ledgeClearancePoint = mantleData.surfacePoint - _mantleForwardDirection * capsuleRadius;
            _ledgeClearancePoint.y = mantleData.surfacePoint.y + (capsuleRadius * 0.3f); // Slightly above to prevent ledge rubbing
            
            // 3. Calculate forward distance needed (V11 logic for smooth movement)
            Vector3 horizontalToTarget = _targetMantlePosition - new Vector3(_startPosition.x, _targetMantlePosition.y, _startPosition.z);
            _forwardDistanceNeeded = Vector3.Dot(horizontalToTarget, _mantleForwardDirection);
            
            // Consume jump input ONLY if button was required and there's an active jump request
            // This prevents consuming double jump inputs unnecessarily
            if (requireButtonPress && Controller.IsJumpRequested())
            {
                Controller.ConsumeJumpRequest();
                Debug.Log($"🧗 MANTLE STARTED - Button triggered during {(Motor.GroundingStatus.IsStableOnGround ? "Ground" : "Airborne")} state");
            }
            else if (!requireButtonPress)
            {
                Debug.Log($"🧗 MANTLE STARTED - Auto triggered during {(Motor.GroundingStatus.IsStableOnGround ? "Ground" : "Airborne")} state");
            }
            else
            {
                Debug.Log($"🧗 MANTLE STARTED - Button triggered via recent input during {(Motor.GroundingStatus.IsStableOnGround ? "Ground" : "Airborne")} state");
            }
            
            // Force unground for smooth movement
            Motor.ForceUnground();
        }
        
        public override void OnExitState()
        {
            _isMantling = false;
            _mantlePhase = 0;
            _attachedRigidbody = null;
            
            Debug.Log("Mantle completed");
        }
        
        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            // Smoothly rotate toward the mantle direction
            if (_mantleForwardDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(_mantleForwardDirection, Vector3.up);
                currentRotation = Quaternion.Slerp(currentRotation, targetRotation, deltaTime * 10f);
            }
        }
        
        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (!_isMantling) return;
            
            _mantleTimer += deltaTime;
            
            // Adjust targets for moving platforms
            UpdateMovingPlatformTargets(deltaTime);
            
            // Execute mantle movement based on current phase (V11 approach)
            switch (_mantlePhase)
            {
                case 0: // UP PHASE
                    ExecuteUpPhase(ref currentVelocity, deltaTime);
                    break;
                    
                case 1: // FORWARD PHASE
                    ExecuteForwardPhase(ref currentVelocity, deltaTime);
                    break;
                    
                case 2: // COMPLETE
                    _isMantling = false;
                    break;
            }
            
            // Safety timeout
            if (_mantleTimer >= maxMantleDuration)
            {
                _isMantling = false;
            }
        }
        
        private void ExecuteClearancePhase(ref Vector3 currentVelocity)
        {
            // PHASE 0: CLEARANCE - Move up and toward clearance point simultaneously
            
            // Move purely vertically at constant speed
            Vector3 verticalVelocity = Vector3.up * mantleUpSpeed;
            
            // Move horizontally toward clearance point
            Vector3 horizontalTarget = new Vector3(_ledgeClearancePoint.x, Motor.TransientPosition.y, _ledgeClearancePoint.z);
            Vector3 horizontalDirection = (horizontalTarget - Motor.TransientPosition).normalized;
            Vector3 horizontalVelocity = horizontalDirection * mantleForwardSpeed;
            
            // Combine vertical and horizontal movement
            currentVelocity = verticalVelocity + horizontalVelocity;
            
            // Include platform velocity if attached
            if (_attachedRigidbody != null)
            {
                Vector3 platformVelocity = _attachedRigidbody.linearVelocity;
                currentVelocity += platformVelocity;
            }
            
            // Transition condition: reached clearance height
            if (Motor.TransientPosition.y >= _ledgeClearancePoint.y)
            {
                _mantlePhase = 1;
                // Snap to clearance point for precise forward phase start
                Motor.SetPosition(_ledgeClearancePoint);
                Debug.Log("Phase 1: Moving forward to surface");
            }
        }
        
        private void ExecuteCommitPhase(ref Vector3 currentVelocity)
        {
            // PHASE 1: COMMIT - Move from clearance point to final landing spot
            
            Vector3 directionToTarget = (_targetMantlePosition - Motor.TransientPosition).normalized;
            currentVelocity = directionToTarget * mantleForwardSpeed;
            
            // Small downward force to ensure proper grounding detection
            currentVelocity.y += -2f;
            
            // Include platform velocity if attached
            if (_attachedRigidbody != null)
            {
                currentVelocity += _attachedRigidbody.linearVelocity;
            }
            
            // Check for completion based on horizontal distance only
            Vector3 currentHorizontal = new Vector3(Motor.TransientPosition.x, 0, Motor.TransientPosition.z);
            Vector3 targetHorizontal = new Vector3(_targetMantlePosition.x, 0, _targetMantlePosition.z);
            
            if (Vector3.Distance(currentHorizontal, targetHorizontal) < 0.1f)
            {
                // Complete mantle - place character exactly on surface
                Motor.SetPosition(_targetMantlePosition);
                currentVelocity = Vector3.zero;
                _isMantling = false;
                Debug.Log("Mantle complete: On surface");
            }
        }
        
        private void UpdateMovingPlatformTargets(float deltaTime)
        {
            if (_attachedRigidbody != null)
            {
                Vector3 platformMovement = _attachedRigidbody.linearVelocity * deltaTime;
                _ledgeClearancePoint += platformMovement;
                _targetMantlePosition += platformMovement;
            }
        }

        private void ExecuteUpPhase(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 currentPos = Motor.TransientPosition;
            float currentHeight = currentPos.y;
            float targetHeight = _ledgeClearancePoint.y;
            
            // Check if we've reached the target height (V11 smooth threshold logic)
            if (currentHeight >= targetHeight - heightThreshold)
            {
                _mantlePhase = 1; // Switch to forward phase
                Debug.Log("Phase 1: Moving forward to surface");
                return;
            }
            
            // Move upward (V11 style - pure vertical)
            Vector3 upwardVelocity = Vector3.up * mantleUpSpeed;
            
            // Maintain some horizontal position relative to moving platform
            if (_attachedRigidbody != null)
            {
                Vector3 platformVelocity = _attachedRigidbody.linearVelocity;
                platformVelocity.y = 0; // Only horizontal component
                upwardVelocity += platformVelocity;
            }
            
            currentVelocity = upwardVelocity;
        }
        
        private void ExecuteForwardPhase(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 currentPos = Motor.TransientPosition;
            
            // Calculate how far we've moved forward from the start of forward phase (V11 logic)
            Vector3 upPhaseEndPosition = new Vector3(_startPosition.x, _ledgeClearancePoint.y, _startPosition.z);
            Vector3 forwardMovement = currentPos - upPhaseEndPosition;
            float currentForwardDistance = Vector3.Dot(forwardMovement, _mantleForwardDirection);
            
            // Check if we've moved far enough forward (V11 smooth completion)
            if (currentForwardDistance >= _forwardDistanceNeeded - completionThreshold)
            {
                _mantlePhase = 2; // Complete
                return;
            }
            
            // Move straight forward in the stored direction (V11 approach)
            Vector3 forwardVelocity = _mantleForwardDirection * mantleForwardSpeed;
            
            // Add gentle downward movement to settle onto surface during forward movement
            float heightDifference = currentPos.y - _targetMantlePosition.y;
            if (heightDifference > 0.1f) // Only if we're above the target
            {
                float downwardSpeed = heightDifference * 3f; // Proportional descent
                forwardVelocity.y = -downwardSpeed;
            }
            
            // Include platform velocity if attached
            if (_attachedRigidbody != null)
            {
                forwardVelocity += _attachedRigidbody.linearVelocity;
            }
            
            currentVelocity = forwardVelocity;
        }
        
        private bool ShouldExitMantle()
        {
            // Exit if timed out
            if (_mantleTimer >= maxMantleDuration) return true;
            
            // Exit if completed (V11 style)
            if (_mantlePhase >= 2) 
            {
                // Complete mantle - place character exactly on surface (correct positioning)
                Motor.SetPosition(_targetMantlePosition);
                Debug.Log("Mantle complete: On surface");
                return true;
            }
            
            return false;
        }
        
        public override void BeforeCharacterUpdate(float deltaTime) { }
        public override void AfterCharacterUpdate(float deltaTime) { }
        public override void PostGroundingUpdate(float deltaTime) { }
        
        #region Public API
        public bool IsMantling => _isMantling;
        public int CurrentMantlePhase => _mantlePhase;
        public float MantleProgress => _mantleTimer / maxMantleDuration;
        public Vector3 MantleForwardDirection => _mantleForwardDirection;
        public float ForwardDistanceNeeded => _forwardDistanceNeeded;
        public bool RequiresButton => requireButtonPress;
        #endregion
        
        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            if (_isMantling)
            {
                // Start position
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(_startPosition, 0.1f);
                
                // Clearance point
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_ledgeClearancePoint, 0.15f);
                
                // Final target position
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_targetMantlePosition, 0.2f);
                
                // Current position
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(Motor.TransientPosition, 0.25f);
                
                // Phase visualization (V11 style with proper phases)
                if (_mantlePhase == 0) // UP phase
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(Motor.TransientPosition, new Vector3(Motor.TransientPosition.x, _ledgeClearancePoint.y, Motor.TransientPosition.z));
                }
                else if (_mantlePhase == 1) // FORWARD phase
                {
                    Gizmos.color = Color.magenta;
                    Vector3 forwardTarget = Motor.TransientPosition + _mantleForwardDirection * (_forwardDistanceNeeded * 0.5f);
                    Gizmos.DrawLine(Motor.TransientPosition, forwardTarget);
                    
                    // Show forward direction arrow
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawRay(Motor.TransientPosition, _mantleForwardDirection * 0.5f);
                }
                else if (_mantlePhase == 2) // COMPLETE phase
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(Motor.TransientPosition, _targetMantlePosition);
                }
                
                // Movement path visualization (V11 straight lines)
                Gizmos.color = Color.cyan;
                Vector3 upTarget = new Vector3(_startPosition.x, _ledgeClearancePoint.y, _startPosition.z);
                Vector3 forwardEndTarget = upTarget + _mantleForwardDirection * _forwardDistanceNeeded;
                Gizmos.DrawLine(_startPosition, upTarget);
                Gizmos.DrawLine(upTarget, forwardEndTarget);
            }
        }
        #endregion
    }
}