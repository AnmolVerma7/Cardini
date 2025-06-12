// BlinkAbility.cs (in Cardini.Motion.Abilities namespace/folder)
using UnityEngine;
namespace Cardini.Motion.Abilities
{
    public class BlinkAbility : MonoBehaviour, IAbility
    {
        private AbilitySO _data;
        private CardiniController _owner;
        private bool _isActive = false; // For an instant ability, this might be true only for a frame

        public bool IsCurrentlyActive => _isActive;

        public void Initialize(AbilitySO abilityData, CardiniController ownerController)
        {
            _data = abilityData;
            _owner = ownerController;
        }
        public void OnEquip() { Debug.Log($"{_data.AbilityName} equipped!"); }
        public void OnUnequip() { Debug.Log($"{_data.AbilityName} unequipped!"); }

        public bool CanActivate()
        {
            // Check cooldown via AbilityManager, or if it manages its own
            // Check resources if any
            return true; // Simple for now
        }

        public void HandleInput(InputBridge.ButtonInputState useButtonState, InputBridge.ButtonInputState cancelButtonState)
        {
            if (useButtonState.IsPressed) // Blink on press
            {
                ActivateAndExecute();
            }
        }

        private void ActivateAndExecute()
        {
            if (!CanActivate()) return;
            _isActive = true; // For one frame perhaps
            Debug.Log($"Executing Blink! Target: forward by 5 units (placeholder)");
            Vector3 blinkTarget = _owner.Motor.TransientPosition + _owner.LookInputVector * 5f; // Simple forward blink
            _owner.Motor.SetPositionAndRotation(blinkTarget, _owner.Motor.TransientRotation);
            
            // If AbilityManager handles cooldowns centrally upon use confirmation
            // _owner.abilityManager.NotifyAbilityUsedAndStartCooldown(_data); 
            
            _isActive = false; // Instant ability
        }
        
        public void UpdateAbility(float deltaTime) { /* Not needed for instant blink */ }
        public float GetCooldownProgress() { return _owner.abilityManager.GetAbilityCooldownProgress(_data); } // Delegate to manager
    }
}