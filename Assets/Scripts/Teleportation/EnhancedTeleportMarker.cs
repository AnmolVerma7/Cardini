using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enhanced teleport marker with various visual effects and animations
/// </summary>
[RequireComponent(typeof(Renderer))]
public class EnhancedTeleportMarker : MonoBehaviour
{
    #region Fields
    
    
    [Header("Rings Effect")]
    [SerializeField] private bool enableRings = true;
    [SerializeField] private GameObject ringPrefab;
    [SerializeField] private int maxRings = 3;
    [SerializeField] private float ringSpawnInterval = 1f;
    [SerializeField] private float ringExpansionSpeed = 1f;
    [SerializeField] private float ringMaxScale = 3f;
    [SerializeField] private float ringFadeDuration = 0.5f;
    
    // State tracking
    private Vector3 _originalPosition;
    private float _initialTime;
    private float _rotationDirection = 1f;
    private List<RingEffect> _activeRings = new List<RingEffect>();
    private float _nextRingTime;
    
    // Component references
    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;
    private ParticleSystem _particleSystem;
    private AudioSource _audioSource;
    
    // Class to track ring effects
    private class RingEffect
    {
        public GameObject Ring;
        public float CurrentScale;
        public float Alpha;
        
        public RingEffect(GameObject ring)
        {
            Ring = ring;
            CurrentScale = 1f;
            Alpha = 1f;
        }
    }
    #endregion
    
    #region Unity Lifecycle
    private void Awake()
    {
        _originalPosition = transform.localPosition;
        _initialTime = Time.unscaledTime;
        
        // Get component references
        _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();
        _particleSystem = GetComponentInChildren<ParticleSystem>();
        _audioSource = GetComponent<AudioSource>();
    }
    
    private void OnEnable()
    {
        _initialTime = Time.unscaledTime;
        _nextRingTime = Time.unscaledTime + ringSpawnInterval;
        
        // Play particle effect if available
        if (_particleSystem != null)
        {
            _particleSystem.Play();
        }
        
        // Start with a fresh ring set
        ClearRings();
    }
    
    private void OnDisable()
    {
        // Stop particles
        if (_particleSystem != null)
        {
            _particleSystem.Stop();
        }
        
        // Clear all rings
        ClearRings();
    }
    
    private void Update()
    {
        if (enableRings) UpdateRingEffects();

    }
    #endregion
    
    #region Visual Effects
    
    /// <summary>
    /// Update ring expansion effects
    /// </summary>
    private void UpdateRingEffects()
    {
        // Spawn new ring if it's time
        if (Time.unscaledTime >= _nextRingTime && _activeRings.Count < maxRings)
        {
            SpawnRing();
            _nextRingTime = Time.unscaledTime + ringSpawnInterval;
        }
        
        // Update existing rings
        for (int i = _activeRings.Count - 1; i >= 0; i--)
        {
            RingEffect ring = _activeRings[i];
            
            // Expand ring
            ring.CurrentScale += ringExpansionSpeed * Time.unscaledDeltaTime;
            ring.Ring.transform.localScale = Vector3.one * ring.CurrentScale;
            
            // Fade out when approaching max scale
            float scaleProgress = ring.CurrentScale / ringMaxScale;
            if (scaleProgress > 0.7f) // Start fading at 70% of max scale
            {
                ring.Alpha = Mathf.Lerp(1f, 0f, (scaleProgress - 0.7f) / 0.3f);
                
                // Update alpha
                Renderer ringRenderer = ring.Ring.GetComponent<Renderer>();
                if (ringRenderer != null)
                {
                    Color color = ringRenderer.material.color;
                    color.a = ring.Alpha;
                    ringRenderer.material.color = color;
                }
            }
            
            // Remove ring when fully expanded
            if (ring.CurrentScale >= ringMaxScale)
            {
                Destroy(ring.Ring);
                _activeRings.RemoveAt(i);
            }
        }
    }
    
    /// <summary>
    /// Spawn a new expanding ring
    /// </summary>
    private void SpawnRing()
    {
        if (ringPrefab == null) return;
        
        GameObject ringObj = Instantiate(ringPrefab, transform.position, Quaternion.identity);
        ringObj.transform.SetParent(transform);
        ringObj.transform.localPosition = Vector3.zero;
        ringObj.transform.localRotation = Quaternion.identity;
        ringObj.transform.localScale = Vector3.one;
        
        // Make ring face up
        ringObj.transform.rotation = Quaternion.LookRotation(Vector3.up);
        
        // Add to active rings
        _activeRings.Add(new RingEffect(ringObj));
    }
    
    /// <summary>
    /// Clear all active ring effects
    /// </summary>
    private void ClearRings()
    {
        foreach (var ring in _activeRings)
        {
            if (ring.Ring != null)
            {
                Destroy(ring.Ring);
            }
        }
        
        _activeRings.Clear();
    }
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Play a sound effect (if audio source is available)
    /// </summary>
    public void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    /// <summary>
    /// Create a standard marker GameObject
    /// </summary>
    public static GameObject CreateStandardMarker()
    {
        // Create parent object
        GameObject markerObj = new GameObject("TeleportMarker");
        
        // Add this component
        EnhancedTeleportMarker marker = markerObj.AddComponent<EnhancedTeleportMarker>();
        
        // Create visual elements
        GameObject visualObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visualObj.transform.SetParent(markerObj.transform);
        visualObj.transform.localPosition = Vector3.zero;
        visualObj.transform.localScale = new Vector3(1f, 0.05f, 1f);
        visualObj.transform.localRotation = Quaternion.identity;
        
        // Set up material
        Renderer renderer = visualObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", Color.cyan * 2f);
            renderer.material.SetColor("_Color", Color.cyan);
        }
        
        // Create ring prefab
        GameObject ringPrefab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringPrefab.transform.localScale = new Vector3(1f, 0.01f, 1f);
        
        // Set up ring material
        Renderer ringRenderer = ringPrefab.GetComponent<Renderer>();
        if (ringRenderer != null)
        {
            ringRenderer.material = new Material(Shader.Find("Transparent/Diffuse"));
            ringRenderer.material.color = new Color(0f, 0.8f, 1f, 0.5f);
        }
        
        // Set ring prefab
        marker.ringPrefab = ringPrefab;
        
        // Hide ring prefab
        ringPrefab.SetActive(false);
        
        return markerObj;
    }
    #endregion
}