// PlayerDebugUI.cs
using UnityEngine;
using TMPro; // Make sure to add this for TextMeshPro
using Cardini.Motion; // Ensure this matches your actual namespace for CardiniController
namespace Cardini.UI
{
    public class PlayerDebugUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CardiniController playerController;
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private TextMeshProUGUI groundingText;
        [SerializeField] private TextMeshProUGUI speedText;

        void Update()
        {
            if (playerController == null || stateText == null) return;

            // Display the current movement state enum as a string
            stateText.text = $"State: {playerController.CurrentMovementState}";

            // Enhanced ground status info
            string groundInfo;
            var status = playerController.Motor.GroundingStatus;
            
            if (status.IsStableOnGround)
            {
                float groundAngle = Vector3.Angle(Vector3.up, status.GroundNormal);
                if (groundAngle > 0.1f)
                {
                    groundInfo = $"On Slope ({groundAngle:F1}°)";
                }
                else
                {
                    groundInfo = "Grounded (Flat)";
                }
            }
            else if (status.FoundAnyGround)
            {
                groundInfo = "Sliding (Unstable)";
            }
            else
            {
                // Enhanced airborne info with vertical velocity
                Vector3 velocity = playerController.Motor.Velocity;
                float verticalSpeed = velocity.y;
                groundInfo = $"Airborne (Vertical: {verticalSpeed:F2} m/s)";
            }

            groundingText.text = $"Status: {groundInfo}";

            // Display the current horizontal and vertical speed
            Vector3 velocity2D = new Vector3(playerController.Motor.Velocity.x, 0f, playerController.Motor.Velocity.z);
            float horizontalSpeed = velocity2D.magnitude;
            speedText.text = $"Ground Speed: {horizontalSpeed:F2} m/s";
        }
    }
}