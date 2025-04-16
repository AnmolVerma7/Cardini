using UnityEngine;

public class FreeLookOrientation : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;
    public Transform playerObj;
    public float rotationSpeed = 15f;

    private void Start()
    {
        // Lock cursor to center of screen and make it invisible
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Get the main camera's forward direction, flattened to horizontal plane
        Vector3 viewDir = Camera.main.transform.forward;
        viewDir.y = 0;
        viewDir.Normalize();
        
        // Update orientation forward to match camera direction
        orientation.forward = viewDir;
        
        // Rotate player model based on movement direction
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");
        
        if (horizontalInput != 0 || verticalInput != 0)
        {
            // Calculate move direction
            Vector3 inputDir = orientation.forward * verticalInput + orientation.right * horizontalInput;
            inputDir = inputDir.normalized;
            
            // Rotate player model to face movement direction
            if (inputDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(inputDir);
                playerObj.rotation = Quaternion.Slerp(playerObj.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
    }
}