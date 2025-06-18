// BaseLocomotionSettingsSO.cs
using UnityEngine;
namespace Cardini.Motion
{
    [CreateAssetMenu(fileName = "NewLocomotionSettings", menuName = "Cardini/Settings/Locomotion Settings")]
    public class BaseLocomotionSettingsSO : ScriptableObject
    {
        [Header("Capsule Dimensions")]
        public float DefaultCapsuleHeight = 2f; // Added this
        public float CrouchedCapsuleHeight = 1f;
        // public float CapsuleRadius = 0.5f; // KCC Motor has this, but good to be aware if you ever override it

        [Header("Input Options")]
        public bool UseToggleSprint = false;
        public bool UseToggleCrouch = false;
        [Range(0.0001f, 0.3f)] public float WalkThreshold = 0.2f;
        [Range(0.31f, 0.9f)] public float JogThreshold = 0.7f;

        [Header("Movement Speeds")]
        public float MaxWalkSpeed = 5f;
        public float MaxJogSpeed = 8f;
        public float MaxSprintSpeed = 12f;
        public float MaxCrouchSpeed = 3f;

        [Header("Ground Mechanics")]
        public float StableMovementSharpness = 15f;
        public float OrientationSharpness = 10f;
        public CardiniOrientationMethod OrientationMethod = CardiniOrientationMethod.TowardsMovement;
        [Header("Air Mechanics")]
        public float MaxAirMoveSpeed = 10f;
        public float AirAccelerationSpeed = 15f;
        public float Drag = 0.1f;

        [Header("Jumping")]
        public bool AllowJumpingWhenSliding = false;
        public float JumpUpSpeed_IdleWalk = 10f;
        public float JumpUpSpeed_Jog = 11f;
        public float JumpUpSpeed_Sprint = 12f;
        public float JumpScalableForwardSpeed_IdleWalk = 0f;
        public float JumpScalableForwardSpeed_Jog = 1f;
        public float JumpScalableForwardSpeed_Sprint = 2f;

        [Tooltip("How long before landing a jump input is accepted (Jump Buffering)")]
        public float JumpPreGroundingGraceTime = 0.1f;
        [Tooltip("How long after leaving ground a jump is still possible (Coyote Time)")]
        public float JumpPostGroundingGraceTime = 0.1f;

        [Header("Double Jumping")]
        public bool AllowDoubleJump = false; 
        public float DoubleJumpUpSpeed_IdleWalk = 8f; 
        public float DoubleJumpUpSpeed_Jog = 9f; 
        public float DoubleJumpUpSpeed_Sprint = 10f; 
        public float DoubleJumpScalableForwardSpeed_IdleWalk = 0f; 
        public float DoubleJumpScalableForwardSpeed_Jog = 1f; 
        public float DoubleJumpScalableForwardSpeed_Sprint = 2f; 

        [Header("Misc")]
        public Vector3 Gravity = new Vector3(0, -30f, 0);
    }
}