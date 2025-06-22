// IPlayerAnimator.cs
// Ensure this is in your Cardini.Motion namespace
using UnityEngine;

namespace Cardini.Motion
{
    public interface IPlayerAnimator
    {
        void SetGrounded(bool isGrounded);
        void SetCrouching(bool isCrouching);
        void SetSliding(bool isSliding);
        // void SetWallRunning(bool isWallRunning, float wallRunDirection);

        /// <summary>
        /// Sets parameters for locomotion blend trees.
        /// </summary>
        /// <param name="normalizedSpeedOrTier">A value representing speed or movement tier.
        /// E.g., 0=idle, 1=walk, 2=jog, 3=sprint, for your "Normalize" parameter.</param>
        /// <param name="velocityX">Local X velocity (strafe) for 2D blend, normalized (-1 to 1).</param>
        /// <param name="velocityZ">Local Z velocity (forward/backward) for 2D blend, normalized (-1 to 1).</param>
        void SetLocomotionSpeeds(float normalizedSpeedOrTier, float velocityX, float velocityZ);

        void SetMovementState(CharacterMovementState movementState);
        void TriggerJump();
        void TriggerLand();
    }
}