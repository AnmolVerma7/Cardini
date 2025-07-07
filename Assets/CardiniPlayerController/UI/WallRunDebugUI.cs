using UnityEngine;
using TMPro;

namespace Cardini.Motion
{
    /// <summary>
    /// Simple debug UI for wall running information
    /// </summary>
    public class WallRunDebugUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WallRunModule wallRunModule;
        [SerializeField] private WallDetector wallDetector;
        
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI wallStatusText;
        [SerializeField] private TextMeshProUGUI wallRunInfoText;
        [SerializeField] private TextMeshProUGUI jumpDirectionText;
        
        [Header("Display Options")]
        public bool showWallStatus = true;
        public bool showWallRunInfo = true;
        public bool showJumpDirection = true;
        
        void Update()
        {
            UpdateWallStatus();
            UpdateWallRunInfo();
            UpdateJumpDirection();
        }
        
        void UpdateWallStatus()
        {
            if (!showWallStatus || wallStatusText == null || wallDetector == null) return;
            
            var wall = wallDetector.CurrentWall;
            
            if (wall.hasWall)
            {
                string sideText = wall.isLeftWall ? "LEFT" : "RIGHT";
                string qualityText = $"{wall.wallQuality:P0}";
                wallStatusText.text = $"Wall: {sideText} (Quality: {qualityText})";
                wallStatusText.color = Color.green;
            }
            else
            {
                wallStatusText.text = "Wall: NONE";
                wallStatusText.color = Color.gray;
            }
        }
        
        void UpdateWallRunInfo()
        {
            if (!showWallRunInfo || wallRunInfoText == null || wallRunModule == null) return;
            
            if (wallRunModule.IsWallRunning)
            {
                float timeLeft = wallRunModule.GetWallRunTimeRemaining();
                // float speedMult = wallRunModule.GetSpeedMultiplier();
                int jumpsLeft = wallRunModule.GetWallJumpsRemaining();
                
                wallRunInfoText.text = $"Wall Running\n" +
                                     $"Time: {timeLeft:F1}s\n" +
                                    //  $"Speed: {speedMult:P0}\n" +
                                     $"Jumps: {jumpsLeft}";
                wallRunInfoText.color = Color.cyan;
            }
            else
            {
                wallRunInfoText.text = "Not Wall Running";
                wallRunInfoText.color = Color.gray;
            }
        }
        
        void UpdateJumpDirection()
        {
            if (!showJumpDirection || jumpDirectionText == null || wallRunModule == null) return;
            
            if (wallRunModule.IsWallRunning && wallDetector != null && wallDetector.CurrentWall.hasWall)
            {
                // Get input and analyze what jump direction would be
                Vector3 input = Vector3.zero;
                
                // Try to get input from CardiniController if available
                var controller = wallRunModule.GetComponent<CardiniController>();
                if (controller != null)
                {
                    input = controller.MoveInputVector;
                }
                
                string directionInfo = GetJumpDirectionInfo(input, wallDetector.CurrentWall);
                jumpDirectionText.text = $"Jump Direction:\n{directionInfo}";
                jumpDirectionText.color = Color.yellow;
            }
            else
            {
                jumpDirectionText.text = "Jump Direction:\nN/A";
                jumpDirectionText.color = Color.gray;
            }
        }
        
        private string GetJumpDirectionInfo(Vector3 input, WallDetector.WallInfo wall)
        {
            if (input.magnitude < 0.1f)
            {
                return "No Input → Normal Jump";
            }
            
            Vector3 horizontalInput = Vector3.ProjectOnPlane(input, Vector3.up).normalized;
            float dotWithNormal = Vector3.Dot(horizontalInput, wall.wallNormal);
            float dotWithForward = Vector3.Dot(horizontalInput, wall.wallForward);
            
            // These thresholds should match the ones in WallRunModule
            float towardWallThreshold = -0.3f;
            float parallelWallThreshold = 0.7f;
            
            if (dotWithNormal < towardWallThreshold)
            {
                return "Toward Wall → Angled Jump";
            }
            else if (Mathf.Abs(dotWithForward) > parallelWallThreshold)
            {
                string direction = dotWithForward > 0 ? "Forward" : "Backward";
                return $"Parallel ({direction}) → Angled Jump";
            }
            else if (dotWithNormal >= 0)
            {
                return "Away from Wall → Direct Jump";
            }
            else
            {
                return "Diagonal → Direct Jump";
            }
        }
        
        // Auto-find components if not assigned
        void Awake()
        {
            if (wallRunModule == null)
                wallRunModule = FindObjectOfType<WallRunModule>();
                
            if (wallDetector == null)
                wallDetector = FindObjectOfType<WallDetector>();
        }
    }
}