using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles detection and validation of teleportation targets
/// </summary>
public class TeleportTargetDetector : MonoBehaviour
{
    #region Fields
    // Core references
    private TeleportationSettings _settings;
    private Transform _playerTransform;
    private Transform _cameraTransform;
    private Transform _orientationTransform;
    
    // Detection state
    private Vector3 _hitPosition;
    private Vector3 _targetPosition;
    private Vector3 _surfaceNormal;
    private Vector3 _ledgePosition;
    private bool _isTargetingLedge;
    private bool _hasValidTarget;
    
    // Constants
    private const float PLAYER_EYE_HEIGHT = 1.6f;
    private const float PLAYER_HEIGHT = 2f;
    private const float PLAYER_RADIUS = 0.4f;
    private const float MIN_LEDGE_THICKNESS = 0.3f;
    private const float SURFACE_LEDGE_MIN_ANGLE = 75f;
    private const float SURFACE_LEDGE_MAX_ANGLE = 105f;
    private const float BOTTOM_SURFACE_ANGLE = 150f;
    
    // Debug visualization
    private RaycastHit[] _raycastHitCache = new RaycastHit[5];
    #endregion
    
    #region Events
    public event Action<Vector3, bool, Vector3> OnTargetFound;
    public event Action OnNoTargetFound;
    #endregion
    
    #region Initialization
    public void Initialize(TeleportationSettings settings, Transform playerTransform, 
                          Transform cameraTransform, Transform orientationTransform)
    {
        _settings = settings;
        _playerTransform = playerTransform;
        _cameraTransform = cameraTransform;
        _orientationTransform = orientationTransform;
        
        if (_settings == null)
        {
            Debug.LogError("TeleportTargetDetector: No settings provided!");
        }
    }
    #endregion
    
    #region Target Detection
    /// <summary>
    /// Update the target detection (called during aiming state)
    /// </summary>
    public void UpdateTarget()
    {
        _hasValidTarget = DetectTeleportDestination();
        
        if (_hasValidTarget)
        {
            OnTargetFound?.Invoke(_targetPosition, _isTargetingLedge, _surfaceNormal);
        }
        else
        {
            OnNoTargetFound?.Invoke();
        }
    }
    
    /// <summary>
    /// Main method to detect valid teleportation destinations
    /// </summary>
    private bool DetectTeleportDestination()
    {
        ResetDetectionState();
        
        Vector3 playerPosition = _playerTransform.position;
        Vector3 cameraPosition = _cameraTransform.position;
        Vector3 cameraForward = _cameraTransform.forward;
        
        // Get camera angle but don't use it for threshold decisions
        float verticalAngle = GetCameraVerticalAngle();
        
        // Primary detection: direct camera ray
        if (TryCameraRayDetection(cameraPosition, cameraForward, playerPosition))
        {
            return true;
        }
        
        // Secondary detection: always try vertical detection regardless of angle
        if (_settings.floatWhenLookingUp && 
            TryVerticalDetection(playerPosition, cameraForward, false))
        {
            return true;
        }
        
        return false;
    }
    
    private bool TryCameraRayDetection(Vector3 cameraPosition, Vector3 cameraForward, Vector3 playerPosition)
    {
        // Cast ray from camera
        Ray cameraRay = new Ray(cameraPosition, cameraForward);
        if (!Physics.Raycast(cameraRay, out RaycastHit cameraHit, 
            _settings.maxTeleportationDistance * 2f, 
            _settings.teleportableSurfaces | _settings.teleportationBlockers))
        {
            return false;
        }
        
        // Check if hit a blocker
        if (IsLayerInMask(cameraHit.collider.gameObject.layer, _settings.teleportationBlockers))
        {
            return false;
        }
        
        _hitPosition = cameraHit.point;
        
        // Calculate distance to hit
        float distanceToTarget = Vector3.Distance(playerPosition, cameraHit.point);
        
        // Check distance constraints
        if (distanceToTarget > _settings.maxTeleportationDistance || 
            distanceToTarget < _settings.minTeleportationDistance)
        {
            return TryClampedPositionDetection(playerPosition, cameraHit, distanceToTarget);
        }
        
        // Detect surface type
        float surfaceAngle = Vector3.Angle(cameraHit.normal, Vector3.up);
        _isTargetingLedge = IsLedgeAngle(surfaceAngle);
        
        // Check for underside of platforms
        if (Vector3.Dot(cameraHit.normal, Vector3.up) < -0.7f && !_settings.allowBottomSurfaceTargeting)
        {
            return false;
        }

        // Process based on surface type
        if (_isTargetingLedge && _settings.enableLedgeClimbing)
        {
            return ProcessLedgeTargeting(cameraHit, distanceToTarget);
        }
        else
        {
            return ProcessGroundTargeting(cameraHit, distanceToTarget);
        }
    }
    
    private bool TryClampedPositionDetection(Vector3 playerPosition, RaycastHit cameraHit, float distanceToTarget)
    {
        // Only process if target is too far (not too close)
        if (distanceToTarget <= _settings.maxTeleportationDistance)
            return false;
        
        // Calculate clamped position at max range
        Vector3 directionToHit = (cameraHit.point - playerPosition).normalized;
        Vector3 clampedPosition = playerPosition + directionToHit * _settings.maxTeleportationDistance;
        
        // Find ground below clamped position
        if (!Physics.Raycast(clampedPosition + Vector3.up * 10f, Vector3.down, 
            out RaycastHit groundHit, 20f, _settings.teleportableSurfaces))
        {
            return false;
        }
        
        _hitPosition = groundHit.point;
        
        // Check surface type
        float groundSurfaceAngle = Vector3.Angle(groundHit.normal, Vector3.up);
        _isTargetingLedge = IsLedgeAngle(groundSurfaceAngle);
        
        if (_isTargetingLedge && _settings.enableLedgeClimbing)
        {
            return ProcessLedgeTargeting(groundHit, _settings.maxTeleportationDistance);
        }
        else
        {
            return ProcessGroundTargeting(groundHit, _settings.maxTeleportationDistance);
        }
    }
    
    private bool TryVerticalDetection(Vector3 playerPosition, Vector3 cameraForward, bool isPastThreshold)
    {
        // Project camera forward to get horizontal direction
        Vector3 flatForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
        
        // Calculate far look point
        Vector3 farLookPoint = playerPosition + (flatForward * _settings.maxTeleportationDistance);
        
        // Cast down to find ground
        if (!Physics.Raycast(farLookPoint + Vector3.up * 50f, Vector3.down, 
            out RaycastHit groundHit, 100f, _settings.teleportableSurfaces))
        {
            return false;
        }
        
        _hitPosition = groundHit.point;
        
        // Check if hit is a ledge
        float surfaceAngle = Vector3.Angle(groundHit.normal, Vector3.up);
        _isTargetingLedge = IsLedgeAngle(surfaceAngle) && _settings.enableLedgeClimbing;
        
        if (_isTargetingLedge)
        {
            return ProcessLedgeTargeting(groundHit, _settings.maxTeleportationDistance);
        }
        else
        {
            return ProcessGroundTargeting(groundHit, _settings.maxTeleportationDistance);
        }
    }
    #endregion

    #region Target Processing
    private bool ProcessGroundTargeting(RaycastHit hit, float directDistance)
    {
        // Validation checks
        if (directDistance < _settings.minTeleportationDistance ||
            directDistance > _settings.maxTeleportationDistance ||
            IsPositionBehindPlayer(hit.point))
        {
            return false;
        }
            
        // Check for bottom surface
        float surfaceAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (surfaceAngle > BOTTOM_SURFACE_ANGLE && !_settings.allowBottomSurfaceTargeting)
        {
            return false;
        }
        
        // Calculate teleport position
        Vector3 teleportPosition = hit.point + Vector3.up * _settings.groundedHeightOffset;
        _ledgePosition = hit.point;
        _surfaceNormal = hit.normal;
        
        // Check visibility and surface orientation
        if ((_settings.requireDirectLineOfSight && !IsVisibleFromPlayer(teleportPosition)) ||
            IsSurfaceFacingAwayFromPlayer(hit.normal, hit.point))
        {
            return false;
        }
        
        return SetDestinationPosition(teleportPosition);
    }
    
    private bool ProcessLedgeTargeting(RaycastHit hit, float directDistance)
    {
        // Bottom surface check
        float surfaceAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (surfaceAngle > BOTTOM_SURFACE_ANGLE && !_settings.allowBottomSurfaceTargeting)
        {
            return false;
        }
        
        // Dynamic range calculation
        float heightDiff = hit.point.y - _playerTransform.position.y;
        float verticalFactor = Mathf.Clamp01((heightDiff + 2f) / 4f);
        float rangeMultiplier = 1f + verticalFactor * 0.5f;
        
        float maxRange = Mathf.Min(_settings.maxTeleportationDistance * rangeMultiplier, 
                        _settings.maxTeleportationDistance * 1.3f);
        float minDist = _settings.minTeleportationDistance;
        
        if (directDistance > maxRange || directDistance < minDist)
        {
            return false;
        }
        
        // Wall normal calculation
        Vector3 wallNormal = GetNormalizedWallNormal(hit.normal);
        if (wallNormal == Vector3.zero)
        {
            return false;
        }
        
        // Find and validate ledge position
        Vector3 ledgePosition = FindLedgeTopPosition(hit);
        if (!ValidateLedgePosition(ledgePosition, hit.point, MIN_LEDGE_THICKNESS))
        {
            return false;
        }
        
        _ledgePosition = ledgePosition;
        
        if (IsPositionBehindPlayer(ledgePosition))
        {
            return false;
        }
        
        // Calculate teleport position
        Vector3 teleportPosition = CalculateLedgeTeleportPosition(ledgePosition, wallNormal, 
                                                                 heightDiff, surfaceAngle);
        
        // Line of sight check
        if (_settings.requireDirectLineOfSight && !IsVisibleFromPlayer(teleportPosition))
        {
            return false;
        }
        
        return SetDestinationPosition(teleportPosition);
    }
    
    private Vector3 CalculateLedgeTeleportPosition(Vector3 ledgePosition, Vector3 wallNormal, 
                                             float heightDiff, float surfaceAngle)
    {
        // Height offset calculation - no camera angle dependency
        float heightFactor = Mathf.Clamp01(Mathf.Abs(heightDiff) / 2f);
        
        // For same-height platforms, use consistent offset
        float dynamicOffset = Mathf.Abs(heightDiff) < 0.5f ? 
                            Mathf.Lerp(0.6f, 0.8f, heightFactor) : 
                            Mathf.Lerp(0.5f, _settings.ledgeHeightOffset, heightFactor);
        
        // Calculate base position
        Vector3 teleportPosition = ledgePosition + Vector3.up * dynamicOffset;
        
        // Apply wall offset based on surface angle
        float baseOffset = Mathf.Lerp(0.25f, 0.4f, (surfaceAngle - SURFACE_LEDGE_MIN_ANGLE) / 30f);
        
        // Use consistent offset for same-height platforms
        float heightAdjustedOffset = Mathf.Abs(heightDiff) < 0.5f ?
                                baseOffset * 1.2f : // More offset for same-height platforms
                                baseOffset * (1f + heightFactor * 0.5f);
        
        teleportPosition -= wallNormal * heightAdjustedOffset;
        
        // Ensure minimum ground clearance
        EnsureMinimumGroundClearance(ref teleportPosition, 0.25f);
        
        return teleportPosition;
    }
    #endregion
    
    #region Ledge Detection
    private struct LedgeCandidate
    {
        public Vector3 position;
        public Vector3 normal;
        public float thickness;
        public float distanceScore;
        public float normalScore;
        public float heightScore;
        public float edgeScore; // New property to prioritize true edges
        
        public float CalculateTotalScore()
        {
            const float thicknessWeight = 0.35f;
            const float distanceWeight = 0.25f;
            const float normalWeight = 0.15f;
            const float heightWeight = 0.15f;
            const float edgeWeight = 0.1f; // Weight for edge proximity
            
            float thicknessScore = Mathf.Clamp01((thickness - 0.3f) / 1.7f);
            
            return (thicknessScore * thicknessWeight) + 
                (distanceScore * distanceWeight) + 
                (normalScore * normalWeight) + 
                (heightScore * heightWeight) +
                (edgeScore * edgeWeight); // Add edge score to total
        }
    }
    
    private Vector3 FindLedgeTopPosition(RaycastHit hit)
    {
        Vector3 wallNormal = GetNormalizedWallNormal(hit.normal);
        if (wallNormal == Vector3.zero)
        {
            return hit.point;
        }
        
        float wallVerticalAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (!IsLedgeAngle(wallVerticalAngle))
        {
            return hit.point;
        }
        
        // Calculate search parameters - no angle-based adjustment
        float heightDiff = hit.point.y - _playerTransform.position.y;
        
        // Fix for equal-height platforms - use consistent search height
        float baseSearchHeight = 1.8f;
        float heightAdjustment = Mathf.Abs(heightDiff) < 0.5f ? 0.3f : 
                                Mathf.Clamp01(heightDiff / 4f) * 0.5f;
        float searchHeight = baseSearchHeight + heightAdjustment;
        
        // Configure search origin with fixed position
        Vector3 aboveHitPoint = hit.point + Vector3.up * searchHeight;
        
        // Use more search distances for platforms near player height
        List<LedgeCandidate> candidates = new List<LedgeCandidate>();
        float[] searchDistances = Mathf.Abs(heightDiff) < 0.5f ?
            new float[] { 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.3f, 0.4f, 0.5f, 0.6f, 0.8f, 1.0f } :
            new float[] { 0.05f, 0.1f, 0.2f, 0.3f, 0.4f, 0.6f, 0.8f, 1.0f, 1.5f };
        
        foreach (float distance in searchDistances)
        {
            // Use consistent scaling regardless of camera angle
            float scaledDistance = distance * (1f + Mathf.Clamp01(Mathf.Abs(heightDiff) / 3f) * 0.2f);
            ScanForLedgeCandidatesAtDistance(hit, wallNormal, aboveHitPoint, scaledDistance, searchHeight, candidates);
        }
        
        // Find best candidate
        LedgeCandidate bestCandidate = FindBestLedgeCandidate(candidates, hit.point);
        
        // Fallback if no candidates found
        if (bestCandidate.position == Vector3.zero)
        {
            return hit.point + Vector3.up * Mathf.Max(0.8f, heightDiff * 0.4f);
        }
        
        // Store surface normal
        _surfaceNormal = bestCandidate.normal;
        
        // Anti-phasing safety offset
        float safetyOffset = Mathf.Lerp(0.35f, 0.2f, Mathf.Clamp01((bestCandidate.thickness - 0.3f) / 1.7f));
        
        // Extra offset for platform edges to prevent phasing
        if (Mathf.Abs(heightDiff) < 0.8f && bestCandidate.thickness < 0.8f)
        {
            safetyOffset += 0.1f; // Add extra offset for platform edges at similar height
        }
        
        // Apply vertical offset based on thickness (thinner edges need more vertical clearance)
        float verticalOffset = bestCandidate.thickness < 0.5f ? 0.12f : 0.08f;
        
        // Return final position
        return bestCandidate.position - wallNormal * safetyOffset + Vector3.up * verticalOffset;
    }
    
    private List<LedgeCandidate> ScanForLedgeCandidates(RaycastHit hit, Vector3 wallNormal, 
                                                      Vector3 aboveHitPoint, float searchHeight,
                                                      float cameraVerticalAngle)
    {
        float heightDiff = hit.point.y - _playerTransform.position.y;
        List<LedgeCandidate> candidates = new List<LedgeCandidate>();
        
        // Configure search distances
        float[] searchDistances = { 0.05f, 0.1f, 0.15f, 0.2f, 0.3f, 0.4f, 0.5f, 0.7f, 0.9f, 1.2f, 1.5f };
        
        foreach (float distance in searchDistances)
        {
            // Adjust distance scaling based on camera angle
            float distanceMultiplier = GetDistanceMultiplier(cameraVerticalAngle);
            float scaledDistance = distance * (1f + Mathf.Clamp01(heightDiff / 4f) * 0.3f) * distanceMultiplier;
            
            // Scan at the current distance
            ScanForLedgeCandidatesAtDistance(hit, wallNormal, aboveHitPoint, scaledDistance, 
                                           searchHeight, candidates);
        }
        
        return candidates;
    }
    
    private void ScanForLedgeCandidatesAtDistance(RaycastHit hit, Vector3 wallNormal, Vector3 aboveHitPoint,
                                           float distance, float searchHeight, List<LedgeCandidate> candidates)
    {
        // Use finer grid for more precise detection
        float horizontalStep = 0.15f;
        float verticalStep = 0.1f;
        
        for (float horizontalOffset = -0.3f; horizontalOffset <= 0.3f; horizontalOffset += horizontalStep)
        {
            for (float verticalOffset = -0.3f; verticalOffset <= 0.3f; verticalOffset += verticalStep)
            {
                // Calculate check point
                Vector3 checkPoint = aboveHitPoint - wallNormal * distance;
                Vector3 horizontalDir = Vector3.Cross(wallNormal, Vector3.up).normalized;
                checkPoint += horizontalDir * horizontalOffset + Vector3.up * verticalOffset;
                
                // Cast ray down to find ledge surface
                if (Physics.Raycast(checkPoint, Vector3.down, out RaycastHit topHit,
                    searchHeight * 2f, _settings.teleportableSurfaces))
                {
                    // Check if flat enough
                    float upDot = Vector3.Dot(topHit.normal, Vector3.up);
                    if (upDot > 0.85f) // Slightly more forgiving angle check
                    {
                        // Calculate ledge thickness
                        float thickness = CalculateLedgeThickness(topHit.point, wallNormal);
                        
                        // Add candidate if thick enough
                        if (thickness >= MIN_LEDGE_THICKNESS)
                        {
                            // Calculate edge score - prefer thinner ledges for equal-height platforms
                            float edgeScore = 0f;
                            if (thickness < 1.0f)
                            {
                                edgeScore = 1f - Mathf.Clamp01(thickness / 1.0f);
                            }
                            
                            // Boost score for ledges at player height level
                            float heightDiffToPlayer = Mathf.Abs(topHit.point.y - _playerTransform.position.y);
                            float sameHeightBonus = heightDiffToPlayer < 0.5f ? 0.3f : 0f;
                            
                            candidates.Add(new LedgeCandidate
                            {
                                position = topHit.point,
                                normal = topHit.normal,
                                thickness = thickness,
                                distanceScore = 1f - (distance / 2.5f) + edgeScore * 0.3f + sameHeightBonus,
                                normalScore = upDot,
                                heightScore = topHit.point.y >= hit.point.y ? 1f : 0f,
                                edgeScore = edgeScore
                            });
                        }
                    }
                }
            }
        }
    }
    
    private float CalculateLedgeThickness(Vector3 ledgePoint, Vector3 wallNormal)
    {
        float maxThicknessCheck = 2f;
        
        // Try direct raycast first
        if (Physics.Raycast(ledgePoint + Vector3.up * 0.05f, -wallNormal, 
                          out RaycastHit backHit, maxThicknessCheck, _settings.teleportableSurfaces))
        {
            // Check if we hit immediately (might be on wall)
            if (backHit.distance < 0.05f)
            {
                if (Physics.Raycast(ledgePoint + Vector3.up * 0.1f, -wallNormal, 
                                  out backHit, maxThicknessCheck, _settings.teleportableSurfaces))
                {
                    return backHit.distance;
                }
                return 0f;
            }
            return backHit.distance;
        }
        
        // No direct hit - do interval check
        for (float distance = 0.1f; distance <= maxThicknessCheck; distance += 0.1f)
        {
            Vector3 checkPoint = ledgePoint - wallNormal * distance;
            if (!Physics.Raycast(checkPoint + Vector3.up * 0.1f, Vector3.down,
                               0.2f, _settings.teleportableSurfaces))
            {
                return distance; // Found edge
            }
        }
        
        return maxThicknessCheck; // Very thick ledge
    }
    
    private LedgeCandidate FindBestLedgeCandidate(List<LedgeCandidate> candidates, Vector3 hitPoint)
    {
        if (candidates.Count == 0)
            return new LedgeCandidate();
        
        // Sort by score (highest first)
        candidates.Sort((a, b) => b.CalculateTotalScore().CompareTo(a.CalculateTotalScore()));
        
        // Take top candidates and validate
        for (int i = 0; i < Mathf.Min(3, candidates.Count); i++)
        {
            var candidate = candidates[i];
            if (ValidateLedgePosition(candidate.position, hitPoint, candidate.thickness))
            {
                return candidate;
            }
        }
        
        return new LedgeCandidate();
    }
    
    private bool ValidateLedgePosition(Vector3 position, Vector3 hitPoint, float thickness)
    {
        // Get normalized wall normal
        Vector3 wallNormal = GetNormalizedWallDirectionFromPoints(hitPoint, position);
        if (wallNormal == Vector3.zero)
            return false;
        
        // Special handling for platform edges at player height level
        float heightDiffToPlayer = Mathf.Abs(position.y - _playerTransform.position.y);
        bool isSameHeightPlatform = heightDiffToPlayer < 0.5f;
        
        // For platform edges, require more clearance
        if (isSameHeightPlatform)
        {
            if (!HasPlatformEdgeClearance(position, wallNormal))
                return false;
        }
        else
        {
            // Standard checks for ledges at different heights
            if (!HasVerticalClearance(position))
                return false;
                
            if (!HasHorizontalClearance(position, wallNormal))
                return false;
        }
        
        // Adjust thickness requirement for same-height platforms
        float minThickness = isSameHeightPlatform ? 0.25f : MIN_LEDGE_THICKNESS;
        
        // Check thickness
        if (thickness < minThickness)
            return false;
        
        // Check stable ground
        return HasStableGround(position);
    }

    private bool HasPlatformEdgeClearance(Vector3 position, Vector3 wallNormal)
    {
        // Specialized clearance check for platform edges
        
        // Check vertical clearance first
        if (Physics.SphereCast(
            position + Vector3.up * PLAYER_HEIGHT,
            PLAYER_RADIUS * 0.7f, // Slightly reduced radius for platform edges
            Vector3.down,
            out RaycastHit clearanceHit,
            PLAYER_HEIGHT - 0.1f,
            _settings.teleportationBlockers))
        {
            return false;
        }
        
        // For platform edges, check specifically in front direction
        if (Physics.Raycast(
            position + Vector3.up * 0.5f,
            -wallNormal, // Direction toward the platform
            PLAYER_RADIUS * 0.8f,
            _settings.teleportationBlockers))
        {
            return false;
        }
        
        // And check perpendicular directions
        Vector3 sideDir = Vector3.Cross(wallNormal, Vector3.up).normalized;
        if (Physics.Raycast(position + Vector3.up * 0.5f, sideDir, PLAYER_RADIUS, _settings.teleportationBlockers) ||
            Physics.Raycast(position + Vector3.up * 0.5f, -sideDir, PLAYER_RADIUS, _settings.teleportationBlockers))
        {
            return false;
        }
        
        return true;
    }
    #endregion
    
    #region Helper Methods
    private void ResetDetectionState()
    {
        _hitPosition = Vector3.zero;
        _isTargetingLedge = false;
        _hasValidTarget = false;
        _surfaceNormal = Vector3.up;
    }
    
    private float GetCameraVerticalAngle()
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
        return Vector3.SignedAngle(
            flatForward,
            _cameraTransform.forward,
            Vector3.Cross(Vector3.up, _cameraTransform.forward)
        );
    }
    
    private bool IsPositionBehindPlayer(Vector3 position)
    {
        Vector3 directionToPosition = position - _playerTransform.position;
        float angleToTarget = Vector3.Angle(_orientationTransform.forward, directionToPosition);
        return angleToTarget > _settings.maxLookAngle;
    }
    
    private bool IsVisibleFromPlayer(Vector3 targetPoint)
    {
        Vector3 playerEyePosition = _playerTransform.position + Vector3.up * PLAYER_EYE_HEIGHT;
        Vector3 directionToTarget = targetPoint - playerEyePosition;
        float distanceToTarget = directionToTarget.magnitude;
        
        return !Physics.Raycast(playerEyePosition, directionToTarget.normalized,
            distanceToTarget, _settings.teleportationBlockers);
    }
    
    private bool SetDestinationPosition(Vector3 teleportPosition)
    {
        // First make sure we're not misteleporting to a location the player isn't looking at
        // Calculate angle between camera forward and direction to teleport
        Vector3 directionToTarget = teleportPosition - _cameraTransform.position;
        float angleToCameraForward = Vector3.Angle(_cameraTransform.forward, directionToTarget);
        
        // If angle is too large, this might be a misteleport
        if (angleToCameraForward > 45f)
        {
            // Check if there's a more direct target in between
            if (Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, 
                            out RaycastHit directHit, 
                            Vector3.Distance(_cameraTransform.position, teleportPosition) * 0.8f, 
                            _settings.teleportableSurfaces))
            {
                LogDebugMessage("Teleport rejected: Target not directly in view");
                return false;
            }
        }
        
        // Check if path passes through solid objects
        if (IsTeleportPathBlocked(_playerTransform.position, teleportPosition))
        {
            LogDebugMessage("Teleport blocked: Path intersects with solid geometry");
            return false;
        }
        
        // Ensure minimum clearance
        if (_isTargetingLedge)
        {
            EnsureMinimumGroundClearance(ref teleportPosition, 0.2f);
        }
        
        // Check surface orientation
        if (_surfaceNormal != Vector3.zero)
        {
            Vector3 dirToPlayer = (_playerTransform.position - teleportPosition).normalized;
            float dotProduct = Vector3.Dot(_surfaceNormal, dirToPlayer);
            
            if (dotProduct < -0.7f) // Surface facing away from player
            {
                LogDebugMessage("Teleport blocked: Target surface facing away from player");
                return false;
            }
        }
        
        // Final obstruction check
        Vector3 directionToTarget2 = teleportPosition - _playerTransform.position;
        bool isBlocked = Physics.SphereCast(
            _playerTransform.position + Vector3.up * 1f,
            0.5f,
            directionToTarget.normalized,
            out RaycastHit blockHit,
            directionToTarget.magnitude,
            _settings.teleportationBlockers
        );
        
        if (isBlocked)
            return false;
        
        _hasValidTarget = true;
        _targetPosition = teleportPosition;
        
        return true;
    }
    
    private bool IsTeleportPathBlocked(Vector3 startPosition, Vector3 endPosition)
    {
        Vector3 direction = endPosition - startPosition;
        float distance = direction.magnitude;
        
        startPosition += Vector3.up * 0.1f; // Avoid starting inside ground
        
        return Physics.CapsuleCast(
            startPosition,
            startPosition + Vector3.up * 1.7f, // Player height
            PLAYER_RADIUS,
            direction.normalized,
            out RaycastHit hit,
            distance,
            _settings.teleportationBlockers
        );
    }
    
    private void EnsureMinimumGroundClearance(ref Vector3 position, float minClearance)
    {
        if (Physics.Raycast(position, Vector3.down, out RaycastHit surfaceHit, 0.5f, _settings.teleportableSurfaces))
        {
            float currentClearance = position.y - surfaceHit.point.y;
            if (currentClearance < minClearance)
            {
                position.y = surfaceHit.point.y + minClearance;
            }
        }
    }
    
    private bool HasVerticalClearance(Vector3 position)
    {
        float playerHeight = PLAYER_HEIGHT;
        float playerRadius = PLAYER_RADIUS;
        
        return !Physics.SphereCast(
            position + Vector3.up * playerHeight,
            playerRadius * 0.8f,
            Vector3.down,
            out RaycastHit clearanceHit,
            playerHeight - 0.1f,
            _settings.teleportationBlockers
        );
    }
    
    private bool HasHorizontalClearance(Vector3 position, Vector3 wallNormal)
    {
        float[] checkAngles = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
        int validAngles = 0;
        
        foreach (float angle in checkAngles)
        {
            Vector3 checkDir = Quaternion.Euler(0, angle, 0) * wallNormal;
            bool clear = !Physics.Raycast(
                position + Vector3.up * 0.5f,
                checkDir,
                PLAYER_RADIUS * 1.2f,
                _settings.teleportationBlockers
            );
            
            if (clear) validAngles++;
        }
        
        return validAngles >= 5; // Need at least 5 of 8 directions clear
    }
    
    private bool HasStableGround(Vector3 position)
    {
        return Physics.Raycast(
            position + Vector3.up * 0.1f,
            Vector3.down,
            0.3f,
            _settings.teleportableSurfaces
        );
    }
    
    private bool IsSurfaceFacingAwayFromPlayer(Vector3 surfaceNormal, Vector3 surfacePoint)
    {
        Vector3 dirToPlayer = (_playerTransform.position - surfacePoint).normalized;
        float facingPlayer = Vector3.Dot(surfaceNormal, dirToPlayer);
        return facingPlayer < -0.6f;
    }
    
    private Vector3 GetNormalizedWallNormal(Vector3 normal)
    {
        Vector3 wallNormal = normal;
        wallNormal.y = 0;
        
        if (wallNormal.magnitude <= 0.01f)
            return Vector3.zero;
            
        return wallNormal.normalized;
    }
    
    private Vector3 GetNormalizedWallDirectionFromPoints(Vector3 from, Vector3 to)
    {
        Vector3 direction = from - to;
        direction.y = 0;
        
        if (direction.magnitude <= 0.01f)
            return Vector3.zero;
            
        return direction.normalized;
    }
    
    private float CalculateAngleAdjustment(float cameraVerticalAngle)
    {
        if (cameraVerticalAngle < -10f) // Looking up
        {
            return Mathf.Min(Mathf.Abs(cameraVerticalAngle) * 0.05f, 1.0f);
        }
        else if (cameraVerticalAngle > 30f) // Looking down
        {
            return Mathf.Min(cameraVerticalAngle * 0.02f, 0.5f);
        }
        return 0f;
    }
    
    private float GetDistanceMultiplier(float cameraVerticalAngle)
    {
        if (cameraVerticalAngle < -25f) // Looking up steeply
        {
            return 0.7f;
        }
        else if (cameraVerticalAngle > 40f) // Looking down steeply
        {
            return 1.2f;
        }
        return 1.0f;
    }
    
    private bool IsLedgeAngle(float angle)
    {
        return angle >= SURFACE_LEDGE_MIN_ANGLE && angle <= SURFACE_LEDGE_MAX_ANGLE;
    }
    
    private bool IsLayerInMask(int layer, LayerMask mask)
    {
        return ((1 << layer) & mask) != 0;
    }
    
    private void LogDebugMessage(string message)
    {
        if (_settings.enableDebugLogging)
        {
            Debug.Log(message);
        }
    }
    
    public Vector3 GetTargetPosition() => _targetPosition;
    public bool IsTargetingLedge() => _isTargetingLedge;
    public Vector3 GetSurfaceNormal() => _surfaceNormal;
    #endregion
    
    #region Debug Visualization
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || _settings == null || !_settings.showDebugVisualization)
            return;
            
        if (_hasValidTarget)
        {
            DrawTargetVisualization();
        }
    }
    
    private void DrawTargetVisualization()
    {
        // Hit point
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_hitPosition, 0.2f);
        
        // Target position
        Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
        Gizmos.DrawWireSphere(_targetPosition, 0.3f);
        
        // Surface normal
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(_hitPosition, _surfaceNormal * 1f);
        
        // Line to target
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(_playerTransform.position, _targetPosition);
        
        #if UNITY_EDITOR
        DrawEditorLabels();
        DrawTeleportTrajectory();
        #endif
    }
    
    #if UNITY_EDITOR
    private void DrawEditorLabels()
    {
        // Distance and height info
        UnityEditor.Handles.color = Color.white;
        float distance = Vector3.Distance(_playerTransform.position, _targetPosition);
        UnityEditor.Handles.Label(
            (_playerTransform.position + _targetPosition) * 0.5f,
            $"Distance: {distance:F2}m\n" +
            $"Height Δ: {(_targetPosition.y - _playerTransform.position.y):F2}m"
        );
        
        // Ledge label
        if (_isTargetingLedge)
        {
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(_hitPosition + Vector3.up * 0.5f, "LEDGE");
        }
    }
    
    private void DrawTeleportTrajectory()
    {
        if (_settings.showDebugVisualization)
        {
            // Draw teleport path
            Gizmos.color = Color.cyan;
            Vector3 startPos = _playerTransform.position + Vector3.up * 1f;
            Vector3 endPos = _targetPosition;
            Vector3 midPoint = (startPos + endPos) * 0.5f + Vector3.up * 1f;
            
            for (int i = 0; i < 20; i++)
            {
                float t1 = i / 20f;
                float t2 = (i + 1) / 20f;
                
                Vector3 p1 = QuadraticBezier(startPos, midPoint, endPos, t1);
                Vector3 p2 = QuadraticBezier(startPos, midPoint, endPos, t2);
                
                Gizmos.DrawLine(p1, p2);
            }
        }
    }
    
    private Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        return (uu * p0) + (2 * u * t * p1) + (tt * p2);
    }
    #endif
    #endregion
}