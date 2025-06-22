// Enhanced Input Context System
using UnityEngine;

namespace Cardini.Motion
{
    /// <summary>
    /// Enhanced input context system for managing complex input states, timing, and toggle logic.
    /// Supports slide initiation, cancellation, and state validation.
    /// </summary>
    [System.Serializable]
    public class InputContext
    {
        [Header("Movement Input")]
        public Vector2 MoveAxes;
        public Vector3 MoveInputVector;
        public Vector3 LookInputVector;

        [Header("Action States")]
        public ActionInputState Jump = new ActionInputState();
        public ActionInputState Sprint = new ActionInputState();
        public ActionInputState Crouch = new ActionInputState();

        [Header("Derived States")]
        public bool IsSprinting;
        public bool ShouldBeCrouching;
        public bool IsMoving => MoveInputVector.sqrMagnitude > 0.01f;

        [Header("Toggle States")]
        public bool SprintToggleActive;
        public bool CrouchToggleActive;

        [Header("Slide Context")]
        public SlideInputContext Slide = new SlideInputContext();

        public void Reset()
        {
            Jump.Reset();
            Sprint.Reset();
            Crouch.Reset();
            Slide.Reset();
        }
    }

    [System.Serializable]
    public class ActionInputState
    {
        [Header("Raw Input")]
        public bool Pressed;
        public bool Held;
        public bool Released;

        [Header("Timing")]
        public float TimeSincePressed;
        public float TimeSinceReleased;
        public float HoldDuration;

        [Header("State")]
        public bool WasConsumed;

        public void UpdateTiming(float deltaTime)
        {
            if (Held)
            {
                HoldDuration += deltaTime;
                TimeSincePressed += deltaTime;
            }
            else
            {
                TimeSinceReleased += deltaTime;
                HoldDuration = 0f;
            }

            if (Pressed)
            {
                TimeSincePressed = 0f;
            }

            if (Released)
            {
                TimeSinceReleased = 0f;
            }
        }

        public void Consume()
        {
            WasConsumed = true;
        }

        public void Reset()
        {
            Pressed = false;
            Held = false;
            Released = false;
            WasConsumed = false;
            TimeSincePressed = Mathf.Infinity;
            TimeSinceReleased = Mathf.Infinity;
            HoldDuration = 0f;
        }
    }

    [System.Serializable]
    public class SlideInputContext
    {
        [Header("Slide Requests")]
        public bool InitiationRequested;
        public bool CancelRequested;

        [Header("Slide Timing")]
        public float MinHoldTimeForSlide = 0.001f; // Configurable minimum hold time
        public float TimeSinceSlideRequest;

        [Header("Slide Conditions")]
        public bool CanInitiateSlide;
        public bool IsSlideActive;

        public void Reset()
        {
            InitiationRequested = false;
            CancelRequested = false;
            TimeSinceSlideRequest = Mathf.Infinity;
            CanInitiateSlide = false;
        }

        public void UpdateTiming(float deltaTime)
        {
            TimeSinceSlideRequest += deltaTime;
        }
    }
}