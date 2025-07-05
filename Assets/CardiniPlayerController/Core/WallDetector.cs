using UnityEngine;
using System.Collections.Generic;

public class WallDetector : MonoBehaviour
{
    [Header("Wall Detection")]
    [SerializeField] private float wallDistanceSide = 0.7f;
    [SerializeField] private float wallDistanceFront = 1f;
    [SerializeField] private float wallDistanceBack = 1f;
    [SerializeField] private float doubleRayOffset = 0.1f; // Offset for double ray system
    [SerializeField] private LayerMask wallLayers = -1;
    [SerializeField] private LayerMask groundLayers = -1;
    
    [Header("Wall Running Requirements")]
    [SerializeField] private float minHeightAboveGround = 2f;
    [SerializeField] private float minWallAngle = 80f; // How steep wall needs to be
    [SerializeField] private float minWallNormalAngleChange = 15f; // To detect "new" walls
    
    [Header("Gizmo Settings")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color leftWallColor = Color.red;
    [SerializeField] private Color rightWallColor = Color.green;
    [SerializeField] private Color frontWallColor = Color.blue;
    [SerializeField] private Color backWallColor = Color.gray;
    [SerializeField] private Color heightCheckColor = Color.yellow;
    [SerializeField] private Color wallForwardColor = Color.cyan;
    [SerializeField] private float gizmoSphereSize = 0.1f;
    
    // Results (always calculated)
    public WallData CurrentWallData { get; private set; }
    
    // Internal detection results
    private RaycastHit _leftWallHit;
    private RaycastHit _leftWallHit2;
    private RaycastHit _rightWallHit;
    private RaycastHit _rightWallHit2;
    private RaycastHit _frontWallHit;
    private RaycastHit _backWallHit;
    private RaycastHit _groundCheckHit;
    
    // Wall tracking for "new wall" detection
    private Transform _lastWall;
    private Vector3 _lastWallNormal;
    
    void Update()
    {
        ScanForWalls();
    }
    
    void ScanForWalls()
    {
        var origin = transform.position;
        var forward = transform.forward;
        var right = transform.right;
        var up = transform.up;
        
        // Calculate offset positions for double ray system
        var offsetForward = forward * (doubleRayOffset * 0.5f);
        var pos1 = origin - offsetForward;
        var pos2 = origin + offsetForward;
        
        // === WALL DETECTION ===
        
        // Left wall detection (double ray system)
        bool wallLeft1 = Physics.Raycast(pos1, -right, out _leftWallHit, wallDistanceSide, wallLayers);
        bool wallLeft2 = Physics.Raycast(pos2, -right, out _leftWallHit2, wallDistanceSide, wallLayers);
        bool hasLeftWall = wallLeft1 && wallLeft2;
        
        // Right wall detection (double ray system)  
        bool wallRight1 = Physics.Raycast(pos1, right, out _rightWallHit, wallDistanceSide, wallLayers);
        bool wallRight2 = Physics.Raycast(pos2, right, out _rightWallHit2, wallDistanceSide, wallLayers);
        bool hasRightWall = wallRight1 && wallRight2;
        
        // Front wall detection (sphere cast for better detection)
        bool hasFrontWall = Physics.SphereCast(origin, 0.25f, forward, out _frontWallHit, wallDistanceFront, wallLayers);
        
        // Back wall detection
        bool hasBackWall = Physics.Raycast(origin, -forward, out _backWallHit, wallDistanceBack, wallLayers);
        
        // === HEIGHT REQUIREMENT CHECK ===
        bool isHighEnough = !Physics.Raycast(origin, Vector3.down, out _groundCheckHit, minHeightAboveGround, groundLayers);
        
        // === DETERMINE PRIMARY WALL ===
        WallType primaryWallType = WallType.None;
        RaycastHit primaryWallHit = new RaycastHit();
        
        if (hasLeftWall)
        {
            primaryWallType = WallType.Left;
            primaryWallHit = _leftWallHit;
        }
        else if (hasRightWall)
        {
            primaryWallType = WallType.Right;
            primaryWallHit = _rightWallHit;
        }
        else if (hasFrontWall)
        {
            primaryWallType = WallType.Front;
            primaryWallHit = _frontWallHit;
        }
        else if (hasBackWall)
        {
            primaryWallType = WallType.Back;
            primaryWallHit = _backWallHit;
        }
        
        // === WALL VALIDATION ===
        bool canWallRun = false;
        Vector3 wallNormal = Vector3.zero;
        Vector3 wallForward = Vector3.zero;
        float wallAngle = 0f;
        float wallLookAngle = 0f;
        bool isNewWall = false;
        
        if (primaryWallType != WallType.None)
        {
            wallNormal = primaryWallHit.normal;
            
            // Calculate wall angle (steepness)
            wallAngle = Vector3.Angle(Vector3.up, wallNormal);
            
            // Calculate look angle (for climbing validation)
            wallLookAngle = Vector3.Angle(forward, -wallNormal);
            
            // Calculate wall forward direction (for wallrunning movement)
            wallForward = Vector3.Cross(wallNormal, up);
            
            // Choose correct direction based on player orientation
            if ((forward - wallForward).magnitude > (forward + wallForward).magnitude)
            {
                wallForward = -wallForward;
            }
            
            // Check if this is a new wall
            isNewWall = IsNewWall(primaryWallHit.transform, wallNormal);
            
            // Determine if wallrunning is possible
            canWallRun = isHighEnough && 
                        wallAngle >= minWallAngle && 
                        (hasLeftWall || hasRightWall); // Only side walls for wallrunning
        }
        
        // === BUILD RESULT ===
        CurrentWallData = new WallData
        {
            canWallRun = canWallRun,
            hasLeftWall = hasLeftWall,
            hasRightWall = hasRightWall,
            hasFrontWall = hasFrontWall,
            hasBackWall = hasBackWall,
            isHighEnough = isHighEnough,
            isNewWall = isNewWall,
            
            wallNormal = wallNormal,
            wallForward = wallForward,
            wallTransform = primaryWallHit.transform,
            wallAngle = wallAngle,
            wallLookAngle = wallLookAngle,
            primaryWallType = primaryWallType,
            
            // Store hit points for gizmo visualization
            leftWallPoint = hasLeftWall ? _leftWallHit.point : Vector3.zero,
            rightWallPoint = hasRightWall ? _rightWallHit.point : Vector3.zero,
            frontWallPoint = hasFrontWall ? _frontWallHit.point : Vector3.zero,
            backWallPoint = hasBackWall ? _backWallHit.point : Vector3.zero,
            groundCheckPoint = _groundCheckHit.point
        };
        
        // Update wall tracking for next frame
        if (isNewWall && primaryWallHit.transform != null)
        {
            _lastWall = primaryWallHit.transform;
            _lastWallNormal = wallNormal;
        }
    }
    
    private bool IsNewWall(Transform wallTransform, Vector3 wallNormal)
    {
        // If no previous wall, this is definitely new
        if (_lastWall == null) return true;
        
        // If different transform, it's a new wall
        if (wallTransform != _lastWall) return true;
        
        // If normal angle changed significantly, treat as new wall
        float normalAngleChange = Vector3.Angle(_lastWallNormal, wallNormal);
        if (normalAngleChange > minWallNormalAngleChange) return true;
        
        return false;
    }
    
    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        var origin = transform.position;
        var forward = transform.forward;
        var right = transform.right;
        
        // Calculate offset positions
        var offsetForward = forward * (doubleRayOffset * 0.5f);
        var pos1 = origin - offsetForward;
        var pos2 = origin + offsetForward;
        
        // === DETECTION RAYS ===
        
        // Left wall rays
        Gizmos.color = leftWallColor;
        Gizmos.DrawRay(pos1, -right * wallDistanceSide);
        Gizmos.DrawRay(pos2, -right * wallDistanceSide);
        
        // Right wall rays
        Gizmos.color = rightWallColor;
        Gizmos.DrawRay(pos1, right * wallDistanceSide);
        Gizmos.DrawRay(pos2, right * wallDistanceSide);
        
        // Front wall ray
        Gizmos.color = frontWallColor;
        Gizmos.DrawRay(origin, forward * wallDistanceFront);
        Gizmos.DrawWireSphere(origin + forward * wallDistanceFront, 0.25f); // Show sphere cast
        
        // Back wall ray
        Gizmos.color = backWallColor;
        Gizmos.DrawRay(origin, -forward * wallDistanceBack);
        
        // Height check ray
        Gizmos.color = heightCheckColor;
        Gizmos.DrawRay(origin, Vector3.down * minHeightAboveGround);
        
        // === WALL HIT POINTS ===
        var data = CurrentWallData;
        
        if (data.hasLeftWall)
        {
            Gizmos.color = leftWallColor;
            Gizmos.DrawWireSphere(data.leftWallPoint, gizmoSphereSize);
        }
        
        if (data.hasRightWall)
        {
            Gizmos.color = rightWallColor;
            Gizmos.DrawWireSphere(data.rightWallPoint, gizmoSphereSize);
        }
        
        if (data.hasFrontWall)
        {
            Gizmos.color = frontWallColor;
            Gizmos.DrawWireSphere(data.frontWallPoint, gizmoSphereSize);
        }
        
        if (data.hasBackWall)
        {
            Gizmos.color = backWallColor;
            Gizmos.DrawWireSphere(data.backWallPoint, gizmoSphereSize);
        }
        
        // Ground check hit point
        if (!data.isHighEnough && data.groundCheckPoint != Vector3.zero)
        {
            Gizmos.color = heightCheckColor;
            Gizmos.DrawWireSphere(data.groundCheckPoint, gizmoSphereSize);
        }
        
        // === WALL DIRECTION VISUALIZATION ===
        if (data.canWallRun && data.wallNormal != Vector3.zero)
        {
            Vector3 wallCenter = Vector3.zero;
            
            // Get wall center based on primary wall type
            switch (data.primaryWallType)
            {
                case WallType.Left:
                    wallCenter = data.leftWallPoint;
                    break;
                case WallType.Right:
                    wallCenter = data.rightWallPoint;
                    break;
                case WallType.Front:
                    wallCenter = data.frontWallPoint;
                    break;
                case WallType.Back:
                    wallCenter = data.backWallPoint;
                    break;
            }
            
            if (wallCenter != Vector3.zero)
            {
                // Wall normal (away from wall)
                Gizmos.color = Color.white;
                Gizmos.DrawRay(wallCenter, data.wallNormal * 0.5f);
                
                // Wall forward direction (movement direction along wall)
                Gizmos.color = wallForwardColor;
                Gizmos.DrawRay(wallCenter, data.wallForward * 0.8f);
                
                // Draw arrow head for wall forward
                Vector3 arrowHead = wallCenter + data.wallForward * 0.8f;
                Vector3 arrowSide = Vector3.Cross(data.wallForward, Vector3.up) * 0.1f;
                Gizmos.DrawLine(arrowHead, arrowHead - data.wallForward * 0.1f + arrowSide);
                Gizmos.DrawLine(arrowHead, arrowHead - data.wallForward * 0.1f - arrowSide);
            }
        }
        
        // === STATUS INDICATORS ===
        
        // Draw a large sphere around player if wallrunning is possible
        if (data.canWallRun)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.1f); // Transparent green
            Gizmos.DrawWireSphere(origin, 1f);
        }
        
        // Draw info in scene view
        #if UNITY_EDITOR
        if (data.canWallRun)
        {
            Vector3 labelPos = origin + Vector3.up * 2f;
            string info = $"Wall Run Ready!\nType: {data.primaryWallType}\nAngle: {data.wallAngle:F1}°\nHeight OK: {data.isHighEnough}";
            UnityEditor.Handles.Label(labelPos, info);
        }
        else if (data.primaryWallType != WallType.None)
        {
            Vector3 labelPos = origin + Vector3.up * 2f;
            string reason = !data.isHighEnough ? "Too Low" : 
                          data.wallAngle < minWallAngle ? "Wall Too Shallow" : 
                          "No Side Wall";
            UnityEditor.Handles.Label(labelPos, $"Can't Wall Run\nReason: {reason}");
        }
        #endif
    }
    
    [System.Serializable]
    public struct WallData
    {
        public bool canWallRun;
        public bool hasLeftWall;
        public bool hasRightWall;
        public bool hasFrontWall;
        public bool hasBackWall;
        public bool isHighEnough;
        public bool isNewWall;
        
        public Vector3 wallNormal;
        public Vector3 wallForward;
        public Transform wallTransform;
        public float wallAngle;
        public float wallLookAngle;
        public WallType primaryWallType;
        
        // Hit points for gizmo visualization
        public Vector3 leftWallPoint;
        public Vector3 rightWallPoint;
        public Vector3 frontWallPoint;
        public Vector3 backWallPoint;
        public Vector3 groundCheckPoint;
    }
    
    public enum WallType
    {
        None,
        Left,
        Right,
        Front,
        Back
    }
}