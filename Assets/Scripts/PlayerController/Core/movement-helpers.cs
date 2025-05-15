using UnityEngine;

namespace Cardini.Motion // Added namespace
{
    /// <summary>
    /// Static helper class containing common movement-related calculations and physics checks
    /// used by various Cardini movement modules.
    /// </summary>
    public static class MovementHelpers
    {
        #region Ground & Slope Checks
        /// <summary>
        /// Checks if the player is currently on a slope steeper than flat ground but within the walkable limit.
        /// Assumes player pivot is at base (Y=0) and Capsule Collider Center Y is 1.
        /// </summary>
        /// <param name="transform">Player's transform (where Rigidbody/Collider are).</param>
        /// <param name="playerHeight">Collider height.</param>
        /// <param name="slopeHit">Outputs Raycast hit info for the slope.</param>
        /// <param name="whatIsGround">LayerMask for ground geometry.</param>
        /// <param name="maxSlopeAngle">Maximum walkable slope angle.</param>
        /// <returns>True if on a valid slope, false otherwise.</returns>
        public static bool IsOnSlope(Transform transform, float playerHeight, out RaycastHit slopeHit, LayerMask whatIsGround, float maxSlopeAngle)
        {
            if (transform == null) { slopeHit = default; return false; } // Safety check

            // Start the raycast slightly ABOVE the transform's origin (collider base)
            float startOffset = 0.1f;
            Vector3 origin = transform.position + Vector3.up * startOffset;
            float checkDistance = (playerHeight * 0.5f) + 0.3f + startOffset; // Check slightly further down than ground check

            #if UNITY_EDITOR
            // Visualize the ray in the Scene view during Play mode in the editor
            // Debug.DrawRay(origin, Vector3.down * checkDistance, Color.blue, 0f, false); // Set depthTest = false to see through objects
            #endif

            if (Physics.Raycast(origin, Vector3.down, out slopeHit, checkDistance, whatIsGround))
            {
                float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
                // A valid slope is not flat ground (angle > ~0) and below the max angle
                return angle < maxSlopeAngle && angle > 0.1f; // Use tolerance
            }
            return false;
        }

        /// <summary>
        /// Checks if the player is currently grounded using a SphereCast for robustness.
        /// Assumes player pivot is at the base (Y=0) and Capsule Collider Center Y is 1.
        /// </summary>
        /// <param name="transform">Player's transform (where Rigidbody/Collider are).</param>
        /// <param name="playerHeight">Collider height (Used indirectly via assumptions about collider setup).</param>
        /// <param name="whatIsGround">LayerMask for ground geometry.</param>
        /// <returns>True if grounded, false otherwise.</returns>
        public static bool IsGrounded(Transform transform, float playerHeight, LayerMask whatIsGround)
        {
            if (transform == null) return false; // Safety check

            // --- Revised SphereCast Version ---
            // These values should ideally match or be derived from the actual player collider settings
            float capsuleRadius = 0.5f; // Example: Match CardiniController Capsule Collider Radius
            float sphereRadius = capsuleRadius * 0.9f; // Sphere radius slightly smaller than capsule
            float groundedCheckDepth = 0.2f; // How far below the pivot (base of capsule) to check for ground

            // Start the sphere cast origin slightly ABOVE the pivot point so the sphere doesn't start intersecting the ground
            float startHeightOffset = sphereRadius + 0.01f; // Start sphere center just above the pivot
            Vector3 sphereOrigin = transform.position + Vector3.up * startHeightOffset;

            // Cast distance needs to cover the offset PLUS the desired check depth below the pivot
            float checkDistance = startHeightOffset + groundedCheckDepth;

            #if UNITY_EDITOR
            // Visualize the cast origin and direction (Line is easier than sphere in static context)
            // Debug.DrawLine(sphereOrigin, sphereOrigin + Vector3.down * checkDistance, Color.red, 0f, false);
            #endif

            // Perform the SphereCast
            return Physics.SphereCast(sphereOrigin, sphereRadius, Vector3.down, out RaycastHit hit, checkDistance, whatIsGround);
        }
        #endregion

        #region Input & Direction Calculations
        /// <summary>
        /// Calculates the movement direction projected onto the slope's plane.
        /// </summary>
        /// <param name="direction">Desired movement direction (e.g., from input).</param>
        /// <param name="slopeHit">RaycastHit info from the slope check.</param>
        /// <returns>Normalized direction vector parallel to the slope.</returns>
        public static Vector3 GetSlopeMoveDirection(Vector3 direction, RaycastHit slopeHit)
        {
            return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
        }

        /// <summary>
        /// Calculates the desired world-space movement direction based on input and orientation.
        /// </summary>
        /// <returns>Normalized movement direction vector (or Vector3.zero if orientation is null).</returns>
        public static Vector3 CalculateMoveDirection(Transform orientation, float horizontalInput, float verticalInput)
        {
            if (orientation == null) {
                Debug.LogError("MovementHelpers: Orientation transform is null!");
                return Vector3.zero;
            }
            // Combine orientation's forward/right (flattened horizontally) with input axes
            Vector3 forward = orientation.forward;
            Vector3 right = orientation.right;
            forward.y = 0f; // Ensure movement is horizontal relative to orientation
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 direction = forward * verticalInput + right * horizontalInput;
            return direction.normalized; // Return normalized vector
        }
        #endregion

        #region Jump Timing Helpers
        /// <summary>
        /// Updates the coyote time counter based on grounded state changes.
        /// </summary>
        /// <returns>The updated coyote time counter.</returns>
        public static float UpdateCoyoteTime(float coyoteTimeCounter, float coyoteTime, bool wasGrounded, bool isGrounded, bool didJumpThisTick)
        {
            if (didJumpThisTick) { return 0f; } // No coyote time if we just jumped
            if (wasGrounded && !isGrounded) { return coyoteTime; } // Start timer when leaving ground
            if (isGrounded) { return coyoteTime; } // Reset timer when grounded
            else { return Mathf.Max(0f, coyoteTimeCounter - Time.deltaTime); } // Count down in air
        }

        /// <summary>
        /// Updates the jump buffer counter based on jump input.
        /// </summary>
        /// <returns>The updated jump buffer counter.</returns>
        public static float UpdateJumpBuffer(float jumpBufferCounter, float jumpBufferTime, bool jumpPressed)
        {
            // If pressed, start/reset buffer
            if (jumpPressed) { return jumpBufferTime; }
            // Otherwise, count down timer
            else { return Mathf.Max(0f, jumpBufferCounter - Time.deltaTime); }
        }
        #endregion

        #region Step Handling
        /// <summary>
        /// Attempts to automatically step the player up small obstacles using raycasts.
        /// </summary>
        /// <param name="rb">Player's Rigidbody.</param>
        /// <param name="moveDirection">Current world-space movement direction.</param>
        /// <param name="maxStepHeight">Maximum height the player can step up.</param>
        /// <param name="stepSmooth">Smoothing factor for the upward movement (higher = faster snap).</param>
        public static void AutoStep(Rigidbody rb, Vector3 moveDirection, float maxStepHeight, float stepSmooth)
        {
            if (rb == null || moveDirection.magnitude < 0.1f) return;

            float stepCheckDistForward = 0.6f; // How far ahead to check for the step
            float stepCheckDistSlightlyAhead = 0.1f; // Small offset for upper/downward checks
            float stepHeightBuffer = 0.1f; // Extra height for checks
            float stepUpOffset = 0.05f; // How far above the detected step surface to place the player
            float lowerRayOriginOffset = 0.05f; // Start lower ray slightly above the absolute base

            // 1. Check for obstacle near feet
            Vector3 lowerOrigin = rb.position + Vector3.up * lowerRayOriginOffset;
            if (Physics.Raycast(lowerOrigin, moveDirection, out RaycastHit hitLower, stepCheckDistForward))
            {
                // 2. Check for clearance above obstacle
                Vector3 upperOrigin = lowerOrigin + Vector3.up * maxStepHeight;
                if (!Physics.Raycast(upperOrigin, moveDirection, stepCheckDistForward + stepCheckDistSlightlyAhead))
                {
                    // 3. Check downwards from ahead to find the step surface
                    Vector3 stepCheckOrigin = rb.position + Vector3.up * (maxStepHeight + stepHeightBuffer) + moveDirection * stepCheckDistSlightlyAhead;
                    if (Physics.Raycast(stepCheckOrigin, Vector3.down, out RaycastHit hitUpper, maxStepHeight + stepHeightBuffer * 2))
                    {
                        // Optional: Could add a slope check for hitUpper.normal here

                        // Step Up: Move player smoothly to just above the step surface
                        Vector3 targetPosition = new Vector3(rb.position.x, hitUpper.point.y + stepUpOffset, rb.position.z);
                        // Use MovePosition for smoother interpolation during physics step
                        // Lerp factor adjusted for Time.fixedDeltaTime and desired smoothness
                        rb.MovePosition(Vector3.Lerp(rb.position, targetPosition, stepSmooth * 10f * Time.fixedDeltaTime));
                    }
                }
            }
        }
        #endregion

        #region Wall Detection
        /// <summary>
        /// Performs raycasts to detect walls to the left and right of the player.
        /// </summary>
        /// <returns>True if either a left or right wall is detected within range.</returns>
        public static bool CheckForWall(Transform transform, Transform orientation, float wallCheckDistance, LayerMask whatIsWall,
                                out RaycastHit leftWallHit, out RaycastHit rightWallHit)
        {
            // Initialize out parameters
            leftWallHit = default;
            rightWallHit = default;

            if (transform == null || orientation == null) return false; // Safety check

            Vector3 pos = transform.position; // Use the main transform's position
            Vector3 right = orientation.right;
            Vector3 left = -right;

            bool foundR = Physics.Raycast(pos, right, out rightWallHit, wallCheckDistance, whatIsWall);
            bool foundL = Physics.Raycast(pos, left, out leftWallHit, wallCheckDistance, whatIsWall);

            #if UNITY_EDITOR
            // Visualize wall checks
            // Debug.DrawRay(pos, right * wallCheckDistance, foundR ? Color.green : Color.red);
            // Debug.DrawRay(pos, left * wallCheckDistance, foundL ? Color.green : Color.red);
            #endif

            return foundL || foundR;
        }
        #endregion

        #region Utility
        /// <summary>
        /// Helper method to round a float value to a specific number of decimal places.
        /// </summary>
        public static float Round(float value, int digits)
        {
            float mult = Mathf.Pow(10.0f, (float)digits);
            return Mathf.Round(value * mult) / mult;
        }
        #endregion
    }
}