// AbilityManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For LINQ if needed later
using Cardini.Motion.Abilities; // Assuming your abilities are in this namespace
namespace Cardini.Motion
{
    public class AbilityManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CardiniController cardiniController;
        [SerializeField] private InputBridge inputBridge;
        // [SerializeField] private AbilityWheelUI abilityWheelUI; // For later

        [Header("Ability Configuration")]
        [Tooltip("All abilities known to the game. Player unlocks a subset of these.")]
        [SerializeField] private List<AbilitySO> allGameAbilities = new List<AbilitySO>();
        
        // Runtime Data
        private List<AbilitySO> _unlockedAbilities = new List<AbilitySO>(); // Abilities the player currently possesses
        
        // Example: Fixed size wheels for simplicity. Could be dynamic.
        private const int UTILITY_WHEEL_SIZE = 4; 
        private const int ABILITY_WHEEL_SIZE = 4;
        private AbilitySO[] _utilityWheelSlots = new AbilitySO[UTILITY_WHEEL_SIZE];
        private AbilitySO[] _abilityWheelSlots = new AbilitySO[ABILITY_WHEEL_SIZE];

        private IAbility _currentlyEquippedAbilityInstance;
        private AbilitySO _currentlyEquippedAbilityData;
        private bool _isAbilityWheelOpen = false;
        private AbilityType _currentWheelType = AbilityType.Utility; // Default to utility or last used

        // Cooldown tracking
        private Dictionary<AbilitySO, float> _abilityCooldownTimers = new Dictionary<AbilitySO, float>();

        void Awake()
        {
            if (cardiniController == null) cardiniController = GetComponentInParent<CardiniController>();
            if (inputBridge == null) inputBridge = GetComponentInParent<InputBridge>();
            // if (abilityWheelUI == null) abilityWheelUI = FindObjectOfType<AbilityWheelUI>(); // Or assign

            // For testing: Unlock all known abilities and populate wheels
            _unlockedAbilities.AddRange(allGameAbilities); 
            PopulateTestWheels();
        }

        void Update()
        {
            HandleAbilityWheelState();
            HandleEquippedAbilityInput();
            UpdateCooldowns(Time.deltaTime);
        }

        private void PopulateTestWheels() // Temporary for testing
        {
            int uSlot = 0;
            int aSlot = 0;
            foreach (var abSO in _unlockedAbilities)
            {
                if (abSO.Type == AbilityType.Utility && uSlot < UTILITY_WHEEL_SIZE)
                {
                    _utilityWheelSlots[uSlot++] = abSO;
                }
                else if (abSO.Type == AbilityType.CombatAbility && aSlot < ABILITY_WHEEL_SIZE)
                {
                    _abilityWheelSlots[aSlot++] = abSO;
                }
            }
            Debug.Log("Test wheels populated.");
        }


        private void HandleAbilityWheelState()
        {
            // This logic is now mostly in CardiniController.HandleMajorStateInputs
            // AbilityManager just needs to know when to act based on that state.
            
            _isAbilityWheelOpen = (cardiniController.CurrentMajorState == CharacterState.AbilitySelection);

            if (_isAbilityWheelOpen)
            {
                // TODO: Handle wheel navigation input from InputBridge.LookInput
                // Update a _highlightedAbilitySO based on LookInput and _currentWheelType
                // Tell abilityWheelUI to update its visuals.
                // Example: if (InputBridge.SomeButtonToSwitchWheelType.IsPressed) _currentWheelType = ...
            }
        }
        
        // Called by CardiniController when AbilitySelect is released
        public void ConfirmAbilitySelection() 
        {
            if (_isAbilityWheelOpen) // Should be true if called correctly
            {
                // AbilitySO selectedAbilitySO = abilityWheelUI.GetHighlightedAbility(); // Get from UI
                // For now, let's just pick the first available one for testing if UI not ready
                AbilitySO selectedAbilitySO = null;
                if (_currentWheelType == AbilityType.Utility && _utilityWheelSlots.Length > 0)
                    selectedAbilitySO = _utilityWheelSlots.FirstOrDefault(ab => ab != null);
                else if (_currentWheelType == AbilityType.CombatAbility && _abilityWheelSlots.Length > 0)
                    selectedAbilitySO = _abilityWheelSlots.FirstOrDefault(ab => ab != null);

                if (selectedAbilitySO != null)
                {
                    EquipAbility(selectedAbilitySO);
                }
                _isAbilityWheelOpen = false; // Redundant if CardiniController handles state change
            }
        }

        public void EquipAbility(AbilitySO abilityData)
        {
            if (_currentlyEquippedAbilityInstance != null)
            {
                _currentlyEquippedAbilityInstance.OnUnequip();
                // If abilities are MonoBehaviours added as components, you might Destroy or disable them.
                // For now, let's assume we can reuse/reinitialize or they are not persistent components.
            }

            _currentlyEquippedAbilityData = abilityData;
            if (_currentlyEquippedAbilityData != null)
            {
                // How to get the IAbility instance?
                // Option 1: Abilities are components on the player, find by type or tag.
                // Option 2: Instantiate a prefab associated with the AbilitySO.
                // Option 3: A factory pattern.
                // For now, let's assume we need a way to create/get the runtime instance.
                // This part needs more design based on how your abilities are structured.
                // Placeholder:
                _currentlyEquippedAbilityInstance = CreateAbilityInstance(abilityData);
                if (_currentlyEquippedAbilityInstance != null)
                {
                    _currentlyEquippedAbilityInstance.Initialize(abilityData, cardiniController);
                    _currentlyEquippedAbilityInstance.OnEquip();
                    Debug.Log($"Equipped: {abilityData.AbilityName}");
                }
                else
                {
                     Debug.LogError($"Could not create instance for {abilityData.AbilityName}");
                    _currentlyEquippedAbilityData = null;
                }
            }
            else
            {
                _currentlyEquippedAbilityInstance = null;
            }
        }

        // Placeholder for ability instantiation
        private IAbility CreateAbilityInstance(AbilitySO abilityData)
        {
            // Example: If your ability MonoBehaviours are named like "BlinkAbility"
            // and AbilitySO.AbilityName is "Blink"
            // You could use reflection, or a dictionary, or a component naming convention.
            // Simplest for now: if abilities are components already on the player GameObject:
            if (abilityData.AbilityName == "Blink") // Hardcoded for example
            {
                BlinkAbility blink = GetComponentInChildren<BlinkAbility>(true); // true to include inactive
                if (blink == null) blink = gameObject.AddComponent<BlinkAbility>(); // Add if not present
                return blink;
            }
            // if (abilityData.AbilityName == "FlyMode")
            // {
            //     FlyModeAbility fly = GetComponentInChildren<FlyModeAbility>(true);
            //     if (fly == null) fly = gameObject.AddComponent<FlyModeAbility>();
            //     return fly;
            // }
            // Add more cases or a better system
            return null; 
        }


        private void HandleEquippedAbilityInput()
        {
            if (_isAbilityWheelOpen || _currentlyEquippedAbilityInstance == null || cardiniController.CurrentMajorState != CharacterState.Locomotion) // Don't use abilities if wheel is open or not in locomotion
            {
                return;
            }

            // Check cooldown
            if (_abilityCooldownTimers.TryGetValue(_currentlyEquippedAbilityData, out float lastUsedTime))
            {
                if (Time.time < lastUsedTime + _currentlyEquippedAbilityData.CooldownDuration)
                {
                    // Still on cooldown
                    return; 
                }
            }
            
            if (_currentlyEquippedAbilityInstance.CanActivate()) // Check ability-specific conditions (resources, etc.)
            {
                 // Use crouch button as cancel if ability is cancelable and active
                bool cancelPressed = _currentlyEquippedAbilityData.IsCancelable && 
                                     _currentlyEquippedAbilityInstance.IsCurrentlyActive && 
                                     inputBridge.Crouch.IsPressed; 

                _currentlyEquippedAbilityInstance.HandleInput(inputBridge.UseEquippedAbility, 
                                                             cancelPressed ? inputBridge.Crouch : new InputBridge.ButtonInputState()); // Pass an empty state if not cancelling

                // If ability was used (e.g. on press, on release) and it's not a channelled/held one that manages its own cooldown start.
                // This needs refinement based on how abilities signal they've "fired".
                // For now, let's assume if UseEquippedAbility.IsPressed (for OnPress types) or WasReleasedThisFrame (for OnRelease types)
                // and the ability isn't "IsCurrentlyActive" for a hold, we start cooldown.
                // A better way: The ability itself calls a method on AbilityManager like "NotifyAbilityUsedAndStartCooldown()"
                // For simplicity, let's try a basic cooldown start here.
                if ((_currentlyEquippedAbilityData.ActivationType == AbilityActivationType.OnPress && inputBridge.UseEquippedAbility.IsPressed) ||
                    (_currentlyEquippedAbilityData.ActivationType == AbilityActivationType.OnRelease && inputBridge.UseEquippedAbility.WasReleasedThisFrame))
                {
                    StartCooldown(_currentlyEquippedAbilityData);
                }
            }
        }

        private void UpdateCooldowns(float deltaTime)
        {
            // This is just for a conceptual timer; actual cooldowns might be managed differently
            // For UI purposes, GetCooldownProgress() on IAbility is better.
        }

        public void StartCooldown(AbilitySO abilityData)
        {
            if (abilityData == null) return;
            _abilityCooldownTimers[abilityData] = Time.time;
            Debug.Log($"{abilityData.AbilityName} is now on cooldown.");
        }

        public float GetAbilityCooldownProgress(AbilitySO abilityData)
        {
            if (abilityData == null || abilityData.CooldownDuration <= 0f) return 0f; // Ready or no cooldown

            if (_abilityCooldownTimers.TryGetValue(abilityData, out float lastUsedTime))
            {
                float timeSinceUsed = Time.time - lastUsedTime;
                if (timeSinceUsed < abilityData.CooldownDuration)
                {
                    return 1f - (timeSinceUsed / abilityData.CooldownDuration); // Inverted: 1 = full cooldown, 0 = ready
                }
            }
            return 0f; // Ready
        }


        // Public method to be called by CardiniController when it exits AbilitySelection state
        public void OnAbilityWheelClosed()
        {
            // Assuming _highlightedAbilitySO was set during wheel navigation
            // For now, let's just log or equip a test ability
            // EquipAbility(_highlightedAbilitySO); // This would be the ideal call
        }
    }
}