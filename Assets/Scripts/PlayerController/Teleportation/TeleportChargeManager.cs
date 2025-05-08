using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Manages teleportation charges, cooldowns and UI updates
/// </summary>
public class TeleportChargeManager : MonoBehaviour
{
    #region Fields
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI chargesDisplay;
    [SerializeField] private TextMeshProUGUI cooldownDisplay;
    
    [Header("Audio Feedback")]
    [SerializeField] private AudioClip chargeReadySound;
    [SerializeField] private AudioClip chargeDepletedSound;
    [SerializeField] private AudioSource audioSource;
    
    // Settings
    private TeleportationSettings _settings;
    
    // Charge data
    private int _currentCharges;
    private List<float> _chargeCooldowns = new List<float>();
    
    // Audio feedback
    private bool _wasLastCharge = false;
    #endregion
    
    #region Events
    /// <summary>
    /// Triggered when charge counts change (current, max)
    /// </summary>
    public event Action<int, int> OnChargesChanged;
    
    /// <summary>
    /// Triggered when a charge is regenerated
    /// </summary>
    public event Action OnChargeRegained;
    
    /// <summary>
    /// Triggered when charges become depleted
    /// </summary>
    public event Action OnChargesDepleted;
    #endregion
    
    #region Properties
    public int CurrentCharges => _currentCharges;
    public int MaxCharges => _settings?.maxCharges ?? 0;
    #endregion
    
    #region Initialization
    /// <summary>
    /// Initialize the charge manager with settings
    /// </summary>
    public void Initialize(TeleportationSettings settings)
    {
        _settings = settings;
        
        if (_settings == null)
        {
            Debug.LogError("TeleportChargeManager: No settings provided!");
            return;
        }
        
        // Setup initial charges
        _currentCharges = _settings.maxCharges;
        
        // Initialize cooldowns
        _chargeCooldowns.Clear();
        for (int i = 0; i < _settings.maxCharges; i++)
        {
            _chargeCooldowns.Add(0f);
        }
        
        // Setup audio
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && (chargeReadySound != null || chargeDepletedSound != null))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D sound
                audioSource.volume = 0.5f;
            }
        }
        
        // Trigger initial update
        UpdateChargesDisplay();
    }
    #endregion
    
    #region Unity Lifecycle
    private void Update()
    {
        ProcessChargeCooldowns();
        UpdateChargesDisplay();
    }
    #endregion
    
    #region Charge Management
    /// <summary>
    /// Use a charge for teleportation
    /// </summary>
    public void UseCharge()
    {
        if (_currentCharges <= 0) return;
        
        _currentCharges--;
        
        // Track if this was the last charge
        _wasLastCharge = _currentCharges == 0;
        
        // Find first available cooldown slot
        for (int i = 0; i < _chargeCooldowns.Count; i++)
        {
            if (_chargeCooldowns[i] <= 0)
            {
                _chargeCooldowns[i] = _settings.chargeCooldownDuration;
                break;
            }
        }
        
        // Play sound if depleted
        if (_wasLastCharge && audioSource != null && chargeDepletedSound != null)
        {
            audioSource.PlayOneShot(chargeDepletedSound);
            OnChargesDepleted?.Invoke();
        }
        
        // Notify listeners
        OnChargesChanged?.Invoke(_currentCharges, _settings.maxCharges);
    }
    
    /// <summary>
    /// Add charges (from pickups or abilities)
    /// </summary>
    public void AddCharges(int amount)
    {
        int oldCharges = _currentCharges;
        _currentCharges = Mathf.Min(_currentCharges + amount, _settings.maxCharges);
        
        // Cancel cooldowns for added charges
        int addedCharges = _currentCharges - oldCharges;
        int cancelledCooldowns = 0;
        
        for (int i = 0; i < _chargeCooldowns.Count && cancelledCooldowns < addedCharges; i++)
        {
            if (_chargeCooldowns[i] > 0)
            {
                _chargeCooldowns[i] = 0;
                cancelledCooldowns++;
            }
        }
        
        // Notify listeners
        OnChargesChanged?.Invoke(_currentCharges, _settings.maxCharges);
    }
    
    /// <summary>
    /// Process charge cooldowns and regenerate charges
    /// </summary>
    private void ProcessChargeCooldowns()
    {
        bool chargeRegained = false;
        
        for (int i = 0; i < _chargeCooldowns.Count; i++)
        {
            if (_chargeCooldowns[i] <= 0) continue;
            
            _chargeCooldowns[i] -= Time.deltaTime;
            
            if (_chargeCooldowns[i] <= 0)
            {
                _chargeCooldowns[i] = 0;
                
                if (_currentCharges < _settings.maxCharges)
                {
                    _currentCharges++;
                    chargeRegained = true;
                    
                    // Notify listeners
                    OnChargesChanged?.Invoke(_currentCharges, _settings.maxCharges);
                    OnChargeRegained?.Invoke();
                    
                    // Play sound if we were out of charges
                    if (_wasLastCharge && audioSource != null && chargeReadySound != null)
                    {
                        audioSource.PlayOneShot(chargeReadySound);
                        _wasLastCharge = false;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Check if there are charges available
    /// </summary>
    public bool HasCharges() => _currentCharges > 0;
    
    /// <summary>
    /// Get the time until next charge regenerates
    /// </summary>
    private float GetNextChargeTime()
    {
        float lowestActiveCooldown = _settings.chargeCooldownDuration;
        
        foreach (float cooldown in _chargeCooldowns)
        {
            if (cooldown > 0 && cooldown < lowestActiveCooldown)
                lowestActiveCooldown = cooldown;
        }
        
        return lowestActiveCooldown;
    }
    #endregion
    
    #region UI Updates
    /// <summary>
    /// Update the UI displays for charges and cooldowns
    /// </summary>
    private void UpdateChargesDisplay()
    {
        // Update charge count text
        if (chargesDisplay != null)
            chargesDisplay.text = $"Blink: {_currentCharges}/{_settings.maxCharges}";
        
        // Update cooldown text
        if (cooldownDisplay != null)
        {
            if (_currentCharges < _settings.maxCharges)
            {
                float nextChargeCooldown = GetNextChargeTime();
                cooldownDisplay.text = nextChargeCooldown > 0 ? $"Next: {nextChargeCooldown:F1}s" : "";
                
                // Optional: Add color coding based on cooldown time
                if (nextChargeCooldown < 1.0f)
                    cooldownDisplay.color = Color.green;
                else if (nextChargeCooldown < 2.0f)
                    cooldownDisplay.color = Color.yellow;
                else
                    cooldownDisplay.color = Color.white;
            }
            else
            {
                cooldownDisplay.text = "Charges Full";
                cooldownDisplay.color = Color.white;
            }
        }
    }
    #endregion
}