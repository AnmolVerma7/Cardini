using UnityEngine;

namespace Cardini.Motion
{
    /// <summary>
    /// Enhanced wall detector with customizable height detection and wall running validation.
    /// </summary>
    public class WallDetector : MonoBehaviour
    {
        [Header("Detection Range")]
        public float wallDistance = 1f;
        [Tooltip("How many height checks to perform (more = better curved wall detection)")]
        [Range(1, 5)]
        public int heightChecks = 3;
        [Tooltip("Height range for wall detection")]
        public float detectionHeight = 1.5f;
        [Tooltip("Lower detection offset from player center (negative = below, positive = above)")]
        public float lowerDetectionOffset = 0.2f;
        public LayerMask wallLayers = -1;
        
        [Header("Wall Validation")]
        [Tooltip("Minimum wall steepness to be considered valid (degrees from vertical)")]
        [Range(0f, 45f)]
        public float minWallSteepness = 10f;
        [Tooltip("Maximum distance between height check hits to be same wall")]
        public float maxWallContinuity = 0.5f;
        [Tooltip("Minimum wall quality required for wall running (0-1)")]
        [Range(0f, 1f)]
        public float minWallQuality = 0.5f;
        
        [Header("Gizmos")]
        public bool showGizmos = true;
        public bool showDetailedInfo = false;
        
        public WallInfo CurrentWall { get; private set; }
        
        // Debug info
        private RaycastHit[] _leftHits = new RaycastHit[5];
        private RaycastHit[] _rightHits = new RaycastHit[5];
        private bool[] _leftValid = new bool[5];
        private bool[] _rightValid = new bool[5];
        
        void Update()
        {
            CheckForWalls();
        }
        
        void CheckForWalls()
        {
            Vector3 baseOrigin = transform.position + Vector3.up * lowerDetectionOffset;
            Vector3 right = transform.right;
            
            // Perform multi-height detection
            WallInfo leftWall = CheckWallSide(baseOrigin, -right, true);
            WallInfo rightWall = CheckWallSide(baseOrigin, right, false);
            
            // Choose the best wall (prefer the one we're closer to)
            WallInfo bestWall = new WallInfo();
            
            if (leftWall.hasWall && rightWall.hasWall)
            {
                // Choose closer wall
                float leftDistance = Vector3.Distance(transform.position, leftWall.wallPoint);
                float rightDistance = Vector3.Distance(transform.position, rightWall.wallPoint);
                bestWall = leftDistance < rightDistance ? leftWall : rightWall;
            }
            else if (leftWall.hasWall)
            {
                bestWall = leftWall;
            }
            else if (rightWall.hasWall)
            {
                bestWall = rightWall;
            }
            
            CurrentWall = bestWall;
        }
        
        private WallInfo CheckWallSide(Vector3 baseOrigin, Vector3 direction, bool isLeft)
        {
            WallInfo wallInfo = new WallInfo();
            
            var hits = isLeft ? _leftHits : _rightHits;
            var valid = isLeft ? _leftValid : _rightValid;
            
            int validHits = 0;
            Vector3 averageNormal = Vector3.zero;
            Vector3 averagePoint = Vector3.zero;
            Transform wallTransform = null;
            
            // Perform multiple height checks
            for (int i = 0; i < heightChecks; i++)
            {
                float heightOffset = (i / (float)(heightChecks - 1)) * detectionHeight;
                Vector3 origin = baseOrigin + Vector3.up * heightOffset;
                
                valid[i] = Physics.Raycast(origin, direction, out hits[i], wallDistance, wallLayers);
                
                if (valid[i])
                {
                    // Validate wall steepness
                    float wallAngle = Vector3.Angle(hits[i].normal, Vector3.up);
                    if (wallAngle > minWallSteepness && wallAngle < 180f - minWallSteepness)
                    {
                        // Check continuity with previous hits
                        bool isContinuous = true;
                        if (validHits > 0)
                        {
                            float distance = Vector3.Distance(hits[i].point, averagePoint / validHits);
                            isContinuous = distance < maxWallContinuity;
                        }
                        
                        if (isContinuous)
                        {
                            averageNormal += hits[i].normal;
                            averagePoint += hits[i].point;
                            validHits++;
                            
                            if (wallTransform == null)
                                wallTransform = hits[i].transform;
                        }
                    }
                }
            }
            
            // If we have enough valid hits, create wall info
            if (validHits > 0)
            {
                wallInfo.hasWall = true;
                wallInfo.wallNormal = (averageNormal / validHits).normalized;
                wallInfo.wallPoint = averagePoint / validHits;
                wallInfo.isLeftWall = isLeft;
                wallInfo.wallTransform = wallTransform;
                wallInfo.validHitCount = validHits;
                
                // Calculate wall forward direction
                wallInfo.wallForward = Vector3.Cross(wallInfo.wallNormal, Vector3.up).normalized;
                
                // Make sure it points in player's general forward direction
                if (Vector3.Dot(wallInfo.wallForward, transform.forward) < 0)
                    wallInfo.wallForward = -wallInfo.wallForward;
                    
                // Calculate wall quality (more hits = better quality)
                wallInfo.wallQuality = (float)validHits / heightChecks;
                
                // Determine if this wall can be used for wall running
                wallInfo.canWallRun = wallInfo.wallQuality >= minWallQuality;
            }
            
            return wallInfo;
        }
        
        void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            Vector3 baseOrigin = transform.position + Vector3.up * lowerDetectionOffset;
            Vector3 right = transform.right;
            
            // Draw detection rays
            for (int i = 0; i < heightChecks; i++)
            {
                float heightOffset = (i / (float)(heightChecks - 1)) * detectionHeight;
                Vector3 origin = baseOrigin + Vector3.up * heightOffset;
                
                // Left rays
                Gizmos.color = _leftValid[i] ? Color.green : Color.red;
                Gizmos.DrawRay(origin, -right * wallDistance);
                
                // Right rays  
                Gizmos.color = _rightValid[i] ? Color.green : Color.red;
                Gizmos.DrawRay(origin, right * wallDistance);
                
                // Draw hit points
                if (showDetailedInfo)
                {
                    if (_leftValid[i])
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(_leftHits[i].point, 0.05f);
                    }
                    if (_rightValid[i])
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(_rightHits[i].point, 0.05f);
                    }
                }
            }
            
            // Draw base origin indicator
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(baseOrigin, 0.05f);
            Gizmos.DrawLine(transform.position, baseOrigin);
            
            // Draw current wall info
            if (CurrentWall.hasWall)
            {
                // Wall point
                Gizmos.color = CurrentWall.canWallRun ? Color.white : Color.gray;
                Gizmos.DrawWireSphere(CurrentWall.wallPoint, 0.1f);
                
                // Wall normal
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(CurrentWall.wallPoint, CurrentWall.wallNormal * 0.5f);
                
                // Wall forward
                Gizmos.color = Color.green;
                Gizmos.DrawRay(CurrentWall.wallPoint, CurrentWall.wallForward * 0.8f);
                
                // Wall quality indicator
                if (showDetailedInfo)
                {
                    Gizmos.color = Color.Lerp(Color.red, Color.green, CurrentWall.wallQuality);
                    Gizmos.DrawWireCube(CurrentWall.wallPoint + Vector3.up * 0.3f, 
                                       Vector3.one * (0.1f + CurrentWall.wallQuality * 0.1f));
                }
                
                // Wall run capability indicator
                if (CurrentWall.canWallRun)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(CurrentWall.wallPoint + Vector3.up * 0.6f, 0.15f);
                }
            }
        }
        
        [System.Serializable]
        public struct WallInfo
        {
            public bool hasWall;
            public Vector3 wallNormal;
            public Vector3 wallForward;
            public Vector3 wallPoint;
            public bool isLeftWall;
            public Transform wallTransform;
            public int validHitCount;
            public float wallQuality; // 0-1, how good the wall detection is
            public bool canWallRun; // Whether this wall is suitable for wall running
        }
    }
}