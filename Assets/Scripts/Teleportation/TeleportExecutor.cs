using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Handles the execution of teleportation and post-teleport stabilization
/// </summary>
public class TeleportExecutor : MonoBehaviour
{
    #region Fields
    // Core references
    private TeleportationSettings _settings;
    private Transform _playerTransform;
    private Transform _playerObj;
    private Transform _playerCamera;
    private Rigidbody _playerRigidbody;
    
    // Teleportation state
    private Vector3 _velocityBeforeTeleport;
    
    // Audio/Visual feedback
    [Header("Audio Feedback")]
    [SerializeField] private AudioClip teleportStartSound;
    [SerializeField] private AudioClip teleportEndSound;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Visual Feedback")]
    [SerializeField] private ParticleSystem teleportStartVFX;
    [SerializeField] private ParticleSystem teleportEndVFX;
    
    [Header("Camera Effects")]
    [SerializeField] private AnimationCurve cameraFOVCurve;
    [SerializeField] private float cameraFOVDuration = 0.3f;
    [SerializeField] private float cameraFOVChange = 10f;
    private float _originalFOV;
    private Camera _mainCamera;
    #endregion
    
    #region Events
    public event Action OnTeleportStart;
    public event Action OnTeleportComplete;
    #endregion
    
    #region Initialization
    /// <summary>
    /// Initialize the teleport executor with settings and references
    /// </summary>
    public void Initialize(TeleportationSettings settings, Transform playerTransform, 
                          Transform playerObj, Transform playerCamera, Rigidbody playerRigidbody)
    {
        _settings = settings;
        _playerTransform = playerTransform;
        _playerObj = playerObj;
        _playerCamera = playerCamera;
        _playerRigidbody = playerRigidbody;
        
        if (_settings == null)
        {
            Debug.LogError("TeleportExecutor: No settings provided!");
        }
        
        // Setup audio
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && (teleportStartSound != null || teleportEndSound != null))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f; // Mix of 2D and 3D sound
                audioSource.volume = 0.5f;
            }
        }
        
        // Find camera if available
        if (_playerCamera != null)
        {
            _mainCamera = _playerCamera.GetComponent<Camera>();
            if (_mainCamera != null)
            {
                _originalFOV = _mainCamera.fieldOfView;
            }
        }
    }
    #endregion
    
    #region Teleportation Execution
    /// <summary>
    /// Execute teleportation to the target position
    /// </summary>
    public void ExecuteTeleport(Vector3 destination, bool isLedge)
    {
        if (_playerRigidbody == null)
        {
            Debug.LogError("TeleportExecutor: No Rigidbody found. Cannot teleport.");
            return;
        }
        
        // Store velocity before teleport
        _velocityBeforeTeleport = _playerRigidbody.linearVelocity;
        
        // Start teleport sequence
        StartCoroutine(ExecuteTeleportSequence(destination, isLedge));
    }
    
    /// <summary>
    /// Coroutine that handles the teleportation sequence
    /// </summary>
    private IEnumerator ExecuteTeleportSequence(Vector3 destination, bool isLedge)
    {
        // Notify listeners
        OnTeleportStart?.Invoke();
        
        // Wait for physics update to complete
        yield return new WaitForFixedUpdate();
        
        // Store initial position for distance calculation
        Vector3 startPosition = _playerTransform.position;
        float teleportDistance = Vector3.Distance(startPosition, destination);
        
        if (_settings.enableDebugLogging)
        {
            Debug.Log($"Teleport Distance: {teleportDistance:F2}m | Max: {_settings.maxTeleportationDistance:F2}m");
        }
        
        // Play departure effects
        PlayTeleportDepartureEffects(startPosition, isLedge);
        
        // Begin camera effect if available
        if (_mainCamera != null && cameraFOVCurve != null)
        {
            StartCoroutine(TeleportCameraEffect());
        }
        
        // Save physics state
        Vector3 originalVelocity = _playerRigidbody.linearVelocity;
        bool wasKinematic = _playerRigidbody.isKinematic;
        
        // Reset physics for clean teleport
        _playerRigidbody.linearVelocity = Vector3.zero;
        _playerRigidbody.angularVelocity = Vector3.zero;
        _playerRigidbody.isKinematic = true;
        
        // Optional short delay for effects
        yield return new WaitForSecondsRealtime(0.05f);
        
        // Perform teleport
        _playerTransform.position = destination;
        
        // Debug actual distance
        if (_settings.enableDebugLogging)
        {
            float actualDistance = Vector3.Distance(startPosition, _playerTransform.position);
            Debug.Log($"Actual Distance Traveled: {actualDistance:F2}m");
            
            if (Mathf.Abs(teleportDistance - actualDistance) > 0.1f)
            {
                Debug.LogWarning($"Distance Discrepancy: Expected {teleportDistance:F2}m but traveled {actualDistance:F2}m");
            }
        }
        
        // Sync player model position
        if (_playerObj != null)
        {
            _playerObj.position = _playerTransform.position;
        }
        
        // Play arrival effects
        PlayTeleportArrivalEffects(destination, isLedge);
        
        yield return null;
        
        // Restore physics state
        _playerRigidbody.isKinematic = wasKinematic;
        
        // Apply momentum preservation
        if (_settings.preserveMomentumOnTeleport)
        {
            _playerRigidbody.linearVelocity = _velocityBeforeTeleport * 
                                        _settings.momentumRetentionPercentage * 
                                        _settings.teleportationSnapFactor;
        }
        
        // Stabilize physics
        yield return StartCoroutine(StabilizePlayerAfterTeleport());
        
        // Restore time scale
        StartCoroutine(RestoreTimeScaleGradually());
        
        // Notify completion
        OnTeleportComplete?.Invoke();
    }
    
    /// <summary>
    /// Gradually restore time scale after teleportation
    /// </summary>
    private IEnumerator RestoreTimeScaleGradually()
    {
        float timeElapsed = 0f;
        float startTimeScale = Time.timeScale;
        float duration = (_settings.timeRecoveryRate > 0) ? 1f / _settings.timeRecoveryRate : 0.2f;
        
        while (timeElapsed < duration)
        {
            timeElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timeElapsed / duration);
            
            // Smooth step easing
            float smoothProgress = progress * progress * (3f - 2f * progress);
            Time.timeScale = Mathf.Lerp(startTimeScale, 1f, smoothProgress);
            
            yield return null;
        }
        
        // Ensure time scale is exactly 1
        Time.timeScale = 1f;
    }
    
    /// <summary>
    /// Stabilize player physics after teleportation
    /// </summary>
    private IEnumerator StabilizePlayerAfterTeleport()
    {
        // Store original damping values
        float originalDrag = _playerRigidbody.linearDamping;
        float originalAngularDrag = _playerRigidbody.angularDamping;
        
        // Apply higher damping temporarily for stability
        _playerRigidbody.linearDamping = Mathf.Max(originalDrag, 0.5f);
        _playerRigidbody.angularDamping = Mathf.Max(originalAngularDrag, 0.5f);
        
        // Sync camera position
        if (_playerCamera != null && _playerObj != null)
        {
            _playerCamera.position = _playerTransform.position + (_playerCamera.position - _playerObj.position);
        }
        
        // Wait for physics to settle
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        
        // Restore original physics values
        _playerRigidbody.linearDamping = originalDrag;
        _playerRigidbody.angularDamping = originalAngularDrag;
    }
    #endregion
    
    #region Visual & Audio Effects
    /// <summary>
    /// Play teleport departure effects
    /// </summary>
    private void PlayTeleportDepartureEffects(Vector3 position, bool isLedge)
    {
        // Play audio
        if (audioSource != null && teleportStartSound != null)
        {
            audioSource.pitch = isLedge ? 1.2f : 1.0f; // Slightly higher pitch for ledges
            audioSource.PlayOneShot(teleportStartSound);
        }
        
        // Play VFX
        if (teleportStartVFX != null)
        {
            teleportStartVFX.transform.position = position;
            
            // Customize VFX based on teleport type
            var main = teleportStartVFX.main;
            if (isLedge)
            {
                main.startColor = new Color(0.8f, 0.6f, 0.2f, 1f); // Orange for ledges
            }
            else
            {
                main.startColor = new Color(0.2f, 0.6f, 0.8f, 1f); // Blue for normal teleports
            }
            
            teleportStartVFX.Play();
        }
    }
    
    /// <summary>
    /// Play teleport arrival effects
    /// </summary>
    private void PlayTeleportArrivalEffects(Vector3 position, bool isLedge)
    {
        // Play audio
        if (audioSource != null && teleportEndSound != null)
        {
            audioSource.pitch = isLedge ? 1.1f : 1.0f; // Slightly higher pitch for ledges
            audioSource.PlayOneShot(teleportEndSound);
        }
        
        // Play VFX
        if (teleportEndVFX != null)
        {
            teleportEndVFX.transform.position = position;
            
            // Customize VFX based on teleport type
            var main = teleportEndVFX.main;
            if (isLedge)
            {
                main.startColor = new Color(0.8f, 0.6f, 0.2f, 1f); // Orange for ledges
            }
            else
            {
                main.startColor = new Color(0.2f, 0.6f, 0.8f, 1f); // Blue for normal teleports
            }
            
            teleportEndVFX.Play();
        }
    }
    
    /// <summary>
    /// Animate camera FOV during teleport
    /// </summary>
    private IEnumerator TeleportCameraEffect()
    {
        if (_mainCamera == null || cameraFOVCurve == null)
            yield break;
            
        float startTime = Time.unscaledTime;
        float startFOV = _mainCamera.fieldOfView;
        float halfDuration = cameraFOVDuration * 0.5f;
        
        // Increase FOV (widen view) during first half
        while (Time.unscaledTime < startTime + halfDuration)
        {
            float elapsed = Time.unscaledTime - startTime;
            float normalizedTime = elapsed / halfDuration;
            float curveValue = cameraFOVCurve.Evaluate(normalizedTime);
            
            _mainCamera.fieldOfView = startFOV + (cameraFOVChange * curveValue);
            
            yield return null;
        }
        
        // Reset start time for second half
        startTime = Time.unscaledTime;
        startFOV = _mainCamera.fieldOfView;
        
        // Decrease FOV (return to normal) during second half
        while (Time.unscaledTime < startTime + halfDuration)
        {
            float elapsed = Time.unscaledTime - startTime;
            float normalizedTime = elapsed / halfDuration;
            float curveValue = cameraFOVCurve.Evaluate(1 - normalizedTime);
            
            _mainCamera.fieldOfView = _originalFOV + (cameraFOVChange * curveValue);
            
            yield return null;
        }
        
        // Ensure FOV is reset exactly
        _mainCamera.fieldOfView = _originalFOV;
    }
    #endregion
}