// AbilityManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Cardini.Motion.Abilities; // Assuming your IAbility implementations are here

namespace Cardini.Motion
{
    public class AbilityManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CardiniController cardiniController;
        [SerializeField] private InputBridge inputBridge;
        
        [Header("Radial Menu Configuration")]
        [Tooltip("The 'Radial Menu Name' set in the Inspector of your UltimateRadialMenu component.")]
        [SerializeField] private string abilityWheelName = "CardiniAbilityWheel";

        [Header("Ability Data")]
        [Tooltip("All abilities that can possibly be in the game.")]
        [SerializeField] private List<AbilitySO> allGameAbilities = new List<AbilitySO>();
        
        // Runtime Data
        private List<AbilitySO> _unlockedAbilities = new List<AbilitySO>();
        [SerializeField] private AbilitySO[] utilityWheelConfiguration = new AbilitySO[4]; 
        [SerializeField] private AbilitySO[] combatWheelConfiguration = new AbilitySO[4];  

        private IAbility _currentlyEquippedAbilityInstance;
        private AbilitySO _currentlyEquippedAbilityData;
        public AbilitySO CurrentlyEquippedAbility => _currentlyEquippedAbilityData; // For DebugUI
        private Dictionary<AbilitySO, float> _abilityCooldownTimers = new Dictionary<AbilitySO, float>();
        private AbilityType _currentWheelTypeBeingDisplayed = AbilityType.Utility; 

        void Awake()
        {
            // Ensure references
            if (cardiniController == null) cardiniController = GetComponentInParent<CardiniController>();
            if (inputBridge == null) inputBridge = GetComponentInParent<InputBridge>();
            if (UltimateRadialMenu.ReturnComponent(abilityWheelName) == null)
                Debug.LogError($"AbilityManager: URM '{abilityWheelName}' not found!", this);

            _unlockedAbilities.AddRange(allGameAbilities); 
            SetupInitialWheelConfiguration();
            
            // Ensure URM is disabled at start if it's not already handled by URM's settings
            UltimateRadialMenu.Disable(abilityWheelName); 
        }
        
        private void SetupInitialWheelConfiguration() // For testing
        {
            int uIndex = 0, cIndex = 0;
            foreach (var abilitySO in _unlockedAbilities)
            {
                if (abilitySO.Type == AbilityType.Utility && uIndex < utilityWheelConfiguration.Length)
                    utilityWheelConfiguration[uIndex++] = abilitySO;
                else if (abilitySO.Type == AbilityType.CombatAbility && cIndex < combatWheelConfiguration.Length)
                    combatWheelConfiguration[cIndex++] = abilitySO;
            }
        }

        public void SetAbilityWheelVisible(bool visible, AbilityType wheelTypeToShow)
        {
            if (visible)
            {
                _currentWheelTypeBeingDisplayed = wheelTypeToShow;
                if (!string.IsNullOrEmpty(abilityWheelName))
                {
                    PopulateRadialMenuWithConfiguredAbilities();
                    UltimateRadialMenu.Enable(abilityWheelName);
                    // Debug.Log($"Showing Radial Menu: {abilityWheelName} with {wheelTypeToShow} abilities.");
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(abilityWheelName))
                {
                    UltimateRadialMenu.Disable(abilityWheelName);
                    // Debug.Log($"Hiding Radial Menu: {abilityWheelName}");
                }
            }
        }

        void PopulateRadialMenuWithConfiguredAbilities()
        {
            if (string.IsNullOrEmpty(abilityWheelName)) return;
            UltimateRadialMenu.ClearMenu(abilityWheelName); 

            AbilitySO[] abilitiesToDisplay = (_currentWheelTypeBeingDisplayed == AbilityType.Utility) 
                                             ? utilityWheelConfiguration 
                                             : combatWheelConfiguration;

            foreach (AbilitySO abilitySO in abilitiesToDisplay)
            {
                if (abilitySO == null || !_unlockedAbilities.Contains(abilitySO)) continue;

                UltimateRadialButtonInfo buttonInfo = new UltimateRadialButtonInfo();
                {
                    buttonInfo.UpdateName(abilitySO.AbilityName); // Internal name for the button
                    // buttonInfo.name = abilitySO.AbilityName; // Display name in the wheel
                    buttonInfo.UpdateIcon(abilitySO.Icon);
                };
                buttonInfo.UpdateText(abilitySO.AbilityName);
               
                // buttonInfo.description = abilitySO.Description; // If URM supports it
                // buttonInfo.key = abilitySO.name; // If URM uses a 'key' for identification
                                
                UltimateRadialMenu.RegisterButton(abilityWheelName, 
                    () => HandleAbilitySelectedFromWheel(abilitySO), // This lambda is called on URM interaction
                    buttonInfo);
            }
        }

        // This method is now THE place where selection from wheel results in equipping.
        // Called by the lambda in RegisterButton (triggered by URM's internal click/release logic).
        private void HandleAbilitySelectedFromWheel(AbilitySO selectedAbilitySO)
        {
            // Debug.Log($"HandleAbilitySelectedFromWheel CALLED with: {selectedAbilitySO?.AbilityName ?? "null"}");
            if (selectedAbilitySO != null)
            {
                EquipAbility(selectedAbilitySO);

                // Automatically close wheel and return control after selection.
                if (cardiniController != null)
                {
                    // Tell CardiniController to go back to Locomotion and restore time.
                    // SetAbilityWheelVisible(false,...) will be called by CardiniController's HandleMajorStateInputs
                    // when AbilitySelect button is released.
                    // However, since selection *immediately* closes the wheel, we might do it here too.
                    cardiniController.RequestCloseAbilityWheel(); // New method in CardiniController
                }
            }
        }
    
        void Update()
        {
            // Only handle using equipped abilities if not in selection mode
            if (cardiniController.CurrentMajorState == CharacterState.Locomotion || 
                cardiniController.CurrentMajorState == CharacterState.Combat) 
            {
                HandleEquippedAbilityInput();
            }
            UpdateCooldowns(Time.deltaTime);
        }
        
        // This method is called by CardiniController when the AbilitySelect button is physically released by the player.
        // If "On Menu Release" is checked in URM's Input Manager, URM should have already triggered
        // the selection via the lambda in RegisterButton -> HandleAbilitySelectedFromWheel.
        // So, this method might just confirm the wheel should be hidden if it wasn't already by a direct click.
        public void ConfirmAbilitySelection() 
        {
            // Debug.Log("AbilityManager: ConfirmAbilitySelection (L1 released). Current Equipped: " + (CurrentlyEquippedAbility != null ? CurrentlyEquippedAbility.AbilityName : "None"));
            // The actual equipping is now handled by HandleAbilitySelectedFromWheel triggered by URM.
            // CardiniController will call SetAbilityWheelVisible(false) when L1 is released.
        }

        public void EquipAbility(AbilitySO abilityData)
        {
            if (_currentlyEquippedAbilityInstance != null)
            {
                _currentlyEquippedAbilityInstance.OnUnequip();
            }
            _currentlyEquippedAbilityData = abilityData; // This sets the public CurrentlyEquippedAbility
            // Debug.Log($"EquipAbility: _currentlyEquippedAbilityData set to {abilityData?.AbilityName ?? "null"}");

            if (_currentlyEquippedAbilityData != null)
            {
                _currentlyEquippedAbilityInstance = CreateAbilityInstance(abilityData);
                if (_currentlyEquippedAbilityInstance != null)
                {
                    _currentlyEquippedAbilityInstance.Initialize(abilityData, cardiniController);
                    _currentlyEquippedAbilityInstance.OnEquip();
                } else { /* Error already logged in CreateAbilityInstance */ }
            } else {
                _currentlyEquippedAbilityInstance = null;
            }
        }

        private IAbility CreateAbilityInstance(AbilitySO abilityData)
        {
            // Ensure the ability script is already a component on this GameObject or a child
            // This approach assumes abilities are pre-added as components.
            if (abilityData == null) return null;
            // Debug.Log($"CreateAbilityInstance for: {abilityData.AbilityName}");

            IAbility instance = null;
            if (abilityData.AbilityName == "Blink") // Example, use a more robust mapping later
            {
                instance = GetComponentInChildren<BlinkAbility>(true); // true to include inactive
                if (instance == null) Debug.LogError("BlinkAbility component not found!");
            }
            // else if (abilityData.AbilityName == "FlyMode")
            // {
            //    instance = GetComponentInChildren<FlyModeAbility>(true);
            //    if (instance == null) Debug.LogError("FlyModeAbility component not found!");
            // }
            
            if (instance == null) Debug.LogWarning($"No MonoBehaviour instance found for ability: {abilityData.AbilityName}");
            return instance; 
        }
        private void HandleEquippedAbilityInput()
        {
            // 1. Pre-checks
            if (_currentlyEquippedAbilityInstance == null || _currentlyEquippedAbilityData == null)
            {
                // No ability equipped, nothing to do.
                return; 
            }

            // Don't process ability use if wheel is open or not in a suitable game state
            if (cardiniController.CurrentMajorState == CharacterState.AbilitySelection || 
                (cardiniController.CurrentMajorState != CharacterState.Locomotion && 
                cardiniController.CurrentMajorState != CharacterState.Combat)) // Example: only allow in Locomotion or Combat
            {
                return;
            }

            // 2. Cooldown Check
            if (_abilityCooldownTimers.TryGetValue(_currentlyEquippedAbilityData, out float lastUsedTime))
            {
                if (Time.time < lastUsedTime + _currentlyEquippedAbilityData.CooldownDuration)
                {
                    // Debug.Log($"{_currentlyEquippedAbilityData.AbilityName} is ON COOLDOWN. Ends at: {lastUsedTime + _currentlyEquippedAbilityData.CooldownDuration:F2}, Now: {Time.time:F2}");
                    return; 
                }
            }
            
            // 3. Ability's Own Activation Check (e.g., enough mana, correct character sub-state)
            if (!_currentlyEquippedAbilityInstance.CanActivate())
            {
                // Debug.Log($"{_currentlyEquippedAbilityData.AbilityName} internal CanActivate() returned false.");
                return;
            }

            // 4. Handle Cancel Input (if ability is active and cancelable)
            bool cancelInputMade = false;
            if (_currentlyEquippedAbilityData.IsCancelable && _currentlyEquippedAbilityInstance.IsCurrentlyActive)
            {
                if (inputBridge.Crouch.IsPressed) // Assuming Crouch is the cancel button for now
                {
                    cancelInputMade = true;
                }
            }

            // 5. Pass input state to the ability
            _currentlyEquippedAbilityInstance.HandleInput(
                inputBridge.UseEquippedAbility, 
                cancelInputMade ? inputBridge.Crouch : new InputBridge.ButtonInputState() // Pass actual crouch state if cancelling
            );
            
            // 6. Determine if cooldown should be started based on this frame's input and ability type
            // This logic assumes the ability's HandleInput resulted in an "execution"
            bool executionShouldTriggerCooldown = false;
            var useButton = inputBridge.UseEquippedAbility;

            switch (_currentlyEquippedAbilityData.ActivationType)
            {
                case AbilityActivationType.OnPress: // Assuming Tap is like OnPress
                case AbilityActivationType.Consumable:
                    if (useButton.IsPressed) executionShouldTriggerCooldown = true;
                    break;
                case AbilityActivationType.OnRelease:
                    if (useButton.WasReleasedThisFrame) executionShouldTriggerCooldown = true;
                    break;
                case AbilityActivationType.OnHold:
                    // For 'OnHold' that activates *while held*, cooldown might start on release if it was active.
                    // This requires the ability to track if it *was* successfully doing its thing.
                    // A simpler 'OnHold' might be an ability that charges while held and fires on release (which is like OnRelease).
                    // If it's an ongoing effect, the ability itself might call StartCooldown when the hold ends.
                    // Let's assume for now that if an OnHold ability was active and is now released, it triggers cooldown.
                    if (useButton.WasReleasedThisFrame && _currentlyEquippedAbilityInstance.IsCurrentlyActive) // Check IsCurrentlyActive
                    {
                        executionShouldTriggerCooldown = true;
                    }
                    break;
                case AbilityActivationType.Toggle:
                    // Cooldown for toggle might start each time it's activated (toggled ON)
                    if (useButton.IsPressed && _currentlyEquippedAbilityInstance.IsCurrentlyActive) // Assuming IsCurrentlyActive is true AFTER a successful toggle ON
                    {
                        executionShouldTriggerCooldown = true;
                    }
                    break;
            }

            if (executionShouldTriggerCooldown)
            {
                // To prevent starting cooldown if the ability decided not to execute (e.g. target out of range)
                // it would be better if the ability itself confirms execution.
                // For now, this is a manager-level guess.
                // If an ability has an aiming phase and then executes, this might trigger cooldown too early (on aim start).
                // This is where an event from the ability: OnAbilityExecutedForCooldown?.Invoke(_data); is more robust.
                StartCooldown(_currentlyEquippedAbilityData);
            }
        }

        private void UpdateCooldowns(float deltaTime) {} // Placeholder for now
        public void StartCooldown(AbilitySO abilityData) 
        {
            if (abilityData == null) return;
            _abilityCooldownTimers[abilityData] = Time.time;
            Debug.Log($"{abilityData.AbilityName} is now on cooldown.");
        }
        public float GetAbilityCooldownProgress(AbilitySO abilityData) { /* As before */ return 0f; }
    }
}