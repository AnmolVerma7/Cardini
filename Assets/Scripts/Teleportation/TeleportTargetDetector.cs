using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Improved detector for teleportation targets with robust handling of various surface types
/// and edge cases in a third-person setup. Can be set to strict camera ray mode for precise targeting.
/// </summary>
public class TeleportTargetDetector : MonoBehaviour
{
    #region Fields
    // Core references
    private TeleportationSettings _settings;
    private Transform _playerTransform;
    private Transform _cameraTransform;
    private Transform _orientationTransform;
    
    // Detection results
    private Vector3 _targetPosition;
    private Vector3 _surfaceNormal = Vector3.up;
    private Vector3 _hitPoint;
    private bool _isLedgeTarget;
    private bool _hasValidTarget;
    
    // Player dimensions
    private const float PLAYER_HEIGHT = 2.0f;
    private const float PLAYER_RADIUS = 0.4f;
    private const float PLAYER_EYE_HEIGHT = 1.6f;
    
    // Ledge detection parameters
    private const float MIN_LEDGE_THICKNESS = 0.3f;
    private const float LEDGE_MIN_ANGLE = 70f;
    private const float LEDGE_MAX_ANGLE = 110f;
    
    // Cache
    private RaycastHit[] _rayHits = new RaycastHit[8];
    #endregion
    
    #region Events and Properties
    /// <summary>
    /// Triggered when a valid teleport target is found: (position, isLedge, surfaceNormal)
    /// </summary>
    public event Action<Vector3, bool, Vector3> OnTargetFound;
    
    /// <summary>
    /// Triggered when no valid target could be found
    /// </summary>
    public event Action OnNoTargetFound;
    
    /// <summary>
    /// Current target position (valid only when HasValidTarget is true)
    /// </summary>
    public Vector3 TargetPosition => _targetPosition;
    
    /// <summary>
    /// Whether target is a ledge (wall/vertical surface)
    /// </summary>
    public bool IsLedgeTarget => _isLedgeTarget;
    
    /// <summary>
    /// Surface normal at the target location
    /// </summary>
    public Vector3 SurfaceNormal => _surfaceNormal;
    
    /// <summary>
    /// Whether a valid teleport target is currently detected
    /// </summary>
    public bool HasValidTarget => _hasValidTarget;
    #endregion
    
    #region Initialization
    /// <summary>
    /// Initialize the detector with required references
    /// </summary>
    public void Initialize(TeleportationSettings settings, Transform playerTransform, 
                          Transform cameraTransform, Transform orientationTransform)
    {
        _settings = settings;
        _playerTransform = playerTransform;
        _cameraTransform = cameraTransform;
        _orientationTransform = orientationTransform;
        
        if (_settings == null)
            Debug.LogError("TeleportTargetDetector: Settings not assigned!");
        
        ValidateSetup();
    }
    
    /// <summary>
    /// Validate required components are correctly set up
    /// </summary>
    private void ValidateSetup()
    {
        if (_playerTransform == null)
            Debug.LogError("TeleportTargetDetector: Player transform not assigned!");
        
        if (_cameraTransform == null)
            Debug.LogError("TeleportTargetDetector: Camera transform not assigned!");
        
        if (_orientationTransform == null)
            Debug.LogError("TeleportTargetDetector: Orientation transform not assigned!");
        
        if (_settings != null)
        {
            if (_settings.teleportableSurfaces.value == 0)
                Debug.LogWarning("TeleportTargetDetector: No teleportable surfaces defined in settings!");
            
            if (_settings.teleportationBlockers.value == 0)
                Debug.LogWarning("TeleportTargetDetector: No teleportation blockers defined in settings!");
        }
    }
    #endregion
    
    #region Public Methods
    /// <summary>
    /// Update teleport target detection (call this every frame during aiming)
    /// </summary>
    public void UpdateTarget()
    {
        _hasValidTarget = DetectTarget();
        
        if (_hasValidTarget)
        {
            OnTargetFound?.Invoke(_targetPosition, _isLedgeTarget, _surfaceNormal);
        }
        else
        {
            OnNoTargetFound?.Invoke();
        }
    }
    #endregion
    
    #region Main Detection Logic
    /// <summary>
    /// Main detection method that finds valid teleport destinations
    /// </summary>
    private bool DetectTarget()
    {
        ResetDetectionState();
        
        // In strict mode, only use direct camera ray detection
        if (_settings.strictCameraRayOnly)
        {
            // Optionally use proximity assist if enabled
            if (_settings.useProximityAssist && _settings.proximityAssistAngle > 0)
            {
                return TryProximityAssistedRayDetection();
            }
            else
            {
                return TryDirectRayDetection(0);
            }
        }
        
        // Standard detection with full assistance features
        
        // First try direct camera ray detection (most common case)
        if (TryDirectRayDetection(0))
            return true;
        
        // Scale additional detection methods based on assistance strength
        float assistStrength = _settings.targetAssistanceStrength;
        if (assistStrength <= 0)
            return false;
            
        // Get camera angle and determine if we're looking up
        float verticalAngle = GetCameraVerticalAngle();
        bool isLookingUp = verticalAngle < 0; // Negative angle means looking up
        
        // Try vertical detection if enabled and we're looking up
        if (isLookingUp && _settings.floatWhenLookingUp && assistStrength > 0.2f)
        {
            // Minimum angle threshold scales with assistance strength (lower = more sensitive)
            float upAngleThreshold = Mathf.Lerp(20f, 5f, assistStrength);
            float upIntensity = Mathf.Abs(verticalAngle) / 90f; // 0 to 1 scale
            
            if (upIntensity * 90f > upAngleThreshold)
            {
                if (TryVerticalDetection())
                    return true;
            }
        }
        
        // Try distance-limited detection if enabled
        if (_settings.allowDistanceLimitedTargets && assistStrength > 0.3f)
        {
            if (TryDistanceLimitedDetection())
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Try to detect a target by casting a ray directly from the camera
    /// </summary>
    private bool TryDirectRayDetection(float angleOffset = 0)
    {
        // Get ray direction (optionally with offset)
        Vector3 rayDirection = _cameraTransform.forward;
        if (angleOffset != 0)
        {
            // Apply small angle offset for proximity assist
            Vector3 right = _cameraTransform.right;
            rayDirection = Quaternion.AngleAxis(angleOffset, Vector3.up) * rayDirection;
        }
        
        // Cast ray from camera
        Ray cameraRay = new Ray(_cameraTransform.position, rayDirection);
        if (!Physics.Raycast(cameraRay, out RaycastHit hit, 
                          _settings.maxTeleportationDistance * 1.5f, 
                          _settings.teleportableSurfaces | _settings.teleportationBlockers))
        {
            return false;
        }
        
        // If we hit a blocker, no teleport
        if (IsInLayerMask(hit.collider.gameObject.layer, _settings.teleportationBlockers))
        {
            return false;
        }
        
        _hitPoint = hit.point;
        _surfaceNormal = hit.normal;
        
        // Check if this is a ledge (vertical surface)
        float surfaceAngle = Vector3.Angle(hit.normal, Vector3.up);
        _isLedgeTarget = IsLedgeAngle(surfaceAngle);
        
        // Get distance from player to hit point
        float distanceToTarget = Vector3.Distance(_playerTransform.position, hit.point);
        
        // Check distance constraints
        if (distanceToTarget < _settings.minTeleportationDistance || 
            distanceToTarget > _settings.maxTeleportationDistance)
        {
            return false;
        }
        
        // Check if we're looking at ceiling/bottom surface
        if (Vector3.Dot(hit.normal, Vector3.up) < -0.7f && !_settings.allowBottomSurfaceTargeting)
        {
            return false;
        }
        
        // Process based on surface type
        if (_isLedgeTarget && _settings.enableLedgeClimbing)
        {
            return ProcessLedgeTarget(hit);
        }
        else
        {
            return ProcessGroundTarget(hit);
        }
    }
    
    /// <summary>
    /// Try proximity-assisted detection that checks small angle offsets to help with targeting
    /// </summary>
    private bool TryProximityAssistedRayDetection()
    {
        // First try direct ray with no offset
        if (TryDirectRayDetection(0))
            return true;
            
        // Proximity assist system - cast rays with small angle offsets to help with targeting
        float maxAngle = _settings.proximityAssistAngle;
        
        // Try small offsets in different directions
        float[] angles = { 2f, 5f, 8f, -2f, -5f, -8f };
        
        foreach (float angle in angles)
        {
            // Skip if beyond max allowed angle
            if (Mathf.Abs(angle) > maxAngle)
                continue;
                
            if (TryDirectRayDetection(angle))
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Try detection for targets beyond max distance by limiting to max range
    /// </summary>
    private bool TryDistanceLimitedDetection()
    {
        // Cast a ray from camera
        Ray cameraRay = new Ray(_cameraTransform.position, _cameraTransform.forward);
        if (!Physics.Raycast(cameraRay, out RaycastHit hit, 100f, 
                           _settings.teleportableSurfaces | _settings.teleportationBlockers))
        {
            return false;
        }
        
        // If we hit a blocker or the hit is within max range, skip
        if (IsInLayerMask(hit.collider.gameObject.layer, _settings.teleportationBlockers) ||
            Vector3.Distance(_playerTransform.position, hit.point) <= _settings.maxTeleportationDistance)
        {
            return false;
        }
        
        // Calculate clamped position at max distance
        Vector3 dirToHit = (hit.point - _playerTransform.position).normalized;
        Vector3 clampedPos = _playerTransform.position + dirToHit * _settings.maxTeleportationDistance;
        
        // Cast down from clamped position to find ground
        if (Physics.Raycast(clampedPos + Vector3.up * 10f, Vector3.down, out RaycastHit groundHit, 
                         20f, _settings.teleportableSurfaces))
        {
            _hitPoint = groundHit.point;
            _surfaceNormal = groundHit.normal;
            
            // Check if this is a ledge
            float surfaceAngle = Vector3.Angle(groundHit.normal, Vector3.up);
            _isLedgeTarget = IsLedgeAngle(surfaceAngle);
            
            // Process based on surface type
            if (_isLedgeTarget && _settings.enableLedgeClimbing)
            {
                return ProcessLedgeTarget(groundHit);
            }
            else
            {
                return ProcessGroundTarget(groundHit);
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Try vertical detection for platforms above the player when looking up
    /// </summary>
    private bool TryVerticalDetection()
    {
        // Get camera vertical angle to determine intensity
        float verticalAngle = GetCameraVerticalAngle();
        float upIntensity = Mathf.Clamp01(Mathf.Abs(verticalAngle) / 90f);
        
        // Project camera forward onto horizontal plane
        Vector3 flatForward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
        
        // Calculate far look point - adjust distance based on how far up we're looking
        // Looking steeper up = closer distance to prevent unrealistic far teleports
        float adjustedDistance = Mathf.Lerp(
            _settings.maxTeleportationDistance,
            _settings.maxTeleportationDistance * 0.7f,
            upIntensity
        );
        
        Vector3 farLookPoint = _playerTransform.position + flatForward * adjustedDistance;
        
        // Cast ray down from above to find ground
        if (Physics.Raycast(farLookPoint + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 
                         100f, _settings.teleportableSurfaces))
        {
            _hitPoint = hit.point;
            _surfaceNormal = hit.normal;
            
            // Check if this is a ledge
            float surfaceAngle = Vector3.Angle(hit.normal, Vector3.up);
            _isLedgeTarget = IsLedgeAngle(surfaceAngle);
            
            // Skip if platform is below player (we're looking up)
            // Use a height threshold that scales with how much we're looking up
            float minHeightGain = Mathf.Lerp(0.5f, 1.5f, upIntensity);
            if (hit.point.y < _playerTransform.position.y + minHeightGain)
                return false;
            
            // Process based on surface type
            if (_isLedgeTarget && _settings.enableLedgeClimbing)
            {
                return ProcessLedgeTarget(hit);
            }
            else
            {
                return ProcessGroundTarget(hit);
            }
        }
        
        return false;
    }
    #endregion
    
    #region Target Processing
    /// <summary>
    /// Process a ground (horizontal) surface target
    /// </summary>
    private bool ProcessGroundTarget(RaycastHit hit)
    {
        // Check if target is behind player
        if (IsPositionBehindPlayer(hit.point))
        {
            return false;
        }
        
        // Calculate teleport position with height offset
        Vector3 teleportPos = hit.point + Vector3.up * _settings.groundedHeightOffset;
        
        // Check if we can see the target
        if (_settings.requireDirectLineOfSight && !IsTargetVisible(teleportPos))
        {
            return false;
        }
        
        // Check if surface is facing away from player
        if (IsSurfaceFacingAwayFromPlayer(hit.normal, hit.point))
        {
            return false;
        }
        
        // Check for clearance at target position
        if (!HasPlayerClearance(teleportPos))
        {
            return false;
        }
        
        // Check for clear path to target
        if (IsPathBlocked(_playerTransform.position, teleportPos))
        {
            return false;
        }
        
        // Set final target position
        _targetPosition = teleportPos;
        
        return true;
    }
    
    /// <summary>
    /// Process a ledge (vertical) surface target
    /// </summary>
    private bool ProcessLedgeTarget(RaycastHit hit)
    {
        // Find the top of the ledge
        Vector3 ledgeTopPosition = FindLedgeTop(hit);
        if (ledgeTopPosition == Vector3.zero)
        {
            return false;
        }
        
        // Check if target is behind player
        if (IsPositionBehindPlayer(ledgeTopPosition))
        {
            return false;
        }
        
        // Calculate height difference for dynamic offset
        float heightDiff = ledgeTopPosition.y - _playerTransform.position.y;
        
        // Calculate teleport position with appropriate offset
        Vector3 wallNormal = GetHorizontalNormal(hit.normal);
        Vector3 teleportPos = ledgeTopPosition + Vector3.up * _settings.ledgeHeightOffset;
        
        // Offset from wall based on surface angle
        float wallOffset = 0.4f; // Base offset from wall
        teleportPos -= wallNormal * wallOffset;
        
        // Check for clearance at target position
        if (!HasPlayerClearance(teleportPos))
        {
            return false;
        }
        
        // Check if we can see the target
        if (_settings.requireDirectLineOfSight && !IsTargetVisible(teleportPos))
        {
            return false;
        }
        
        // Check for clear path to target
        if (IsPathBlocked(_playerTransform.position, teleportPos))
        {
            return false;
        }
        
        // Set final target position
        _targetPosition = teleportPos;
        
        return true;
    }
    #endregion
    
    #region Ledge Detection
    /// <summary>
    /// Find the top position of a ledge/wall
    /// </summary>
    private Vector3 FindLedgeTop(RaycastHit hit)
    {
        // Get horizontal normal of the wall
        Vector3 wallNormal = GetHorizontalNormal(hit.normal);
        if (wallNormal.magnitude < 0.1f) // Not a valid wall
        {
            return Vector3.zero;
        }
        
        // Calculate search height based on hit position
        float searchHeight = Mathf.Max(2.0f, hit.point.y - _playerTransform.position.y + 2.0f);
        
        // Cast multiple rays to find the ledge top
        Vector3 searchStartPos = hit.point + Vector3.up * searchHeight;
        
        // Try multiple distances from wall
        float[] searchDistances = { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f, 1.2f, 1.5f };
        List<LedgePoint> candidates = new List<LedgePoint>();
        
        // Search for potential ledge points
        foreach (float distance in searchDistances)
        {
            Vector3 searchPos = searchStartPos - wallNormal * distance;
            
            // Cast ray downward to find surface
            if (Physics.Raycast(searchPos, Vector3.down, out RaycastHit topHit, 
                            searchHeight * 2f, _settings.teleportableSurfaces))
            {
                // Verify it's a horizontal surface (ledge top)
                float angle = Vector3.Angle(topHit.normal, Vector3.up);
                if (angle < 30f) // Nearly horizontal
                {
                    // Check ledge thickness
                    float thickness = MeasureLedgeThickness(topHit.point, wallNormal);
                    
                    if (thickness >= MIN_LEDGE_THICKNESS)
                    {
                        // Add to candidates
                        candidates.Add(new LedgePoint
                        {
                            position = topHit.point,
                            normal = topHit.normal,
                            thickness = thickness,
                            distance = distance,
                            height = topHit.point.y
                        });
                    }
                }
            }
        }
        
        // If no candidates, give up
        if (candidates.Count == 0)
        {
            return Vector3.zero;
        }
        
        // Find best candidate (prefer closer to wall, thicker ledges, higher positions)
        LedgePoint bestLedge = candidates[0];
        float bestScore = ScoreLedgePoint(bestLedge);
        
        for (int i = 1; i < candidates.Count; i++)
        {
            float score = ScoreLedgePoint(candidates[i]);
            if (score > bestScore)
            {
                bestScore = score;
                bestLedge = candidates[i];
            }
        }
        
        // Validate final ledge position
        if (!ValidateLedgePosition(bestLedge.position, wallNormal))
        {
            return Vector3.zero;
        }
        
        // Store surface normal for later use
        _surfaceNormal = bestLedge.normal;
        
        // Apply a small safety offset from wall to prevent clipping
        float safetyOffset = 0.1f;
        return bestLedge.position - wallNormal * safetyOffset;
    }
    
    /// <summary>
    /// Measure the thickness of a ledge
    /// </summary>
    private float MeasureLedgeThickness(Vector3 ledgePoint, Vector3 wallNormal)
    {
        float maxThickness = 3.0f;
        
        // Cast ray backwards from ledge point
        if (Physics.Raycast(ledgePoint + Vector3.up * 0.05f, -wallNormal, out RaycastHit hit, 
                         maxThickness, _settings.teleportableSurfaces))
        {
            return hit.distance;
        }
        
        // Step by step thickness check if raycast fails
        for (float distance = 0.3f; distance <= maxThickness; distance += 0.1f)
        {
            Vector3 checkPoint = ledgePoint - wallNormal * distance + Vector3.up * 0.05f;
            
            // Cast down to see if we're still on the ledge
            if (!Physics.Raycast(checkPoint, Vector3.down, 0.2f, _settings.teleportableSurfaces))
            {
                // Reached edge of ledge
                return distance;
            }
        }
        
        return maxThickness; // Very thick ledge
    }
    
    /// <summary>
    /// Score a ledge point based on ideal properties
    /// </summary>
    private float ScoreLedgePoint(LedgePoint ledge)
    {
        // Prioritize ledges based on:
        // 1. Thickness (thicker is better, to a point)
        // 2. Distance from wall (closer is better)
        // 3. Height (higher is better)
        
        float thicknessScore = Mathf.Clamp01((ledge.thickness - 0.3f) / 2.0f);
        float distanceScore = 1.0f - Mathf.Clamp01(ledge.distance / 2.0f);
        float heightScore = Mathf.Clamp01((ledge.height - _playerTransform.position.y) / 5.0f);
        
        // Combined score with weighting
        return (thicknessScore * 0.5f) + (distanceScore * 0.3f) + (heightScore * 0.2f);
    }
    
    /// <summary>
    /// Validate a ledge position for player clearance and stability
    /// </summary>
    private bool ValidateLedgePosition(Vector3 position, Vector3 wallNormal)
    {
        // Check for vertical clearance (enough room for player)
        if (Physics.SphereCast(position + Vector3.up * PLAYER_HEIGHT, PLAYER_RADIUS * 0.8f,
                           Vector3.down, out RaycastHit hit, PLAYER_HEIGHT - 0.5f, 
                           _settings.teleportationBlockers))
        {
            return false;
        }
        
        // Check for horizontal clearance in front of wall
        if (Physics.Raycast(position + Vector3.up * 1.0f, -wallNormal, 
                         PLAYER_RADIUS, _settings.teleportationBlockers))
        {
            return false;
        }
        
        // Check for stable ground to stand on
        if (!Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, 
                          0.3f, _settings.teleportableSurfaces))
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Structure for storing ledge candidate information
    /// </summary>
    private struct LedgePoint
    {
        public Vector3 position;
        public Vector3 normal;
        public float thickness;
        public float distance;
        public float height;
    }
    #endregion
    
    #region Utility Methods
    /// <summary>
    /// Reset state for new detection pass
    /// </summary>
    private void ResetDetectionState()
    {
        _hasValidTarget = false;
        _isLedgeTarget = false;
        _hitPoint = Vector3.zero;
        _surfaceNormal = Vector3.up;
    }
    
    /// <summary>
    /// Check if a position is behind the player (based on orientation)
    /// </summary>
    private bool IsPositionBehindPlayer(Vector3 position)
    {
        Vector3 dirToPosition = position - _playerTransform.position;
        float angle = Vector3.Angle(_orientationTransform.forward, dirToPosition);
        return angle > _settings.maxLookAngle;
    }
    
    /// <summary>
    /// Check if a position is visible from the player's eye position
    /// </summary>
    private bool IsTargetVisible(Vector3 position)
    {
        Vector3 eyePos = _playerTransform.position + Vector3.up * PLAYER_EYE_HEIGHT;
        Vector3 dirToTarget = position - eyePos;
        
        return !Physics.Raycast(eyePos, dirToTarget.normalized, 
                             dirToTarget.magnitude, _settings.teleportationBlockers);
    }
    
    /// <summary>
    /// Check if there's enough clearance for the player at a position
    /// </summary>
    private bool HasPlayerClearance(Vector3 position)
    {
        // Use capsule cast to check for player clearance
        return !Physics.CheckCapsule(
            position + Vector3.up * PLAYER_RADIUS,
            position + Vector3.up * (PLAYER_HEIGHT - PLAYER_RADIUS),
            PLAYER_RADIUS,
            _settings.teleportationBlockers
        );
    }
    
    /// <summary>
    /// Check if path from player to target is blocked
    /// </summary>
    private bool IsPathBlocked(Vector3 startPos, Vector3 endPos)
    {
        Vector3 direction = endPos - startPos;
        float distance = direction.magnitude;
        
        // Slightly raise origin to avoid ground collision
        startPos += Vector3.up * 0.1f;
        
        // Use capsule cast to check path (player body shape)
        return Physics.CapsuleCast(
            startPos + Vector3.up * PLAYER_RADIUS,
            startPos + Vector3.up * (PLAYER_HEIGHT - PLAYER_RADIUS),
            PLAYER_RADIUS,
            direction.normalized,
            distance,
            _settings.teleportationBlockers
        );
    }
    
    /// <summary>
    /// Get just the horizontal component of a normal (for walls)
    /// </summary>
    private Vector3 GetHorizontalNormal(Vector3 normal)
    {
        Vector3 horizontalNormal = normal;
        horizontalNormal.y = 0;
        
        if (horizontalNormal.magnitude < 0.01f)
            return Vector3.zero;
            
        return horizontalNormal.normalized;
    }
    
    /// <summary>
    /// Check if a surface normal is facing away from the player
    /// </summary>
    private bool IsSurfaceFacingAwayFromPlayer(Vector3 normal, Vector3 point)
    {
        Vector3 dirToPlayer = (_playerTransform.position - point).normalized;
        float facingDot = Vector3.Dot(normal, dirToPlayer);
        return facingDot < -0.5f; // Normal pointing opposite from player
    }
    
    /// <summary>
    /// Get the vertical angle of the camera (negative = looking up)
    /// </summary>
    private float GetCameraVerticalAngle()
    {
        // Project forward onto horizontal plane
        Vector3 flatForward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
        
        // Calculate angle between flat forward and actual forward
        return Vector3.SignedAngle(
            flatForward,
            _cameraTransform.forward,
            Vector3.Cross(Vector3.up, _cameraTransform.forward)
        );
    }
    
    /// <summary>
    /// Check if an angle falls within the ledge angle range
    /// </summary>
    private bool IsLedgeAngle(float angle)
    {
        return angle >= LEDGE_MIN_ANGLE && angle <= LEDGE_MAX_ANGLE;
    }
    
    /// <summary>
    /// Check if a layer is in a layer mask
    /// </summary>
    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return ((1 << layer) & mask) != 0;
    }
    #endregion
    
    #region Debug Visualization
    /// <summary>
    /// Draw debug visualization in the scene view
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || _settings == null || !_settings.showDebugVisualization)
            return;
            
        if (_hasValidTarget)
        {
            // Draw hit point
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_hitPoint, 0.2f);
            
            // Draw target position
            Gizmos.color = _isLedgeTarget ? 
                new Color(1f, 0.6f, 0.2f, 0.8f) : // Orange for ledges
                new Color(0.2f, 0.6f, 1f, 0.8f);  // Blue for ground
            Gizmos.DrawWireSphere(_targetPosition, 0.3f);
            
            // Draw surface normal
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(_hitPoint, _surfaceNormal * 1f);
            
            // Draw line to target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_playerTransform.position, _targetPosition);
            
            // Draw camera ray
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_cameraTransform.position, _cameraTransform.forward * 
                        Vector3.Distance(_cameraTransform.position, _hitPoint));
            
            #if UNITY_EDITOR
            // Draw teleport info
            float distance = Vector3.Distance(_playerTransform.position, _targetPosition);
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                (_playerTransform.position + _targetPosition) * 0.5f,
                $"Distance: {distance:F1}m\n" +
                $"Height Δ: {(_targetPosition.y - _playerTransform.position.y):F1}m\n" +
                $"Type: {(_isLedgeTarget ? "Ledge" : "Ground")}"
            );
            #endif
        }
        else
        {
            // Draw camera ray even when no target found
            Gizmos.color = Color.gray;
            Gizmos.DrawRay(_cameraTransform.position, _cameraTransform.forward * 
                        _settings.maxTeleportationDistance);
        }
    }
    #endregion
}