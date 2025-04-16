#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Custom editor for the TeleportationSystem
/// </summary>
[CustomEditor(typeof(TeleportationSystem))]
public class TeleportationSystemEditor : Editor
{
    private SerializedProperty _settingsProp;
    private SerializedProperty _orientationProp;
    private SerializedProperty _playerObjProp;
    private SerializedProperty _playerCameraProp;
    private SerializedProperty _playerRigidbodyProp;
    private SerializedProperty _teleportationAimKeyProp;
    private SerializedProperty _teleportationExecuteKeyProp;
    private SerializedProperty _teleportationCancelKeyProp;
    private SerializedProperty _chargeManagerProp;
    private SerializedProperty _targetDetectorProp;
    private SerializedProperty _visualizerProp;
    private SerializedProperty _executorProp;
    
    private bool _showControls = true;
    private bool _showComponents = true;
    private bool _showDebug = false;
    
    private GUIStyle _headerStyle;
    private GUIStyle _boxStyle;
    private Texture2D _headerBackground;
    private Color _headerColor = new Color(0.2f, 0.6f, 1f, 0.5f);
    
    private void OnEnable()
    {
        // Get serialized properties
        _settingsProp = serializedObject.FindProperty("settings");
        _orientationProp = serializedObject.FindProperty("orientation");
        _playerObjProp = serializedObject.FindProperty("playerObj");
        _playerCameraProp = serializedObject.FindProperty("playerCamera");
        _playerRigidbodyProp = serializedObject.FindProperty("playerRigidbody");
        _teleportationAimKeyProp = serializedObject.FindProperty("teleportationAimKey");
        _teleportationExecuteKeyProp = serializedObject.FindProperty("teleportationExecuteKey");
        _teleportationCancelKeyProp = serializedObject.FindProperty("teleportationCancelKey");
        _chargeManagerProp = serializedObject.FindProperty("chargeManager");
        _targetDetectorProp = serializedObject.FindProperty("targetDetector");
        _visualizerProp = serializedObject.FindProperty("visualizer");
        _executorProp = serializedObject.FindProperty("executor");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        InitializeStyles();
        
        DrawHeader();
        DrawSettings();
        DrawCoreReferences();
        DrawControlsSection();
        DrawComponentsSection();
        DrawDebugSection();
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void InitializeStyles()
    {
        if (_headerStyle == null)
        {
            _headerStyle = new GUIStyle(EditorStyles.boldLabel);
            _headerStyle.fontSize = 14;
            _headerStyle.alignment = TextAnchor.MiddleLeft;
            _headerStyle.margin = new RectOffset(5, 5, 5, 5);
            _headerStyle.padding = new RectOffset(5, 5, 5, 5);
        }
        
        if (_boxStyle == null)
        {
            _boxStyle = new GUIStyle(EditorStyles.helpBox);
            _boxStyle.padding = new RectOffset(10, 10, 10, 10);
            _boxStyle.margin = new RectOffset(0, 0, 5, 5);
        }
        
        if (_headerBackground == null)
        {
            _headerBackground = new Texture2D(1, 1);
            _headerBackground.SetPixel(0, 0, _headerColor);
            _headerBackground.Apply();
            _headerStyle.normal.background = _headerBackground;
        }
    }
    
    private void DrawHeader()
    {
        GUILayout.BeginVertical(_boxStyle);
        
        GUILayout.BeginHorizontal();
        GUILayout.Label("Teleportation System", _headerStyle);
        
        TeleportationSystem teleportSystem = (TeleportationSystem)target;
        EditorGUI.BeginDisabledGroup(Application.isPlaying);
        
        if (GUILayout.Button("Auto Setup", GUILayout.Width(100)))
        {
            AutoSetupComponents();
        }
        
        EditorGUI.EndDisabledGroup();
        GUILayout.EndHorizontal();
        
        string description = "Manages player teleportation with markers, ledge detection, and charges.";
        EditorGUILayout.HelpBox(description, MessageType.None);
        
        if (teleportSystem.settings == null)
        {
            EditorGUILayout.HelpBox("No Teleportation Settings assigned! The system will not function correctly.", MessageType.Error);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Settings Asset"))
            {
                CreateSettingsAsset();
            }
            GUILayout.EndHorizontal();
        }
        
        GUILayout.EndVertical();
    }
    
    private void DrawSettings()
    {
        GUILayout.BeginVertical(_boxStyle);
        
        EditorGUILayout.PropertyField(_settingsProp, new GUIContent("Teleportation Settings", "ScriptableObject with all teleportation parameters"));
        
        if (_settingsProp.objectReferenceValue != null)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Edit Settings", GUILayout.Width(120)))
            {
                Selection.activeObject = _settingsProp.objectReferenceValue;
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        GUILayout.EndVertical();
    }
    
    private void DrawCoreReferences()
    {
        GUILayout.BeginVertical(_boxStyle);
        
        EditorGUILayout.LabelField("Core References", EditorStyles.boldLabel);
        
        EditorGUILayout.PropertyField(_orientationProp, new GUIContent("Orientation", "Transform that determines player facing direction"));
        EditorGUILayout.PropertyField(_playerObjProp, new GUIContent("Player Object", "Transform of the player model (if separate from this GameObject)"));
        EditorGUILayout.PropertyField(_playerCameraProp, new GUIContent("Player Camera", "Transform of the player's camera"));
        EditorGUILayout.PropertyField(_playerRigidbodyProp, new GUIContent("Player Rigidbody", "Rigidbody component of the player"));
        
        TeleportationSystem teleportSystem = (TeleportationSystem)target;
        
        // Auto-populate player rigidbody if not set
        if (_playerRigidbodyProp.objectReferenceValue == null)
        {
            Rigidbody rb = teleportSystem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                _playerRigidbodyProp.objectReferenceValue = rb;
                serializedObject.ApplyModifiedProperties();
            }
        }
        
        // Check for critical missing references
        if (_playerRigidbodyProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Rigidbody reference is required! Add a Rigidbody to the player or assign one.", MessageType.Error);
            
            if (GUILayout.Button("Add Rigidbody"))
            {
                Undo.RecordObject(teleportSystem.gameObject, "Add Rigidbody");
                Rigidbody newRb = teleportSystem.gameObject.AddComponent<Rigidbody>();
                newRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                newRb.freezeRotation = true;
                _playerRigidbodyProp.objectReferenceValue = newRb;
                serializedObject.ApplyModifiedProperties();
            }
        }
        
        GUILayout.EndVertical();
    }
    
    private void DrawControlsSection()
    {
        GUILayout.BeginVertical(_boxStyle);
        
        _showControls = EditorGUILayout.Foldout(_showControls, "Controls", true, EditorStyles.foldoutHeader);
        
        if (_showControls)
        {
            EditorGUILayout.PropertyField(_teleportationAimKeyProp, new GUIContent("Aim Key", "Key used to start teleport aiming"));
            EditorGUILayout.PropertyField(_teleportationExecuteKeyProp, new GUIContent("Execute Key", "Key used to execute teleportation"));
            EditorGUILayout.PropertyField(_teleportationCancelKeyProp, new GUIContent("Cancel Key", "Key used to cancel teleportation"));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("Consider replacing these keys with your game's input system.", MessageType.Info);
        }
        
        GUILayout.EndVertical();
    }
    
    private void DrawComponentsSection()
    {
        GUILayout.BeginVertical(_boxStyle);
        
        _showComponents = EditorGUILayout.Foldout(_showComponents, "Sub-Components", true, EditorStyles.foldoutHeader);
        
        if (_showComponents)
        {
            EditorGUILayout.PropertyField(_chargeManagerProp, new GUIContent("Charge Manager", "Manages teleportation charges and cooldowns"));
            EditorGUILayout.PropertyField(_targetDetectorProp, new GUIContent("Target Detector", "Detects valid teleportation destinations"));
            EditorGUILayout.PropertyField(_visualizerProp, new GUIContent("Visualizer", "Handles visual representation of teleport targets"));
            EditorGUILayout.PropertyField(_executorProp, new GUIContent("Executor", "Executes the teleportation movement"));
            
            TeleportationSystem teleportSystem = (TeleportationSystem)target;
            
            bool componentsSet = true;
            if (_chargeManagerProp.objectReferenceValue == null) componentsSet = false;
            if (_targetDetectorProp.objectReferenceValue == null) componentsSet = false;
            if (_visualizerProp.objectReferenceValue == null) componentsSet = false;
            if (_executorProp.objectReferenceValue == null) componentsSet = false;
            
            if (!componentsSet)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("One or more sub-components are missing. These will be auto-created at runtime, but you can create them now for better configuration.", MessageType.Warning);
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                
                EditorGUI.BeginDisabledGroup(Application.isPlaying);
                if (GUILayout.Button("Create Missing Components", GUILayout.Width(200)))
                {
                    CreateMissingComponents();
                }
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        GUILayout.EndVertical();
    }
    
    private void DrawDebugSection()
    {
        GUILayout.BeginVertical(_boxStyle);
        
        _showDebug = EditorGUILayout.Foldout(_showDebug, "Debug", true, EditorStyles.foldoutHeader);
        
        if (_showDebug)
        {
            TeleportationSystem teleportSystem = (TeleportationSystem)target;
            
            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            
            // Show current state
            string currentState = Application.isPlaying ? teleportSystem.CurrentState.ToString() : "Not Playing";
            EditorGUILayout.LabelField("Current State", currentState);
            
            // Show charges if available
            string chargesInfo = "N/A";
            if (Application.isPlaying && teleportSystem.GetComponent<TeleportChargeManager>() != null)
            {
                chargesInfo = $"{teleportSystem.GetCurrentCharges()} / {(teleportSystem.settings != null ? teleportSystem.settings.maxCharges : 0)}";
            }
            EditorGUILayout.LabelField("Current Charges", chargesInfo);
            
            // Test teleport button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Test Teleport Forward (5m)", GUILayout.Width(200)))
            {
                if (teleportSystem.GetCurrentCharges() > 0)
                {
                    Vector3 forwardPos = teleportSystem.transform.position + teleportSystem.transform.forward * 5f;
                    teleportSystem.ForceTeleport(forwardPos);
                }
                else
                {
                    Debug.LogWarning("Cannot test teleport: No charges available!");
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Add charge button 
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Add Charge", GUILayout.Width(100)))
            {
                teleportSystem.AddCharges(1);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.EndDisabledGroup();
        }
        
        GUILayout.EndVertical();
    }
    
    private void CreateSettingsAsset()
    {
        // Create a settings asset
        TeleportationSettings settings = ScriptableObject.CreateInstance<TeleportationSettings>();
        
        // Set default values
        settings.teleportableSurfaces = LayerMask.GetMask("Default");
        settings.teleportationBlockers = 0; // You may want to set up a specific layer for blockers
        
        // Configure some reasonable defaults
        settings.maxTeleportationDistance = 12f;
        settings.minTeleportationDistance = 2f;
        settings.maxCharges = 3;
        settings.chargeCooldownDuration = 3f;
        
        // Create the asset
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Teleportation Settings",
            "TeleportationSettings",
            "asset",
            "Save teleportation settings as"
        );
        
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Assign to the teleportation system
            _settingsProp.objectReferenceValue = settings;
            serializedObject.ApplyModifiedProperties();
            
            // Show the asset
            EditorGUIUtility.PingObject(settings);
        }
    }
    
    private void CreateMissingComponents()
    {
        TeleportationSystem teleportSystem = (TeleportationSystem)target;
        GameObject go = teleportSystem.gameObject;
        
        bool needsUpdate = false;
        
        // Create charge manager if missing
        if (_chargeManagerProp.objectReferenceValue == null)
        {
            Undo.RecordObject(go, "Add Charge Manager");
            TeleportChargeManager chargeManager = go.GetComponent<TeleportChargeManager>();
            if (chargeManager == null)
            {
                chargeManager = go.AddComponent<TeleportChargeManager>();
            }
            _chargeManagerProp.objectReferenceValue = chargeManager;
            needsUpdate = true;
        }
        
        // Create target detector if missing
        if (_targetDetectorProp.objectReferenceValue == null)
        {
            Undo.RecordObject(go, "Add Target Detector");
            TeleportTargetDetector targetDetector = go.GetComponent<TeleportTargetDetector>();
            if (targetDetector == null)
            {
                targetDetector = go.AddComponent<TeleportTargetDetector>();
            }
            _targetDetectorProp.objectReferenceValue = targetDetector;
            needsUpdate = true;
        }
        
        // Create visualizer if missing
        if (_visualizerProp.objectReferenceValue == null)
        {
            Undo.RecordObject(go, "Add Visualizer");
            TeleportVisualizer visualizer = go.GetComponent<TeleportVisualizer>();
            if (visualizer == null)
            {
                visualizer = go.AddComponent<TeleportVisualizer>();
            }
            _visualizerProp.objectReferenceValue = visualizer;
            needsUpdate = true;
        }
        
        // Create executor if missing
        if (_executorProp.objectReferenceValue == null)
        {
            Undo.RecordObject(go, "Add Executor");
            TeleportExecutor executor = go.GetComponent<TeleportExecutor>();
            if (executor == null)
            {
                executor = go.AddComponent<TeleportExecutor>();
            }
            _executorProp.objectReferenceValue = executor;
            needsUpdate = true;
        }
        
        if (needsUpdate)
        {
            serializedObject.ApplyModifiedProperties();
        }
    }
    
    private void AutoSetupComponents()
    {
        TeleportationSystem teleportSystem = (TeleportationSystem)target;
        GameObject go = teleportSystem.gameObject;
        
        // Create missing components
        CreateMissingComponents();
        
        // Try to find player references
        if (_playerRigidbodyProp.objectReferenceValue == null)
        {
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody>();
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.freezeRotation = true;
            }
            _playerRigidbodyProp.objectReferenceValue = rb;
        }
        
        // Try to find camera
        if (_playerCameraProp.objectReferenceValue == null)
        {
            // Look for camera as child first
            Camera childCamera = go.GetComponentInChildren<Camera>();
            if (childCamera != null)
            {
                _playerCameraProp.objectReferenceValue = childCamera.transform;
            }
            else
            {
                // Try main camera
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    _playerCameraProp.objectReferenceValue = mainCamera.transform;
                }
            }
        }
        
        // Try to find orientation
        if (_orientationProp.objectReferenceValue == null)
        {
            Transform orientationTransform = go.transform.Find("Orientation");
            if (orientationTransform == null)
            {
                // Create orientation transform
                GameObject orientationObj = new GameObject("Orientation");
                orientationObj.transform.SetParent(go.transform);
                orientationObj.transform.localPosition = Vector3.zero;
                orientationObj.transform.localRotation = Quaternion.identity;
                orientationTransform = orientationObj.transform;
            }
            _orientationProp.objectReferenceValue = orientationTransform;
        }
        
        // Set player object to self if not set
        if (_playerObjProp.objectReferenceValue == null)
        {
            _playerObjProp.objectReferenceValue = go.transform;
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}
#endif