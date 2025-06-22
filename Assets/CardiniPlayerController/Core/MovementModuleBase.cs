// MovementModuleBase.cs
using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    public abstract class MovementModuleBase : MonoBehaviour
    {
        [System.Serializable]
        public struct ModuleConditions
        {
            [Tooltip("Requires the character to be on stable ground to enter this module.")]
            public bool RequireGrounded;
            [Tooltip("Requires the character to be NOT currently grounded (i.e., airborne) to enter this module.")]
            public bool RequireAirborne; // Added for flexibility for air modules
            [Tooltip("Requires the character to be sprinting to enter this module.")]
            public bool RequireSprint;
            [Tooltip("Requires the character to be NOT currently sprinting to enter this module.")]
            public bool BlockIfSprinting; // Added for flexibility for non-sprint modules
            [Tooltip("Requires the character's current horizontal speed to be at least this value to enter this module.")]
            public float MinSpeed;
            [Tooltip("Prevents this module from activating if the character is currently crouching (physical crouch state).")]
            public bool BlockIfCrouching;
        }
        // Properties to access core components, set during Initialize
        protected CardiniController Controller { get; private set; }
        protected KinematicCharacterMotor Motor { get; private set; }
        protected InputBridge InputBridge { get; private set; }
        protected BaseLocomotionSettingsSO Settings { get; private set; }
        protected IPlayerAnimator PlayerAnimator { get; private set; }

        // <--- ADD THIS FIELD START
        [Header("Module Conditions")]
        public ModuleConditions Conditions;

        /// <summary>
        /// If true, the CardiniController will not delegate velocity calculations to any module during KCC's UpdateVelocity.
        /// This module is then solely responsible for setting `currentVelocity` or expecting external forces.
        /// </summary>
        public virtual bool LocksVelocity { get { return false; } }

        /// <summary>
        /// If true, the CardiniController will not delegate rotation calculations to any module during KCC's UpdateRotation.
        /// This module is then solely responsible for setting `currentRotation` or expecting external logic.
        /// </summary>
        public virtual bool LocksRotation { get { return false; } }
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
        /// Provides common checks for CanEnterState based on ModuleConditions configured in the Inspector.
        /// </summary>
        protected bool CommonChecks()
        {
            if (Conditions.RequireGrounded && !Motor.GroundingStatus.IsStableOnGround) return false;
            if (Conditions.RequireAirborne && Motor.GroundingStatus.IsStableOnGround) return false;

            if (Conditions.RequireSprint && !Controller.IsSprinting) return false;
            if (Conditions.BlockIfSprinting && Controller.IsSprinting) return false;

            if (Conditions.MinSpeed > 0f && Controller.Motor.BaseVelocity.magnitude < Conditions.MinSpeed) return false;
            if (Conditions.BlockIfCrouching && Controller.IsCrouching) return false;

            return true;
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