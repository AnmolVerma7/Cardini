using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;
using Cardini.Motion; // Make sure this using directive is present

namespace Cardini.Motion
{
    /// <summary>
    /// The main controller coordinating movement modules, overlay modules, input, and physics state.
    /// Acts as the central hub for the character controller system. Designed for flexibility.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(InputHandler))]
    public class CardiniController : MonoBehaviour
    {
        #region Inspector Fields
        [Header("CORE REFERENCES")]
        [Tooltip("Player's forward orientation, usually follows camera.")]
        [SerializeField] private Transform orientation;
        [Tooltip("The visual player object (for scaling/visual effects). Must contain the Animator.")]
        [SerializeField] private Transform playerObj;
        [Tooltip("Player's Rigidbody component (Should be on this GameObject).")]
        [SerializeField] private Rigidbody rb;
        [Tooltip("InputHandler component (Should be on this GameObject).")]
        [SerializeField] private InputHandler input;
        [Tooltip("Reference to the player's main camera (Needed for camera-relative logic, overlays).")]
        [SerializeField] private Camera playerCamera;

        [Header("PHYSICS SETTINGS")]
        [Tooltip("Logical height of the player used for ground/slope checks. Should generally match collider height.")]
        [SerializeField] private float playerHeight = 2f;
        [Tooltip("Logical radius of the player used for ground checks (SphereCast). Should generally match collider radius.")]
        [SerializeField] private float playerRadius = 0.5f;
        [Tooltip("Layers considered 'Ground' for physics checks.")]
        [SerializeField] private LayerMask whatIsGround = 1;
        [Tooltip("Maximum slope angle (degrees) the player can stand on and move up.")]
        [SerializeField] private float maxSlopeAngle = 45f;

        [Header("MODULES")]
        [Tooltip("Assign all MovementModule components via the Inspector here. List order doesn't strictly matter if Priorities are set correctly.")]
        [SerializeField] private List<MovementModule> attachedMovementModules = new List<MovementModule>();
        [Tooltip("Assign all OverlayModule components via the Inspector here.")]
        [SerializeField] private List<OverlayModule> attachedOverlayModules = new List<OverlayModule>();

        [Header("SPEED CONTROL")]
        [SerializeField] private bool enforceSpeedLimits = true;
        [SerializeField] private float defaultSpeedLimitFallback = 10f;
        [SerializeField] private bool showSpeedLimitLogs = false;

        [Header("DEBUGGING")]
        [SerializeField] private bool showDebugLogs = true;
        [SerializeField] private TextMeshProUGUI text_speed;
        [SerializeField] private TextMeshProUGUI text_mode;
        [SerializeField] private TextMeshProUGUI text_overlays;
        #endregion

        #region Public Accessors
        public Rigidbody Rb => rb;
        public InputHandler Input => input;
        public Transform Orientation => orientation;
        public Transform PlayerObj => playerObj;
        public Camera PlayerCamera => playerCamera;
        public LayerMask WhatIsGround => whatIsGround;
        public bool ShowDebugLogs => showDebugLogs;
        public float PlayerHeight => playerHeight;
        public float PlayerRadius => playerRadius;
        public float MaxSlopeAngle => maxSlopeAngle;
        #endregion

        #region Runtime State
        public MovementModule activeMovementModule { get; private set; }
        private List<OverlayModule> activeOverlayModules = new List<OverlayModule>();
        private List<OverlayModule> overlaysToActivate = new List<OverlayModule>();
        private List<OverlayModule> overlaysToDeactivate = new List<OverlayModule>();
        public bool IsGrounded { get; private set; }
        public bool WasGrounded { get; private set; }
        public bool IsOnSlope { get; private set; }
        public RaycastHit SlopeHit { get; private set; }
        public string currentMovementState { get; private set; } = "idle";
        public CharacterState CurrentState { get; private set; } = CharacterState.Locomotion;
        public bool ForceCrouchStateOnNextBaseLocoActivation { get; set; }
        private Vector3 velocityForDisplay;
        // Derived State Helpers
        public bool IsWallRunning => activeMovementModule is WallRunModule; // Check module type directly
        public bool IsSliding => activeMovementModule is SlideModule;
        public bool IsCrouching => (activeMovementModule is BaseLocomotionModule baseLoco && baseLoco.IsCurrentlyCrouching); // Use the new public property
        public bool IsSprinting => (activeMovementModule is BaseLocomotionModule baseLocoSprint && baseLocoSprint.IsCurrentlySprinting());
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            // Attempt to get core components immediately
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (input == null) input = GetComponent<InputHandler>();

            ValidateCoreReferences(); // Validate required components are present
            InitializeModules();      // Find, sort, and initialize Movement Modules
            InitializeOverlays();     // Find and initialize Overlay Modules

            ForceCrouchStateOnNextBaseLocoActivation = false;
            CurrentState = CharacterState.Locomotion; // Start in standard locomotion
        }

        void Start()
        {
            if (Rb != null) { Rb.freezeRotation = true; }
            else { Debug.LogError("CardiniController: Rigidbody missing on Start! Disabling.", this); enabled = false; return; }

            // Determine and activate the initial movement module
            DetermineActiveMovementModule(false); // Run silently on start
            if (activeMovementModule == null)
            {
                ActivateFallbackModule(); // Try to activate BaseLocomotion if nothing else started
            }
        }

        void Update()
        {
            bool log = ShowDebugLogs; // Cache log flag for this frame's scope

            // --- Primary State Logic ---
            if (CurrentState == CharacterState.Locomotion)
            {
                UpdateWorldState();          // Check grounded, slope, etc.
                TickMovementModules(log);    // Update all module internal logic/timers
                DetermineActiveMovementModule(log); // Decide which module should be active
            }
            else { /* Logic for Combat, Interaction, Cutscene states */ }

            // --- Overlay Management ---
            ManageOverlayModules(log); // Handle concurrent overlays

            // --- UI Update ---
            UpdateUI(); // Update debug displays
        }

        void FixedUpdate()
        {
            // Apply physics based on the active module and state
            if (CurrentState == CharacterState.Locomotion && activeMovementModule != null)
            {
                activeMovementModule.FixedTick(); // Let the active module apply forces
                if (enforceSpeedLimits) { EnforceSpeedLimits(); } // Clamp speed if needed
            }
            else
            {
                // Apply basic drag if no module is active or not in locomotion state
                if (Rb != null) Rb.linearDamping = IsGrounded ? 5f : 0;
            }

            // Store velocity for UI display
            if (Rb != null) velocityForDisplay = Rb.linearVelocity;
        }
        #endregion

        #region Initialization
        private void InitializeModules()
        {
            // Filter nulls from the Inspector list AND sort by Priority
            attachedMovementModules = attachedMovementModules
                .Where(m => m != null) // Ensure no null slots from Inspector break things
                .OrderByDescending(m => m.Priority)
                .ToList();

            bool log = ShowDebugLogs;
            if (log) Debug.Log($"--- Initializing {attachedMovementModules.Count} Movement Modules from Inspector (Sorted by Priority) ---", this);
            for (int i = 0; i < attachedMovementModules.Count; i++)
            {
                var module = attachedMovementModules[i];
                // Initialize should have happened within the loop if module wasn't null
                module.Initialize(this); // Initialize modules from the filtered/sorted list
                if (log) Debug.Log($"[{i}] {module.GetType().Name} (Priority: {module.Priority})", module);
            }
            if (log) Debug.Log($"--- Movement Module Initialization Complete ---", this);

            // The check for BaseLoco should still work on the potentially empty list
            if (!attachedMovementModules.OfType<BaseLocomotionModule>().Any() && log)
            {
                Debug.LogWarning("CardiniController: BaseLocomotionModule not found in attached module list!", this);
            }
        }

        private void InitializeOverlays()
        {
            // Filter nulls from the Inspector list
            attachedOverlayModules = attachedOverlayModules.Where(o => o != null).ToList();

            // Initialize only the modules assigned in the Inspector
            foreach (var module in attachedOverlayModules)
            {
                module.Initialize(this);
            }
            if (ShowDebugLogs) Debug.Log($"CardiniController: Initialized {attachedOverlayModules.Count} Overlay Modules from Inspector list.", this);
        }

        private void ValidateCoreReferences()
        {
            bool abort = false;
            if (Rb == null) { Debug.LogError("CardiniController: Rigidbody component missing!", this); abort = true; }
            if (Input == null) { Debug.LogError("CardiniController: InputHandler component missing!", this); abort = true; }
            if (Orientation == null) { Debug.LogError("CardiniController: Orientation reference missing!", this); abort = true; }
            if (PlayerObj == null) { Debug.LogError("CardiniController: PlayerObj reference missing!", this); abort = true; }
            if (PlayerCamera == null && ShowDebugLogs) { Debug.LogWarning("CardiniController: PlayerCamera reference missing (optional).", this); }
            if (abort) { Debug.LogError("CARDINI CONTROLLER VALIDATION FAILED - DISABLING", this); enabled = false; }
        }

        private void ActivateFallbackModule()
        {
            var baseModule = attachedMovementModules.OfType<BaseLocomotionModule>().FirstOrDefault();
            if (baseModule != null) {
                activeMovementModule = baseModule;
                activeMovementModule.Activate();
                if (ShowDebugLogs) Debug.Log($"CardiniController: Defaulting to initial module: {activeMovementModule.GetType().Name}");
            } else if (ShowDebugLogs) {
                Debug.LogWarning("CardiniController: No active module set on Start and BaseLocomotionModule not found!", this);
            }
        }
        #endregion

        #region State & Module Management
        private void UpdateWorldState()
        {
            bool previousGrounded = IsGrounded;
            IsGrounded = MovementHelpers.IsGrounded(transform, PlayerHeight, WhatIsGround);
            RaycastHit slopeHit;
            IsOnSlope = MovementHelpers.IsOnSlope(transform, PlayerHeight, out slopeHit, WhatIsGround, MaxSlopeAngle);
            SlopeHit = slopeHit;
            // if (IsGrounded != previousGrounded && ShowDebugLogs) { Debug.Log($"<color=brown>Grounded State Changed:</color> Frame {Time.frameCount} | {previousGrounded} -> {IsGrounded}"); }
            WasGrounded = previousGrounded;
        }

        private void TickMovementModules(bool log)
        { foreach (var module in attachedMovementModules) { if (module != null) module.Tick(); } }

        private void DetermineActiveMovementModule(bool log)
        {
            MovementModule desiredModule = null; // What module *should* be active based on trigger conditions?
            int desiredPriority = -1;

            // 1. Check all modules for activation triggers (highest priority wins)
            // List is pre-sorted by priority descending
            foreach (var module in attachedMovementModules)
            {
                if (module != null && module.WantsToActivate())
                {
                    // Found the highest priority module that wants to START now
                    desiredModule = module;
                    desiredPriority = module.Priority;
                    // if (log) Debug.Log($"<color=purple>Cardini.Determine TriggerCheck:</color> Frame {Time.frameCount} | {module.GetType().Name} (P:{module.Priority}) WantsToActivate=True.");
                    break;
                }
            }

            // 2. Decide if a switch should happen
            // - If a module triggered activation (desiredModule != null):
            //      - If it's different from the current active one AND has higher or equal priority, switch to it.
            // - If NO module triggered activation (desiredModule == null):
            //      - Keep the current active module running (it handles its own deactivation via Tick/Request).
            //      - Unless the current active module is null, then activate the fallback (BaseLoco).

            MovementModule moduleToSet = activeMovementModule; // Start by assuming we keep the current one

            if (desiredModule != null) // A module met its trigger conditions this frame
                {
                // Allow switch if:
                // - No module is currently active OR
                // - The desired module is different from the active one AND has higher or equal priority
                //   (Equal priority allows switching e.g., from BaseLoco to Slide)
                if (activeMovementModule == null ||
                    (desiredModule != activeMovementModule && desiredPriority >= (activeMovementModule?.Priority ?? -1)))
                    {
                        moduleToSet = desiredModule; // Target the newly triggered module
                    }
                // Else: A module triggered, but it's the same as the current one or has lower priority. Keep current.
            }
            else // No module met its trigger conditions this frame
            {
                // If nothing is active, try to activate the fallback (BaseLoco)
                if (activeMovementModule == null)
                    {
                        moduleToSet = attachedMovementModules.OfType<BaseLocomotionModule>().FirstOrDefault();
                            if (moduleToSet == null && log) Debug.LogWarning($"<color=red>Cardini.Determine:</color> Frame {Time.frameCount} | No trigger and no BaseLoco fallback found!");
                    }
                // Else: Keep the currently active module running. It will deactivate itself when ready.
                // moduleToSet remains = activeMovementModule
            }


            // 3. Perform the switch if needed
            if (moduleToSet != activeMovementModule)
            {
                MovementModule previouslyActive = activeMovementModule;
                // if (log) Debug.Log($"<color=cyan>CardiniController:</color> Frame {Time.frameCount} | Switching Module: {(previouslyActive != null ? previouslyActive.GetType().Name : "None")} -> {(moduleToSet != null ? moduleToSet.GetType().Name : "None")}");

                if (activeMovementModule != null) activeMovementModule.Deactivate();
                activeMovementModule = moduleToSet;
                if (activeMovementModule != null) activeMovementModule.Activate();
                else currentMovementState = IsGrounded ? "idle" : "air"; // Fallback if ends up null
            }
        }


        private void ManageOverlayModules(bool log) // Kept logic, added null check clarity
        {
            overlaysToActivate.Clear(); overlaysToDeactivate.Clear();
            // Check activation conditions from the Inspector list
            foreach (var overlay in attachedOverlayModules) { if (overlay != null && !overlay.IsActive && overlay.WantsToActivate()) { overlaysToActivate.Add(overlay); } }
            // Activate pending
            foreach (var overlay in overlaysToActivate) { if (!activeOverlayModules.Contains(overlay)) { activeOverlayModules.Add(overlay); overlay.Activate(); } }
            // Tick active and check for deactivation
            for (int i = activeOverlayModules.Count - 1; i >= 0; i--) { var overlay = activeOverlayModules[i]; if (overlay == null) { activeOverlayModules.RemoveAt(i); continue; } overlay.Tick(); if (!overlay.WantsToActivate()) { overlaysToDeactivate.Add(overlay); } }
            // Deactivate pending
            foreach (var overlay in overlaysToDeactivate) { if (activeOverlayModules.Contains(overlay)) { overlay.Deactivate(); activeOverlayModules.Remove(overlay); } }
        }
        #endregion

        #region Public Methods
        public void SetMovementState(string state)
        { if (currentMovementState != state) { currentMovementState = state; } }

        public void RequestMovementModuleDeactivation(MovementModule module)
        { if (activeMovementModule == module) { bool log = ShowDebugLogs; if(log) Debug.Log($"<color=cyan>CardiniController:</color> Frame {Time.frameCount} | Module {module?.GetType().Name ?? "NULL"} requested deactivation."); activeMovementModule.Deactivate(); activeMovementModule = null; } else if (ShowDebugLogs) { Debug.LogWarning($"CardiniController: Module {module?.GetType().Name ?? "NULL"} requested deactivation but was not active (Current: {activeMovementModule?.GetType().Name ?? "None"})."); } }

        public void RequestOverlayAction(OverlayModule requester, string actionType, object data = null, object data2 = null)
        { bool log = ShowDebugLogs; if (log) Debug.Log($"<color=yellow>Cardini Action Request:</color> Frame {Time.frameCount} | Overlay {requester?.GetType().Name ?? "NULL"} -> Action: {actionType}"); switch (actionType) { case "Teleport": HandleTeleportAction(requester, data, data2); break; case "StartDash": if (log) Debug.Log($"<color=yellow>Dash Action:</color> Received (Needs specific implementation)"); break; default: Debug.LogWarning($"CardiniController: Unhandled overlay action type: {actionType}"); break; } }

        public void SetState(CharacterState newState)
        { if (CurrentState == newState) return; bool log = ShowDebugLogs; if (log) Debug.Log($"<color=magenta>Cardini State Machine:</color> Frame {Time.frameCount} | State Change Request: {CurrentState} -> {newState}"); /* Add Exit/Entry Logic */ CurrentState = newState; }
        #endregion

        #region Action Handlers
        private void HandleTeleportAction(OverlayModule requester, object data, object data2)
        { if (data is Vector3 targetPos && data2 is bool isLedge) { if (requester != null) { if (activeOverlayModules.Contains(requester)) { requester.Deactivate(); activeOverlayModules.Remove(requester); } else if (requester.IsActive) { requester.Deactivate(); } } TeleportExecutor executor = GetComponent<TeleportExecutor>(); if (executor != null) { executor.ExecuteTeleport(targetPos, isLedge); } else { Debug.LogError("CardiniController: TeleportExecutor component missing!"); } } else { Debug.LogError($"CardiniController: Invalid data for Teleport action."); } }
        #endregion

        #region Physics Helpers
         private void EnforceSpeedLimits()
         { if (Rb == null) return; float limit = GetCurrentSpeedLimit(); if (limit <= 0) return; bool total = IsOnSlope; if (total) { if (Rb.linearVelocity.magnitude > limit) { Rb.linearVelocity = Rb.linearVelocity.normalized * limit; LogClamping("Slope", limit); } } else { Vector3 flat = new Vector3(Rb.linearVelocity.x, 0f, Rb.linearVelocity.z); if (flat.magnitude > limit) { Vector3 limFlat = flat.normalized * limit; Rb.linearVelocity = new Vector3(limFlat.x, Rb.linearVelocity.y, limFlat.z); LogClamping("Flat/Air", limit); } } }

        // Corrected LogClamping method
        private void LogClamping(string context, float limit)
        {
            if (ShowDebugLogs && showSpeedLimitLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"<color=yellow>SPEED CLAMP ({context}):</color> {limit:F1}");
            }
        }

        // Corrected GetCurrentSpeedLimit with pattern matching
        private float GetCurrentSpeedLimit()
        {
            if (activeMovementModule == null) return defaultSpeedLimitFallback;
            switch (activeMovementModule)
            {
                case BaseLocomotionModule b: return b.GetCurrentMoveSpeed();
                case SlideModule s: return s.GetCurrentMoveSpeed();
                case WallRunModule w: return w.GetCurrentMoveSpeed();
                // Add cases for other modules that define speed limits
                default: return defaultSpeedLimitFallback;
            }
        }
        #endregion

        #region UI Update
        private void UpdateUI() // Use StringBuilder for minor GC optimization if needed later
        { if (text_speed != null) { Vector3 v = velocityForDisplay; Vector3 flat = new Vector3(v.x,0,v.z); text_speed.SetText($"Speed: {(IsOnSlope ? v.magnitude : flat.magnitude):F1}"); } if (text_mode != null) { text_mode.SetText($"Mode: {currentMovementState}"); } if (text_overlays != null) { string names = activeOverlayModules.Any() ? string.Join(", ", activeOverlayModules.Select(o=>(o?.GetType().Name ?? "Null"))) : "None"; text_overlays.SetText($"Overlays: {names}");} }
        #endregion
    }
}

