using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Settings for the teleportation system, implemented as a ScriptableObject for easy tweaking and saving
/// </summary>
[CreateAssetMenu(fileName = "TeleportationSettings", menuName = "Gameplay/Teleportation Settings")]
public class TeleportationSettings : ScriptableObject
{
    #region Nested Classes

    /// <summary>
    /// Audio-specific settings for teleportation
    /// </summary>
    [System.Serializable]
    public class AudioSettings
    {
        [Header("Teleport Sounds")]
        [Tooltip("Sound played when teleportation begins")]
        public AudioClip teleportStartSound;
        
        [Tooltip("Sound played when teleportation completes")]
        public AudioClip teleportEndSound;
        
        [Tooltip("Sound played when a charge is ready")]
        public AudioClip chargeReadySound;
        
        [Tooltip("Sound when charges are depleted")]
        public AudioClip chargeDepletedSound;
        
        [Range(0f, 1f)]
        [Tooltip("Volume for teleportation sounds")]
        public float volume = 0.75f;
        
        [Tooltip("Audio mixer group for teleportation sounds")]
        public AudioMixerGroup audioMixerGroup;
    }
    
    /// <summary>
    /// Visual-specific settings for teleportation
    /// </summary>
    [System.Serializable]
    public class VisualSettings
    {
        [Header("Marker")]
        [Tooltip("Color for valid teleport markers")]
        public Color validColor = new Color(0.2f, 0.6f, 1f, 0.8f); // Blue
        
        [Tooltip("Color for ledge teleport markers")]
        public Color ledgeColor = new Color(1f, 0.6f, 0.2f, 0.8f); // Orange
        
        [Tooltip("Color for invalid teleport markers")]
        public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.8f); // Red
        
        [Header("Effects")]
        [Tooltip("Particle system for teleport start")]
        public GameObject teleportStartVFX;
        
        [Tooltip("Particle system for teleport end")]
        public GameObject teleportEndVFX;
        
        [Tooltip("Material for trajectory")]
        public Material trajectoryMaterial;
        
        [Range(0.05f, 0.5f)]
        [Tooltip("Width of trajectory line")]
        public float trajectoryWidth = 0.1f; // From screenshot value

        [Tooltip("Whether to show a trajectory arc")]
        public bool showTrajectory = true;
    }
    
    /// <summary>
    /// Camera-specific settings for teleportation effects
    /// </summary>
    [System.Serializable]
    public class CameraSettings
    {
        [Tooltip("FOV animation curve for teleport")]
        public AnimationCurve fovCurve;
        
        [Range(0f, 30f)]
        [Tooltip("Maximum FOV change during teleport")]
        public float maxFovChange = 10f;
        
        [Range(0.1f, 1f)]
        [Tooltip("Duration of FOV animation")]
        public float fovAnimationDuration = 0.3f;
        
        [Tooltip("Add motion blur during teleport")]
        public bool useMotionBlur = true;
        
        [Range(0f, 1f)]
        [Tooltip("Motion blur strength")]
        public float motionBlurStrength = 1f;
    }

    #endregion

    #region Core Settings

    [Header("Core Settings")]
    [Tooltip("Layers that can be teleported onto")]
    public LayerMask teleportableSurfaces; // Set to "whatIsGround" in Inspector

    [Tooltip("Layers that block teleportation path")]
    public LayerMask teleportationBlockers; // Set to "whatIsWall" in Inspector

    #endregion

    #region Teleportation Controls

    [Header("Teleportation Controls")]
    [Tooltip("Maximum distance the player can teleport")]
    [Range(5f, 30f)]
    public float maxTeleportationDistance = 12f;

    [Tooltip("Minimum distance required for teleportation to occur")]
    [Range(0.5f, 5f)]
    public float minTeleportationDistance = 2f;

    [Tooltip("Maximum angle (degrees) the player can look away from their forward direction to teleport")]
    [Range(45f, 180f)]
    public float maxLookAngle = 90f;

    #endregion

    #region Time Manipulation

    [Header("Time Manipulation")]
    [Tooltip("Time scale factor when aiming teleport (lower values = more slow-motion effect)")]
    [Range(0f, 1f)]
    public float timeSlowdownFactor = 1f; // Note: 1.0 means no slowdown effect

    [Tooltip("Speed at which time returns to normal after teleporting (higher values = faster recovery)")]
    [Range(1f, 15f)]
    public float timeRecoveryRate = 15f;

    #endregion

    #region Teleportation Charges

    [Header("Teleportation Charges")]
    [Tooltip("Maximum number of teleport charges the player can have")]
    [Range(1, 10)]
    public int maxCharges = 3;

    [Tooltip("Time (in seconds) for a single charge to regenerate")]
    [Range(1f, 20f)]
    public float chargeCooldownDuration = 3f;

    [Tooltip("Whether charges regenerate during teleportation")]
    public bool regenerateDuringTeleport = true;

    #endregion

    #region Marker and Trajectory Settings

    [Header("Marker Settings")]
    [Tooltip("Additional position offset applied to the marker (useful for XZ adjustments)")]
    public Vector3 markerPositionOffset = Vector3.zero;

    [Tooltip("How quickly the marker moves to its target position (higher values = faster movement)")]
    [Range(30f, 100f)]
    public float markerMovementSpeed = 89.5f;
    
    [Header("Arc Settings")]
    [Tooltip("Whether to show a trajectory arc")]
    public bool showTrajectory = true;

    [Tooltip("Maximum number of points to calculate in the arc")]
    [Range(10, 60)]
    public int arcResolution = 30;

    #endregion

    #region Surface Detection and Targeting

    [Header("Surface Detection")]
    [Tooltip("Height offset when teleporting to a regular (horizontal) surface")]
    [Range(0.05f, 2f)]
    public float groundedHeightOffset = 1f;

    [Tooltip("Height offset when teleporting to a ledge")]
    [Range(0.05f, 2f)]
    public float ledgeHeightOffset = 1f;

    [Tooltip("Whether to allow targeting bottom surfaces of objects")]
    public bool allowBottomSurfaceTargeting = false;

    [Header("Line of Sight")]
    [Tooltip("Whether teleportation requires line of sight to the destination")]
    public bool requireDirectLineOfSight = true;

    [Tooltip("Radius of the sphere used for visibility checking")]
    [Range(0.1f, 1f)]
    public float visibilityCheckRadius = 0.3f;

    #endregion

    #region Advanced Mechanics

    [Header("Advanced Mechanics")]
    [Tooltip("Whether to preserve player momentum when teleporting")]
    public bool preserveMomentumOnTeleport = true;

    [Tooltip("Percentage of original momentum retained after teleport (0-1)")]
    [Range(0f, 1f)]
    public float momentumRetentionPercentage = 0.4f;

    [Tooltip("Additional velocity multiplier after teleportation")]
    [Range(0.5f, 2f)]
    public float teleportationSnapFactor = 1.2f;

    [Tooltip("Whether to allow climbing to ledges")]
    public bool enableLedgeClimbing = true;

    [Tooltip("Whether to allow floating when looking up")]
    public bool floatWhenLookingUp = false;

    [Range(-50f, 90f)] 
    [Tooltip("Vertical angle threshold for floating up detection")]
    public float verticalAngleThreshold = -22.5f;

    #endregion

    #region Debug and Extended Settings

    [Header("Debug Settings")]
    [Tooltip("Enable detailed debug visualization")]
    public bool showDebugVisualization = true;

    [Tooltip("Enable detailed debug logging")]
    public bool enableDebugLogging = true;

    [Header("Extended Settings")]
    [Tooltip("Audio settings for teleportation")]
    public AudioSettings audioSettings;
    
    [Tooltip("Visual settings for teleportation")]
    public VisualSettings visualSettings;
    
    [Tooltip("Camera settings for teleportation")]
    public CameraSettings cameraSettings;

    #endregion

    #region Animation Events

    [Header("Events")]
    [Tooltip("Whether to trigger animation events on the player")]
    public bool triggerAnimationEvents = true;
    
    [Tooltip("Animation trigger string for teleport start")]
    public string teleportStartTrigger = "TeleportStart";
    
    [Tooltip("Animation trigger string for teleport end")]
    public string teleportEndTrigger = "TeleportEnd";

    #endregion
}