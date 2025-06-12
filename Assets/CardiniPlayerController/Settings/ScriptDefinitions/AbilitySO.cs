// AbilitySO.cs (in Cardini.Motion)
using UnityEngine;

namespace Cardini.Motion
{
    [CreateAssetMenu(fileName = "NewAbility", menuName = "Cardini/Ability Definition")]
    public class AbilitySO : ScriptableObject
    {
        public string AbilityName = "New Ability";
        [TextArea] public string Description = "Ability description.";
        public Sprite Icon; // For UI wheel
        public AbilityType Type = AbilityType.Utility;
        public AbilityActivationType ActivationType = AbilityActivationType.OnPress;

        public float CooldownDuration = 1f;
        // public float ResourceCost = 0f; // If you have mana/energy
        public bool IsCancelable = true;

        // Add more common fields if needed, e.g.:
        // public AnimationClip UseAnimation; // Generic use animation
        // public GameObject VfxOnActivate;
        // public AudioClip SfxOnActivate;
    }
}