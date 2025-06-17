// StateTransitionSO.cs
using UnityEngine;
using System.Collections.Generic;
// using System.Linq; // No longer strictly needed for this file, but can keep if other parts of the namespace use it.

namespace Cardini.Motion
{
    [CreateAssetMenu(fileName = "NewMovementStateTransitions", menuName = "Cardini/Movement/Movement State Transitions")]
    public class StateTransitionSO : ScriptableObject
    {
        [System.Serializable]
        public class AllowedTransitionEntry
        {
            public CharacterMovementState FromState;
            public List<CharacterMovementState> ToStates;
        }

        [Tooltip("Define allowed transitions from a 'From State' to a list of 'To States'.\n" +
                 "If a 'From State' is not listed, it is assumed all transitions from it are allowed.\n" +
                 "If a 'To State' list is empty, it means no transitions from that 'From State' are allowed except to itself.")]
        public List<AllowedTransitionEntry> AllowedTransitions = new List<AllowedTransitionEntry>();

        private Dictionary<CharacterMovementState, HashSet<CharacterMovementState>> _transitionMap;

        /// <summary>
        /// Populates the internal dictionary for quick lookup. Call this after inspector changes.
        /// </summary>
        public void Initialize()
        {
            _transitionMap = new Dictionary<CharacterMovementState, HashSet<CharacterMovementState>>();
            foreach (var entry in AllowedTransitions)
            {
                _transitionMap[entry.FromState] = new HashSet<CharacterMovementState>(entry.ToStates);
            }
        }

        /// <summary>
        /// Checks if a transition from currentFromState to potentialToState is explicitly allowed.
        /// If currentFromState is not defined in the matrix, it defaults to allowed.
        /// If currentFromState is defined but potentialToState is not in its list, it's blocked.
        /// </summary>
        public bool IsAllowed(CharacterMovementState currentFromState, CharacterMovementState potentialToState)
        {
            // If the fromState is not in our map, it means we don't have explicit rules for it,
            // so we assume all transitions from it are allowed by default.
            if (!_transitionMap.ContainsKey(currentFromState))
            {
                return true; // All transitions allowed if no specific rule exists for 'FromState'
            }

            // If the fromState is in our map, check if the toState is explicitly allowed.
            // Staying in the same state is always allowed.
            if (currentFromState == potentialToState)
            {
                return true;
            }

            return _transitionMap[currentFromState].Contains(potentialToState);
        }

        private void OnEnable()
        {
            // Called when the ScriptableObject is loaded or enabled in the editor/runtime
            Initialize();
        }

        private void OnValidate()
        {
            // Called in editor when a value is changed in the inspector
            Initialize();
        }
    }
}