using UnityEngine;

namespace Cardini.Motion // Added namespace
{
    /// <summary>
    /// Base class for modules that run concurrently with the main movement module
    /// to handle non-physics actions like aiming, charging, time effects, UI updates etc.
    /// They do not typically control physics directly but can request actions from the controller.
    /// Multiple OverlayModules can be active simultaneously.
    /// </summary>
    public abstract class OverlayModule : MonoBehaviour
    {
        protected CardiniController controller;
        public bool IsActive { get; protected set; }

        /// <summary>
        /// Called once by CardiniController during Awake.
        /// </summary>
        /// <param name="controller">The main CardiniController instance.</param>
        public virtual void Initialize(CardiniController controller)
        {
            this.controller = controller;
            if (this.controller == null) {
                 Debug.LogError($"{GetType().Name}: CardiniController reference provided to Initialize is null!", this);
            }
            IsActive = false;
        }

        /// <summary>
        /// Checked every frame by CardiniController. Should this overlay become active?
        /// Based on input holds, environmental checks, game state etc. Should be efficient.
        /// </summary>
        /// <returns>True if activation conditions are met, false otherwise.</returns>
        public abstract bool WantsToActivate();

        /// <summary>
        /// Called by CardiniController when the overlay becomes active (WantsToActivate returned true).
        /// Use for setup: enable UI elements, start time slow, visual effects, etc.
        /// </summary>
        public virtual void Activate()
        {
            if (IsActive) return; // Prevent double activation
            IsActive = true;
            #if UNITY_EDITOR // Example of conditional logging using controller property
            // if(controller != null && controller.ShowDebugLogs) Debug.Log($"<color=#90EE90>Overlay Activated:</color> Frame {Time.frameCount} | {this.GetType().Name}");
            #endif
        }

        /// <summary>
        /// Called by CardiniController when the overlay becomes inactive (WantsToActivate returned false or forced off).
        /// Use for cleanup: disable UI elements, restore time scale, reset internal flags.
        /// </summary>
        public virtual void Deactivate()
        {
            if (!IsActive) return; // Prevent double deactivation
            IsActive = false;
            #if UNITY_EDITOR
            // if(controller != null && controller.ShowDebugLogs) Debug.Log($"<color=#FFB6C1>Overlay Deactivated:</color> Frame {Time.frameCount} | {this.GetType().Name}");
            #endif
        }

        /// <summary>
        /// Called every frame by CardiniController *only while this overlay is active*.
        /// Use for ongoing logic: update aiming visuals, check for input release, manage effects duration.
        /// Can call controller.RequestOverlayAction() to trigger global events.
        /// </summary>
        public abstract void Tick();


        /// <summary>
        /// Ensures Deactivate is called if the GameObject is disabled externally while the overlay was active.
        /// </summary>
        protected virtual void OnDisable()
        {
            // If the object is disabled while the overlay was active, force deactivation logic
            if (IsActive)
            {
                // We call Deactivate directly to ensure any state cleanup happens.
                // CardiniController's ManageOverlayModules loop will naturally remove it
                // from the active list next frame if it checks WantsToActivate again.
                Deactivate();
            }
        }
    }
}