using UnityEngine;
using Cardini.Motion; // Ensure namespace is used

public class FreeLookOrientation : MonoBehaviour
{
    [Header("REFERENCES (Auto-acquired if not assigned)")]
    [Tooltip("The child 'Orientation' empty GameObject. This transform dictates the forward/right for movement input.")]
    [SerializeField] private Transform orientation;
    [Tooltip("The child visual model GameObject (e.g., the one with the Animator).")]
    [SerializeField] private Transform playerObj;
    [Tooltip("Reference to the InputHandler on this GameObject.")]
    [SerializeField] private InputHandler input;

    // Add CardiniController reference
    private CardiniController controller;

    [Header("ROTATION SETTINGS")]
    [Tooltip("How quickly the visual model (PlayerObj) rotates to face its target direction (movement or camera). Higher = faster.")]
    [SerializeField] private float modelRotationSpeed = 15f;
    [Tooltip("How quickly the 'Orientation' transform snaps to align with the camera's horizontal view. Higher = faster.")]
    [SerializeField] private float orientationAlignSpeed = 20f; // Renamed from orientationRotationSpeed

    [Header("MOVEMENT STYLE")]
    [SerializeField, Tooltip("If TRUE, the visual model rotates to face the direction of movement input. " +
                             "If FALSE, the model faces the same direction as the camera's horizontal view (strafe mode).")]
    private bool characterRotatesWithMovementInput = true; // Renamed for clarity

    // Public accessor for other scripts (like BaseLocomotionModule) to query the rotation mode
    public bool ShouldCharacterRotateWithMovementInput() => characterRotatesWithMovementInput;

    // Private state
    private Camera mainCamera;
    private bool _isInitialized = false; // Flag to ensure LateUpdate only runs after successful init

    private void Awake() // Changed to Awake for earlier component fetching
    {
        // Attempt to get components robustly
        if (input == null) input = GetComponentInParent<InputHandler>(); // GetComponentInParent more flexible if on a child
        controller = GetComponentInParent<CardiniController>(); // Add this line
        if (orientation == null)
        {
            orientation = transform.Find("Orientation");
            if (orientation == null) Debug.LogWarning("FreeLookOrientation: Child 'Orientation' not found automatically. Please assign in Inspector or ensure it's a direct child named 'Orientation'.", this);
        }
        if (playerObj == null)
        {
            // Try to find a child with an Animator, assuming that's the player model
            Animator playerAnimator = GetComponentInChildren<Animator>(true); // true to include inactive
            if (playerAnimator != null)
            {
                playerObj = playerAnimator.transform;
            }
            else
            {
                Debug.LogWarning("FreeLookOrientation: Player model (child with Animator) not found automatically. Please assign 'PlayerObj' in Inspector.", this);
            }
        }

        mainCamera = Camera.main;

        // Validate essential references
        if (input == null) { Debug.LogError("FreeLookOrientation: InputHandler reference is missing!", this); enabled = false; return; }
        if (controller == null) { Debug.LogError("FreeLookOrientation: CardiniController reference is missing!", this); enabled = false; return; } // Add controller validation
        if (orientation == null) { Debug.LogError("FreeLookOrientation: 'Orientation' transform reference is missing!", this); enabled = false; return; }
        if (playerObj == null) { Debug.LogError("FreeLookOrientation: 'PlayerObj' (visual model) transform reference is missing!", this); enabled = false; return; }
        if (mainCamera == null) { Debug.LogError("FreeLookOrientation: Main Camera not found in scene!", this); enabled = false; return; }

        _isInitialized = true; // All essential components found
        if (Application.isPlaying) // Only lock cursor in play mode
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        // Debug.Log("FreeLookOrientation initialized successfully.");
    }

    // Removed FindPlayerArmatureInChildren as Awake handles it more directly.

    private void LateUpdate()
    {
        // Ensure initialization was successful and all references are still valid
        if (!_isInitialized || input == null || orientation == null || playerObj == null || mainCamera == null) return;

        AlignOrientationToCameraView();

        if (controller.IsPlayerModelOrientationExternallyManaged) return;

        if (characterRotatesWithMovementInput)
        {
            RotateModelToMovementInputDirection();
        }
        else
        {
            RotateModelToFaceCameraOrientation();
        }
    }

    /// <summary>
    /// Aligns the 'Orientation' transform (movement reference) with the camera's horizontal forward direction.
    /// </summary>
    private void AlignOrientationToCameraView() // Renamed from RotateOrientationToCamera
    {
        // Use camera's forward, but flattened onto the XZ plane
        Vector3 cameraForwardHorizontal = mainCamera.transform.forward;
        cameraForwardHorizontal.y = 0f;

        if (cameraForwardHorizontal.sqrMagnitude > 0.001f) // Check for non-zero magnitude to avoid LookRotation errors
        {
            cameraForwardHorizontal.Normalize();
            Quaternion targetOrientationRotation = Quaternion.LookRotation(cameraForwardHorizontal, Vector3.up);
            orientation.rotation = Quaternion.Slerp(orientation.rotation, targetOrientationRotation, Time.deltaTime * orientationAlignSpeed);
        }
        // If camera is looking straight up/down, orientation might not update, which is usually fine.
    }

    /// <summary>
    /// Rotates the 'PlayerObj' (visual model) to face the direction of the current movement input,
    /// relative to the 'Orientation' transform.
    /// </summary>
    private void RotateModelToMovementInputDirection() // Renamed from RotateModelToActualMovementInput
    {
        float hInput = input.HorizontalInput;
        float vInput = input.VerticalInput;

        // Only rotate if there's significant movement input
        if (Mathf.Abs(hInput) > 0.01f || Mathf.Abs(vInput) > 0.01f)
        {
            // Calculate the world-space direction of the input based on the 'Orientation' transform
            Vector3 worldInputDirection = orientation.forward * vInput + orientation.right * hInput;
            // We only care about the horizontal direction for model rotation
            worldInputDirection.y = 0f;

            if (worldInputDirection.sqrMagnitude > 0.001f)
            {
                worldInputDirection.Normalize();
                Quaternion targetModelRotation = Quaternion.LookRotation(worldInputDirection, Vector3.up);
                playerObj.rotation = Quaternion.Slerp(playerObj.rotation, targetModelRotation, Time.deltaTime * modelRotationSpeed);
            }
        }
        // If no input, the model retains its last facing direction.
    }

    /// <summary>
    /// Rotates the 'PlayerObj' (visual model) to face the same horizontal direction as the 'Orientation' transform (strafe mode).
    /// </summary>
    private void RotateModelToFaceCameraOrientation() // Renamed from RotateModelToCameraOrientation
    {
        // Get the 'Orientation' transform's forward direction, flattened horizontally
        Vector3 orientationForwardHorizontal = orientation.forward;
        orientationForwardHorizontal.y = 0f;

        if (orientationForwardHorizontal.sqrMagnitude > 0.001f)
        {
            orientationForwardHorizontal.Normalize();
            Quaternion targetModelRotation = Quaternion.LookRotation(orientationForwardHorizontal, Vector3.up);
            playerObj.rotation = Quaternion.Slerp(playerObj.rotation, targetModelRotation, Time.deltaTime * modelRotationSpeed);
        }
    }
}