using System;
using UnityEngine;

/// <summary>
/// Teleportation system architecture overview - core system coordination class
/// </summary>
public class TeleportationSystem : MonoBehaviour
{
    #region Component References
    [Header("Teleportation Components")]
    [SerializeField] private TeleportChargeManager chargeManager;
    [SerializeField] private TeleportTargetDetector targetDetector;
    [SerializeField] private TeleportVisualizer visualizer;
    [SerializeField] private TeleportExecutor executor;
    
    [Header("Settings")]
    [SerializeField] public TeleportationSettings settings;
    
    [Header("Core References")]
    [SerializeField] private Transform orientation;
    [SerializeField] private Transform playerObj;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Rigidbody playerRigidbody;
    
    [Header("Controls")]
    [SerializeField] private KeyCode teleportationAimKey = KeyCode.Minus;
    [SerializeField] private KeyCode teleportationExecuteKey = KeyCode.E;
    [SerializeField] private KeyCode teleportationCancelKey = KeyCode.Mouse1;
    #endregion
    
    #region Events
    // Events for communication between components
    public event Action OnTeleportStartAiming;
    public event Action OnTeleportCancelled;
    public event Action OnTeleportExecuting;
    public event Action OnTeleportCompleted;
    public event Action<Vector3, bool> OnValidTargetFound;
    public event Action OnNoValidTargetFound;
    public event Action<int, int> OnChargesUpdated;
    #endregion
    
    #region State Management
    public enum TeleportState
    {
        Idle,
        Aiming,
        Executing,
        Cooldown
    }
    
    private TeleportState _currentState = TeleportState.Idle;
    public TeleportState CurrentState => _currentState;
    
    // Target info
    private bool _hasValidTarget = false;
    private Vector3 _targetPosition;
    private bool _isLedgeTarget = false;
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        ValidateComponents();
        InitializeComponents();
    }
    
    private void OnEnable()
    {
        RegisterEventHandlers();
    }
    
    private void OnDisable()
    {
        UnregisterEventHandlers();
    }
    
    private void Update()
    {
        HandleStateLogic();
        HandleInput();
    }
    #endregion
    
    #region Initialization
    private void ValidateComponents()
    {
        // Auto-create components if missing and log warnings
        if (chargeManager == null)
        {
            Debug.LogWarning("TeleportChargeManager not assigned, creating one automatically.");
            chargeManager = gameObject.AddComponent<TeleportChargeManager>();
        }
        
        if (targetDetector == null)
        {
            Debug.LogWarning("TeleportTargetDetector not assigned, creating one automatically.");
            targetDetector = gameObject.AddComponent<TeleportTargetDetector>();
        }
        
        if (visualizer == null)
        {
            Debug.LogWarning("TeleportVisualizer not assigned, creating one automatically.");
            visualizer = gameObject.AddComponent<TeleportVisualizer>();
        }
        
        if (executor == null)
        {
            Debug.LogWarning("TeleportExecutor not assigned, creating one automatically.");
            executor = gameObject.AddComponent<TeleportExecutor>();
        }
        
        if (settings == null)
        {
            Debug.LogError("TeleportationSettings not assigned. Teleportation system will not function correctly.");
        }
        
        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody>();
            if (playerRigidbody == null)
            {
                Debug.LogError("No Rigidbody found on this GameObject. Teleportation system requires a Rigidbody.");
            }
        }
    }
    
    private void InitializeComponents()
    {
        // Initialize components with required references
        chargeManager?.Initialize(settings);
        targetDetector?.Initialize(settings, transform, playerCamera, orientation);
        visualizer?.Initialize(settings, transform);
        executor?.Initialize(settings, transform, playerObj, playerCamera, playerRigidbody);
    }
    
    private void RegisterEventHandlers()
    {
        // Register to component events
        if (targetDetector != null)
        {
            targetDetector.OnTargetFound += HandleTargetFound;
            targetDetector.OnNoTargetFound += HandleNoTargetFound;
        }
        
        if (chargeManager != null)
        {
            chargeManager.OnChargesChanged += HandleChargesChanged;
        }
        
        if (executor != null)
        {
            executor.OnTeleportComplete += HandleTeleportComplete;
        }
    }
    
    private void UnregisterEventHandlers()
    {
        // Unregister from component events
        if (targetDetector != null)
        {
            targetDetector.OnTargetFound -= HandleTargetFound;
            targetDetector.OnNoTargetFound -= HandleNoTargetFound;
        }
        
        if (chargeManager != null)
        {
            chargeManager.OnChargesChanged -= HandleChargesChanged;
        }
        
        if (executor != null)
        {
            executor.OnTeleportComplete -= HandleTeleportComplete;
        }
    }
    #endregion
    
    #region State Logic
    private void ChangeState(TeleportState newState)
    {
        // Exit current state
        switch (_currentState)
        {
            case TeleportState.Aiming:
                visualizer?.HideMarker();
                Time.timeScale = 1f;
                break;
        }
        
        // Enter new state
        switch (newState)
        {
            case TeleportState.Aiming:
                OnTeleportStartAiming?.Invoke();
                Time.timeScale = settings.timeSlowdownFactor;
                break;
                
            case TeleportState.Executing:
                OnTeleportExecuting?.Invoke();
                ExecuteTeleport();
                break;
                
            case TeleportState.Idle:
                OnTeleportCancelled?.Invoke();
                break;
        }
        
        if (settings.enableDebugLogging)
        {
            Debug.Log($"Teleport state changed: {_currentState} → {newState}");
        }
        
        _currentState = newState;
    }
    
    private void HandleStateLogic()
    {
        switch (_currentState)
        {
            case TeleportState.Aiming:
                targetDetector?.UpdateTarget();
                break;
        }
    }
    
    private void HandleInput()
    {
        switch (_currentState)
        {
            case TeleportState.Idle:
                // Start aiming if we have charges
                if (Input.GetKeyDown(teleportationAimKey) && chargeManager.HasCharges())
                {
                    ChangeState(TeleportState.Aiming);
                }
                break;
                
            case TeleportState.Aiming:
                // Execute teleport if we have a valid target
                if (Input.GetKeyDown(teleportationExecuteKey) && _hasValidTarget)
                {
                    ChangeState(TeleportState.Executing);
                }
                
                // Cancel teleport
                if (Input.GetKeyUp(teleportationAimKey) || Input.GetKeyDown(teleportationCancelKey))
                {
                    ChangeState(TeleportState.Idle);
                }
                break;
        }
    }
    #endregion
    
    #region Event Handlers
    private void HandleTargetFound(Vector3 position, bool isLedge, Vector3 surfaceNormal)
    {
        Debug.Log($"Target found at: {position}");
        _hasValidTarget = true;
        _targetPosition = position;
        _isLedgeTarget = isLedge;
        
        visualizer?.ShowMarker(position, isLedge, surfaceNormal);
        OnValidTargetFound?.Invoke(position, isLedge);
    }
    
    private void HandleNoTargetFound()
    {
        _hasValidTarget = false;
        visualizer?.HideMarker();
        OnNoValidTargetFound?.Invoke();
    }
    
    private void HandleChargesChanged(int current, int max)
    {
        OnChargesUpdated?.Invoke(current, max);
    }
    
    private void HandleTeleportComplete()
    {
        ChangeState(TeleportState.Idle);
        OnTeleportCompleted?.Invoke();
    }
    #endregion
    
    #region Teleportation
    private void ExecuteTeleport()
    {
        if (!_hasValidTarget) return;
        
        // Consume a charge
        chargeManager?.UseCharge();
        
        // Execute the teleport
        executor?.ExecuteTeleport(_targetPosition, _isLedgeTarget);
    }
    #endregion
    
    #region Public API
    /// <summary>
    /// Force teleport to a specific position (used by other systems)
    /// </summary>
    public bool ForceTeleport(Vector3 position, bool isLedge = false)
    {
        if (!chargeManager.HasCharges()) return false;
        
        _targetPosition = position;
        _isLedgeTarget = isLedge;
        _hasValidTarget = true;
        
        ChangeState(TeleportState.Executing);
        return true;
    }
    
    /// <summary>
    /// Add charges to the teleportation system (pickup or ability)
    /// </summary>
    public void AddCharges(int amount)
    {
        chargeManager?.AddCharges(amount);
    }
    
    /// <summary>
    /// Get the current number of charges
    /// </summary>
    public int GetCurrentCharges()
    {
        return chargeManager?.CurrentCharges ?? 0;
    }
    #endregion
}