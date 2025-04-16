using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles visual representation of teleportation targets and trajectories
/// </summary>
public class TeleportVisualizer : MonoBehaviour
{
    #region Fields
    [Header("Visual Settings")]
    [SerializeField] private GameObject teleportMarkerPrefab;
    [SerializeField] private LineRenderer trajectoryRenderer;
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material ledgeMaterial;
    [SerializeField] private Material invalidMaterial;
    
    // Runtime references
    private TeleportationSettings _settings;
    private Transform _playerTransform;
    private GameObject _activeMarker;
    private Vector3[] _arcPoints;
    private Vector3 _markerVelocity = Vector3.zero; // For SmoothDamp

    private float _markerSmoothTime = 0.05f; // Controls snappiness
    private float _markerSnapThreshold = 2.0f; // Distance threshold for snapping
    private bool _firstShow = true; // To track first appearance
    
    // Target state
    private Vector3 _targetMarkerPosition;
    private bool _isTargetingLedge;
    private Vector3 _targetSurfaceNormal;
    #endregion
    
    #region Initialization
    /// <summary>
    /// Initialize the visualizer with settings
    /// </summary>
    public void Initialize(TeleportationSettings settings, Transform playerTransform)
    {
        _settings = settings;
        _playerTransform = playerTransform;
        
        if (_settings == null)
        {
            Debug.LogError("TeleportVisualizer: No settings provided!");
            return;
        }
        
        // Create the marker if needed
        if (teleportMarkerPrefab != null && _activeMarker == null)
        {
            _activeMarker = Instantiate(teleportMarkerPrefab, Vector3.zero, Quaternion.identity);
            _activeMarker.SetActive(false);
        }
        else if (teleportMarkerPrefab == null)
        {
            Debug.LogWarning("TeleportVisualizer: Marker prefab not assigned!");
        }
        
        // Setup trajectory renderer
        if (trajectoryRenderer == null && _settings.showTrajectory)
        {
            GameObject trajectoryObj = new GameObject("TeleportTrajectory");
            trajectoryObj.transform.SetParent(transform);
            
            trajectoryRenderer = trajectoryObj.AddComponent<LineRenderer>();
            trajectoryRenderer.startWidth = 0.05f;
            trajectoryRenderer.endWidth = 0.05f;
            trajectoryRenderer.positionCount = 0;
            
            trajectoryRenderer.gameObject.SetActive(false);
        }
        
        // Initialize arc points array
        _arcPoints = new Vector3[_settings.arcResolution];
    }
    #endregion
    
    #region Visualization Control
    /// <summary>
    /// Show the teleport marker at the specified position
    /// </summary>
    public void ShowMarker(Vector3 position, bool isLedge, Vector3 surfaceNormal)
    {

        _firstShow = true; // Reset first show flag
        if (_activeMarker == null) return;
        
        _targetMarkerPosition = position;
        _isTargetingLedge = isLedge;
        _targetSurfaceNormal = surfaceNormal;
        
        // Ensure marker is active
        if (!_activeMarker.activeSelf)
        {
            _activeMarker.SetActive(true);
            
            // Initial position
            _activeMarker.transform.position = position;
            _markerVelocity = Vector3.zero;
        }
        
        // Show trajectory
        if (trajectoryRenderer != null && _settings.showTrajectory)
        {
            trajectoryRenderer.gameObject.SetActive(true);
            UpdateTrajectory();
        }
        
        // Update marker appearance based on target type
        UpdateMarkerAppearance();
    }
    
    /// <summary>
    /// Hide the teleport marker and trajectory
    /// </summary>
    public void HideMarker()
    {
        if (_activeMarker != null)
        {
            // Reset velocity before hiding
            _markerVelocity = Vector3.zero;
            _activeMarker.SetActive(false);
        }
        
        if (trajectoryRenderer != null)
        {
            trajectoryRenderer.gameObject.SetActive(false);
        }
    }
    #endregion
    
    #region Update Methods
    private void Update()
    {
        if (_activeMarker != null && _activeMarker.activeSelf)
        {
            UpdateMarkerPosition();
            
            if (trajectoryRenderer != null && trajectoryRenderer.gameObject.activeSelf)
            {
                UpdateTrajectory();
            }
        }
    }
    
    /// <summary>
    /// Update the marker position with smooth movement
    /// </summary>
    private void UpdateMarkerPosition()
    {
        if (_activeMarker == null) return;
        
        Vector3 visualMarkerPosition = _targetMarkerPosition + _settings.markerPositionOffset;
        
        // Force immediate position update on first show
        if (_firstShow)
        {
            _activeMarker.transform.position = visualMarkerPosition;
            _markerVelocity = Vector3.zero;
            _firstShow = false;
            return;
        }

        float distanceToTarget = Vector3.Distance(_activeMarker.transform.position, visualMarkerPosition);
        
        // Snap if too far
        if (distanceToTarget > _markerSnapThreshold)
        {
            _activeMarker.transform.position = visualMarkerPosition;
            _markerVelocity = Vector3.zero;
        }
        // Smooth movement for smaller adjustments
        else
        {
            // Calculate smooth movement
            Vector3 smoothedPosition = Vector3.SmoothDamp(
                _activeMarker.transform.position,
                visualMarkerPosition,
                ref _markerVelocity,
                _markerSmoothTime,
                _settings.markerMovementSpeed,
                Time.unscaledDeltaTime
            );
            
            _activeMarker.transform.position = smoothedPosition;
        }

        // Update marker orientation based on surface type
        if (_isTargetingLedge)
        {
            AlignMarkerWithLedge();
        }
        else
        {
            AlignMarkerWithSurface();
        }
    }  


    /// <summary>
    /// Update the teleport trajectory visualization
    /// </summary>
    private void UpdateTrajectory()
    {
        if (trajectoryRenderer == null || _playerTransform == null) return;
        
        // Calculate arc between player and target
        UpdateTrajectoryPoints(_playerTransform.position, _activeMarker.transform.position);
        
        // Update line renderer
        trajectoryRenderer.positionCount = _arcPoints.Length;
        trajectoryRenderer.SetPositions(_arcPoints);
        
        // Update color based on target type
        if (_isTargetingLedge)
        {
            trajectoryRenderer.startColor = new Color(1f, 0.6f, 0.2f, 0.6f);
            trajectoryRenderer.endColor = new Color(1f, 0.6f, 0.2f, 0.2f);
        }
        else
        {
            trajectoryRenderer.startColor = new Color(0.2f, 0.6f, 1f, 0.6f);
            trajectoryRenderer.endColor = new Color(0.2f, 0.6f, 1f, 0.2f);
        }
    }
    
    /// <summary>
    /// Calculate arc points for trajectory visualization
    /// </summary>
    private void UpdateTrajectoryPoints(Vector3 start, Vector3 end)
    {
        if (_arcPoints == null || _arcPoints.Length != _settings.arcResolution)
        {
            _arcPoints = new Vector3[_settings.arcResolution];
        }
        
        Vector3 directionToTarget = end - start;
        float distance = directionToTarget.magnitude;
        
        // Calculate arc height based on distance
        float arcHeight = Mathf.Min(distance * 0.25f, 3f);
        
        for (int i = 0; i < _settings.arcResolution; i++)
        {
            float t = i / (float)(_settings.arcResolution - 1);
            
            // Quadratic bezier for a simple arc
            Vector3 m = Vector3.Lerp(start, end, t);
            
            // Add arc height at midpoint
            Vector3 arcPoint = m + Vector3.up * arcHeight * Mathf.Sin(t * Mathf.PI);
            
            _arcPoints[i] = arcPoint;
        }

        // better trajectory visualization for ledge
        if (_isTargetingLedge)
        {
            // Add a slight forward curve for ledge targeting in the second half
            for (int i = _settings.arcResolution / 2; i < _settings.arcResolution; i++)
            {
                float t = (i - _settings.arcResolution / 2) / (float)(_settings.arcResolution / 2);
                Vector3 forwardDir = Vector3.ProjectOnPlane(end - start, Vector3.up).normalized;
                float forwardCurve = Mathf.Sin(t * Mathf.PI) * distance * 0.1f;
                _arcPoints[i] += forwardDir * forwardCurve;
            }
        }
    }
    
    /// <summary>
    /// Align marker with the target surface
    /// </summary>
    private void AlignMarkerWithSurface()
    {
        if (_activeMarker == null) return;
        
        // Blend between upright orientation and surface orientation
        Quaternion targetRotation = Quaternion.identity;
        
        if (_isTargetingLedge)
        {
            // For ledges, orient marker to face outward from ledge
            Vector3 outwardDirection = -_targetSurfaceNormal;
            outwardDirection.y = 0;
            
            if (outwardDirection.magnitude > 0.01f)
            {
                outwardDirection.Normalize();
                targetRotation = Quaternion.LookRotation(outwardDirection, Vector3.up);
            }
        }
        else
        {
            // For ground, align with surface normal but keep pointing toward player
            Vector3 directionToPlayer = _playerTransform.position - _activeMarker.transform.position;
            directionToPlayer.y = 0;
            
            if (directionToPlayer.magnitude > 0.01f)
            {
                directionToPlayer.Normalize();
                
                // Create rotation that considers both surface normal and direction to player
                targetRotation = Quaternion.LookRotation(directionToPlayer, _targetSurfaceNormal);
            }
            else
            {
                // Fallback if player is directly above/below
                targetRotation = Quaternion.FromToRotation(Vector3.up, _targetSurfaceNormal);
            }
        }
        
        // Apply rotation with smooth interpolation
        _activeMarker.transform.rotation = Quaternion.Slerp(
            _activeMarker.transform.rotation,
            targetRotation,
            Time.unscaledDeltaTime * 8f
        );
    }

    private void AlignMarkerWithLedge()
    {
        if (_activeMarker == null || _targetSurfaceNormal == Vector3.zero) return;
        
        // For ledges, create a more horizontal orientation
        Vector3 ledgeNormal = _targetSurfaceNormal;
        ledgeNormal.y = 0;
        
        if (ledgeNormal.magnitude > 0.01f)
        {
            ledgeNormal.Normalize();
            
            // Create rotation that faces outward from ledge
            Quaternion targetRotation = Quaternion.LookRotation(-ledgeNormal, Vector3.up);
            
            // Apply rotation with quick interpolation for snappy feel
            _activeMarker.transform.rotation = Quaternion.Slerp(
                _activeMarker.transform.rotation,
                targetRotation,
                Time.unscaledDeltaTime * 15f  // Higher value for ledges - extra snappy
            );
        }
    }
    
    /// <summary>
    /// Update the marker appearance based on target type
    /// </summary>
    private void UpdateMarkerAppearance()
    {
        if (_activeMarker == null) return;
        
        // Find or create necessary components
        Renderer markerRenderer = _activeMarker.GetComponentInChildren<Renderer>();
        ParticleSystem particles = _activeMarker.GetComponentInChildren<ParticleSystem>();
        
        if (markerRenderer != null)
        {
            // Update marker material based on target type
            if (_isTargetingLedge && ledgeMaterial != null)
            {
                markerRenderer.material = ledgeMaterial;
            }
            else if (validMaterial != null)
            {
                markerRenderer.material = validMaterial;
            }
        }
        
        if (particles != null)
        {
            // Adjust particles based on teleport type
            var main = particles.main;
            
            if (_isTargetingLedge)
            {
                // Orange/yellow for ledges
                main.startColor = new Color(1f, 0.6f, 0.2f, 0.8f);
            }
            else
            {
                // Blue for regular teleports
                main.startColor = new Color(0.2f, 0.6f, 1f, 0.8f);
            }
        }
    }
    #endregion
    
    #region Marker Animation
    /// <summary>
    /// Update the marker scale with a pulse animation
    /// </summary>
    public void SetPulseAnimation(bool enable)
    {
        if (_activeMarker == null) return;
        
        TeleportMarker markerComponent = _activeMarker.GetComponent<TeleportMarker>();
        if (markerComponent != null)
        {
            markerComponent.enabled = enable;
        }
    }
    #endregion
}

/// <summary>
/// Component for teleport marker animations
/// </summary>
public class TeleportMarker : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 50f;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.1f;
    [SerializeField] private float hoverHeight = 0.1f;
    [SerializeField] private float hoverSpeed = 1f;
    
    private Vector3 initialScale;
    private Vector3 originalPosition;
    private float initialTime;
    
    private void Awake()
    {
        initialScale = transform.localScale;
        originalPosition = transform.localPosition;
        initialTime = Time.unscaledTime;
    }
    
    private void OnEnable()
    {
        initialTime = Time.unscaledTime;
    }
    
    private void Update()
    {
        // Rotate the marker
        transform.Rotate(Vector3.up, rotationSpeed * Time.unscaledDeltaTime);
        
        // Pulse effect
        float pulse = 1f + pulseAmount * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI);
        transform.localScale = initialScale * pulse;
        
        // Hover effect
        float hoverOffset = Mathf.Sin(Time.unscaledTime * hoverSpeed * Mathf.PI);
        transform.localPosition = originalPosition + new Vector3(0, hoverOffset * hoverHeight, 0);
    }
}