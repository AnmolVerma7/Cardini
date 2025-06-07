using UnityEngine;
using Cardini.Motion;

namespace Cardini.Motion
{
    public enum CharacterState
    {
        Locomotion,    // Normal movement (walking, sliding, wallrunning, etc.)
        Combat,        // Combat-specific state (attacking, blocking, etc.) - Placeholder
        Interaction,   // Interacting with objects, UI, NPCs, etc. - Placeholder
        Cutscene       // Player control suspended - Placeholder
                       // Add more states as needed
    }

    /// <summary>
    /// Defines the specific movement actions or states the character can be in.
    /// Used by CardiniController and MovementModules for detailed state tracking.
    /// </summary>
    public enum CharacterMovementState // This is the NEW enum for MOVEMENT states
    {
        None,        // Default or uninitialized
        Idle,
        Walking,
        Jogging,     // For your 3-tier system
        Sprinting,   // Your "Boost" tier
        Crouching,
        Jumping,
        Falling,
        Sliding,
        WallRunning, // Or will be replaced by StickyMovement
        WallJumping,
        StickyMovement,
        Teleporting
        // Add more specific parkour/action states as you develop them:
        // e.g., Mantling, Vaulting, LedgeGrabbing, etc.
    }
    public enum CardiniOrientationMethod { TowardsCamera, TowardsMovement }
    public enum CardiniBonusOrientationMethod { None, TowardsGravity, TowardsGroundSlopeAndGravity }
}