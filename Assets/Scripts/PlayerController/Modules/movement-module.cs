using UnityEngine;

namespace Cardini.Motion // Added namespace
{
    /// <summary>
    /// Base abstract class for all distinct movement states (e.g., Locomotion, Slide, WallRun).
    /// Only one MovementModule can be active at a time, managed by CardiniController based on Priority.
    /// </summary>
    // Modules often interact with the Rigidbody via the controller, adding RequireComponent clarifies dependency.
    [RequireComponent(typeof(Rigidbody))]
    public abstract class MovementModule : MonoBehaviour
    {
        protected CardiniController controller;

        /// <summary>
        /// The priority level of this module. Higher values take precedence in activation checks.
        /// Base Locomotion should typically be 0 or low. Specialized actions higher.
        /// </summary>
        public virtual int Priority => 0; // Added default priority

        /// <summary>
        /// Called once by CardiniController during Awake to set up the reference.
        /// </summary>
        /// <param name="controller">The main CardiniController instance.</param>
        public virtual void Initialize(CardiniController controller)
        {
            this.controller = controller;
            // Basic check - CardiniController should validate its own components are present before calling this.
            if (this.controller == null) {
                 Debug.LogError($"{GetType().Name}: CardiniController reference provided to Initialize is null!", this);
            }
        }

        /// <summary>
        /// Checked every frame by CardiniController's Update loop. Should this module attempt to become active?
        /// Based on input and current character/world state. Should be quick and efficient.
        /// </summary>
        /// <returns>True if activation conditions are met, false otherwise.</returns>
        public abstract bool WantsToActivate();

        /// <summary>
        /// Called by CardiniController when this module becomes the active movement state.
        /// Use for initialization specific to the state's activation (e.g., reset timers, apply initial forces, set animator states).
        /// </summary>
        public abstract void Activate();

        /// <summary>
        /// Called by CardiniController when this module stops being the active movement state.
        /// Use for cleanup specific to the state's deactivation (e.g., stop coroutines, reset scale, reset animator states).
        /// </summary>
        public abstract void Deactivate();

        /// <summary>
        /// Called every frame by CardiniController's Update loop (if controller is in Locomotion state).
        /// Use for non-physics logic, state updates, input checks, managing timers/cooldowns relevant while active OR inactive.
        /// </summary>
        public abstract void Tick();

        /// <summary>
        /// Called every physics step by CardiniController's FixedUpdate loop, *only* if this module is active
        /// (and controller is in Locomotion state). Use for applying forces, manipulating Rigidbody velocity/position.
        /// </summary>
        public abstract void FixedTick();
    }
}