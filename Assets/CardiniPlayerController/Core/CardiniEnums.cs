using UnityEngine;
using Cardini.Motion;

namespace Cardini.Motion
{
    public enum CharacterState
    {
        Locomotion, // Normal movement (walking, sliding, wallrunning, etc.)
        AbilitySelection,
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
        Walking,    // 1st tier of movement speed
        Jogging,     // 2nd tier of movement speed
        Sprinting,   // 3rd tier of movement speed
        Crouching,
        Sliding,
        Jumping,     // Regular Jump
        // DoubleJumping, // Double Jump
        Falling,
        WallRunning, // Or will be replaced by StickyMovement
        WallJumping,
        Teleporting,
        // Add more specific parkour/action states as you develop them:
        // e.g., Mantling, Vaulting, LedgeGrabbing, etc.
    }
    public enum CardiniOrientationMethod
    {
        TowardsCamera,
        TowardsMovement
    }

    public enum AbilityType
    {
        Utility,
        CombatAbility,
    }

    public enum AbilityActivationType
    {
        OnPress,          // Activate immediately on button press
        OnHold,           // Activate while button is held down
        OnRelease,        // Activate when button is released
        Toggle,           // Toggle activation state on button press
        Consumable     // Instant activation without button input (e.g., passive abilities)
    }
}