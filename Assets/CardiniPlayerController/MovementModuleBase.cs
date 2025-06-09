// MovementModuleBase.cs
using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    public abstract class MovementModuleBase : MonoBehaviour
    {
        // Properties to access core components, set during Initialize
        protected CardiniController Controller { get; private set; }
        protected KinematicCharacterMotor Motor { get; private set; }
        protected InputBridge InputBridge { get; private set; }
        protected BaseLocomotionSettingsSO Settings { get; private set; }
        protected IPlayerAnimator PlayerAnimator { get; private set; }
        // We'll add EnvironmentScanner references here later

        /// <summary>
        /// Called by CardiniController to provide references to core components.
        /// </summary>
        public virtual void Initialize(CardiniController controller)
        {
            Controller = controller;
            Motor = controller.Motor;
            InputBridge = controller.inputBridge;
            Settings = controller.Settings;
            PlayerAnimator = controller.PlayerAnimator;
        }

        /// <summary>
        /// Called by CardiniController to determine if this module can/should become active.
        /// </summary>
        public abstract bool CanEnterState();

        /// <summary>
        /// Called by CardiniController when this module is activated.
        /// Use for one-time setup when entering the state.
        /// </summary>
        public abstract void OnEnterState();

        /// <summary>
        /// Called by CardiniController when this module is deactivated.
        /// Use for cleanup or final actions before exiting the state.
        /// </summary>
        public abstract void OnExitState();

        // KCC Interface methods that CardiniController will delegate to the active module
        public abstract void UpdateRotation(ref Quaternion currentRotation, float deltaTime);
        public abstract void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime);
        public abstract void AfterCharacterUpdate(float deltaTime);
        public abstract void PostGroundingUpdate(float deltaTime);
        
        // Optional, if some modules need it. Can be empty in base.
        public virtual void BeforeCharacterUpdate(float deltaTime) { } 

        /// <summary>
        /// The specific CharacterMovementState this module primarily represents.
        /// Used by CardiniController to update its overall debug state.
        /// </summary>
        public abstract CharacterMovementState AssociatedPrimaryMovementState { get; }
        
        /// <summary>
        /// Priority of this module. Higher numbers take precedence if multiple modules CanEnterState.
        /// </summary>
        public virtual int Priority { get { return 0; } } 
    }
}