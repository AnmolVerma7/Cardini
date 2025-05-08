namespace Cardini.Motion
{
    /// <summary>
    /// High-level states for the character controller, allowing separation
    /// between major modes like locomotion, combat, or specific interactions.
    /// </summary>
    public enum CharacterState
    {
        Locomotion,    // Normal movement (walking, sliding, wallrunning, etc.)
        Combat,        // Combat-specific state (attacking, blocking, etc.) - Placeholder
        Interaction,   // Interacting with objects, UI, NPCs, etc. - Placeholder
        Cutscene       // Player control suspended - Placeholder
        // Add more states as needed
    }
}