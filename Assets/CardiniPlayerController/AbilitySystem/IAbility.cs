// IAbility.cs (in Cardini.Motion)
using UnityEngine; // For InputBridge access if needed

namespace Cardini.Motion
{
    public interface IAbility
    {
        // Initialize with its data and the player controller that owns/uses it
        void Initialize(AbilitySO abilityData, CardiniController ownerController);

        // Called by AbilityManager when this ability is equipped from the wheel
        void OnEquip();
        // Called by AbilityManager when this ability is unequipped
        void OnUnequip();

        // Called by AbilityManager to check if this ability can be activated
        // (checks cooldowns, resources, character state, etc.)
        bool CanActivate();

        // Called by AbilityManager every frame an ability is equipped,
        // passing the state of the 'use' and 'cancel' buttons.
        // The ability itself decides how to react based on its ActivationType.
        void HandleInput(InputBridge.ButtonInputState useButtonState,
                         InputBridge.ButtonInputState cancelButtonState); // Or pass full InputBridge

        // For abilities with an ongoing effect or aiming phase
        void UpdateAbility(float deltaTime);

        // For an ability to signal it's currently doing something (e.g., aiming, active effect)
        bool IsCurrentlyActive { get; }

        // For UI to show cooldown progress (0 = ready, 1 = full cooldown)
        float GetCooldownProgress(); 
        
        
        // Optional: If an ability needs to force its cancellation externally (e.g., player stunned)
        // void ForceCancel(); 
    }
}