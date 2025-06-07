using UnityEngine;

namespace Cardini.Motion
{
    public class CardiniPlayer : MonoBehaviour
    {
        [Header("Component References")]
        public CardiniController characterController;
        public InputBridge inputBridge;
        public Camera mainCamera; 

        void Awake()
        {
            if (characterController == null) characterController = GetComponent<CardiniController>();
            if (inputBridge == null) inputBridge = GetComponent<InputBridge>();
            if (mainCamera == null) mainCamera = Camera.main;


            if (characterController == null) Debug.LogError("CardiniPlayer: CardiniController missing!", this);
            if (inputBridge == null) Debug.LogError("CardiniPlayer: InputBridge missing!", this);
            if (mainCamera == null) Debug.LogError("CardiniPlayer: Main Camera not found/assigned!", this);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            if (characterController == null || inputBridge == null || mainCamera == null) return;

            CardiniController.ControllerInputs ccInputs = new CardiniController.ControllerInputs();

            ccInputs.MoveAxes = inputBridge.MoveInput;
            ccInputs.CameraRotation = mainCamera.transform.rotation; // Use main camera's rotation
            ccInputs.JumpPressed = inputBridge.Jump.IsPressed;
            // Crouch inputs are now handled within CardiniController using InputBridge directly for toggle/hold

            characterController.SetControllerInputs(ref ccInputs);
        }

        void LateUpdate()
        {
            if (inputBridge != null)
            {
                inputBridge.ProcessButtonStatesFrameEnd();
            }
        }
    }
}