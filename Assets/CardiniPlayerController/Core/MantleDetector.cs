using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    /// <summary>
    /// Mantle detector following the exact method from the video:
    /// 1. Sphere check at max height/distance
    /// 2. Sphere cast upward for obstructions
    /// 3. Downward raycast for surface
    /// 4. Ground angle validation
    /// 5. Capsule overlap check at final position
    /// </summary>
    public class MantleDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [Tooltip("Maximum height the player can mantle")]
        public float maxMantleHeight = 2.5f;
        
        [Tooltip("Maximum forward distance to check for mantle")]
        public float maxMantleDistance = 1.5f;
        
        [Tooltip("Radius for sphere checks")]
        public float sphereCheckRadius = 0.3f;
        
        [Tooltip("Layers that can be mantled")]
        public LayerMask mantleLayers = -1;
        
        [Header("Validation")]
        [Tooltip("Maximum ground angle that can be mantled onto (should match your character controller's max stable slope)")]
        public float maxGroundAngle = 45f;
        
        [Header("Gizmos")]
        public bool showGizmos = true;
        public Color validMantleColor = Color.green;
        public Color invalidMantleColor = Color.red;
        public Color checkPositionColor = Color.yellow;
        
        // Cached references
        private CardiniController _controller;
        private KinematicCharacterMotor _motor;
        private float _capsuleHeight;
        private float _capsuleRadius;
        
        // Current detection result
        public MantleData CurrentMantleData { get; private set; }
        
        // Debug visualization data
        private Vector3 _lastSphereCheckPos;
        private Vector3 _lastSurfaceHitPoint;
        private Vector3 _lastMantlePosition;
        private bool _lastCheckWasValid;
        private string _lastFailReason = "";
        
        void Start()
        {
            _controller = GetComponent<CardiniController>();
            _motor = _controller?.Motor;
            
            if (_motor != null)
            {
                _capsuleHeight = _motor.Capsule.height;
                _capsuleRadius = _motor.Capsule.radius;
            }
            else
            {
                _capsuleHeight = 2f;
                _capsuleRadius = 0.5f;
                Debug.LogWarning("SimpleMantleDetector: Could not get motor reference, using default capsule dimensions");
            }
        }
        
        void Update()
        {
            CurrentMantleData = DetectMantle();
        }
        
        private MantleData DetectMantle()
        {
            var mantleData = new MantleData();
            
            Vector3 playerPos = transform.position;
            Vector3 playerForward = transform.forward;
            
            // Step 1: Sphere collision check at max mantle height and distance
            Vector3 sphereCheckPosition = playerPos + Vector3.up * maxMantleHeight + playerForward * maxMantleDistance;
            _lastSphereCheckPos = sphereCheckPosition;
            
            if (Physics.CheckSphere(sphereCheckPosition, sphereCheckRadius, mantleLayers))
            {
                _lastCheckWasValid = false;
                _lastFailReason = "Initial sphere check hit obstruction";
                return mantleData; // Invalid - obstruction detected
            }
            
            // Step 2: Sphere cast upward to look for obstructions
            Vector3 sphereCastStart = playerPos + playerForward * maxMantleDistance;
            float sphereCastDistance = maxMantleHeight;
            
            if (Physics.SphereCast(sphereCastStart, sphereCheckRadius, Vector3.up, out RaycastHit obstructionHit, sphereCastDistance, mantleLayers))
            {
                _lastCheckWasValid = false;
                _lastFailReason = "Upward sphere cast hit obstruction";
                return mantleData; // Invalid - obstruction in path
            }
            
            // Step 3: Downward raycast to find surface
            Vector3 raycastStart = sphereCheckPosition + Vector3.up * 0.5f; // Start slightly above the sphere check position
            float raycastDistance = maxMantleHeight + 1f; // Cast down far enough to find surface
            
            if (!Physics.Raycast(raycastStart, Vector3.down, out RaycastHit surfaceHit, raycastDistance, mantleLayers))
            {
                _lastCheckWasValid = false;
                _lastFailReason = "No surface found below";
                return mantleData; // Invalid - no surface found
            }
            
            _lastSurfaceHitPoint = surfaceHit.point;
            
            // Step 4: Check ground angle against max stable ground angle
            float surfaceAngle = Vector3.Angle(surfaceHit.normal, Vector3.up);
            if (surfaceAngle > maxGroundAngle)
            {
                _lastCheckWasValid = false;
                _lastFailReason = $"Surface too steep: {surfaceAngle:F1}° > {maxGroundAngle:F1}°";
                return mantleData; // Invalid - surface too steep
            }
            
            // Step 5: Calculate final mantle position and do capsule overlap check
            Vector3 mantlePosition = surfaceHit.point + Vector3.up * (_capsuleHeight * 0.5f);
            _lastMantlePosition = mantlePosition;
            
            // Capsule overlap check to ensure no obstructions at final position
            Vector3 capsuleTop = mantlePosition + Vector3.up * (_capsuleHeight * 0.5f - _capsuleRadius);
            Vector3 capsuleBottom = mantlePosition - Vector3.up * (_capsuleHeight * 0.5f - _capsuleRadius);
            
            if (Physics.CheckCapsule(capsuleBottom, capsuleTop, _capsuleRadius * 0.9f, mantleLayers))
            {
                _lastCheckWasValid = false;
                _lastFailReason = "Final position blocked";
                return mantleData; // Invalid - final position obstructed
            }
            
            // All checks passed! Populate mantle data
            mantleData.isValid = true;
            mantleData.mantlePosition = mantlePosition;
            mantleData.surfacePoint = surfaceHit.point;
            mantleData.surfaceNormal = surfaceHit.normal;
            mantleData.objectHeight = surfaceHit.point.y - playerPos.y;
            mantleData.surfaceCollider = surfaceHit.collider;
            
            // Check for physics body (for moving platforms)
            Rigidbody rb = surfaceHit.collider.GetComponentInParent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                mantleData.physicsBody = rb;
            }
            
            _lastCheckWasValid = true;
            _lastFailReason = "";
            
            return mantleData;
        }
        
        void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            Vector3 playerPos = transform.position;
            Vector3 playerForward = transform.forward;
            
            // Draw max mantle height and distance indicators
            Gizmos.color = Color.white;
            Vector3 maxHeightPoint = playerPos + Vector3.up * maxMantleHeight;
            Gizmos.DrawLine(playerPos, maxHeightPoint);
            Gizmos.DrawWireSphere(maxHeightPoint, 0.1f);
            
            Vector3 maxDistancePoint = playerPos + playerForward * maxMantleDistance;
            Gizmos.DrawLine(playerPos, maxDistancePoint);
            Gizmos.DrawWireSphere(maxDistancePoint, 0.1f);
            
            // Draw initial sphere check position
            Gizmos.color = checkPositionColor;
            if (_lastSphereCheckPos != Vector3.zero)
            {
                Gizmos.DrawWireSphere(_lastSphereCheckPos, sphereCheckRadius);
            }
            
            // Draw results
            if (_lastCheckWasValid)
            {
                // Valid mantle - draw in green
                Gizmos.color = validMantleColor;
                
                if (_lastSurfaceHitPoint != Vector3.zero)
                {
                    Gizmos.DrawWireSphere(_lastSurfaceHitPoint, 0.15f);
                }
                
                if (_lastMantlePosition != Vector3.zero)
                {
                    Gizmos.DrawWireSphere(_lastMantlePosition, 0.2f);
                    
                    // Draw connection line
                    Gizmos.DrawLine(playerPos, _lastMantlePosition);
                    
                    // Draw capsule at final position
                    DrawWireCapsule(_lastMantlePosition, _capsuleHeight, _capsuleRadius);
                }
            }
            else
            {
                // Invalid mantle - draw in red
                Gizmos.color = invalidMantleColor;
                
                if (_lastSurfaceHitPoint != Vector3.zero)
                {
                    Gizmos.DrawWireSphere(_lastSurfaceHitPoint, 0.1f);
                }
            }
            
            // Draw sphere cast visualization
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Vector3 sphereCastStart = playerPos + playerForward * maxMantleDistance;
            Vector3 sphereCastEnd = sphereCastStart + Vector3.up * maxMantleHeight;
            Gizmos.DrawLine(sphereCastStart, sphereCastEnd);
        }
        
        void DrawWireCapsule(Vector3 position, float height, float radius)
        {
            Vector3 top = position + Vector3.up * (height * 0.5f - radius);
            Vector3 bottom = position - Vector3.up * (height * 0.5f - radius);
            
            // Draw spheres at top and bottom
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);
            
            // Draw connecting lines
            Vector3 right = Vector3.right * radius;
            Vector3 forward = Vector3.forward * radius;
            
            Gizmos.DrawLine(top + right, bottom + right);
            Gizmos.DrawLine(top - right, bottom - right);
            Gizmos.DrawLine(top + forward, bottom + forward);
            Gizmos.DrawLine(top - forward, bottom - forward);
        }
    }
    
    [System.Serializable]
    public struct MantleData
    {
        public bool isValid;
        public Vector3 mantlePosition;      // Final position where player will end up
        public Vector3 surfacePoint;        // The surface contact point
        public Vector3 surfaceNormal;       // Surface normal
        public float objectHeight;          // Height difference from player to surface
        public Collider surfaceCollider;    // The collider being mantled onto
        public Rigidbody physicsBody;       // For moving platforms (null if static)
    }
}