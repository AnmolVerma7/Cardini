// MovementModuleBase.cs (in Cardini.Motion namespace)
using UnityEngine;
using KinematicCharacterController;

namespace Cardini.Motion
{
    public abstract class MovementModuleBase : MonoBehaviour
    {
        protected CardiniController Controller { get; private set; }
        protected KinematicCharacterMotor Motor { get; private set; }
        protected InputBridge InputBridge { get; private set; }
        protected BaseLocomotionSettingsSO Settings { get; private set; }
        // Add IPlayerAnimator, EnvironmentScanner later

        public virtual void Initialize(CardiniController controller)
        {
            Controller = controller;
            Motor = controller.Motor; // Get from Controller
            InputBridge = controller.inputBridge; // Get from Controller
            Settings = controller.Settings; // Get from Controller
        }

        // Called by CardiniController to see if this module wants to take over
        public abstract bool CanEnterState();
        // Called by CardiniController when this module becomes active
        public abstract void OnEnterState();
        // Called by CardiniController when this module is deactivated
        public abstract void OnExitState();

        // KCC Interface methods to be implemented by concrete modules
        public abstract void UpdateRotation(ref Quaternion currentRotation, float deltaTime);
        public abstract void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime);
        public abstract void AfterCharacterUpdate(float deltaTime);
        public abstract void PostGroundingUpdate(float deltaTime);
        // public virtual void BeforeCharacterUpdate(float deltaTime) {} // Optional

        // What specific movement state this module represents
        public abstract CharacterMovementState AssociatedMovementState { get; }
        // Priority for activation if multiple modules CanEnterState
        public virtual int Priority { get { return 0; } } 
    }
}