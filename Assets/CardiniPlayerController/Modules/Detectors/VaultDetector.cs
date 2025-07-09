using UnityEngine;
using System.Collections.Generic;

public class VaultDetector : MonoBehaviour
{
    [Header("Vault Detection")]
    [SerializeField] private float vaultConeAngle = 90f;
    [SerializeField] private float coneVisualizationLength = 1.5f;
    [SerializeField] private float forwardDistance = 2f;
    [SerializeField] private float vaultHeight = 1.5f;
    [SerializeField] private float maxVaultDistance = 2f; // Max width of object to vault
    [SerializeField] private float landingDistance = 1f; // How far past the obstacle to land
    [SerializeField] private float minLandingDistance = 0.3f;
    [SerializeField] private float waistHeight = 1f;
    [SerializeField] private LayerMask vaultableLayers = -1;
    [SerializeField] private AnimationCurve vaultArcCurve = AnimationCurve.EaseInOut(0f, 0.5f, 1f, 0.15f);
    [SerializeField] private float baseArcHeight = 0.3f;
    [SerializeField] private float initiationDistance = 1.5f; // How far back from start point player can initiate vault
    [SerializeField] private float minVaultDistance = 0.3f; // Minimum distance to vault (too close threshold)
    
    [Header("Gizmo Settings")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color vaultConeColor = new Color(1f, 1f, 0f, 0.2f); // Transparent yellow
    [SerializeField] private Color forwardRayColor = Color.magenta;
    [SerializeField] private Color upRayColor = Color.yellow;
    [SerializeField] private Color downRayColor = Color.blue;
    [SerializeField] private Color startPointColor = Color.green;
    [SerializeField] private Color endPointColor = Color.red;
    [SerializeField] private Color landingPointColor = Color.black;
    [SerializeField] private Color vaultArcColor = Color.cyan;
    [SerializeField] private Color fullTrajectoryColor = new Color(0f, 1f, 0.5f, 0.8f); // Bright green
    [SerializeField] private float gizmoSphereSize = 0.1f;
    [SerializeField] private int arcResolution = 20;
    [SerializeField] private Color initiationPointColor = Color.blue;
    
    // Results (always calculated)
    public VaultData CurrentVaultData { get; private set; }
    
    void Update()
    {
        ScanForVault();
    }
    
    void ScanForVault()
    {
        var origin = transform.position + Vector3.up * waistHeight;
        var forward = transform.forward;

        // 1. Forward ray (purple) - detect obstacle
        if (Physics.Raycast(origin, forward, out RaycastHit obstacleHit, forwardDistance, vaultableLayers))
        {
            // Debug.Log($"Forward ray hit: {obstacleHit.collider.name}");

            // Get the surface normal and calculate the perpendicular vault direction
            Vector3 surfaceNormal = obstacleHit.normal;
            Vector3 vaultDirection = -surfaceNormal; // Direction perpendicular to the surface
            vaultDirection.y = 0; // Keep it horizontal
            vaultDirection.Normalize();
            
            // If the vault direction is too different from player's forward (like hitting a side wall), 
            // we might want to skip vaulting
            float angleToSurface = Vector3.Angle(forward, vaultDirection);
            if (angleToSurface > vaultConeAngle) // Adjust this threshold as needed
            {
                CurrentVaultData = new VaultData
                {
                    canVault = false,
                    obstacleHitPoint = obstacleHit.point,
                    forwardRayOrigin = origin,
                    forwardRayEnd = obstacleHit.point
                };
                // Debug.Log($"Approach angle too steep: {angleToSurface:F1}°");
                return;
            }

            // ALTERNATIVE APPROACH: Use bounds to find the top
            var collider = obstacleHit.collider;
            var bounds = collider.bounds;
            
            // Get the top Y position of the obstacle
            var obstacleTop = bounds.max.y;
            
            // Check if we're already on top of or above the obstacle
            if (transform.position.y >= obstacleTop - 0.1f)
            {
                CurrentVaultData = new VaultData
                {
                    canVault = false,
                    obstacleHitPoint = obstacleHit.point,
                    forwardRayOrigin = origin,
                    forwardRayEnd = obstacleHit.point
                };
                // Debug.Log("Already on or above obstacle - no vault needed");
                return;
            }
            
            // Check if this is vaultable height
            if (obstacleTop <= transform.position.y + vaultHeight && obstacleTop > transform.position.y)
            {
                // Calculate start point on top of obstacle at the hit point
                var startPoint = new Vector3(obstacleHit.point.x, obstacleTop + 0.1f, obstacleHit.point.z);
                
                // For visualization, set up ray origin
                var upOrigin = new Vector3(obstacleHit.point.x, transform.position.y, obstacleHit.point.z);
                
                // Cast a ray from the start point in the vault direction to find the far edge
                Vector3 rayStart = startPoint + vaultDirection * 0.1f; // Start slightly inside
                if (Physics.Raycast(rayStart, vaultDirection, out RaycastHit farEdgeHit, maxVaultDistance + 0.5f, vaultableLayers))
                {
                    // We hit something - check if it's the same obstacle
                    if (farEdgeHit.collider != obstacleHit.collider)
                    {
                        // Hit a different obstacle, can't vault
                        CurrentVaultData = new VaultData
                        {
                            canVault = false,
                            obstacleHitPoint = obstacleHit.point,
                            forwardRayOrigin = origin,
                            forwardRayEnd = obstacleHit.point
                        };
                        // Debug.Log("Another obstacle in the way");
                        return;
                    }
                }
                
                // Calculate obstacle depth using bounds projection along vault direction
                Vector3 boundsMin = bounds.min;
                Vector3 boundsMax = bounds.max;
                
                // Project bounds corners onto vault direction to find actual depth
                float minDist = float.MaxValue;
                float maxDist = float.MinValue;
                
                // Check all 8 corners of the bounds
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? boundsMin.x : boundsMax.x,
                        (i & 2) == 0 ? boundsMin.y : boundsMax.y,
                        (i & 4) == 0 ? boundsMin.z : boundsMax.z
                    );
                    
                    float dist = Vector3.Dot(corner - obstacleHit.point, vaultDirection);
                    minDist = Mathf.Min(minDist, dist);
                    maxDist = Mathf.Max(maxDist, dist);
                }
                
                float obstacleDepth = maxDist - minDist;
                
                // Check if vault distance is within allowed range
                if (obstacleDepth > maxVaultDistance)
                {
                    CurrentVaultData = new VaultData
                    {
                        canVault = false,
                        obstacleHitPoint = obstacleHit.point,
                        upRayOrigin = upOrigin,
                        upRayHit = new Vector3(obstacleHit.point.x, obstacleTop, obstacleHit.point.z),
                        forwardRayOrigin = origin,
                        forwardRayEnd = obstacleHit.point
                    };
                    Debug.Log($"Obstacle too wide: {obstacleDepth:F2}m > {maxVaultDistance:F2}m");
                    return;
                }

                // End point calculation - find the far edge in the vault direction
                Vector3 endPoint = startPoint + vaultDirection * obstacleDepth;
                endPoint.y = obstacleTop + 0.1f; // Keep at same height as start

                // Landing point is further in the vault direction past the end point
                // var landingPoint = endPoint + vaultDirection * landingDistance;
                float landingLerp = Mathf.InverseLerp(minVaultDistance, maxVaultDistance, obstacleDepth);
                float scaledLandingDistance = Mathf.Lerp(minLandingDistance, landingDistance, landingLerp);
                var landingPoint = endPoint + vaultDirection * scaledLandingDistance;

                // Cast down from landing point to find ground
                var downRayStart = landingPoint + Vector3.up * vaultHeight;
                if (Physics.Raycast(downRayStart, Vector3.down, out RaycastHit landHit, vaultHeight * 3f))
                {
                    landingPoint = landHit.point;
                }
                else
                {
                    // If no ground found, estimate landing at player's current height
                    landingPoint.y = transform.position.y;
                }

                // Calculate initiation point (back from start point in opposite vault direction)
                Vector3 initiationPoint = startPoint - vaultDirection * initiationDistance;
                initiationPoint.y = transform.position.y; // Keep at player height

                // Check if player is in initiation zone
                // Player should be within distance AND roughly aligned with vault direction
                float distanceToStart = Vector3.Distance(transform.position, startPoint);
                Vector3 playerToStart = (startPoint - transform.position).normalized;
                float alignment = Vector3.Dot(playerToStart, vaultDirection);
                
                // Also check if player is looking within the cone angle
                float playerLookAngle = Vector3.Angle(forward, vaultDirection);
                bool withinCone = playerLookAngle <= vaultConeAngle;
                
                // In zone if: within initiation distance, aligned with vault direction, not too close, AND within cone
                bool inInitiationZone = distanceToStart <= initiationDistance && 
                                      distanceToStart > minVaultDistance && // Not too close
                                      alignment > 0.5f && // Facing generally toward the vault
                                      withinCone; // Looking within the cone angle

                // Calculate full trajectory arc (player → start → end → landing)
                var fullTrajectoryPoints = inInitiationZone ? 
                    CalculateFullTrajectory(transform.position, startPoint, endPoint, landingPoint, obstacleTop) : 
                    null;
                
                // SUCCESS - We can vault!
                var arcPoints = CalculateVaultArc(startPoint, endPoint, obstacleTop);

                CurrentVaultData = new VaultData
                {
                    canVault = true,
                    startPoint = startPoint,
                    endPoint = endPoint,
                    landingPoint = landingPoint,
                    obstacleHitPoint = obstacleHit.point,
                    upRayOrigin = upOrigin,
                    upRayHit = new Vector3(obstacleHit.point.x, obstacleTop, obstacleHit.point.z),
                    downRayOrigin = downRayStart,
                    downRayHit = landingPoint,
                    vaultDistance = obstacleDepth,
                    arcPoints = arcPoints,
                    forwardRayOrigin = origin,
                    forwardRayEnd = obstacleHit.point,
                    vaultDirection = vaultDirection, // Store this for later use
                    initiationPoint = initiationPoint,
                    fullTrajectoryPoints = fullTrajectoryPoints,
                    inInitiationZone = inInitiationZone,
                };
                
                // Debug.Log($"Vault possible! Height: {obstacleTop - transform.position.y:F2}m, Width: {obstacleDepth:F2}m, Direction: {vaultDirection}");
                if (inInitiationZone)
                {
                    // Debug.Log("In vault initiation zone!");
                }
                return;
            }
            else
            {
                CurrentVaultData = new VaultData
                {
                    canVault = false,
                    obstacleHitPoint = obstacleHit.point,
                    forwardRayOrigin = origin,
                    forwardRayEnd = obstacleHit.point
                };
                // Debug.Log($"Obstacle too high! Height: {obstacleTop - transform.position.y:F2}m, Max vault: {vaultHeight:F2}m");
            }
        }
        else
        {
            CurrentVaultData = new VaultData 
            { 
                canVault = false,
                forwardRayOrigin = origin,
                forwardRayEnd = origin + forward * forwardDistance
            };
        }
    }
    
    Vector3[] CalculateVaultArc(Vector3 start, Vector3 end, float obstacleHeight)
    {
        var points = new Vector3[arcResolution];
        var midPoint = (start + end) * 0.5f;
        
        float vaultDistance = Vector3.Distance(start, end);
        float normalizedDistance = vaultDistance / maxVaultDistance;
        
        // Use curve to determine arc height based on vault distance
        float curveMultiplier = vaultArcCurve.Evaluate(normalizedDistance);
        float arcHeight = baseArcHeight + (vaultDistance * curveMultiplier);
        
        midPoint.y = obstacleHeight + arcHeight;
            
        // Generate smooth bezier curve
        for (int i = 0; i < arcResolution; i++)
        {
            float t = (float)i / (arcResolution - 1);
            points[i] = CalculateBezierPoint(start, midPoint, end, t);
        }
        
        return points;
    }
    
    Vector3 CalculateBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
    }

    Vector3[] CalculateFullTrajectory(Vector3 playerPos, Vector3 start, Vector3 end, Vector3 landing, float obstacleHeight)
    {
        // First, calculate the vault arc points (same as the cyan arc)
        var vaultArcPoints = CalculateVaultArc(start, end, obstacleHeight);
        
        var points = new List<Vector3>();
        
        // SEGMENT 1: Player to Start Point (smooth approach)
        int approachPoints = 10;
        for (int i = 0; i < approachPoints; i++)
        {
            float t = (float)i / (approachPoints - 1);
            
            // Connect to first point of vault arc
            Vector3 targetPoint = vaultArcPoints[0];
            Vector3 midPoint = (playerPos + targetPoint) * 0.5f;
            midPoint.y = Mathf.Lerp(playerPos.y, targetPoint.y, 0.7f);
            
            float u = 1 - t;
            points.Add(u * u * playerPos + 2 * u * t * midPoint + t * t * targetPoint);
        }
        
        // SEGMENT 2: The existing vault arc (cyan line) - skip first point to avoid duplicate
        for (int i = 1; i < vaultArcPoints.Length; i++)
        {
            points.Add(vaultArcPoints[i]);
        }
        
        // SEGMENT 3: End Point to Landing (smooth descent)
        int landingPoints = 10;
        Vector3 lastVaultPoint = vaultArcPoints[vaultArcPoints.Length - 1];
        
        for (int i = 1; i < landingPoints; i++)
        {
            float t = (float)i / (landingPoints - 1);
            
            // Connect from last vault point to landing
            Vector3 midPoint = (lastVaultPoint + landing) * 0.5f;
            midPoint.y = Mathf.Lerp(lastVaultPoint.y, landing.y, 0.3f);
            
            float u = 1 - t;
            points.Add(u * u * lastVaultPoint + 2 * u * t * midPoint + t * t * landing);
        }
        
        return points.ToArray();
    }
    
    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        var data = CurrentVaultData;
        
        // Forward ray (purple/magenta)
        Gizmos.color = forwardRayColor;
        if (data.forwardRayOrigin != Vector3.zero)
        {
            Gizmos.DrawLine(data.forwardRayOrigin, data.forwardRayEnd);
            if (data.obstacleHitPoint != Vector3.zero)
            {
                Gizmos.DrawWireSphere(data.obstacleHitPoint, gizmoSphereSize);
            }
        }
        
        // Up ray (yellow) - visual representation even though we use bounds
        Gizmos.color = upRayColor;
        if (data.upRayOrigin != Vector3.zero && data.upRayHit != Vector3.zero)
        {
            Gizmos.DrawLine(data.upRayOrigin, data.upRayHit);
            Gizmos.DrawWireSphere(data.upRayHit, gizmoSphereSize);
        }
        
        // Down ray (blue)
        Gizmos.color = downRayColor;
        if (data.downRayOrigin != Vector3.zero)
        {
            var downEnd = data.downRayHit != Vector3.zero ? data.downRayHit : data.downRayOrigin + Vector3.down * (vaultHeight * 2);
            Gizmos.DrawLine(data.downRayOrigin, downEnd);
            if (data.downRayHit != Vector3.zero)
            {
                Gizmos.DrawWireSphere(data.downRayHit, gizmoSphereSize);
            }
        }
        
        if (data.canVault)
        {
            // Start point (green)
            Gizmos.color = startPointColor;
            Gizmos.DrawWireSphere(data.startPoint, gizmoSphereSize * 2);
            
            // End point (red)
            Gizmos.color = endPointColor;
            Gizmos.DrawWireSphere(data.endPoint, gizmoSphereSize * 2);
            
            // Landing point (orange)
            Gizmos.color = landingPointColor;
            Gizmos.DrawWireSphere(data.landingPoint, gizmoSphereSize * 2.5f);
            
            // Initiation point and zone visualization
            Gizmos.color = initiationPointColor;
            Gizmos.DrawWireSphere(data.initiationPoint, gizmoSphereSize * 1.5f);
            
            // Draw initiation zone as a transparent sphere
            Gizmos.color = new Color(initiationPointColor.r, initiationPointColor.g, initiationPointColor.b, 0.1f);
            Gizmos.DrawWireSphere(data.startPoint, initiationDistance);
            
            // Draw ground circles for initiation and too-close zones
            DrawGroundCircle(data.startPoint, initiationDistance, initiationPointColor, 32);
            DrawGroundCircle(data.startPoint, minVaultDistance, Color.red, 16);
            
            // Vault arc (cyan)
            Gizmos.color = vaultArcColor;
            for (int i = 0; i < data.arcPoints.Length - 1; i++)
            {
                Gizmos.DrawLine(data.arcPoints[i], data.arcPoints[i + 1]);
            }
            
            // Full trajectory arc (bright green) - only show when player is near
            if (data.fullTrajectoryPoints != null && data.fullTrajectoryPoints.Length > 0)
            {
                Gizmos.color = fullTrajectoryColor;
                
                // Draw thicker lines for the full trajectory
                for (int i = 0; i < data.fullTrajectoryPoints.Length - 1; i++)
                {
                    Gizmos.DrawLine(data.fullTrajectoryPoints[i], data.fullTrajectoryPoints[i + 1]);
                    
                    // Add small spheres along the path for better visibility
                    if (i % 3 == 0)
                    {
                        Gizmos.DrawWireSphere(data.fullTrajectoryPoints[i], gizmoSphereSize * 0.5f);
                    }
                }
                
                // Highlight player's current position if in initiation zone
                if (data.inInitiationZone)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(transform.position, gizmoSphereSize * 3f);
                }
            }
            
            // Draw line from end point to landing point - only if NOT showing full trajectory
            if (data.fullTrajectoryPoints == null || data.fullTrajectoryPoints.Length == 0)
            {
                Gizmos.color = landingPointColor * 0.5f;
                Gizmos.DrawLine(data.endPoint, data.landingPoint);
            }
            
            // Draw vault direction arrow for debugging
            if (data.vaultDirection != Vector3.zero)
            {
                Gizmos.color = Color.white;
                Vector3 arrowStart = data.startPoint;
                Vector3 arrowEnd = arrowStart + data.vaultDirection * 0.5f;
                Gizmos.DrawLine(arrowStart, arrowEnd);
                // Draw arrowhead
                Vector3 right = Vector3.Cross(data.vaultDirection, Vector3.up) * 0.1f;
                Gizmos.DrawLine(arrowEnd, arrowEnd - data.vaultDirection * 0.1f + right);
                Gizmos.DrawLine(arrowEnd, arrowEnd - data.vaultDirection * 0.1f - right);
                
                // Draw vault cone
                Gizmos.color = vaultConeColor;

                // Draw cone lines
                Vector3 coneOrigin = transform.position + Vector3.up * waistHeight;
                float coneLength = coneVisualizationLength;

                // Calculate cone edge directions
                Quaternion leftRot = Quaternion.AngleAxis(-vaultConeAngle, Vector3.up);
                Quaternion rightRot = Quaternion.AngleAxis(vaultConeAngle, Vector3.up);

                Vector3 leftEdge = leftRot * data.vaultDirection;
                Vector3 rightEdge = rightRot * data.vaultDirection;

                // Draw cone edges
                Gizmos.DrawLine(coneOrigin, coneOrigin + leftEdge * coneLength);
                Gizmos.DrawLine(coneOrigin, coneOrigin + rightEdge * coneLength);

                // Draw arc at the end
                int arcSteps = 10;
                for (int i = 0; i < arcSteps; i++)
                {
                    float t1 = (float)i / arcSteps;
                    float t2 = (float)(i + 1) / arcSteps;
                    
                    Quaternion rot1 = Quaternion.AngleAxis(Mathf.Lerp(-vaultConeAngle, vaultConeAngle, t1), Vector3.up);
                    Quaternion rot2 = Quaternion.AngleAxis(Mathf.Lerp(-vaultConeAngle, vaultConeAngle, t2), Vector3.up);
                    
                    Vector3 p1 = coneOrigin + rot1 * data.vaultDirection * coneLength;
                    Vector3 p2 = coneOrigin + rot2 * data.vaultDirection * coneLength;
                    
                    Gizmos.DrawLine(p1, p2);
                }
            }
        }
    }

    void DrawGroundCircle(Vector3 center, float radius, Color color, int segments)
    {
        Gizmos.color = color;
        float angleStep = 360f / segments;
        Vector3 prevPoint = Vector3.zero;
        
        // Find ground height at center
        float groundY = center.y;
        if (Physics.Raycast(center + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f))
        {
            groundY = hit.point.y + 0.05f; // Slightly above ground
        }
        else
        {
            groundY = transform.position.y + 0.05f; // Use player height as fallback
        }
        
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 point = new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                groundY,
                center.z + Mathf.Sin(angle) * radius
            );
            
            if (i > 0)
            {
                Gizmos.DrawLine(prevPoint, point);
            }
            
            prevPoint = point;
        }
    }

    [System.Serializable]
    public struct VaultData
    {
        public bool canVault;
        public Vector3 startPoint;
        public Vector3 endPoint;
        public Vector3 landingPoint; // New: where player will actually land
        public Vector3 obstacleHitPoint;
        public Vector3 upRayOrigin;
        public Vector3 upRayHit;
        public Vector3 downRayOrigin;
        public Vector3 downRayHit;
        public Vector3 forwardRayOrigin;
        public Vector3 forwardRayEnd;
        public float vaultDistance;
        public Vector3[] arcPoints;
        public Vector3 vaultDirection; // New: the perpendicular direction for vaulting
        public Vector3 initiationPoint; // Where player can start vault from
        public Vector3[] fullTrajectoryPoints; // Complete arc from player to landing
        public bool inInitiationZone; // Is player currently in the sweet spot?
    }
}
