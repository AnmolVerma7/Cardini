using UnityEngine;
using Cardini.Motion; // Ensure namespace is used

public class FreeLookOrientation : MonoBehaviour
{
    [Header("References (Assign in Inspector)")]
    [Tooltip("The child 'Orientation' empty GameObject.")]
    [SerializeField] private Transform orientation; // Assign the child "Orientation" object here
    [Tooltip("The child visual model GameObject (e.g., PlayerArmature).")]
    [SerializeField] private Transform playerObj; // Assign the child "PlayerArmature" object here
    [Tooltip("Reference to the InputHandler on this GameObject.")]
    [SerializeField] private InputHandler input; // Assign the InputHandler on this "Player" object

    [Header("Settings")]
    [Tooltip("How quickly the visual model rotates to face movement direction.")]
    [SerializeField] private float modelRotationSpeed = 15f; // Renamed for clarity
    [Tooltip("How quickly the orientation snaps to the camera's direction.")]
    [SerializeField] private float orientationRotationSpeed = 20f; // Added speed for orientation

    // Private state
    private Camera mainCamera; // Declare the camera variable

    private void Start()
    {
        // Attempt to get components if not assigned in Inspector
        if (input == null) input = GetComponent<InputHandler>();
        if (orientation == null) orientation = transform.Find("Orientation");
        if (playerObj == null) playerObj = FindPlayerArmatureInChildren(transform);

        // Cache camera
        mainCamera = Camera.main;

        // Validate essential references
        bool error = false;
        if (input == null) { Debug.LogError("FreeLookOrientation: InputHandler missing!", this); error = true; }
        if (orientation == null) { Debug.LogError("FreeLookOrientation: Child 'Orientation' missing!", this); error = true; }
        if (playerObj == null) { Debug.LogError("FreeLookOrientation: Child player model ('PlayerArmature') missing!", this); error = true; }
        if (mainCamera == null) { Debug.LogError("FreeLookOrientation: Main Camera not found!", this); error = true; }
        if (error) { enabled = false; return; }

        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("FreeLookOrientation initialized."); // Removed InputHandler reference here
    }

    // Helper to find the armature more robustly if not assigned
    private Transform FindPlayerArmatureInChildren(Transform parent)
    {
        Animator animator = parent.GetComponentInChildren<Animator>();
        return animator?.transform; // Return the transform of the object with the Animator
    }

    private void LateUpdate() // Run after Camera updates (like Cinemachine)
    {
        // Exit if references are missing (checked in Start, but safety first)
        if (input == null || orientation == null || playerObj == null || mainCamera == null) return;

        // --- 1. Rotate Orientation based on Camera ---
        RotateOrientationToCamera();

        // --- 2. Rotate Player Model based on Movement Input ---
        RotateModelToMovement(); // Keep original behaviour for now
    }

    private void RotateOrientationToCamera()
    {
        Vector3 viewDir = mainCamera.transform.forward;
        viewDir.y = 0;
        if (viewDir.sqrMagnitude > 0.01f)
        {
            viewDir.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(viewDir, Vector3.up);

            // --- SMOOTHED ROTATION ---
            // Use the dedicated orientationRotationSpeed field
            orientation.rotation = Quaternion.Slerp(orientation.rotation, targetRotation, Time.deltaTime * orientationRotationSpeed);
            // --- END SMOOTHED ROTATION ---
        }
    }

    private void RotateModelToMovement()
    {
        float hInput = input.HorizontalInput;
        float vInput = input.VerticalInput;

        if (hInput != 0 || vInput != 0)
        {
            // Use the (now updated) orientation's frame of reference
            Vector3 inputDir = orientation.forward * vInput + orientation.right * hInput;
            inputDir.Normalize();

            if (inputDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(inputDir, Vector3.up);
                // Use the modelRotationSpeed field
                playerObj.rotation = Quaternion.Slerp(playerObj.rotation, targetRotation, Time.deltaTime * modelRotationSpeed);
            }
        }
        // If no input, model retains its last rotation relative to the parent Orientation
    }
}