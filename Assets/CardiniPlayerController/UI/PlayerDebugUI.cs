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
        [SerializeField] private AbilityManager abilityManager;
        [SerializeField] private TextMeshProUGUI abilityText;
        [SerializeField] private TextMeshProUGUI majorStateText;
        [SerializeField] private TextMeshProUGUI activeModuleText;
        [SerializeField] private TextMeshProUGUI movementStateText;
        [SerializeField] private TextMeshProUGUI groundingText;
        [SerializeField] private TextMeshProUGUI speedText;

        void Update()
        {
            if (playerController == null) return;

            if (majorStateText != null)
            {
                majorStateText.text = $"Major State: {playerController.CurrentMajorState}";
            }

            if (movementStateText != null)
            {
                // This already shows the sub-state from the active module
                movementStateText.text = $"Movement State: {playerController.CurrentMovementState}";
            }

            // Optional: Display active module name
            if (activeModuleText != null)
            {
                activeModuleText.text = $"Active Module: {playerController.ActiveModuleName}";
            }

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

            if (abilityText != null && abilityManager != null)
            {
                var currentAbility = abilityManager.CurrentlyEquippedAbility; // You'll need to add this property
                if (currentAbility != null)
                {
                    float cooldownProgress = abilityManager.GetAbilityCooldownProgress(currentAbility);
                    string cooldownStatus = cooldownProgress > 0 ? $" (CD: {(cooldownProgress * 100):F0}%)" : " (Ready)";
                    abilityText.text = $"Ability: {currentAbility.AbilityName}{cooldownStatus}";
                }
                else
                {
                    abilityText.text = "Ability: None";
                }
            }
        }
        
        
    }
}