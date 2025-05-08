using UnityEngine;
using Cardini.Motion;

/// <summary>
/// Manages teleport functionality including aim state, target detection,
/// visualization, and execution. Handles time dilation during targeting.
/// </summary>
[RequireComponent(typeof(TeleportTargetDetector))]
[RequireComponent(typeof(TeleportVisualizer))]
[RequireComponent(typeof(TeleportExecutor))]
[RequireComponent(typeof(TeleportChargeManager))]
public class TeleportOverlayModule : OverlayModule
{
    #region Settings
    [Header("Settings")]
    [SerializeField, Tooltip("ScriptableObject containing teleportation parameters")]
    private TeleportationSettings settings;
    #endregion

    #region Component References
    private TeleportTargetDetector targetDetector;
    private TeleportVisualizer visualizer;
    private TeleportExecutor executor;
    private TeleportChargeManager chargeManager;
    #endregion

    #region State
    private float originalTimeScale = 1f;
    private float originalFixedDeltaTime = 0.02f;
    #endregion

    public override void Initialize(CardiniController controller)
    {
        base.Initialize(controller);
        originalFixedDeltaTime = Time.fixedDeltaTime;

        targetDetector = GetComponent<TeleportTargetDetector>();
        visualizer = GetComponent<TeleportVisualizer>();
        executor = GetComponent<TeleportExecutor>();
        chargeManager = GetComponent<TeleportChargeManager>();

        bool setupValid = ValidateSetup(controller);

        if (!setupValid)
        {
            enabled = false;
            return;
        }

        InitializeSubComponents(controller);

        SubscribeToEvents();

        if (controller.ShowDebugLogs)
            Debug.Log("<color=purple>Teleport Overlay:</color> Initialized successfully.");
    }

    private bool ValidateSetup(CardiniController controller)
    {
        bool setupValid = true;

        if (targetDetector == null || visualizer == null || executor == null || chargeManager == null)
        {
            Debug.LogError($"TeleportOverlayModule on {gameObject.name}: Missing required Teleport component(s)! Add Detector, Visualizer, Executor, and ChargeManager.", gameObject);
            setupValid = false;
        }

        if (settings == null)
        {
            Debug.LogError($"TeleportOverlayModule on {gameObject.name}: TeleportationSettings asset not assigned in the inspector!", gameObject);
            setupValid = false;
        }

        if (controller.PlayerCamera == null || controller.Orientation == null || controller.PlayerObj == null || controller.Rb == null)
        {
            Debug.LogError($"TeleportOverlayModule on {gameObject.name}: CardiniController is missing required references (PlayerCamera, Orientation, PlayerObj, Rb). Assign them in the CardiniController inspector.", controller.gameObject);
            setupValid = false;
        }

        return setupValid;
    }

    private void InitializeSubComponents(CardiniController controller)
    {
        targetDetector.Initialize(settings, controller.transform, controller.PlayerCamera.transform, controller.Orientation);
        visualizer.Initialize(settings, controller.transform);
        executor.Initialize(settings, controller.transform, controller.PlayerObj, controller.PlayerCamera.transform, controller.Rb);
        chargeManager.Initialize(settings);
    }

    private void SubscribeToEvents()
    {
        targetDetector.OnTargetFound += HandleTargetFound;
        targetDetector.OnNoTargetFound += HandleNoTargetFound;
        executor.OnTeleportComplete += HandleTeleportationComplete;
    }

    public override bool WantsToActivate()
    {
        if (controller == null || controller.Input == null || chargeManager == null)
            return false;

        return controller.Input.TeleportAimHeld && chargeManager.HasCharges();
    }

    public override void Activate()
    {
        if (IsActive) return;
        base.Activate();

        originalTimeScale = Time.timeScale;

        if (settings != null && settings.timeSlowdownFactor < 0.99f)
        {
            Time.timeScale = settings.timeSlowdownFactor;
            Time.fixedDeltaTime = originalFixedDeltaTime * Time.timeScale;

            if (controller.ShowDebugLogs)
                Debug.Log($"<color=purple>Teleport Overlay:</color> Frame {Time.frameCount} | Activated. Time Scale set to {Time.timeScale:F2}");
        }
        else
        {
            if (controller.ShowDebugLogs)
                Debug.Log($"<color=purple>Teleport Overlay:</color> Frame {Time.frameCount} | Activated. (No time slowdown)");
        }

        if (visualizer != null) visualizer.enabled = true;
    }

    public override void Deactivate()
    {
        if (!IsActive) return;
        base.Deactivate();

        if (Time.timeScale != originalTimeScale)
        {
            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDeltaTime;

            if (controller.ShowDebugLogs)
                Debug.Log($"<color=purple>Teleport Overlay:</color> Frame {Time.frameCount} | Deactivated. Time Scale restored to {Time.timeScale:F2}");
        }
        else
        {
            if (controller.ShowDebugLogs)
                Debug.Log($"<color=purple>Teleport Overlay:</color> Frame {Time.frameCount} | Deactivated. (Time scale was normal)");
        }

        if (visualizer != null)
        {
            visualizer.HideMarker();
            visualizer.enabled = false;
        }
    }

    public override void Tick()
    {
        if (!IsActive || targetDetector == null || chargeManager == null || controller == null || controller.Input == null) return;

        if (controller.Input.TeleportCancelPressed)
        {
            if (controller.ShowDebugLogs)
                Debug.Log($"<color=orange>Teleport Overlay:</color> Frame {Time.frameCount} | Cancel input detected! Requesting Deactivation.");

            Deactivate();
            return;
        }

        targetDetector.UpdateTarget();

        if (controller.Input.TeleportExecutePressed)
        {
            HandleExecuteInput();
        }
    }

    private void HandleExecuteInput()
    {
        if (targetDetector.HasValidTarget && chargeManager.HasCharges())
        {
            if (controller.ShowDebugLogs)
                Debug.Log($"<color=purple>Teleport Overlay:</color> Frame {Time.frameCount} | Execute input detected! Requesting Teleport action.");

            controller.RequestOverlayAction(this, "Teleport", targetDetector.TargetPosition, targetDetector.IsLedgeTarget);
        }
        else
        {
            if (controller.ShowDebugLogs)
            {
                if (!targetDetector.HasValidTarget)
                    Debug.Log($"<color=orange>Teleport Overlay:</color> Frame {Time.frameCount} | Execute input, but no valid target.");
                else if (!chargeManager.HasCharges())
                    Debug.Log($"<color=orange>Teleport Overlay:</color> Frame {Time.frameCount} | Execute input, but no charges available.");
            }
        }
    }

    private void HandleTargetFound(Vector3 position, bool isLedge, Vector3 surfaceNormal)
    {
        if (visualizer != null && visualizer.enabled)
        {
            visualizer.ShowMarker(position, isLedge, surfaceNormal);
        }
    }

    private void HandleNoTargetFound()
    {
        if (visualizer != null && visualizer.enabled)
        {
            visualizer.HideMarker();
        }
    }

    private void HandleTeleportationComplete()
    {
        if (controller.ShowDebugLogs)
            Debug.Log($"<color=purple>Teleport Overlay:</color> Frame {Time.frameCount} | Received Teleport Complete event from Executor.");
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (targetDetector != null)
        {
            targetDetector.OnTargetFound -= HandleTargetFound;
            targetDetector.OnNoTargetFound -= HandleNoTargetFound;
        }

        if (executor != null)
        {
            executor.OnTeleportComplete -= HandleTeleportationComplete;
        }
    }
}