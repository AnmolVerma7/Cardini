﻿/* UltimateRadialMenuEditor.cs */
/* Written by Kaz Crowe */
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditorInternal;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[CustomEditor( typeof( UltimateRadialMenu ) )]
public class UltimateRadialMenuEditor : Editor
{
	UltimateRadialMenu targ;

	// GENERATE RADIAL MENU OPTIONS //
	int menuButtonCount = 4;

	// RADIAL MENU POSITIONING //
	SerializedProperty scalingAxis, menuSize;
	SerializedProperty horizontalPosition, verticalPosition, depthPosition;
	SerializedProperty menuButtonSize, radialMenuButtonRadius;
	SerializedProperty angleOffset, followOrbitalRotation;
	SerializedProperty startingAngle;
	// Canvas Options //
	UnityEngine.Canvas parentCanvas;
	RectTransform parentCanvasRectTrans;
	float parentCanvasScale = 1.0f;
	Vector3 parentCanvasPosition = Vector3.zero;
	Vector3 parentCanvasRotation = Vector3.zero;
	Vector2 parentCanvasSizeDelta = Vector2.zero;
	// Input Settings //
	SerializedProperty minRange, maxRange;
	SerializedProperty infiniteMaxRange, buttonInputAngle;

	// RADIAL MENU SETTINGS //
	SerializedProperty radialMenuStyle, normalSprite, normalColor;
	// Menu Toggle //
	SerializedProperty initialState, radialMenuToggle, toggleInDuration, toggleOutDuration;
	// Text Options //
	Color nameTextColor = Color.white, nameTextOutlineColor = Color.white;
	SerializedProperty displayButtonName, nameText, nameFont, nameOutline;
	SerializedProperty nameTextRatioX, nameTextRatioY, nameTextSize, nameTextHorizontalPosition, nameTextVerticalPosition;
	Color descriptionTextColor = Color.white, descriptionTextOutlineColor = Color.white;
	SerializedProperty displayButtonDescription, descriptionText, descriptionFont, descriptionOutline;
	SerializedProperty descriptionTextRatioX, descriptionTextRatioY, descriptionTextSize, descriptionTextHorizontalPosition, descriptionTextVerticalPosition;
	// Pointer //
	int newStyleButtonCount = 2;
	bool duplicateButtonCount = false;
	Image pointerImage;
	Sprite pointerSprite = null;
	// Button Icon //
	Sprite iconPlaceholderSprite;
	SerializedProperty useButtonIcon, iconNormalColor;
	SerializedProperty iconSize, iconHorizontalPosition, iconVerticalPosition, iconRotation, iconLocalRotation;
	// Button Text //
	SerializedProperty useButtonText, textNormalColor;
	SerializedProperty textAreaRatioX, textAreaRatioY, textSize, textHorizontalPosition, textVerticalPosition;
	SerializedProperty textLocalPosition, textLocalRotation, displayNameOnButton, buttonTextFont, buttonTextOutline, buttonTextOutlineColor;

	// BUTTON INTERACTION //
	SerializedProperty spriteSwap, colorChange,  scaleTransform;
	SerializedProperty iconColorChange, iconScaleTransform, textColorChange;
	// Highlighted //
	SerializedProperty highlightedSprite, highlightedColor, highlightedScaleModifier, positionModifier;
	SerializedProperty iconHighlightedColor, iconHighlightedScaleModifier, textHighlightedColor;
	// Pressed //
	SerializedProperty pressedSprite, pressedColor, pressedScaleModifier, pressedPositionModifier;
	SerializedProperty iconPressedColor, iconPressedScaleModifier, textPressedColor;
	// Selected //
	SerializedProperty selectedSprite, selectedColor, selectedScaleModifier, selectedPositionModifier, selectButtonOnInteract;
	SerializedProperty iconSelectedColor, iconSelectedScaleModifier, textSelectedColor;
	// Disabled //
	SerializedProperty disabledSprite, disabledColor, disabledScaleModifier, disabledPositionModifier;
	SerializedProperty iconDisabledColor, iconDisabledScaleModifier, textDisabledColor;

	// MENU BUTTON CUSTOMIZATION //
	ReorderableList ReorderableRadialButtons;
	static int selectedRadialButtonIndex = 0;
	int SelectedRadialButtonIndex
	{
		get
		{
			return selectedRadialButtonIndex;
		}
		set
		{
			selectedRadialButtonIndex = value;

			if( ReorderableRadialButtons != null )
				ReorderableRadialButtons.index = value;
		}
	}
	List<string> buttonNames = new List<string>();
	List<SerializedProperty> buttonTransform;
	List<SerializedProperty> buttonName, description;
	List<SerializedProperty> icon, text;
	List<Sprite> iconSprites;
	List<string> buttonText;
	List<SerializedProperty> rmbIconSize, rmbIconRotation;
	List<SerializedProperty> rmbIconHorizontalPosition, rmbIconVerticalPosition;
	List<SerializedProperty> useIconUnique, invertScaleY;
	List<SerializedProperty> buttonDisabled, unityEvent;

	// SCRIPT REFERENCE //
	bool RadialMenuNameAssigned, RadialMenuNameDuplicate, RadialMenuNameUnassigned;
	SerializedProperty radialMenuName;
	class ExampleCode
	{
		public string optionName = "";
		public string optionDescription = "";
		public string basicCode = "";
	}
	ExampleCode[] StaticExampleCodes = new ExampleCode[]
	{
		new ExampleCode() { optionName = "RegisterButton", optionDescription = "Registers the provided information to the targeted radial menu.", basicCode = "UltimateRadialMenu.RegisterButton( \"{0}\", YourCallbackFunction, buttonInfo );" },
		new ExampleCode() { optionName = "Enable", optionDescription = "Enables the radial menu visually.", basicCode = "UltimateRadialMenu.Enable( \"{0}\" );" },
		new ExampleCode() { optionName = "Disable", optionDescription = "Disables the radial menu visually.", basicCode = "UltimateRadialMenu.Disable( \"{0}\" );" },
		new ExampleCode() { optionName = "RemoveButton", optionDescription = "Removes the radial button at the targeted index.", basicCode = "UltimateRadialMenu.RemoveButton( \"{0}\", 0 );" },
		new ExampleCode() { optionName = "ClearMenu", optionDescription = "Removes all of the buttons on the menu.", basicCode = "UltimateRadialMenu.ClearMenu( \"{0}\" );" },
		new ExampleCode() { optionName = "ClearButtonInformations", optionDescription = "Clears all of the registered information on the radial menu but leaves the buttons on the menu.", basicCode = "UltimateRadialMenu.ClearButtonInformations( \"{0}\" );" },
		new ExampleCode() { optionName = "ReturnComponent", optionDescription = "Returns the Ultimate Radial Menu component that is registered with the target name.", basicCode = "UltimateRadialMenu.ReturnComponent( \"{0}\" );" },
	};
	ExampleCode[] PublicExampleCodes = new ExampleCode[]
	{
		new ExampleCode() { optionName = "RegisterButton", optionDescription = "Registers the provided information to this radial menu.", basicCode = "radialMenu.RegisterButton( YourCallbackFunction, buttonInfo );" },
		new ExampleCode() { optionName = "Enable", optionDescription = "Enables the radial menu visually.", basicCode = "radialMenu.Enable();" },
		new ExampleCode() { optionName = "Disable", optionDescription = "Disables the radial menu visually.", basicCode = "radialMenu.Disable();" },
		new ExampleCode() { optionName = "RemoveButton", optionDescription = "Removes the radial button at the targeted index.", basicCode = "radialMenu.RemoveButton( 0 );" },
		new ExampleCode() { optionName = "ClearMenu", optionDescription = "Removes all of the buttons on the menu.", basicCode = "radialMenu.ClearMenu();" },
		new ExampleCode() { optionName = "ClearButtonInformations", optionDescription = "Clears all of the registered information on the radial menu but leaves the buttons on the menu.", basicCode = "radialMenu.ClearButtonInformations();" },
	};
	List<string> exampleCodeOptions = new List<string>();
	int exampleCodeIndex = 0;

	// DEVELOPMENT MODE //
	bool showDefaultInspector = false;
	
	// SCENE GUI //
	class DisplaySceneGizmo
	{
		public bool hover = false;

		public bool HighlightGizmo
		{
			get
			{
				return hover;
			}
		}
	}
	DisplaySceneGizmo DisplayMinRange = new DisplaySceneGizmo();
	DisplaySceneGizmo DisplayMaxRange = new DisplaySceneGizmo();
	DisplaySceneGizmo DisplayInputAngle = new DisplaySceneGizmo();
	static bool isDirty = false;
	bool wasDirtyLastFrame = false;
	// Gizmo Colors //
	Color colorDefault = Color.black;
	Color colorValueChanged = Color.cyan;
	Color colorButtonSelected = Color.yellow;
	Color colorButtonUnselected = Color.white;
	Color colorTextBox = Color.yellow;

	// EDITOR STYLES //
	GUIStyle collapsableSectionStyle = new GUIStyle();
	GUIStyle helpBoxStyle = new GUIStyle();

	// MISC //
	bool prefabRootError = false;
	bool isInProjectWindow = false;

	// DRAG AND DROP //
	bool disableDragAndDrop = false;
	bool isDraggingObject = false;
	Vector2 dragAndDropMousePos = Vector2.zero;
	double dragAndDropStartTime = 0.0f;
	double dragAndDropCurrentTime = 0.0f;

	string BuiltInFontResource
	{
		get
		{
#if UNITY_2022_2_OR_NEWER
			return "LegacyRuntime.ttf";
#else
			return "Arial.ttf";
#endif
		}
	}


	void OnEnable ()
	{
		StoreReferences();
		
		Undo.undoRedoPerformed += StoreReferences;

		if( EditorPrefs.HasKey( "URM_ColorHexSetup" ) )
		{
			ColorUtility.TryParseHtmlString( EditorPrefs.GetString( "URM_ColorDefaultHex" ), out colorDefault );
			ColorUtility.TryParseHtmlString( EditorPrefs.GetString( "URM_ColorValueChangedHex" ), out colorValueChanged );
			ColorUtility.TryParseHtmlString( EditorPrefs.GetString( "URM_ColorButtonSelectedHex" ), out colorButtonSelected );
			ColorUtility.TryParseHtmlString( EditorPrefs.GetString( "URM_ColorButtonUnselectedHex" ), out colorButtonUnselected );
			ColorUtility.TryParseHtmlString( EditorPrefs.GetString( "URM_ColorTextBoxHex" ), out colorTextBox );
		}

		prefabRootError = false;
		if( PrefabUtility.GetPrefabAssetType( targ.gameObject ) != PrefabAssetType.NotAPrefab )
		{
			if( PrefabUtility.GetOutermostPrefabInstanceRoot( targ.gameObject ) != targ.gameObject )
			{
				if( PrefabUtility.GetOutermostPrefabInstanceRoot( targ.gameObject ) != null )
					prefabRootError = true;
			}
			else if( PrefabUtility.GetOutermostPrefabInstanceRoot( targ.gameObject ) == targ.gameObject )
				PrefabUtility.UnpackPrefabInstance( targ.gameObject, PrefabUnpackMode.Completely, InteractionMode.UserAction );
		}

		disableDragAndDrop = EditorPrefs.GetBool( "UUI_DisableDragAndDrop" );
	}

	void OnDisable ()
	{
		Undo.undoRedoPerformed -= StoreReferences;
	}

	void StoreReferences ()
	{
		targ = ( UltimateRadialMenu )target;

		if( targ == null )
			return;
		
		isInProjectWindow = Selection.activeGameObject != null && AssetDatabase.Contains( Selection.activeGameObject );
		
		CheckForParentCanvas();
		
		if( SelectedRadialButtonIndex >= targ.UltimateRadialButtonList.Count )
			SelectedRadialButtonIndex = 0;
		
		// GENERATE RADIAL MENU OPTIONS //
		followOrbitalRotation = serializedObject.FindProperty( "followOrbitalRotation" );
		startingAngle = serializedObject.FindProperty( "startingAngle" );

		// RADIAL MENU POSITIONING //
		scalingAxis = serializedObject.FindProperty( "scalingAxis" );
		menuSize = serializedObject.FindProperty( "menuSize" );
		horizontalPosition = serializedObject.FindProperty( "horizontalPosition" );
		verticalPosition = serializedObject.FindProperty( "verticalPosition" );
		depthPosition = serializedObject.FindProperty( "depthPosition" );
		menuButtonSize = serializedObject.FindProperty( "menuButtonSize" );
		radialMenuButtonRadius = serializedObject.FindProperty( "radialMenuButtonRadius" );
		angleOffset = serializedObject.FindProperty( "angleOffset" );
		// Canvas Options //
		if( parentCanvas != null )
		{
			parentCanvasRectTrans = parentCanvas.GetComponent<RectTransform>();
			parentCanvasScale = parentCanvasRectTrans.localScale.x;
			parentCanvasPosition = parentCanvasRectTrans.position;
			parentCanvasRotation = parentCanvasRectTrans.eulerAngles;
			parentCanvasSizeDelta = parentCanvasRectTrans.sizeDelta;
		}
		// Input Settings //
		minRange = serializedObject.FindProperty( "minRange" );
		maxRange = serializedObject.FindProperty( "maxRange" );
		infiniteMaxRange = serializedObject.FindProperty( "infiniteMaxRange" );
		buttonInputAngle = serializedObject.FindProperty( "buttonInputAngle" );

		// RADIAL MENU OPTIONS //
		radialMenuStyle = serializedObject.FindProperty( "radialMenuStyle" );
		normalSprite = serializedObject.FindProperty( "normalSprite" );
		normalColor = serializedObject.FindProperty( "normalColor" );
		// Menu Toggle //
		initialState = serializedObject.FindProperty( "initialState" );
		radialMenuToggle = serializedObject.FindProperty( "radialMenuToggle" );
		toggleInDuration = serializedObject.FindProperty( "toggleInDuration" );
		toggleOutDuration = serializedObject.FindProperty( "toggleOutDuration" );
		// Pointer //
		pointerImage = ( Image )serializedObject.FindProperty( "pointerImage" ).objectReferenceValue;
		// Menu Text //
		displayButtonName = serializedObject.FindProperty( "displayButtonName" );
		nameText = serializedObject.FindProperty( "nameText" );
		if( targ.nameText != null )
			nameTextColor = targ.nameText.color;
		nameFont = serializedObject.FindProperty( "nameFont" );
		if( targ.nameFont == null )
		{
			serializedObject.FindProperty( "nameFont" ).objectReferenceValue = Resources.GetBuiltinResource<Font>( BuiltInFontResource );
			serializedObject.ApplyModifiedProperties();
		}
		nameOutline = serializedObject.FindProperty( "nameOutline" );
		if( targ.nameText != null && targ.nameText.GetComponent<UnityEngine.UI.Outline>() )
			nameTextOutlineColor = targ.nameText.GetComponent<UnityEngine.UI.Outline>().effectColor;
		nameTextRatioX = serializedObject.FindProperty( "nameTextRatioX" );
		nameTextRatioY = serializedObject.FindProperty( "nameTextRatioY" );
		nameTextSize = serializedObject.FindProperty( "nameTextSize" );
		nameTextHorizontalPosition = serializedObject.FindProperty( "nameTextHorizontalPosition" );
		nameTextVerticalPosition = serializedObject.FindProperty( "nameTextVerticalPosition" );
		displayButtonDescription = serializedObject.FindProperty( "displayButtonDescription" );
		descriptionText = serializedObject.FindProperty( "descriptionText" );
		if( targ.descriptionText != null )
			descriptionTextColor = targ.descriptionText.color;
		descriptionFont = serializedObject.FindProperty( "descriptionFont" );
		if( targ.descriptionFont == null )
		{
			serializedObject.FindProperty( "descriptionFont" ).objectReferenceValue = Resources.GetBuiltinResource<Font>( BuiltInFontResource );
			serializedObject.ApplyModifiedProperties();
		}
		descriptionOutline = serializedObject.FindProperty( "descriptionOutline" );
		if( targ.descriptionText != null && targ.descriptionText.GetComponent<UnityEngine.UI.Outline>() )
			descriptionTextOutlineColor = targ.descriptionText.GetComponent<UnityEngine.UI.Outline>().effectColor;
		descriptionTextRatioX = serializedObject.FindProperty( "descriptionTextRatioX" );
		descriptionTextRatioY = serializedObject.FindProperty( "descriptionTextRatioY" );
		descriptionTextSize = serializedObject.FindProperty( "descriptionTextSize" );
		descriptionTextHorizontalPosition = serializedObject.FindProperty( "descriptionTextHorizontalPosition" );
		descriptionTextVerticalPosition = serializedObject.FindProperty( "descriptionTextVerticalPosition" );
		// Button Icon //
		useButtonIcon = serializedObject.FindProperty( "useButtonIcon" );
		iconSize = serializedObject.FindProperty( "iconSize" );
		iconHorizontalPosition = serializedObject.FindProperty( "iconHorizontalPosition" );
		iconVerticalPosition = serializedObject.FindProperty( "iconVerticalPosition" );
		iconRotation = serializedObject.FindProperty( "iconRotation" );
		iconLocalRotation = serializedObject.FindProperty( "iconLocalRotation" );
		iconNormalColor = serializedObject.FindProperty( "iconNormalColor" );
		// Button Text //
		useButtonText = serializedObject.FindProperty( "useButtonText" );
		textLocalRotation = serializedObject.FindProperty( "textLocalRotation" );
		buttonTextFont = serializedObject.FindProperty( "buttonTextFont" );
		if( targ.buttonTextFont == null )
		{
			serializedObject.FindProperty( "buttonTextFont" ).objectReferenceValue = Resources.GetBuiltinResource<Font>( BuiltInFontResource );
			serializedObject.ApplyModifiedProperties();
		}
		buttonTextOutline = serializedObject.FindProperty( "buttonTextOutline" );
		buttonTextOutlineColor = serializedObject.FindProperty( "buttonTextOutlineColor" );
		textNormalColor = serializedObject.FindProperty( "textNormalColor" );
		textSize = serializedObject.FindProperty( "textSize" );
		textHorizontalPosition = serializedObject.FindProperty( "textHorizontalPosition" );
		textVerticalPosition = serializedObject.FindProperty( "textVerticalPosition" );
		textAreaRatioX = serializedObject.FindProperty( "textAreaRatioX" );
		textAreaRatioY = serializedObject.FindProperty( "textAreaRatioY" );
		textLocalPosition = serializedObject.FindProperty( "textLocalPosition" );
		displayNameOnButton = serializedObject.FindProperty( "displayNameOnButton" );

		// BUTTON INTERACTION //
		spriteSwap = serializedObject.FindProperty( "spriteSwap" );
		colorChange = serializedObject.FindProperty( "colorChange" );
		scaleTransform = serializedObject.FindProperty( "scaleTransform" );
		iconColorChange = serializedObject.FindProperty( "iconColorChange" );
		iconScaleTransform = serializedObject.FindProperty( "iconScaleTransform" );
		textColorChange = serializedObject.FindProperty( "textColorChange" );
		// Highlighted //
		highlightedSprite = serializedObject.FindProperty( "highlightedSprite" );
		highlightedColor = serializedObject.FindProperty( "highlightedColor" );
		highlightedScaleModifier = serializedObject.FindProperty( "highlightedScaleModifier" );
		positionModifier = serializedObject.FindProperty( "positionModifier" );
		iconHighlightedColor = serializedObject.FindProperty( "iconHighlightedColor" );
		iconHighlightedScaleModifier = serializedObject.FindProperty( "iconHighlightedScaleModifier" );
		textHighlightedColor = serializedObject.FindProperty( "textHighlightedColor" );
		// Pressed //
		pressedSprite = serializedObject.FindProperty( "pressedSprite" );
		pressedColor = serializedObject.FindProperty( "pressedColor" );
		pressedScaleModifier = serializedObject.FindProperty( "pressedScaleModifier" );
		pressedPositionModifier = serializedObject.FindProperty( "pressedPositionModifier" );
		iconPressedColor = serializedObject.FindProperty( "iconPressedColor" );
		iconPressedScaleModifier = serializedObject.FindProperty( "iconPressedScaleModifier" );
		textPressedColor = serializedObject.FindProperty( "textPressedColor" );
		// Selected //
		selectedSprite = serializedObject.FindProperty( "selectedSprite" );
		selectedColor = serializedObject.FindProperty( "selectedColor" );
		selectedScaleModifier = serializedObject.FindProperty( "selectedScaleModifier" );
		selectedPositionModifier = serializedObject.FindProperty( "selectedPositionModifier" );
		selectButtonOnInteract = serializedObject.FindProperty( "selectButtonOnInteract" );
		iconSelectedColor = serializedObject.FindProperty( "iconSelectedColor" );
		iconSelectedScaleModifier = serializedObject.FindProperty( "iconSelectedScaleModifier" );
		textSelectedColor = serializedObject.FindProperty( "textSelectedColor" );
		// Disabled //
		disabledSprite = serializedObject.FindProperty( "disabledSprite" );
		disabledColor = serializedObject.FindProperty( "disabledColor" );
		disabledScaleModifier = serializedObject.FindProperty( "disabledScaleModifier" );
		disabledPositionModifier = serializedObject.FindProperty( "disabledPositionModifier" );
		iconDisabledColor = serializedObject.FindProperty( "iconDisabledColor" );
		iconDisabledScaleModifier = serializedObject.FindProperty( "iconDisabledScaleModifier" );
		textDisabledColor = serializedObject.FindProperty( "textDisabledColor" );

		// RADIAL BUTTON LIST //
		buttonTransform = new List<SerializedProperty>();
		buttonName = new List<SerializedProperty>();
		description = new List<SerializedProperty>();
		icon = new List<SerializedProperty>();
		iconSprites = new List<Sprite>();
		buttonText = new List<string>();
		rmbIconSize = new List<SerializedProperty>();
		rmbIconHorizontalPosition = new List<SerializedProperty>();
		rmbIconVerticalPosition = new List<SerializedProperty>();
		rmbIconRotation = new List<SerializedProperty>();
		useIconUnique = new List<SerializedProperty>();
		invertScaleY = new List<SerializedProperty>();
		text = new List<SerializedProperty>();
		buttonDisabled = new List<SerializedProperty>();
		unityEvent = new List<SerializedProperty>();
		buttonNames = new List<string>();

		for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
		{
			buttonNames.Add( "Radial Button " + i.ToString( "00" ) );
			if( targ.UltimateRadialButtonList[ i ].buttonTransform.name.Contains( "Radial" ) )
				targ.UltimateRadialButtonList[ i ].buttonTransform.name = "Radial Button " + i.ToString( "00" );

			if( i > 0 )
				targ.UltimateRadialButtonList[ i ].buttonTransform.SetSiblingIndex( targ.UltimateRadialButtonList[ i - 1 ].buttonTransform.GetSiblingIndex() + 1 );

			buttonTransform.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].buttonTransform", i ) ) );
			buttonName.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].name", i ) ) );
			description.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].description", i ) ) );

			icon.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].icon", i ) ) );
			iconSprites.Add( targ.UltimateRadialButtonList[ i ].icon != null ? targ.UltimateRadialButtonList[ i ].icon.sprite : null );
			buttonText.Add( targ.UltimateRadialButtonList[ i ].text != null ? targ.UltimateRadialButtonList[ i ].text.text : string.Empty );
			rmbIconSize.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].iconSize", i ) ) );
			rmbIconHorizontalPosition.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].iconHorizontalPosition", i ) ) );
			useIconUnique.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].useIconUnique", i ) ) );
			invertScaleY.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].invertScaleY", i ) ) );
			rmbIconVerticalPosition.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].iconVerticalPosition", i ) ) );
			rmbIconRotation.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].iconRotation", i ) ) );

			text.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].text", i ) ) );
			buttonDisabled.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].buttonDisabled", i ) ) );
			unityEvent.Add( serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].unityEvent", i ) ) );

			if( targ.useButtonIcon && targ.UltimateRadialButtonList[ i ].icon == null && icon[ i ] != null )
			{
				Image[] possibleImages = targ.UltimateRadialButtonList[ i ].buttonTransform.gameObject.GetComponentsInChildren<Image>();
				for( int n = 0; n < possibleImages.Length; n++ )
				{
					if( possibleImages[ n ] == targ.UltimateRadialButtonList[ i ].radialImage )
						continue;

					if( possibleImages[ n ].name != "Icon" )
						continue;

					icon[ i ].objectReferenceValue = possibleImages[ n ];
					serializedObject.ApplyModifiedProperties();
				}
			}
			if( targ.useButtonText && targ.UltimateRadialButtonList[ i ].text == null && text[ i ] != null )
			{
				Text childText = targ.UltimateRadialButtonList[ i ].buttonTransform.gameObject.GetComponentInChildren<Text>();

				if( childText != null )
				{
					text[ i ].objectReferenceValue = childText;
					serializedObject.ApplyModifiedProperties();
				}
			}
		}

		// SCRIPT REFERENCE //
		RadialMenuNameDuplicate = DuplicateRadialMenuName();
		RadialMenuNameUnassigned = targ.radialMenuName == string.Empty;
		RadialMenuNameAssigned = RadialMenuNameDuplicate == false && targ.radialMenuName != string.Empty;
		radialMenuName = serializedObject.FindProperty( "radialMenuName" );
		exampleCodeOptions = new List<string>();
		
		UpdateExampleCodeOptions();

#if UNITY_2022_2_OR_NEWER
		EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
#else
		EventSystem eventSystem = FindObjectOfType<EventSystem>();
#endif
		if( !targ.GetComponent<UltimateRadialMenuInputManager>() && eventSystem != null && !eventSystem.GetComponent<UltimateRadialMenuInputManager>() )
			Undo.AddComponent( eventSystem.gameObject, typeof( UltimateRadialMenuInputManager ) );

		SetupReorderableList();
	}

	void SetupReorderableList ()
	{
		if( targ.UltimateRadialButtonList.Count == 0 || isInProjectWindow )
			return;

		ReorderableRadialButtons = new ReorderableList( serializedObject, serializedObject.FindProperty( "UltimateRadialButtonList.Array" ), true, false, false, false )
		{
			headerHeight = 1.0f,
			footerHeight = 0.0f,
		};
		
		ReorderableRadialButtons.drawElementCallback = ( Rect rect, int index, bool isActive, bool isFocused ) =>
		{
			if( index >= targ.UltimateRadialButtonList.Count )
				return;

			EditorGUI.LabelField( new Rect( rect.x, rect.y, EditorGUIUtility.currentViewWidth, EditorGUIUtility.singleLineHeight ), "Radial Button " + index.ToString( "00" ) );
		};

		ReorderableRadialButtons.onSelectCallback = ( ReorderableList l ) =>
		{
			SelectedRadialButtonIndex = l.index;
			SceneView.RepaintAll();
		};

		ReorderableRadialButtons.onReorderCallback = ( ReorderableList l ) =>
		{
			SelectedRadialButtonIndex = ReorderableRadialButtons.index;
		};
	}

	bool DisplayHeaderDropdown ( string headerName, string editorPref )
	{
		EditorGUILayout.Space();

		GUIStyle toolbarStyle = new GUIStyle( EditorStyles.toolbarButton ) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = 11 };
		GUILayout.BeginHorizontal();
		GUILayout.Space( -10 );

		if( GUILayout.Button( ( EditorPrefs.GetBool( editorPref ) ? "▼ " : "► " ) + headerName, toolbarStyle ) )
		{
			EditorPrefs.SetBool( editorPref, !EditorPrefs.GetBool( editorPref ) );
			SceneView.RepaintAll();
		}
		GUILayout.EndHorizontal();

		if( EditorPrefs.GetBool( editorPref ) )
		{
			EditorGUILayout.Space();
			return true;
		}
		return false;
	}

	void EndMainSection ( string editorPref )
	{
		if( EditorPrefs.GetBool( editorPref ) )
			GUILayout.Space( 1 );
		else if( DragAndDropHover() )
		{
			EditorPrefs.SetBool( editorPref, true );
			SceneView.RepaintAll();
		}
	}

	bool DisplayCollapsibleBoxSection ( string sectionTitle, string editorPref, bool error = false )
	{
		if( error )
			sectionTitle += " <color=#ff0000ff>*</color>";

		EditorGUILayout.BeginVertical( "Box" );

		if( EditorPrefs.GetBool( editorPref ) )
			collapsableSectionStyle.fontStyle = FontStyle.Bold;

		if( GUILayout.Button( sectionTitle, collapsableSectionStyle ) )
		{
			EditorPrefs.SetBool( editorPref, !EditorPrefs.GetBool( editorPref ) );
			SceneView.RepaintAll();
		}

		if( EditorPrefs.GetBool( editorPref ) )
			collapsableSectionStyle.fontStyle = FontStyle.Normal;

		return EditorPrefs.GetBool( editorPref );
	}

	bool DisplayCollapsibleBoxSection ( string sectionTitle, string editorPref, SerializedProperty enabledProp, ref bool valueChanged, bool error = false )
	{
		valueChanged = false;

		if( enabledProp.boolValue && error )
			sectionTitle += " <color=#ff0000ff>*</color>";

		EditorGUILayout.BeginVertical( "Box" );

		if( EditorPrefs.GetBool( editorPref ) && enabledProp.boolValue )
			collapsableSectionStyle.fontStyle = FontStyle.Bold;
		else if( isInProjectWindow && !enabledProp.boolValue  )
			EditorPrefs.SetBool( editorPref, false );

		EditorGUILayout.BeginHorizontal();

		EditorGUI.BeginDisabledGroup( isInProjectWindow );
		EditorGUI.BeginChangeCheck();
		enabledProp.boolValue = EditorGUILayout.Toggle( enabledProp.boolValue, GUILayout.Width( 25 ) );
		if( EditorGUI.EndChangeCheck() )
		{
			serializedObject.ApplyModifiedProperties();
			EditorPrefs.SetBool( editorPref, enabledProp.boolValue );
			valueChanged = true;
			SceneView.RepaintAll();
		}
		EditorGUI.EndDisabledGroup();

		GUILayout.Space( -25 );

		EditorGUI.BeginDisabledGroup( !enabledProp.boolValue );
		if( GUILayout.Button( sectionTitle, collapsableSectionStyle ) )
		{
			EditorPrefs.SetBool( editorPref, !EditorPrefs.GetBool( editorPref ) );
			SceneView.RepaintAll();
		}
		EditorGUI.EndDisabledGroup();

		EditorGUILayout.EndHorizontal();

		if( EditorPrefs.GetBool( editorPref ) )
			collapsableSectionStyle.fontStyle = FontStyle.Normal;

		return EditorPrefs.GetBool( editorPref ) && enabledProp.boolValue;
	}

	void EndCollapsibleSection ( string editorPref )
	{
		if( EditorPrefs.GetBool( editorPref ) )
			GUILayout.Space( 1 );
		else if( DragAndDropHover() )
		{
			EditorPrefs.SetBool( editorPref, true );
			SceneView.RepaintAll();
		}

		EditorGUILayout.EndVertical();
	}

	bool DragAndDropHover ()
	{
		if( disableDragAndDrop )
			return false;

		if( DragAndDrop.objectReferences.Length == 0 )
		{
			dragAndDropStartTime = 0.0f;
			dragAndDropCurrentTime = 0.0f;
			isDraggingObject = false;
			return false;
		}

		isDraggingObject = true;

		var rect = GUILayoutUtility.GetLastRect();
		if( Event.current.type == EventType.Repaint && rect.Contains( Event.current.mousePosition ) )
		{
			if( dragAndDropStartTime == 0.0f )
			{
				dragAndDropStartTime = EditorApplication.timeSinceStartup;
				dragAndDropCurrentTime = 0.0f;
			}

			if( dragAndDropMousePos == Event.current.mousePosition )
				dragAndDropCurrentTime = EditorApplication.timeSinceStartup - dragAndDropStartTime;
			else
			{
				dragAndDropStartTime = EditorApplication.timeSinceStartup;
				dragAndDropCurrentTime = 0.0f;
			}

			if( dragAndDropCurrentTime >= 0.5f )
			{
				dragAndDropStartTime = 0.0f;
				dragAndDropCurrentTime = 0.0f;
				return true;
			}

			dragAndDropMousePos = Event.current.mousePosition;
		}

		return false;
	}

	void CheckPropertyHover ( DisplaySceneGizmo displaySceneGizmo )
	{
		displaySceneGizmo.hover = false;
		var rect = GUILayoutUtility.GetLastRect();
		if( Event.current.type == EventType.Repaint && rect.Contains( Event.current.mousePosition ) )
		{
			displaySceneGizmo.hover = true;
			isDirty = true;
		}
	}

	void GUILayoutAfterIndentSpace ()
	{
		GUILayout.Space( 2 );
	}

	void UpdateExampleCodeOptions ()
	{
		exampleCodeOptions = new List<string>();
		if( targ.radialMenuName != string.Empty )
		{
			for( int i = 0; i < StaticExampleCodes.Length; i++ )
				exampleCodeOptions.Add( StaticExampleCodes[ i ].optionName );
		}
		else
		{
			for( int i = 0; i < PublicExampleCodes.Length; i++ )
				exampleCodeOptions.Add( PublicExampleCodes[ i ].optionName );
		}
	}

	void HelpBox ( string error, string solution )
	{
		EditorGUILayout.LabelField( "<color=red><b>×</b></color> <i><b>Error:</b></i> " + error + ".", helpBoxStyle );
		EditorGUILayout.Space();
		EditorGUILayout.LabelField( "<color=green><b>√</b></color> <i><b>Solution:</b></i> " + solution + ".", helpBoxStyle );
	}

	public override void OnInspectorGUI ()
	{
		serializedObject.Update();

		if( targ == null )
			return;

		collapsableSectionStyle = new GUIStyle( EditorStyles.label ) { alignment = TextAnchor.MiddleCenter, onActive = new GUIStyleState() { textColor = Color.black }, richText = true };
		collapsableSectionStyle.active.textColor = collapsableSectionStyle.normal.textColor;

		helpBoxStyle = new GUIStyle( EditorStyles.label ) { richText = true, wordWrap = true };

		bool valueChanged = false;

		if( prefabRootError )
		{
			EditorGUILayout.BeginVertical( "Box" );
			HelpBox( "The Ultimate Radial Menu is not the root of this prefab and therefore cannot be unpacked properly.\n\nThis can cause some strange behavior, as well as not being able to remove buttons in the editor that are part of the prefab. This is caused because of Unity's new prefab manager", "Please remove the Ultimate Radial Menu from the prefab in order to edit it, or <b>Unpack</b> the root prefab object" );
			EditorGUILayout.EndVertical();
		}

		if( EditorPrefs.GetBool( "UUI_DevelopmentMode" ) )
		{
			EditorGUILayout.Space();
			GUIStyle toolbarStyle = new GUIStyle( EditorStyles.toolbarButton ) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = 11, richText = true };
			GUILayout.BeginHorizontal();
			GUILayout.Space( -10 );
			showDefaultInspector = GUILayout.Toggle( showDefaultInspector, ( showDefaultInspector ? "▼" : "►" ) + "<color=#ff0000ff>Development Inspector</color>", toolbarStyle );
			GUILayout.EndHorizontal();
			if( showDefaultInspector )
			{
				EditorGUILayout.Space();

				base.OnInspectorGUI();
			}
			else if( DragAndDropHover() )
				showDefaultInspector = true;
		}

		if( targ.UltimateRadialButtonList.Count == 0 && !Application.isPlaying )
		{
			EditorGUILayout.BeginVertical( "Box" );

			EditorGUI.BeginChangeCheck();
			menuButtonCount = EditorGUILayout.IntField( new GUIContent( "Menu Button Count", "The amount of menu buttons to generate for this radial menu." ), menuButtonCount );
			if( EditorGUI.EndChangeCheck() )
			{
				if( menuButtonCount < 2 )
					menuButtonCount = 2;
			}
			
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( radialMenuStyle, new GUIContent( "Radial Menu Style", "The style to be used for the radial buttons as the menu changes the count of buttons." ) );
			if( targ.radialMenuStyle != null )
			{
				GUIStyle noteStyle = new GUIStyle( EditorStyles.miniLabel ) { alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true };
				EditorGUILayout.LabelField( "<color=red>*</color> Button sprite driven from assigned style.", noteStyle );
			}
			else
				EditorGUILayout.PropertyField( normalSprite, new GUIContent( "Normal Sprite", "The sprite to be applied to each radial button." ) );

			EditorGUILayout.PropertyField( followOrbitalRotation, new GUIContent( "Follow Orbital Rotation", "Determines whether or not the buttons should follow the rotation of the menu." ) );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			if( GUILayout.Button( "Generate" ) )
			{
				if( targ.radialMenuStyle != null )
					menuButtonSize.floatValue = 1.0f;

				useButtonIcon.boolValue = false;
				useButtonText.boolValue = false;
				serializedObject.ApplyModifiedProperties();
				
				if( targ.radialMenuStyle == null && targ.normalSprite == null )
				{
					if( EditorUtility.DisplayDialog( "Ultimate Radial Menu", "You are about to create a radial menu with no assigned sprites.", "Continue", "Cancel" ) )
						GenerateRadialImages();
				}
				else
					GenerateRadialImages();

				UpdateRadialButtonStyle();
			}

			EditorGUILayout.EndVertical();
			Repaint();
			return;
		}

		if( DisplayHeaderDropdown( "Radial Menu Positioning", "URM_RadialMenuPositioning" ) )
		{
			// CHANGE CHECK FOR APPLYING SETTINGS DURING RUNTIME //
			if( Application.isPlaying )
			{
				EditorGUILayout.HelpBox( "The application is running. Changes made here will revert when exiting play mode.", MessageType.Warning );
				EditorGUI.BeginChangeCheck();
			}

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( scalingAxis, new GUIContent( "Scaling Axis", "Determines whether the Ultimate Radial Menu is sized according to Screen Height or Screen Width." ) );
			EditorGUILayout.Slider( menuSize, 0.0f, 10.0f, new GUIContent( "Menu Size", "Determines the overall size of the radial menu." ) );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.Slider( horizontalPosition, 0.0f, 100.0f, new GUIContent( "Horizontal Position", "The horizontal position of the radial menu." ) );
			EditorGUILayout.Slider( verticalPosition, 0.0f, 100.0f, new GUIContent( "Vertical Position", "The vertical position of the radial menu." ) );
			if( targ.IsWorldSpaceRadialMenu )
				EditorGUILayout.PropertyField( depthPosition, new GUIContent( "Depth Position", "The depth of the radial menu." ) );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.Slider( radialMenuButtonRadius, 0.0f, 1.5f, new GUIContent( "Button Radius", "The distance that the buttons will be from the center of the menu." ) );
			EditorGUILayout.Slider( menuButtonSize, 0.0f, 1.0f, new GUIContent( "Button Size", "The size of the radial buttons." ) );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( followOrbitalRotation, new GUIContent( "Orbital Rotation", "Determines whether or not the buttons should follow the rotation of the menu." ) );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.Slider( serializedObject.FindProperty( "overallAngle" ), 0.0f, 360.0f, new GUIContent( "Overall Angle", "The overall angle for the radial menu buttons to populate." ) );
			if( EditorGUI.EndChangeCheck() )
			{
				if( !EditorPrefs.GetBool( "URM_OverallAngleWarning" ) && serializedObject.FindProperty( "overallAngle" ).floatValue < 360.0f && targ.overallAngle == 360.0f )
				{
					if( EditorUtility.DisplayDialog( "Ultimate Radial Menu", "Lowering the overall angle from 360 degrees will change the setting below from Starting Angle to Center Angle, and positioning calculations will be slightly different. Continue?", "Yes", "No" ) )
					{
						serializedObject.ApplyModifiedProperties();
						EditorPrefs.SetBool( "URM_OverallAngleWarning", true );
					}
					else
					{
						serializedObject.FindProperty( "overallAngle" ).floatValue = 360.0f;
						serializedObject.ApplyModifiedProperties();
					}
				}
				else
					serializedObject.ApplyModifiedProperties();

				UpdateRadialButtonStyle();
			}

			if( targ.overallAngle == 360.0f )
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.Slider( startingAngle, 0.0f, 360.0f, new GUIContent( "Starting Angle", "The angle for the first radial button in the list." ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( angleOffset, new GUIContent( "Angle Offset", "Determines how the first button should be positioned at the top of the menu." ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
			}
			else
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.Slider( serializedObject.FindProperty( "centerAngle" ), -180.0f, 180.0f, new GUIContent( "Center Angle", "The center angle for the whole radial menu buttons." ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				bool customAnchorOffset = serializedObject.FindProperty( "centerOffset" ).vector2Value != -Vector2.one;

				EditorGUI.BeginChangeCheck();
				customAnchorOffset = EditorGUILayout.Toggle( new GUIContent( "Custom Anchor Offset", "Determines if there should be an offset applied to the center of the menu." ), customAnchorOffset );
				if( EditorGUI.EndChangeCheck() )
				{
					if( customAnchorOffset )
						serializedObject.FindProperty( "centerOffset" ).vector2Value = new Vector2( 0.5f, 0.5f );
					else
						serializedObject.FindProperty( "centerOffset" ).vector2Value = -Vector2.one;

					serializedObject.ApplyModifiedProperties();
				}

				if( customAnchorOffset )
				{
					EditorGUI.indentLevel++;
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.Slider( serializedObject.FindProperty( "centerOffset.x" ), 0.0f, 1.0f, new GUIContent( "Horizontal Offset" ) );
					EditorGUILayout.Slider( serializedObject.FindProperty( "centerOffset.y" ), 0.0f, 1.0f, new GUIContent( "Vertical Offset" ) );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();
					EditorGUI.indentLevel--;
				}
			}
			
			if( parentCanvas != null && parentCanvas.renderMode == RenderMode.WorldSpace )
			{
				if( DisplayCollapsibleBoxSection( "Canvas Options", "URM_CanvasOptions" ) )
				{
					EditorGUI.BeginChangeCheck();
					parentCanvasScale = EditorGUILayout.Slider( new GUIContent( "Canvas Scale", "The scale of the canvas rect transform." ), parentCanvasScale, 0.0f, 1.0f );
					if( EditorGUI.EndChangeCheck() )
					{
						Undo.RecordObject( parentCanvas.GetComponent<RectTransform>(), "Change Canvas Scale" );
						parentCanvas.GetComponent<RectTransform>().localScale = Vector3.one * parentCanvasScale;
					}

					EditorGUI.BeginChangeCheck();
					parentCanvasPosition = EditorGUILayout.Vector3Field( new GUIContent( "Canvas Position", "The position of the canvas rect transform." ), parentCanvasPosition );
					if( EditorGUI.EndChangeCheck() )
					{
						Undo.RecordObject( parentCanvas.GetComponent<RectTransform>(), "Change Canvas Position" );
						parentCanvas.GetComponent<RectTransform>().position = parentCanvasPosition;
					}

					EditorGUI.BeginChangeCheck();
					parentCanvasSizeDelta = EditorGUILayout.Vector2Field( new GUIContent( "Canvas Size Delta", "The size delta of the canvas rect transform." ), parentCanvasSizeDelta );
					if( EditorGUI.EndChangeCheck() )
					{
						Undo.RecordObject( parentCanvas.GetComponent<RectTransform>(), "Change Canvas Size Delta" );
						parentCanvas.GetComponent<RectTransform>().sizeDelta = parentCanvasSizeDelta;
					}

					EditorGUI.BeginChangeCheck();
					parentCanvasRotation = EditorGUILayout.Vector3Field( new GUIContent( "Canvas Rotation", "The rotation of the canvas rect transform." ), parentCanvasRotation );
					if( EditorGUI.EndChangeCheck() )
					{
						Undo.RecordObject( parentCanvas.GetComponent<RectTransform>(), "Change Canvas Rotation" );
						parentCanvas.GetComponent<RectTransform>().rotation = Quaternion.Euler( parentCanvasRotation );
					}
				}
				EndCollapsibleSection( "URM_CanvasOptions" );
			}

			if( DisplayCollapsibleBoxSection( "Input Settings", "URM_InputSettings" ) )
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.Slider( minRange, 0.0f, targ.maxRange, new GUIContent( "Minimum Range", "The minimum range that will affect the radial menu." ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
				CheckPropertyHover( DisplayMinRange );
				
				EditorGUI.BeginDisabledGroup( targ.infiniteMaxRange );
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.Slider( maxRange, targ.minRange, 1.5f, new GUIContent( "Maximum Range", "The maximum range that will affect the radial menu." ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
				CheckPropertyHover( DisplayMaxRange );
				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( infiniteMaxRange, new GUIContent( "Infinite Max Range", "Determines whether or not the maximum range should be calculated as infinite." ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.Slider( buttonInputAngle, 0.0f, 1.0f, new GUIContent( "Input Angle", "Determines how much of the angle should be used for input." ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
				CheckPropertyHover( DisplayInputAngle );

				if( GUILayout.Button( "Select Input Manager", EditorStyles.miniButton ) )
				{
#if UNITY_2022_2_OR_NEWER
					UltimateRadialMenuInputManager inputManagerObject = FindAnyObjectByType<UltimateRadialMenuInputManager>();
#else
					UltimateRadialMenuInputManager inputManagerObject = FindObjectOfType<UltimateRadialMenuInputManager>();
#endif
					Selection.activeGameObject = inputManagerObject.gameObject;
					EditorGUIUtility.PingObject( Selection.activeGameObject );
				}
				GUILayout.Space( 1 );
			}
			EndCollapsibleSection( "URM_InputSettings" );

			// CHANGE CHECK FOR APPLYING SETTINGS DURING RUNTIME //
			if( Application.isPlaying )
			{
				if( EditorGUI.EndChangeCheck() )
					targ.UpdatePositioning();
			}
		}
		EndMainSection( "URM_RadialMenuPositioning" );

		if( DisplayHeaderDropdown( "Radial Menu Options", "URM_RadialMenuOptions" ) )
		{
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( radialMenuStyle, new GUIContent( "Radial Button Style", "The style to be used for the radial buttons as the menu changes the count of buttons." ) );
			if( EditorGUI.EndChangeCheck() )
			{
				serializedObject.ApplyModifiedProperties();

				UpdateRadialButtonStyle();
			}

			if( targ.radialMenuStyle != null )
			{
				GUIStyle noteStyle = new GUIStyle( EditorStyles.miniLabel ) { alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true };
				EditorGUILayout.LabelField( "<color=red>*</color> This style determines button sprites.", noteStyle );
			}
			else
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( normalSprite, new GUIContent( "Radial Button Sprite", "The default sprite to apply to the radial button image." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					if( normalSprite.objectReferenceValue == null )
					{
						spriteSwap.boolValue = false;
						colorChange.boolValue = false;
					}
					serializedObject.ApplyModifiedProperties();

					for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
					{
						Undo.RecordObject( targ.UltimateRadialButtonList[ i ].radialImage, "Update Radial Button Sprite" );
						if( targ.normalSprite != null )
						{
							if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.spriteSwap )
								targ.UltimateRadialButtonList[ i ].radialImage.sprite = targ.normalSprite;

							if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.colorChange )
								targ.UltimateRadialButtonList[ i ].radialImage.color = normalColor.colorValue;
						}
						else
						{
							if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.spriteSwap )
								targ.UltimateRadialButtonList[ i ].radialImage.sprite = null;

							if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.colorChange )
								targ.UltimateRadialButtonList[ i ].radialImage.color = Color.clear;
						}
						
						// This is added just in case the user has not broken the prefab, at least we can keep the sprites up to date.
						if( prefabRootError )
							PrefabUtility.RecordPrefabInstancePropertyModifications( targ.UltimateRadialButtonList[ i ].radialImage );
					}
				}
			}

			EditorGUI.BeginDisabledGroup( targ.normalSprite == null );
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( normalColor, new GUIContent( "Radial Button Color", "The default color to apply to the radial button image." ) );
			if( EditorGUI.EndChangeCheck() )
			{
				serializedObject.ApplyModifiedProperties();

				if( targ.normalSprite != null )
				{
					for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
					{
						if( targ.UltimateRadialButtonList[ i ].radialImage.sprite == null )
							continue;

						if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.colorChange )
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].radialImage, "Update Radial Button Color" );
							targ.UltimateRadialButtonList[ i ].radialImage.color = targ.normalColor;
						}
					}
				}
			}
			EditorGUI.EndDisabledGroup();
			
			// MENU TOGGLE SETTINGS //
			if( DisplayCollapsibleBoxSection( "Menu Toggle", "URM_MenuToggleSettings" ) )
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( initialState, new GUIContent( "Initial State", "The initial state of the radial menu, either enabled (visible) or disabled (invisible)." ) );
				EditorGUILayout.PropertyField( serializedObject.FindProperty( "menuToggleOverTime" ), new GUIContent( "Toggle Over Time" ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUI.BeginDisabledGroup( !serializedObject.FindProperty( "menuToggleOverTime" ).boolValue );
				EditorGUI.indentLevel++;
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( serializedObject.FindProperty( "menuToggleAlpha" ), new GUIContent( "Use Alpha" ) );
				EditorGUILayout.PropertyField( serializedObject.FindProperty( "menuToggleScale" ), new GUIContent( "Use Scale" ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( toggleInDuration );
				EditorGUILayout.PropertyField( toggleOutDuration );
				if( EditorGUI.EndChangeCheck() )
				{
					if( toggleInDuration.floatValue < 0 )
						toggleInDuration.floatValue = 0.0f;
					if( toggleOutDuration.floatValue < 0 )
						toggleOutDuration.floatValue = 0.0f;

					serializedObject.ApplyModifiedProperties();
				}
				EditorGUI.indentLevel--;
				EditorGUI.EndDisabledGroup();

				GUILayout.Space( 1 );
			}
			EndCollapsibleSection( "URM_MenuToggleSettings" );
			// END MENU TOGGLE SETTINGS //

			// POINTER //
			if( DisplayCollapsibleBoxSection( "Pointer", "URM_Pointer", serializedObject.FindProperty( "usePointer" ), ref valueChanged, pointerImage == null ) )
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( serializedObject.FindProperty( "pointerImage" ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					pointerImage = ( Image )serializedObject.FindProperty( "pointerImage" ).objectReferenceValue;

					if( pointerImage != null )
					{
						serializedObject.FindProperty( "pointerNormalColor" ).colorValue = pointerImage.color;
						serializedObject.ApplyModifiedProperties();
					}
				}

				if( pointerImage != null )
					pointerSprite = pointerImage.sprite;

				EditorGUI.BeginChangeCheck();
				pointerSprite = ( Sprite )EditorGUILayout.ObjectField( "Pointer Sprite", pointerSprite, typeof( Sprite ), true, GUILayout.Height( EditorGUIUtility.singleLineHeight ) );
				if( EditorGUI.EndChangeCheck() && pointerImage != null )
				{
					pointerImage.enabled = false;
					Undo.RecordObject( pointerImage, "Update Pointer Sprite" );
					pointerImage.sprite = pointerSprite;
					pointerImage.enabled = true;
				}

				if( pointerImage == null )
				{
					EditorGUI.BeginDisabledGroup( pointerSprite == null );
					if( GUILayout.Button( "Generate Pointer Image", EditorStyles.miniButton ) )
					{
						GameObject newPointerImage = new GameObject( "Pointer" );
						RectTransform pointerTrans = newPointerImage.AddComponent<RectTransform>();
						newPointerImage.AddComponent<CanvasRenderer>();
						Image pointerImg = newPointerImage.AddComponent<Image>();

						pointerImg.sprite = pointerSprite;
						pointerImg.color = serializedObject.FindProperty( "pointerNormalColor" ).colorValue;

						newPointerImage.transform.SetParent( targ.transform );

						if( serializedObject.FindProperty( "pointerSiblingIndex" ).enumValueIndex != ( int )UltimateRadialMenu.SetSiblingIndex.Disabled )
						{
							if( serializedObject.FindProperty( "pointerSiblingIndex" ).enumValueIndex == ( int )UltimateRadialMenu.SetSiblingIndex.First )
								newPointerImage.transform.SetAsFirstSibling();
							else
								newPointerImage.transform.SetAsLastSibling();
						}

						pointerTrans.pivot = new Vector2( 0.5f, 0.5f );
						pointerTrans.localScale = Vector3.one;

						serializedObject.FindProperty( "pointerImage" ).objectReferenceValue = pointerImg;
						serializedObject.ApplyModifiedProperties();

						pointerImage = pointerImg;

						Undo.RegisterCreatedObjectUndo( newPointerImage, "Create Pointer Object" );
					}
					EditorGUI.EndDisabledGroup();
				}
				else
				{
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( serializedObject.FindProperty( "pointerNormalColor" ), new GUIContent( "Normal Color" ) );
					if( EditorGUI.EndChangeCheck() )
					{
						serializedObject.ApplyModifiedProperties();

						Undo.RecordObject( pointerImage, "Update Pointer Color" );
						pointerImage.color = serializedObject.FindProperty( "pointerNormalColor" ).colorValue;
					}

					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( serializedObject.FindProperty( "pointerSize" ) );
					EditorGUILayout.PropertyField( serializedObject.FindProperty( "pointerSnapOption" ) );
					if( serializedObject.FindProperty( "pointerSnapOption" ).enumValueIndex != ( int )UltimateRadialMenu.PointerSnapOption.Instant )
					{
						EditorGUI.indentLevel++;
						EditorGUILayout.PropertyField( serializedObject.FindProperty( "pointerTargetTime" ) );
						EditorGUILayout.Space();
						EditorGUI.indentLevel--;
					}
					EditorGUILayout.PropertyField( serializedObject.FindProperty( "pointerRotationOffset" ) );
					EditorGUILayout.PropertyField( serializedObject.FindProperty( "pointerColorChange" ) );
					if( serializedObject.FindProperty( "pointerColorChange" ).boolValue )
					{
						EditorGUI.indentLevel++;
						EditorGUILayout.PropertyField( serializedObject.FindProperty( "pointerActiveColor" ), new GUIContent( "Active Color" ) );
						EditorGUILayout.PropertyField( serializedObject.FindProperty( "changeOverTime" ) );

						if( serializedObject.FindProperty( "changeOverTime" ).boolValue )
						{
							EditorGUILayout.PropertyField( serializedObject.FindProperty( "fadeInDuration" ) );
							EditorGUILayout.PropertyField( serializedObject.FindProperty( "fadeOutDuration" ) );
						}

						EditorGUILayout.Space();
						EditorGUI.indentLevel--;
					}
					EditorGUILayout.PropertyField( serializedObject.FindProperty( "pointerSiblingIndex" ) );
					if( EditorGUI.EndChangeCheck() )
					{
						serializedObject.ApplyModifiedProperties();

						if( serializedObject.FindProperty( "pointerSiblingIndex" ).enumValueIndex != ( int )UltimateRadialMenu.SetSiblingIndex.Disabled )
						{
							Undo.RegisterCompleteObjectUndo( targ.transform, "Set Pointer Sibling Index" );
							if( serializedObject.FindProperty( "pointerSiblingIndex" ).enumValueIndex == ( int )UltimateRadialMenu.SetSiblingIndex.First )
								pointerImage.transform.SetAsFirstSibling();
							else
								pointerImage.transform.SetAsLastSibling();
						}
					}
				}
			}
			EndCollapsibleSection( "URM_Pointer" );
			if( valueChanged )
			{
				if( serializedObject.FindProperty( "usePointer" ).boolValue )
				{
					if( pointerImage != null )
					{
						Undo.RecordObject( pointerImage.gameObject, "Enable Pointer Object" );
						pointerImage.gameObject.SetActive( true );
					}
				}
				else
				{
					if( pointerImage != null )
					{
						Undo.RecordObject( pointerImage.gameObject, "Disable Pointer Object" );
						pointerImage.gameObject.SetActive( false );
					}

					serializedObject.FindProperty( "usePointerStyle" ).boolValue = false;
					serializedObject.ApplyModifiedProperties();
				}
			}
			// END POINTER //

			// POINTER STYLE //
			EditorGUI.BeginDisabledGroup( !serializedObject.FindProperty( "usePointer" ).boolValue );
			if( DisplayCollapsibleBoxSection( "Pointer Styles", "URM_PointerStyles", serializedObject.FindProperty( "usePointerStyle" ), ref valueChanged ) )
			{
				EditorGUILayout.BeginHorizontal();

				EditorGUI.BeginChangeCheck();
				newStyleButtonCount = EditorGUILayout.IntField( newStyleButtonCount, GUILayout.Width( 50 ) );
				if( EditorGUI.EndChangeCheck() )
				{
					if( newStyleButtonCount < 2 )
						newStyleButtonCount = 2;

					CheckForDuplicateButtonCount();
				}

				EditorGUI.BeginDisabledGroup( duplicateButtonCount );
				if( GUILayout.Button( "Create New Style", EditorStyles.miniButton ) )
				{
					GUI.FocusControl( "" );

					serializedObject.FindProperty( "PointerStyles" ).arraySize++;
					serializedObject.ApplyModifiedProperties();

					serializedObject.FindProperty( string.Format( "PointerStyles.Array.data[{0}].buttonCount", targ.PointerStyles.Count - 1 ) ).intValue = newStyleButtonCount;
					serializedObject.FindProperty( string.Format( "PointerStyles.Array.data[{0}].pointerSprite", targ.PointerStyles.Count - 1 ) ).objectReferenceValue = null;
					serializedObject.ApplyModifiedProperties();

					targ.PointerStyles = targ.PointerStyles.OrderBy( w => w.buttonCount ).ToList();
					newStyleButtonCount++;
					CheckForDuplicateButtonCount();
				}
				EditorGUI.EndDisabledGroup();

				EditorGUILayout.EndHorizontal();

				for( int i = 0; i < targ.PointerStyles.Count; i++ )
				{
					EditorGUILayout.Space();

					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.LabelField( $"{targ.PointerStyles[ i ].buttonCount.ToString( "00" )} Button Style _____________________________________________________________________", new GUIStyle( GUI.skin.label ) { richText = true } );
					if( GUILayout.Button( "×", EditorStyles.miniButton ) )
					{
						if( EditorUtility.DisplayDialog( "Ultimate Radial Menu Pointer - Warning", "You are about to delete the style for this button count. Are you sure you want to do this?", "Continue", "Cancel" ) )
						{
							serializedObject.FindProperty( "PointerStyles" ).DeleteArrayElementAtIndex( i );
							serializedObject.ApplyModifiedProperties();
							break;
						}
					}
					EditorGUILayout.EndHorizontal();

					EditorGUI.indentLevel++;
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( serializedObject.FindProperty( string.Format( "PointerStyles.Array.data[{0}].pointerSprite", i ) ) );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();
					EditorGUI.indentLevel--;
				}
			}
			EndCollapsibleSection( "URM_PointerStyles" );
			EditorGUI.EndDisabledGroup();
			if( valueChanged ){}
			// END POINTER STYLE //

			// MENU TEXT //
			if( DisplayCollapsibleBoxSection( "Menu Text", "URM_MenuText" ) )
			{
				EditorGUILayout.BeginHorizontal();

				EditorGUI.BeginDisabledGroup( isInProjectWindow );
				EditorGUI.BeginChangeCheck();
				displayButtonName.boolValue = EditorGUILayout.ToggleLeft( new GUIContent( "Display Name", "Determines if the radial menu should have a text component that will display the name of the currently selected button." ), displayButtonName.boolValue );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					if( targ.displayButtonName )
						EditorPrefs.SetBool( "URM_DisplayName", true );

					if( targ.nameText != null )
						targ.nameText.gameObject.SetActive( displayButtonName.boolValue );
				}
				EditorGUI.EndDisabledGroup();

				if( targ.displayButtonName )
				{
					if( GUILayout.Button( EditorPrefs.GetBool( "URM_DisplayName" ) ? "-" : "+", EditorStyles.miniButton, GUILayout.Width( EditorGUIUtility.singleLineHeight ) ) )
						EditorPrefs.SetBool( "URM_DisplayName", !EditorPrefs.GetBool( "URM_DisplayName" ) );
				}
				EditorGUILayout.EndHorizontal();
				
				if( targ.displayButtonName && EditorPrefs.GetBool( "URM_DisplayName" ) )
				{
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( nameText, new GUIContent( "Name Text", "The text component to be used for the name." ) );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();

					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( nameFont, new GUIContent( "Font", "The font to use on the name text." ) );
					if( EditorGUI.EndChangeCheck() )
					{
						serializedObject.ApplyModifiedProperties();

						if( targ.nameText != null )
						{
							Undo.RecordObject( targ.nameText, "Update Name Font" );
							targ.nameText.font = targ.nameFont;
						}
					}

					if( targ.displayButtonName && targ.nameText == null )
					{
						EditorGUILayout.HelpBox( "There is no text component assigned.", MessageType.Warning );
						if( GUILayout.Button( "Generate Name Text", EditorStyles.miniButton ) )
						{
							nameOutline.boolValue = false;

							GameObject newText = new GameObject();
							RectTransform textTransform = newText.AddComponent<RectTransform>();
							newText.AddComponent<CanvasRenderer>();
							Text textComponent = newText.AddComponent<Text>();
							newText.name = "Name Text";

							newText.transform.SetParent( targ.transform );
							textTransform.position = targ.GetComponent<RectTransform>().position;
							textTransform.pivot = new Vector2( 0.5f, 0.5f );
							textTransform.localScale = Vector3.one;
							textComponent.text = "Name Text";
							textComponent.resizeTextForBestFit = true;
							textComponent.resizeTextMinSize = 0;
							textComponent.resizeTextMaxSize = 300;
							textComponent.alignment = TextAnchor.MiddleCenter;
							textComponent.color = nameTextColor;
							if( targ.nameFont != null )
								textComponent.font = targ.nameFont;

							nameText.objectReferenceValue = newText;
							serializedObject.ApplyModifiedProperties();

							Undo.RegisterCreatedObjectUndo( newText, "Create Text Object" );
						}
					}
					else if( targ.nameText != null )
					{
						EditorGUI.BeginChangeCheck();
						nameTextColor = EditorGUILayout.ColorField( "Text Color", nameTextColor );
						if( EditorGUI.EndChangeCheck() )
						{
							if( targ.nameText != null )
							{
								Undo.RecordObject( targ.nameText, "Update Name Text Color" );
								targ.nameText.enabled = false;
								targ.nameText.color = nameTextColor;
								targ.nameText.enabled = true;
							}
						}

						EditorGUI.BeginChangeCheck();
						EditorGUILayout.PropertyField( nameOutline, new GUIContent( "Text Outline", "Determines if the text should have an outline or not." ) );
						if( EditorGUI.EndChangeCheck() )
						{
							serializedObject.ApplyModifiedProperties();

							if( targ.nameText != null )
							{
								if( targ.nameOutline && !targ.nameText.gameObject.GetComponent<UnityEngine.UI.Outline>() )
								{
									Undo.RecordObject( targ.nameText.gameObject, "Update Text Outline" );
									targ.nameText.gameObject.AddComponent<UnityEngine.UI.Outline>();

									nameTextOutlineColor = targ.nameText.gameObject.GetComponent<UnityEngine.UI.Outline>().effectColor;
								}

								if( targ.nameText.gameObject.GetComponent<UnityEngine.UI.Outline>() )
								{
									Undo.RecordObject( targ.nameText.gameObject.GetComponent<UnityEngine.UI.Outline>(), "Update Text Outline" );
									targ.nameText.gameObject.GetComponent<UnityEngine.UI.Outline>().enabled = targ.nameOutline;
								}
							}
						}

						if( targ.nameOutline )
						{
							EditorGUI.indentLevel++;
							EditorGUI.BeginChangeCheck();
							nameTextOutlineColor = EditorGUILayout.ColorField( "Outline Color", nameTextOutlineColor );
							if( EditorGUI.EndChangeCheck() )
							{
								if( targ.nameText != null && targ.nameText.GetComponent<UnityEngine.UI.Outline>() )
								{
									Undo.RecordObject( targ.nameText.GetComponent<UnityEngine.UI.Outline>(), "Update Text Outline" );
									targ.nameText.GetComponent<UnityEngine.UI.Outline>().enabled = false;
									targ.nameText.GetComponent<UnityEngine.UI.Outline>().effectColor = nameTextOutlineColor;
									targ.nameText.GetComponent<UnityEngine.UI.Outline>().enabled = true;
								}
							}
							GUILayoutAfterIndentSpace();
							EditorGUI.indentLevel--;
						}

						EditorGUI.BeginChangeCheck();
						EditorGUILayout.Slider( nameTextRatioX, 0.0f, 1.0f, new GUIContent( "X Ratio", "The horizontal ratio of the text transform." ) );
						EditorGUILayout.Slider( nameTextRatioY, 0.0f, 1.0f, new GUIContent( "Y Ratio", "The vertical ratio of the text transform." ) );
						EditorGUILayout.Slider( nameTextSize, 0.0f, 1.0f, new GUIContent( "Overall Size", "The overall size of the text transform." ) );
						EditorGUILayout.Slider( nameTextHorizontalPosition, 0.0f, 100.0f, new GUIContent( "Horizontal Position", "The horizontal position of the text transform." ) );
						EditorGUILayout.Slider( nameTextVerticalPosition, 0.0f, 100.0f, new GUIContent( "Vertical Position", "The vertical position of the text transform." ) );
						if( EditorGUI.EndChangeCheck() )
							serializedObject.ApplyModifiedProperties();
					}

					EditorGUILayout.Space();
				}
				// --------- END NAME TEXT --------- //

				// --------- DESCRIPTION TEXT --------- //
				EditorGUILayout.BeginHorizontal();

				EditorGUI.BeginDisabledGroup( isInProjectWindow );
				EditorGUI.BeginChangeCheck();
				displayButtonDescription.boolValue = EditorGUILayout.ToggleLeft( new GUIContent( "Display Description", "Determines if the radial menu should have a text component that will display the description of the currently selected button." ), displayButtonDescription.boolValue );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					if( targ.displayButtonDescription )
						EditorPrefs.SetBool( "URM_DisplayDescription", true );

					if( targ.descriptionText != null )
						targ.descriptionText.gameObject.SetActive( displayButtonDescription.boolValue );
				}
				EditorGUI.EndDisabledGroup();

				if( targ.displayButtonDescription )
				{
					if( GUILayout.Button( EditorPrefs.GetBool( "URM_DisplayDescription" ) ? "-" : "+", EditorStyles.miniButton, GUILayout.Width( EditorGUIUtility.singleLineHeight ) ) )
						EditorPrefs.SetBool( "URM_DisplayDescription", !EditorPrefs.GetBool( "URM_DisplayDescription" ) );
				}
				EditorGUILayout.EndHorizontal();

				if( targ.displayButtonDescription && EditorPrefs.GetBool( "URM_DisplayDescription" ) )
				{
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( descriptionText, new GUIContent( "Description Text", "The text component to be used for the button description." ) );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();

					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( descriptionFont, new GUIContent( "Font", "The font to use on the description text." ) );
					if( EditorGUI.EndChangeCheck() )
					{
						serializedObject.ApplyModifiedProperties();

						if( targ.descriptionText != null )
						{
							Undo.RecordObject( targ.descriptionText, "Update Description Font" );
							targ.descriptionText.font = targ.descriptionFont;
						}
					}

					if( targ.displayButtonDescription && targ.descriptionText == null )
					{
						EditorGUILayout.HelpBox( "There is no text component assigned.", MessageType.Warning );
						if( GUILayout.Button( "Generate Description Text", EditorStyles.miniButton ) )
						{
							descriptionOutline.boolValue = false;

							GameObject newText = new GameObject();
							RectTransform textTransform = newText.AddComponent<RectTransform>();
							newText.AddComponent<CanvasRenderer>();
							Text textComponent = newText.AddComponent<Text>();
							newText.name = "Description Text";

							newText.transform.SetParent( targ.transform );
							textTransform.position = targ.GetComponent<RectTransform>().position;
							textTransform.pivot = new Vector2( 0.5f, 0.5f );
							textTransform.localScale = Vector3.one;
							textComponent.text = "Description Text";
							textComponent.resizeTextForBestFit = true;
							textComponent.resizeTextMinSize = 0;
							textComponent.resizeTextMaxSize = 300;
							textComponent.alignment = TextAnchor.UpperCenter;
							textComponent.color = descriptionTextColor;
							if( targ.descriptionFont != null )
								textComponent.font = targ.descriptionFont;

							descriptionText.objectReferenceValue = newText;
							serializedObject.ApplyModifiedProperties();

							Undo.RegisterCreatedObjectUndo( newText, "Create Description Text Object" );
						}
					}
					else if( targ.descriptionText != null )
					{
						EditorGUI.BeginChangeCheck();
						descriptionTextColor = EditorGUILayout.ColorField( "Text Color", descriptionTextColor );
						if( EditorGUI.EndChangeCheck() )
						{
							if( targ.descriptionText != null )
							{
								Undo.RecordObject( targ.descriptionText, "Update Description Text Color" );
								targ.descriptionText.enabled = false;
								targ.descriptionText.color = descriptionTextColor;
								targ.descriptionText.enabled = true;
							}
						}

						EditorGUI.BeginChangeCheck();
						EditorGUILayout.PropertyField( descriptionOutline, new GUIContent( "Text Outline", "Determines if the text should have an outline or not." ) );
						if( EditorGUI.EndChangeCheck() )
						{
							serializedObject.ApplyModifiedProperties();

							if( targ.descriptionText != null )
							{
								if( targ.descriptionOutline && !targ.descriptionText.gameObject.GetComponent<UnityEngine.UI.Outline>() )
								{
									Undo.RecordObject( targ.descriptionText.gameObject, "Update Text Outline" );
									targ.descriptionText.gameObject.AddComponent<UnityEngine.UI.Outline>();

									descriptionTextOutlineColor = targ.descriptionText.gameObject.GetComponent<UnityEngine.UI.Outline>().effectColor;
								}

								if( targ.descriptionText.gameObject.GetComponent<UnityEngine.UI.Outline>() )
								{
									Undo.RecordObject( targ.descriptionText.gameObject.GetComponent<UnityEngine.UI.Outline>(), "Update Text Outline" );
									targ.descriptionText.gameObject.GetComponent<UnityEngine.UI.Outline>().enabled = targ.descriptionOutline;
								}
							}
						}

						if( targ.descriptionOutline )
						{
							EditorGUI.indentLevel++;
							EditorGUI.BeginChangeCheck();
							descriptionTextOutlineColor = EditorGUILayout.ColorField( "Outline Color", descriptionTextOutlineColor );
							if( EditorGUI.EndChangeCheck() )
							{
								if( targ.descriptionText != null && targ.descriptionText.GetComponent<UnityEngine.UI.Outline>() )
								{
									Undo.RecordObject( targ.descriptionText.GetComponent<UnityEngine.UI.Outline>(), "Update Text Outline" );
									targ.descriptionText.GetComponent<UnityEngine.UI.Outline>().enabled = false;
									targ.descriptionText.GetComponent<UnityEngine.UI.Outline>().effectColor = descriptionTextOutlineColor;
									targ.descriptionText.GetComponent<UnityEngine.UI.Outline>().enabled = true;
								}
							}
							GUILayoutAfterIndentSpace();
							EditorGUI.indentLevel--;
						}

						EditorGUI.BeginChangeCheck();
						EditorGUILayout.Slider( descriptionTextRatioX, 0.0f, 1.0f, new GUIContent( "X Ratio", "The horizontal ratio of the text transform." ) );
						EditorGUILayout.Slider( descriptionTextRatioY, 0.0f, 1.0f, new GUIContent( "Y Ratio", "The vertical ratio of the text transform." ) );
						EditorGUILayout.Slider( descriptionTextSize, 0.0f, 1.0f, new GUIContent( "Overall Size", "The overall size of the text transform." ) );
						EditorGUILayout.Slider( descriptionTextHorizontalPosition, 0.0f, 100.0f, new GUIContent( "Horizontal Position", "The horizontal position of the text transform." ) );
						EditorGUILayout.Slider( descriptionTextVerticalPosition, 0.0f, 100.0f, new GUIContent( "Vertical Position", "The vertical position of the text transform." ) );
						if( EditorGUI.EndChangeCheck() )
							serializedObject.ApplyModifiedProperties();
					}
				}
				// --------- END DESCRIPTION TEXT --------- //
				GUILayout.Space( 1 );
			}
			EndCollapsibleSection( "URM_MenuText" );
			// END MENU TEXT //

			// BUTTON ICON //
			if( DisplayCollapsibleBoxSection( "Button Icon", "URM_ButtonIcon", useButtonIcon, ref valueChanged ) )
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( iconNormalColor, new GUIContent( "Icon Color", "The color of the icon image." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
					{
						if( targ.UltimateRadialButtonList[ i ].buttonDisabled || targ.UltimateRadialButtonList[ i ].icon == null || targ.UltimateRadialButtonList[ i ].icon.sprite == null )
							continue;

						Undo.RecordObject( targ.UltimateRadialButtonList[ i ].icon, "Update Radial Button Icon Color" );
						targ.UltimateRadialButtonList[ i ].icon.color = targ.iconNormalColor;
					}
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.Slider( iconSize, 0.0f, 1.0f, new GUIContent( "Icon Size", "The size of the icon image transform." ) );
				EditorGUILayout.Slider( iconHorizontalPosition, 0.0f, 100.0f, new GUIContent( "Horizontal Position", "The horizontal position in relation to the radial button transform." ) );
				EditorGUILayout.Slider( iconVerticalPosition, 0.0f, 100.0f, new GUIContent( "Vertical Position", "The vertical position in relation to the radial button transform." ) );
				EditorGUILayout.PropertyField( iconRotation, new GUIContent( "Rotation Offset", "The rotation offset to apply to the icon transform." ) );
				if( targ.followOrbitalRotation )
				{
					EditorGUILayout.PropertyField( iconLocalRotation, new GUIContent( "Local Rotation", "Determines if the icon transform will use local or global rotation." ) );

					if( targ.iconLocalRotation )
					{
						EditorGUI.indentLevel++;
						EditorGUILayout.PropertyField( serializedObject.FindProperty( "iconSmartRotation" ), new GUIContent( "Smart Rotation", "Determines if the radial menu should try and adjust the rotation so that the icons are displayed more properly." ) );
						EditorGUI.indentLevel--;
					}
				}
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUI.BeginChangeCheck();
				serializedObject.FindProperty( "IconPlaceholderSprite" ).objectReferenceValue = EditorGUILayout.ObjectField( new GUIContent( "Icon Placeholder" ), serializedObject.FindProperty( "IconPlaceholderSprite" ).objectReferenceValue, typeof( Sprite ), false );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();
					for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
					{
						if( targ.UltimateRadialButtonList[ i ].icon == null )
							continue;

						targ.UltimateRadialButtonList[ i ].icon.enabled = false;

						Undo.RecordObject( targ.UltimateRadialButtonList[ i ].icon, "Update Icon Sprite" );
						targ.UltimateRadialButtonList[ i ].icon.sprite = ( Sprite )serializedObject.FindProperty( "IconPlaceholderSprite" ).objectReferenceValue;

						if( serializedObject.FindProperty( "IconPlaceholderSprite" ).objectReferenceValue != null )
							targ.UltimateRadialButtonList[ i ].icon.color = serializedObject.FindProperty( "iconNormalColor" ).colorValue;
						else
							targ.UltimateRadialButtonList[ i ].icon.color = Color.clear;

						targ.UltimateRadialButtonList[ i ].icon.enabled = true;
					}
				}
			}
			EndCollapsibleSection( "URM_ButtonIcon" );
			if( valueChanged )
			{
				for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
				{
					if( targ.useButtonIcon )
					{
						if( targ.UltimateRadialButtonList[ i ].icon == null )
						{
							GameObject newIcon = new GameObject();
							newIcon.AddComponent<CanvasRenderer>();
							RectTransform iconTransform = newIcon.AddComponent<RectTransform>();
							Image iconImage = newIcon.AddComponent<Image>();

							newIcon.transform.SetParent( targ.UltimateRadialButtonList[ i ].buttonTransform );
							newIcon.name = "Icon";

							iconTransform.pivot = new Vector2( 0.5f, 0.5f );
							iconTransform.localScale = Vector3.one;

							icon[ i ].objectReferenceValue = iconImage;
							serializedObject.ApplyModifiedProperties();

							Undo.RegisterCreatedObjectUndo( newIcon, "Create Icon Objects" );

							if( iconPlaceholderSprite != null )
							{
								iconImage.sprite = iconPlaceholderSprite;
								iconImage.color = targ.iconNormalColor;
							}
							else
								iconImage.color = Color.clear;
						}
						else
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].icon.gameObject, "Enable Button Icon" );
							targ.UltimateRadialButtonList[ i ].icon.gameObject.SetActive( true );
						}
					}
					else
					{
						if( targ.UltimateRadialButtonList[ i ].icon != null )
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].icon.gameObject, "Disable Button Icon" );
							targ.UltimateRadialButtonList[ i ].icon.gameObject.SetActive( false );
						}
					}
				}
			}
			// END BUTTON ICON //

			// BUTTON TEXT //
			if( DisplayCollapsibleBoxSection( "Button Text", "URM_ButtonText", useButtonText, ref valueChanged ) )
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( textNormalColor, new GUIContent( "Text Color", "The color to apply to the text component." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int n = 0; n < targ.UltimateRadialButtonList.Count; n++ )
					{
						if( targ.UltimateRadialButtonList[ n ].buttonDisabled || targ.UltimateRadialButtonList[ n ].text == null )
							continue;

						Undo.RecordObject( targ.UltimateRadialButtonList[ n ].text, "Update Button Text Color" );
						targ.UltimateRadialButtonList[ n ].text.color = textNormalColor.colorValue;
					}
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( buttonTextFont, new GUIContent( "Text Font", "The font to apply to the button text." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int n = 0; n < targ.UltimateRadialButtonList.Count; n++ )
					{
						Undo.RecordObject( targ.UltimateRadialButtonList[ n ].text, "Update Button Text Font" );
						targ.UltimateRadialButtonList[ n ].text.font = targ.buttonTextFont;
					}
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( buttonTextOutline, new GUIContent( "Text Outline", "Determines if the text should have an outline or not." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
					{
						if( targ.UltimateRadialButtonList[ i ].text != null )
						{
							if( targ.buttonTextOutline && !targ.UltimateRadialButtonList[ i ].text.gameObject.GetComponent<UnityEngine.UI.Outline>() )
							{
								Undo.RecordObject( targ.UltimateRadialButtonList[ i ].text.gameObject, "Update Text Outline" );
								targ.UltimateRadialButtonList[ i ].text.gameObject.AddComponent<UnityEngine.UI.Outline>();

								buttonTextOutlineColor.colorValue = targ.UltimateRadialButtonList[ i ].text.gameObject.GetComponent<UnityEngine.UI.Outline>().effectColor;
								serializedObject.ApplyModifiedProperties();
							}

							if( targ.UltimateRadialButtonList[ i ].text.gameObject.GetComponent<UnityEngine.UI.Outline>() )
							{
								Undo.RecordObject( targ.UltimateRadialButtonList[ i ].text.gameObject.GetComponent<UnityEngine.UI.Outline>(), "Update Text Outline" );
								targ.UltimateRadialButtonList[ i ].text.gameObject.GetComponent<UnityEngine.UI.Outline>().enabled = targ.buttonTextOutline;
							}
						}
					}
				}

				if( targ.buttonTextOutline )
				{
					EditorGUI.indentLevel++;
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( buttonTextOutlineColor, new GUIContent( "Outline Color" ) );
					if( EditorGUI.EndChangeCheck() )
					{
						serializedObject.ApplyModifiedProperties();

						for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
						{
							if( targ.UltimateRadialButtonList[ i ].text != null && targ.UltimateRadialButtonList[ i ].text.GetComponent<UnityEngine.UI.Outline>() )
							{
								Undo.RecordObject( targ.UltimateRadialButtonList[ i ].text.GetComponent<UnityEngine.UI.Outline>(), "Update Text Outline" );
								targ.UltimateRadialButtonList[ i ].text.GetComponent<UnityEngine.UI.Outline>().enabled = false;
								targ.UltimateRadialButtonList[ i ].text.GetComponent<UnityEngine.UI.Outline>().effectColor = targ.buttonTextOutlineColor;
								targ.UltimateRadialButtonList[ i ].text.GetComponent<UnityEngine.UI.Outline>().enabled = true;
							}
						}
					}
					GUILayoutAfterIndentSpace();
					EditorGUI.indentLevel--;
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( displayNameOnButton, new GUIContent( "Display Name", "Determines if the name of the button should be applied to the text or not." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
					{
						if( targ.UltimateRadialButtonList[ i ].text == null )
							continue;

						if( displayNameOnButton.boolValue && buttonName[ i ].stringValue != string.Empty )
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].text, "Update Button Text Value" );
							targ.UltimateRadialButtonList[ i ].text.text = buttonName[ i ].stringValue;
						}
						else
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].text, "Update Button Text Value" );
							targ.UltimateRadialButtonList[ i ].text.text = "Text";
						}
					}
				}

				EditorGUILayout.Space();

				EditorGUILayout.LabelField( "Positioning", EditorStyles.boldLabel );

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.Slider( textAreaRatioX, 0.0f, 1.0f, new GUIContent( "Ratio X", "The horizontal ratio of the text transform." ) );
				EditorGUILayout.Slider( textAreaRatioY, 0.0f, 1.0f, new GUIContent( "Ratio Y", "The vertical ratio of the text transform." ) );
				EditorGUILayout.Slider( textSize, 0.0f, 0.5f, new GUIContent( "Text Size", "The overall size of the text transform." ) );
				EditorGUILayout.PropertyField( textLocalPosition, new GUIContent( "Local Position", "Determines if the text will position itself according to the local position and rotation of the button." ) );
				if( targ.textLocalPosition )
				{
					EditorGUI.indentLevel++;
					EditorGUILayout.PropertyField( textLocalRotation, new GUIContent( "Local Rotation", "Determines if the text should follow the local rotation of the button or not." ) );
					GUILayoutAfterIndentSpace();
					EditorGUI.indentLevel--;
				}
				EditorGUILayout.Slider( textHorizontalPosition, 0.0f, 100.0f, new GUIContent( "Horizontal Position", "The horizontal position of the text transform." ) );
				EditorGUILayout.Slider( textVerticalPosition, 0.0f, 100.0f, new GUIContent( "Vertical Position", "The vertical position of the text transform." ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( serializedObject.FindProperty( "relativeToIcon" ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
			}
			EndCollapsibleSection( "URM_ButtonText" );
			if( valueChanged )
			{
				for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
				{
					if( targ.useButtonText )
					{
						if( targ.UltimateRadialButtonList[ i ].text == null )
						{
							GameObject newText = new GameObject();
							RectTransform textTransform = newText.AddComponent<RectTransform>();
							newText.AddComponent<CanvasRenderer>();
							Text textComponent = newText.AddComponent<Text>();
							newText.name = "Text";

							newText.transform.SetParent( targ.UltimateRadialButtonList[ i ].buttonTransform );
							newText.transform.SetAsLastSibling();

							textTransform.position = targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].buttonTransform.position;
							textTransform.localScale = Vector3.one;
							textTransform.pivot = new Vector2( 0.5f, 0.5f );

							textComponent.text = "Text";
							textComponent.resizeTextForBestFit = true;
							textComponent.resizeTextMinSize = 0;
							textComponent.resizeTextMaxSize = 300;
							textComponent.alignment = TextAnchor.MiddleCenter;
							textComponent.color = targ.textNormalColor;

							if( targ.buttonTextFont != null )
								textComponent.font = targ.buttonTextFont;

							if( targ.buttonTextOutline && !newText.gameObject.GetComponent<UnityEngine.UI.Outline>() )
								newText.gameObject.AddComponent<UnityEngine.UI.Outline>();

							text[ i ].objectReferenceValue = newText;
							serializedObject.ApplyModifiedProperties();
							
							Undo.RegisterCreatedObjectUndo( newText, "Create Text Objects" );
						}
						else
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].text.gameObject, "Enable Button Text" );
							targ.UltimateRadialButtonList[ i ].text.gameObject.SetActive( true );
						}
					}
					else
					{
						if( targ.UltimateRadialButtonList[ i ].text != null )
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].text.gameObject, "Disable Button Text" );
							targ.UltimateRadialButtonList[ i ].text.gameObject.SetActive( false );
						}
					}
				}
			}
			// END BUTTON TEXT //

			if( isInProjectWindow )
			{
				GUIStyle noteStyle = new GUIStyle( EditorStyles.miniLabel ) { alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true };
				EditorGUILayout.LabelField( "<color=red>*</color> Cannot create/destroy objects from the Prefab.", noteStyle );
			}
		}
		EndMainSection( "URM_RadialMenuOptions" );

		if( DisplayHeaderDropdown( "Button Interaction", "URM_ButtonInteraction" ) )
		{
			EditorGUI.BeginDisabledGroup( targ.normalSprite == null );
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( spriteSwap, new GUIContent( "Sprite Swap", "Determines whether or not the radial buttons will swap sprites when being interacted with." ) );
			if( EditorGUI.EndChangeCheck() )
			{
				serializedObject.ApplyModifiedProperties();

				for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
				{
					Undo.RecordObject( targ.UltimateRadialButtonList[ i ].radialImage, "Update Button Disabled Sprite" );
					if( targ.spriteSwap && targ.UltimateRadialButtonList[ i ].buttonDisabled && targ.disabledSprite != null )
						targ.UltimateRadialButtonList[ i ].radialImage.sprite = targ.disabledSprite;
					else
						targ.UltimateRadialButtonList[ i ].radialImage.sprite = targ.normalSprite;
				}

				UpdateRadialButtonStyle();
			}

			if( targ.spriteSwap && targ.radialMenuStyle != null )
			{
				GUIStyle noteStyle = new GUIStyle( EditorStyles.miniLabel ) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
				EditorGUILayout.LabelField( "<color=red>*</color> The button interaction sprites are determined by the assigned style. To modify the different sprites please edit the Radial Button Style object.", noteStyle );
			}
			
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( colorChange, new GUIContent( "Color Change", "Determines whether or not the radial buttons will change color when being interacted with." ) );
			if( EditorGUI.EndChangeCheck() )
			{
				serializedObject.ApplyModifiedProperties();

				for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
				{
					Undo.RecordObject( targ.UltimateRadialButtonList[ i ].radialImage, "Update Button Color" );
					if( targ.colorChange && targ.UltimateRadialButtonList[ i ].buttonDisabled )
						targ.UltimateRadialButtonList[ i ].radialImage.color = targ.disabledColor;
					else
						targ.UltimateRadialButtonList[ i ].radialImage.color = targ.normalColor;
				}
			}
			EditorGUI.EndDisabledGroup();

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( scaleTransform, new GUIContent( "Scale Transform", "Determines whether or not the radial buttons will scale when being interacted with." ) );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			if( targ.useButtonIcon )
			{
				EditorGUILayout.Space();

				EditorGUILayout.LabelField( "Button Icon", EditorStyles.boldLabel );

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( iconColorChange, new GUIContent( "Color Change", "Determines whether or not the icon will change color when being interacted with." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
					{
						if( targ.UltimateRadialButtonList[ i ].icon == null || targ.UltimateRadialButtonList[ i ].icon.sprite == null )
							continue;

						Undo.RecordObject( targ.UltimateRadialButtonList[ i ].icon, "Update Radial Button Icon Color" );
						
						if( targ.iconColorChange && targ.UltimateRadialButtonList[ i ].buttonDisabled )
							targ.UltimateRadialButtonList[ i ].icon.color = targ.iconDisabledColor;
						else
							targ.UltimateRadialButtonList[ i ].icon.color = targ.iconNormalColor;
					}
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( iconScaleTransform, new GUIContent( "Scale Transform", "Determines whether or not the icon will scale when being interacted with." ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
			}

			if( targ.useButtonText )
			{
				EditorGUILayout.Space();

				EditorGUILayout.LabelField( "Button Text", EditorStyles.boldLabel );

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( textColorChange, new GUIContent( "Color Change", "Determines whether or not the text will change color when being interacted with." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
					{
						if( targ.UltimateRadialButtonList[ i ].text == null )
							continue;

						Undo.RecordObject( targ.UltimateRadialButtonList[ i ].text, "Update Radial Button Text Color" );
						if( targ.UltimateRadialButtonList[ i ].buttonDisabled )
							targ.UltimateRadialButtonList[ i ].text.color = targ.textDisabledColor;
						else
							targ.UltimateRadialButtonList[ i ].text.color = targ.textNormalColor;
					}
				}
			}

			// BUTTON INTERACTION SETTINGS //
			if( DisplayCollapsibleBoxSection( "Normal", "URM_ButtonNormal" ) )
			{
				if( targ.radialMenuStyle == null )
				{
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( normalSprite, new GUIContent( "Button Sprite", "The default sprite to apply to the radial button image." ) );
					if( EditorGUI.EndChangeCheck() )
					{
						if( normalSprite.objectReferenceValue == null )
						{
							spriteSwap.boolValue = false;
							colorChange.boolValue = false;
						}
						serializedObject.ApplyModifiedProperties();
						

						for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].radialImage, "Update Radial Button Sprite" );
							
							if( targ.normalSprite != null )
							{
								if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.spriteSwap )
									targ.UltimateRadialButtonList[ i ].radialImage.sprite = targ.normalSprite;

								if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.colorChange )
									targ.UltimateRadialButtonList[ i ].radialImage.color = normalColor.colorValue;
							}
							else
							{
								if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.spriteSwap )
									targ.UltimateRadialButtonList[ i ].radialImage.sprite = null;

								if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.colorChange )
									targ.UltimateRadialButtonList[ i ].radialImage.color = Color.clear;
							}
						}
					}
				}

				EditorGUI.BeginDisabledGroup( targ.normalSprite == null );
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( normalColor, new GUIContent( "Button Color", "The default color to apply to the radial button image." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();
					for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
					{
						if( targ.UltimateRadialButtonList[ i ].radialImage.sprite == null )
							continue;

						if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.colorChange )
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].radialImage, "Update Radial Button Color" );
							targ.UltimateRadialButtonList[ i ].radialImage.color = targ.normalColor;
						}
					}
				}
				EditorGUI.EndDisabledGroup();

				if( targ.useButtonIcon && targ.iconColorChange )
				{
					EditorGUILayout.Space();
					EditorGUILayout.LabelField( "Button Icon", EditorStyles.boldLabel );
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( iconNormalColor, new GUIContent( "Normal Color", "The color of the icon image." ) );
					if( EditorGUI.EndChangeCheck() )
					{
						serializedObject.ApplyModifiedProperties();

						for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
						{
							if( targ.UltimateRadialButtonList[ i ].buttonDisabled || targ.UltimateRadialButtonList[ i ].icon == null || targ.UltimateRadialButtonList[ i ].icon.sprite == null )
								continue;

							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].icon, "Update Radial Button Icon Color" );
							targ.UltimateRadialButtonList[ i ].icon.color = targ.iconNormalColor;
						}
					}
				}

				if( targ.useButtonText && targ.textColorChange )
				{
					EditorGUILayout.Space();
					EditorGUILayout.LabelField( "Button Text", EditorStyles.boldLabel );
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( textNormalColor, new GUIContent( "Text Color", "The default color to be applied to the text." ) );
					if( EditorGUI.EndChangeCheck() )
					{
						serializedObject.ApplyModifiedProperties();

						for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
						{
							if( targ.UltimateRadialButtonList[ i ].buttonDisabled || targ.UltimateRadialButtonList[ i ].text == null )
								continue;

							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].text, "Update Button Text Color" );
							targ.UltimateRadialButtonList[ i ].text.color = textNormalColor.colorValue;
						}
					}
				}

				GUILayout.Space( 1 );
			}
			EndCollapsibleSection( "URM_ButtonNormal" );

			if( DisplayCollapsibleBoxSection( "Highlighted", "URM_ButtonHighlighted" ) )
			{
				EditorGUI.BeginChangeCheck();

				if( targ.radialMenuStyle == null )
				{
					EditorGUI.BeginDisabledGroup( !targ.spriteSwap );
					EditorGUILayout.PropertyField( highlightedSprite, new GUIContent( "Button Sprite", "The sprite to be applied to the radial button when highlighted." ) );
					EditorGUI.EndDisabledGroup();
				}

				EditorGUI.BeginDisabledGroup( !targ.colorChange );
				EditorGUILayout.PropertyField( highlightedColor, new GUIContent( "Button Color", "The color to be applied to the radial button when highlighted." ) );
				EditorGUI.EndDisabledGroup();
				
				EditorGUI.BeginDisabledGroup( !targ.scaleTransform );
				EditorGUILayout.Slider( highlightedScaleModifier, 0.5f, 1.5f, new GUIContent( "Button Scale", "The scale modifier to be applied to the radial button transform when highlighted." ) );
				EditorGUILayout.Slider( positionModifier, -0.2f, 0.2f, new GUIContent( "Position Modifier", "The position modifier for how much the radial button will expand from it's default position." ) );
				EditorGUI.EndDisabledGroup();

				if( targ.useButtonIcon && ( targ.iconColorChange || targ.iconScaleTransform ) )
				{
					EditorGUILayout.Space();
					EditorGUILayout.LabelField( "Button Icon", EditorStyles.boldLabel );
					if( targ.iconColorChange )
						EditorGUILayout.PropertyField( iconHighlightedColor, new GUIContent( "Icon Color", "The color to be applied to the icon when highlighted." ) );

					if( targ.iconScaleTransform )
						EditorGUILayout.Slider( iconHighlightedScaleModifier, 0.5f, 1.5f, new GUIContent( "Icon Scale", "The scale modifier to be applied to the icon transform when highlighted." ) );
				}

				if( targ.useButtonText && targ.textColorChange )
				{
					EditorGUILayout.Space();
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.LabelField( "Button Text", EditorStyles.boldLabel );
					EditorGUILayout.PropertyField( textHighlightedColor, new GUIContent( "Text Color", "The color to be applied to the text when highlighted." ) );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();
				}

				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
				
				GUILayout.Space( 1 );
			}
			EndCollapsibleSection( "URM_ButtonHighlighted" );

			if( DisplayCollapsibleBoxSection( "Pressed", "URM_ButtonPressed" ) )
			{
				EditorGUI.BeginChangeCheck();

				if( targ.radialMenuStyle == null )
				{
					EditorGUI.BeginDisabledGroup( !targ.spriteSwap );
					EditorGUILayout.PropertyField( pressedSprite, new GUIContent( "Button Sprite", "The sprite to be applied to the radial button when pressed." ) );
					EditorGUI.EndDisabledGroup();
				}

				EditorGUI.BeginDisabledGroup( !targ.colorChange );
				EditorGUILayout.PropertyField( pressedColor, new GUIContent( "Button Color", "The color to be applied to the radial button when pressed." ) );
				EditorGUI.EndDisabledGroup();
				
				EditorGUI.BeginDisabledGroup( !targ.scaleTransform );
				EditorGUILayout.Slider( pressedScaleModifier, 0.5f, 1.5f, new GUIContent( "Button Scale", "The scale modifier to be applied to the radial button transform when pressed." ) );
				EditorGUILayout.Slider( pressedPositionModifier, -0.2f, 0.2f, new GUIContent( "Position Modifier", "The position modifier for how much the radial button will expand from it's default position." ) );
				EditorGUI.EndDisabledGroup();

				if( targ.useButtonIcon && ( targ.iconColorChange || targ.iconScaleTransform ) )
				{
					EditorGUILayout.Space();
					EditorGUILayout.LabelField( "Button Icon", EditorStyles.boldLabel );
					if( targ.iconColorChange )
						EditorGUILayout.PropertyField( iconPressedColor, new GUIContent( "Icon Color", "The color to be applied to the icon when pressed." ) );
					
					if( targ.iconScaleTransform )
						EditorGUILayout.Slider( iconPressedScaleModifier, 0.5f, 1.5f, new GUIContent( "Icon Scale", "The scale modifier to be applied to the icon transform when pressed." ) );
				}

				if( targ.useButtonText && targ.textColorChange )
				{
					EditorGUILayout.Space();
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.LabelField( "Button Text", EditorStyles.boldLabel );
					EditorGUILayout.PropertyField( textPressedColor, new GUIContent( "Text Color", "The color to be applied to the text when pressed." ) );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();
				}

				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
				
				GUILayout.Space( 1 );
			}
			EndCollapsibleSection( "URM_ButtonPressed" );

			if( DisplayCollapsibleBoxSection( "Selected", "URM_ButtonSelected" ) )
			{
				EditorGUI.BeginChangeCheck();

				if( targ.radialMenuStyle == null )
				{
					EditorGUI.BeginDisabledGroup( !targ.spriteSwap );
					EditorGUILayout.PropertyField( selectedSprite, new GUIContent( "Button Sprite", "The sprite to be applied to the radial button when selected." ) );
					EditorGUI.EndDisabledGroup();
				}

				EditorGUI.BeginDisabledGroup( !targ.colorChange );
				EditorGUILayout.PropertyField( selectedColor, new GUIContent( "Button Color", "The color to be applied to the radial button when selected." ) );
				EditorGUI.EndDisabledGroup();
				
				EditorGUI.BeginDisabledGroup( !targ.scaleTransform );
				EditorGUILayout.Slider( selectedScaleModifier, 0.5f, 1.5f, new GUIContent( "Button Scale", "The scale modifier to be applied to the radial button transform when selected." ) );
				EditorGUILayout.Slider( selectedPositionModifier, -0.2f, 0.2f, new GUIContent( "Position Modifier", "The position modifier for how much the radial button will expand from it's default position." ) );
				EditorGUI.EndDisabledGroup();

				EditorGUILayout.Space();
				EditorGUILayout.LabelField( "Automatic Selection", EditorStyles.boldLabel );

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( selectButtonOnInteract, new GUIContent( "Select On Interact", "Determines if the radial menu should show the last interacted button as selected." ) );
				if( targ.selectButtonOnInteract )
				{
					EditorGUI.indentLevel++;
					EditorGUILayout.PropertyField( serializedObject.FindProperty( "toggleSelection" ), new GUIContent( "Toggle Selection", "Should the buttons toggle selection on interaction?" ) );
					EditorGUILayout.PropertyField( serializedObject.FindProperty( "allowMultipleSelected" ), new GUIContent( "Allow Multi Select", "Should multiple buttons be able to be selected at one time?" ) );
					EditorGUI.indentLevel--;
				}
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				if( targ.useButtonIcon && ( targ.iconColorChange || targ.iconScaleTransform ) )
				{
					EditorGUILayout.Space();
					EditorGUILayout.LabelField( "Button Icon", EditorStyles.boldLabel );

					if( targ.iconColorChange )
						EditorGUILayout.PropertyField( iconSelectedColor, new GUIContent( "Icon Color", "The color to be applied to the icon when selected." ) );
					
					if( targ.iconScaleTransform )
						EditorGUILayout.Slider( iconSelectedScaleModifier, 0.5f, 1.5f, new GUIContent( "Icon Scale", "The scale modifier to be applied to the icon transform when selected." ) );
				}

				if( targ.useButtonText && targ.textColorChange )
				{
					EditorGUILayout.Space();
					EditorGUILayout.LabelField( "Button Text", EditorStyles.boldLabel );
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( textSelectedColor, new GUIContent( "Text Color", "The color to be applied to the text when selected." ) );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();
				}

				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
				
				GUILayout.Space( 1 );
			}
			EndCollapsibleSection( "URM_ButtonSelected" );

			if( DisplayCollapsibleBoxSection( "Disabled", "URM_ButtonDisabled" ) )
			{
				if( targ.radialMenuStyle == null )
				{
					EditorGUI.BeginDisabledGroup( !targ.spriteSwap );
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( disabledSprite, new GUIContent( "Button Sprite", "The sprite to be applied to the radial button when disabled." ) );
					if( EditorGUI.EndChangeCheck() )
					{
						serializedObject.ApplyModifiedProperties();

						for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
						{
							if( targ.UltimateRadialButtonList[ i ].buttonDisabled && targ.disabledSprite != null )
							{
								Undo.RecordObject( targ.UltimateRadialButtonList[ i ].radialImage, "Update Button Disabled Sprite" );
								targ.UltimateRadialButtonList[ i ].radialImage.sprite = targ.disabledSprite;
							}
						}
					}
					EditorGUI.EndDisabledGroup();
				}

				EditorGUI.BeginDisabledGroup( !targ.colorChange );
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( disabledColor, new GUIContent( "Button Color", "The color to be applied to the radial button when disabled." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
					{
						if( targ.UltimateRadialButtonList[ i ].buttonDisabled )
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ i ].radialImage, "Update Radial Button Color" );
							targ.UltimateRadialButtonList[ i ].radialImage.color = targ.disabledColor;
						}
					}
				}
				EditorGUI.EndDisabledGroup();
				
				EditorGUI.BeginDisabledGroup( !targ.scaleTransform );
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.Slider( disabledScaleModifier, 0.5f, 1.5f, new GUIContent( "Button Scale", "The scale modifier to be applied to the radial button transform when disabled." ) );
				EditorGUILayout.Slider( disabledPositionModifier, -0.2f, 0.2f, new GUIContent( "Position Modifier", "The position modifier for how much the radial button will expand from it's default position." ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
				EditorGUI.EndDisabledGroup();

				if( targ.useButtonIcon && ( targ.iconColorChange || targ.iconScaleTransform ) )
				{
					EditorGUILayout.Space();

					EditorGUILayout.LabelField( "Button Icon", EditorStyles.boldLabel );
					if( targ.iconColorChange )
					{
						EditorGUI.BeginChangeCheck();
						EditorGUILayout.PropertyField( iconDisabledColor, new GUIContent( "Icon Color", "The color to be applied to the icon when disabled." ) );
						if( EditorGUI.EndChangeCheck() )
						{
							serializedObject.ApplyModifiedProperties();

							for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
							{
								if( targ.UltimateRadialButtonList[ i ].buttonDisabled && targ.useButtonIcon && targ.UltimateRadialButtonList[ i ].icon != null && targ.UltimateRadialButtonList[ i ].icon.sprite != null )
								{
									Undo.RecordObject( targ.UltimateRadialButtonList[ i ].icon, "Update Button Disabled Color" );
									targ.UltimateRadialButtonList[ i ].icon.color = iconDisabledColor.colorValue;
								}
							}
						}
					}

					if( targ.iconScaleTransform )
					{
						EditorGUI.BeginChangeCheck();
						EditorGUILayout.Slider( iconDisabledScaleModifier, 0.5f, 1.5f, new GUIContent( "Icon Scale", "The scale modifier to be applied to the icon transform when disabled." ) );
						if( EditorGUI.EndChangeCheck() )
							serializedObject.ApplyModifiedProperties();
					}
				}

				if( targ.useButtonText && targ.textColorChange )
				{
					EditorGUILayout.Space();
					EditorGUILayout.LabelField( "Button Text", EditorStyles.boldLabel );
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( textDisabledColor, new GUIContent( "Text Color", "The color to be applied to the icon when disabled." ) );
					if( EditorGUI.EndChangeCheck() )
					{
						serializedObject.ApplyModifiedProperties();

						for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
						{
							if( targ.UltimateRadialButtonList[ i ].buttonDisabled && targ.UltimateRadialButtonList[ i ].text != null )
							{
								Undo.RecordObject( targ.UltimateRadialButtonList[ i ].text, "Update Text Disabled Color" );
								targ.UltimateRadialButtonList[ i ].text.color = textDisabledColor.colorValue;
							}
						}
					}
				}
				
				GUILayout.Space( 1 );
			}
			EndCollapsibleSection( "URM_ButtonDisabled" );
		}
		EndMainSection( "URM_ButtonInteraction" );

		if( DisplayHeaderDropdown( "Radial Button List", "URM_RadialButtonList" ) )
		{
			if( Application.isPlaying )
				EditorGUILayout.HelpBox( "Radial Button List cannot be edited during play mode.", MessageType.Info );
			else if( isInProjectWindow )
			{
				EditorGUILayout.BeginVertical( "Box" );
				HelpBox( "Cannot edit the Radial Button List in the project window", "Please double click the prefab object to edit it" );
				EditorGUILayout.EndVertical();
			}
			else
			{
				EditorGUILayout.BeginVertical( "Box" );
				GUILayout.Space( 1 );

				GUIStyle headerStyle = new GUIStyle( GUI.skin.label )
				{
					fontStyle = FontStyle.Bold,
					alignment = TextAnchor.MiddleCenter,
					wordWrap = true
				};

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.BeginHorizontal();
				if( GUILayout.Button( "◄", headerStyle, GUILayout.Width( 25 ) ) )
				{
					SelectedRadialButtonIndex = SelectedRadialButtonIndex == 0 ? targ.UltimateRadialButtonList.Count - 1 : SelectedRadialButtonIndex - 1;
					GUI.FocusControl( "" );
				}
				GUILayout.FlexibleSpace();
				EditorGUILayout.LabelField( buttonNames.Count == 0 ? "" : ( buttonNames[ selectedRadialButtonIndex ] ), headerStyle );
				GUILayout.FlexibleSpace();
				if( GUILayout.Button( "►", headerStyle, GUILayout.Width( 25 ) ) )
				{
					SelectedRadialButtonIndex = SelectedRadialButtonIndex == targ.UltimateRadialButtonList.Count - 1 ? 0 : SelectedRadialButtonIndex + 1;
					GUI.FocusControl( "" );
				}
				EditorGUILayout.EndHorizontal();
				if( EditorGUI.EndChangeCheck() )
					EditorGUIUtility.PingObject( targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].buttonTransform );

				EditorGUILayout.BeginHorizontal();
				EditorGUI.BeginDisabledGroup( Application.isPlaying );
				if( GUILayout.Button( "Insert", EditorStyles.miniButtonLeft ) )
					AddNewRadialButton( selectedRadialButtonIndex + 1 );
				EditorGUI.EndDisabledGroup();
				EditorGUI.BeginDisabledGroup( targ.UltimateRadialButtonList.Count == 1 );
				if( GUILayout.Button( "Remove", EditorStyles.miniButtonRight ) )
				{
					if( EditorUtility.DisplayDialog( "Ultimate Radial Menu", "Warning!\n\nAre you sure that you want to delete this radial button?", "Yes", "No" ) )
						RemoveRadialButton( selectedRadialButtonIndex );
				}
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.EndHorizontal();
				// END RADIAL BUTTON TOOLBAR //

				EditorGUILayout.Space();

				if( SelectedRadialButtonIndex < 0 || SelectedRadialButtonIndex > targ.UltimateRadialButtonList.Count - 1 )
					SelectedRadialButtonIndex = 0;

				if( buttonDisabled[ selectedRadialButtonIndex ] == null )
					StoreReferences();

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( buttonDisabled[ selectedRadialButtonIndex ], new GUIContent( "Disable Button", "Determines if this button should be disabled or not." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					if( buttonDisabled[ selectedRadialButtonIndex ].boolValue == true )
						targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].OnDisable();
					else
						targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].OnEnable();
				}

				EditorGUI.BeginDisabledGroup( buttonDisabled[ selectedRadialButtonIndex ].boolValue );
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( buttonName[ selectedRadialButtonIndex ], new GUIContent( "Name", "The name to be displayed on the radial button." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();
					if( targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].text != null )
					{
						if( displayNameOnButton.boolValue && targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].name != string.Empty )
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].text, "Update Radial Button Name" );
							targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].text.text = buttonName[ selectedRadialButtonIndex ].stringValue;
						}
						else
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].text, "Update Button Text Value" );
							targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].text.text = "Text";
						}
					}
				}

				if( description[ selectedRadialButtonIndex ].stringValue == string.Empty && Event.current.type == EventType.Repaint )
				{
					GUIStyle style = new GUIStyle( GUI.skin.textField );
					style.normal.textColor = new Color( 0.5f, 0.5f, 0.5f, 0.75f );
					style.wordWrap = true;
					EditorGUILayout.TextField( GUIContent.none, "Description", style );
				}
				else
				{
					Event mEvent = Event.current;

					if( mEvent.type == EventType.KeyDown && mEvent.keyCode == KeyCode.Return )
					{
						GUI.SetNextControlName( "DescriptionField" );
						if( GUI.GetNameOfFocusedControl() == "DescriptionField" )
							GUI.FocusControl( "" );
					}

					GUIStyle style = new GUIStyle( GUI.skin.textField ) { wordWrap = true };

					EditorGUI.BeginChangeCheck();
					description[ selectedRadialButtonIndex ].stringValue = EditorGUILayout.TextArea( description[ selectedRadialButtonIndex ].stringValue, style );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();
				}

				EditorGUILayout.Space();

				// ------------------------------------------- ICON SETTINGS ------------------------------------------- //
				if( targ.useButtonIcon )
				{
					EditorGUILayout.LabelField( "Icon Settings", EditorStyles.boldLabel );

					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( icon[ selectedRadialButtonIndex ], new GUIContent( "Icon Image", "The image component associated with this radial button." ) );
					if( EditorGUI.EndChangeCheck() )
					{
						serializedObject.ApplyModifiedProperties();

						if( icon[ selectedRadialButtonIndex ].objectReferenceValue != null )
							serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].iconTransform", selectedRadialButtonIndex ) ).objectReferenceValue = targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].icon.rectTransform;
						else
							serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].iconTransform", selectedRadialButtonIndex ) ).objectReferenceValue = null;

						serializedObject.ApplyModifiedProperties();
					}

					if( targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].icon != null )
					{
						EditorGUI.BeginChangeCheck();
						iconSprites[ selectedRadialButtonIndex ] = ( Sprite )EditorGUILayout.ObjectField( targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].icon.sprite, typeof( Sprite ), true );
						if( EditorGUI.EndChangeCheck() )
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].icon, "Update Radial Button Icon" );

							targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].icon.enabled = false;
							targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].icon.sprite = iconSprites[ selectedRadialButtonIndex ];
							targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].icon.enabled = true;

							if( iconSprites[ selectedRadialButtonIndex ] != null )
								targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].icon.color = targ.iconNormalColor;
							else
								targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].icon.color = Color.clear;
						}
					}
					if( iconLocalRotation.boolValue )
					{
						EditorGUI.BeginChangeCheck();
						EditorGUILayout.PropertyField( invertScaleY[ selectedRadialButtonIndex ], new GUIContent( "Invert Y Scale", "Determines if the radial menu should invert the y scale of this button." ) );
						if( EditorGUI.EndChangeCheck() )
							serializedObject.ApplyModifiedProperties();
					}

					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( useIconUnique[ selectedRadialButtonIndex ], new GUIContent( "Unique Positioning", "Determines if the icon should use positioning different from the prefab radial button or not." ) );
					if( EditorGUI.EndChangeCheck() )
					{
						if( useIconUnique[ selectedRadialButtonIndex ].boolValue == true )
						{
							if( rmbIconSize[ selectedRadialButtonIndex ].floatValue == 0.0f )
								rmbIconSize[ selectedRadialButtonIndex ].floatValue = iconSize.floatValue;
							if( rmbIconHorizontalPosition[ selectedRadialButtonIndex ].floatValue == 0.0f )
								rmbIconHorizontalPosition[ selectedRadialButtonIndex ].floatValue = iconHorizontalPosition.floatValue;
							if( rmbIconVerticalPosition[ selectedRadialButtonIndex ].floatValue == 0.0f )
								rmbIconVerticalPosition[ selectedRadialButtonIndex ].floatValue = iconVerticalPosition.floatValue;
							if( rmbIconRotation[ selectedRadialButtonIndex ].floatValue == 0.0f )
								rmbIconRotation[ selectedRadialButtonIndex ].floatValue = iconRotation.floatValue;
						}
						serializedObject.ApplyModifiedProperties();
					}
					if( useIconUnique[ selectedRadialButtonIndex ].boolValue )
					{
						EditorGUI.BeginChangeCheck();
						EditorGUILayout.Slider( rmbIconSize[ selectedRadialButtonIndex ], 0.0f, 1.0f, new GUIContent( "Icon Size", "The size of the icon image transform." ) );
						EditorGUILayout.Slider( rmbIconHorizontalPosition[ selectedRadialButtonIndex ], 0.0f, 100.0f, new GUIContent( "Horizontal Position", "The horizontal position in relation to the radial button transform." ) );
						EditorGUILayout.Slider( rmbIconVerticalPosition[ selectedRadialButtonIndex ], 0.0f, 100.0f, new GUIContent( "Vertical Position", "The vertical position in relation to the radial button transform." ) );
						EditorGUILayout.PropertyField( rmbIconRotation[ selectedRadialButtonIndex ], new GUIContent( "Rotation Offset", "The rotation offset to apply to the icon transform." ) );
						if( EditorGUI.EndChangeCheck() )
							serializedObject.ApplyModifiedProperties();
					}
					EditorGUILayout.Space();
				}

				// ------------------------------------------- TEXT SETTINGS ------------------------------------------- //
				if( targ.useButtonText )
				{
					EditorGUILayout.LabelField( "Text Settings", EditorStyles.boldLabel );

					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( text[ selectedRadialButtonIndex ], new GUIContent( "Button Text", "The text component to use for this radial button." ) );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();

					if( !targ.displayNameOnButton )
					{
						EditorGUI.BeginChangeCheck();
						buttonText[ selectedRadialButtonIndex ] = EditorGUILayout.TextField( buttonText[ selectedRadialButtonIndex ] );
						if( EditorGUI.EndChangeCheck() )
						{
							Undo.RecordObject( targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].text, "Update Radial Button Text" );

							targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].text.enabled = false;
							targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].text.text = buttonText[ selectedRadialButtonIndex ];
							targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].text.enabled = true;
						}
					}

					EditorGUILayout.Space();
				}

				// UNITY EVENTS //
				EditorGUILayout.BeginHorizontal();
				GUIStyle unityEventLabelStyle = new GUIStyle( GUI.skin.label );

				if( targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].unityEvent.GetPersistentEventCount() > 0 )
					unityEventLabelStyle.fontStyle = FontStyle.Bold;

				EditorGUILayout.LabelField( "Unity Events", unityEventLabelStyle );
				GUILayout.FlexibleSpace();
				if( GUILayout.Button( EditorPrefs.GetBool( "URM_RadialButtonUnityEvents" ) ? "-" : "+", EditorStyles.miniButton, GUILayout.Width( 17 ) ) )
					EditorPrefs.SetBool( "URM_RadialButtonUnityEvents", !EditorPrefs.GetBool( "URM_RadialButtonUnityEvents" ) );
				EditorGUILayout.EndHorizontal();
				if( EditorPrefs.GetBool( "URM_RadialButtonUnityEvents" ) )
				{
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( unityEvent[ selectedRadialButtonIndex ] );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();
				}
				EditorGUI.EndDisabledGroup();

				GUILayout.Space( 1 );
				EditorGUILayout.EndVertical();

				if( ReorderableRadialButtons != null )
					ReorderableRadialButtons.DoLayoutList();

				if( GUILayout.Button( "Clear Radial Buttons", EditorStyles.miniButton ) )
				{
					if( EditorUtility.DisplayDialog( "Ultimate Radial Menu", "Warning!\n\nAre you sure that you want to delete all of the radial buttons?", "Yes", "No" ) )
					{
						DeleteRadialImages();
						StoreReferences();
						Repaint();
						return;
					}
				}
			}
		}
		EndMainSection( "URM_RadialButtonList" );

		if( DisplayHeaderDropdown( "Script Reference", "UUI_ScriptReference" ) )
		{
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( radialMenuName, new GUIContent( "Radial Menu Name", "The name to be used for reference from scripts." ) );
			if( EditorGUI.EndChangeCheck() )
			{
				serializedObject.ApplyModifiedProperties();

				RadialMenuNameDuplicate = DuplicateRadialMenuName();
				RadialMenuNameUnassigned = targ.radialMenuName == string.Empty ? true : false;
				RadialMenuNameAssigned = RadialMenuNameDuplicate == false && targ.radialMenuName != string.Empty ? true : false;

				UpdateExampleCodeOptions();
			}

			if( targ.radialMenuName != string.Empty && RadialMenuNameDuplicate )
			{
				EditorGUILayout.BeginVertical( "Box" );
				HelpBox( "This name has already been used in your scene", "Please be sure to make the Radial Menu Name unique" );
				EditorGUILayout.EndVertical();
			}

			GUIStyle wordWrappedTextArea = new GUIStyle( GUI.skin.textArea ) { wordWrap = true };
			GUIStyle wordWrappedLabel = new GUIStyle( GUI.skin.label ) { wordWrap = true };

			EditorGUILayout.BeginVertical( "Box" );
			GUILayout.Space( 1 );

			if( targ.radialMenuName == string.Empty )
			{
				EditorGUILayout.LabelField( "Radial Menu Variable", EditorStyles.boldLabel );
				EditorGUILayout.TextArea( "public UltimateRadialMenu radialMenu;", wordWrappedTextArea );
				EditorGUILayout.LabelField( "Since the Radial Menu Name property above is not assigned, you will need to use a direct reference to this Ultimate Radial Menu. Please paste this code into your variable declaration section of your script before trying to reference this Ultimate Radial Menu.", wordWrappedLabel );
				EditorGUILayout.Space();
			}
			
			EditorGUILayout.LabelField( "Example Code Generator", EditorStyles.boldLabel );

			exampleCodeIndex = EditorGUILayout.Popup( "Function", exampleCodeIndex, exampleCodeOptions.ToArray() );
			ExampleCode[] ExampleCodes = PublicExampleCodes;
			if( targ.radialMenuName != string.Empty )
				ExampleCodes = StaticExampleCodes;

			EditorGUILayout.Space();

			EditorGUILayout.LabelField( "Function Description", EditorStyles.boldLabel );
			EditorGUILayout.LabelField( ExampleCodes[ exampleCodeIndex ].optionDescription, wordWrappedLabel );

			EditorGUILayout.Space();
			EditorGUILayout.LabelField( "Example Code", EditorStyles.boldLabel );
			EditorGUILayout.TextArea( string.Format( ExampleCodes[ exampleCodeIndex ].basicCode, targ.radialMenuName ), wordWrappedTextArea );

			if( exampleCodeIndex == 0 )
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField( "Needed Variable", EditorStyles.boldLabel );
				EditorGUILayout.TextArea( "UltimateRadialButtonInfo buttonInfo;", wordWrappedTextArea );
				EditorGUILayout.LabelField( "This variable is what you will pass to the radial menu when registering a button. This variable can be used afterwards to communicate with the button on the radial menu that it is assigned to.", wordWrappedLabel );
			}

			GUILayout.Space( 1 );
			EditorGUILayout.EndVertical();

			if( GUILayout.Button( "Open Documentation" ) )
				UltimateRadialMenuReadmeEditor.OpenReadmeDocumentation();
		}
		EndMainSection( "UUI_ScriptReference" );

		EditorGUILayout.Space();

		if( !disableDragAndDrop && isDraggingObject )
			Repaint();

		if( isDirty || ( !isDirty && wasDirtyLastFrame ) )
			SceneView.RepaintAll();

		wasDirtyLastFrame = isDirty;
		isDirty = false;
	}
	
	void CheckForParentCanvas ()
	{
		if( Selection.activeGameObject == null || Selection.activeGameObject != targ.gameObject )
			return;

		// Store the current parent.
		Transform parent = Selection.activeGameObject.transform.parent;

		// Loop through parents as long as there is one.
		while( parent != null )
		{
			// If there is a Canvas component, return that gameObject.
			if( parent.transform.GetComponent<UnityEngine.Canvas>() && parent.transform.GetComponent<UnityEngine.Canvas>().enabled == true )
			{
				parentCanvas = parent.transform.GetComponent<UnityEngine.Canvas>();
				return;
			}

			// Else, shift to the next parent.
			parent = parent.transform.parent;
		}
		if( parent == null && !AssetDatabase.Contains( Selection.activeGameObject ) )
		{
			if( EditorUtility.DisplayDialog( "Ultimate Radial Menu", "Where are you wanting to use this Ultimate Radial Menu?", "World Space", "Screen Space" ) )
				RequestCanvas( Selection.activeGameObject, false );
			else
				RequestCanvas( Selection.activeGameObject );
		}
	}

	bool DuplicateRadialMenuName ()
	{
#if UNITY_2022_2_OR_NEWER
		UltimateRadialMenu[] allRadialMenus = FindObjectsByType<UltimateRadialMenu>( FindObjectsSortMode.None );
#else
		UltimateRadialMenu[] allRadialMenus = FindObjectsOfType<UltimateRadialMenu>();
#endif

		for( int i = 0; i < allRadialMenus.Length; i++ )
		{
			if( allRadialMenus[ i ].radialMenuName == string.Empty )
				continue;

			if( allRadialMenus[ i ] != targ && allRadialMenus[ i ].radialMenuName == targ.radialMenuName )
				return true;
		}
		return false;
	}

	void AddNewRadialButton ( int index )
	{
		serializedObject.FindProperty( "UltimateRadialButtonList" ).InsertArrayElementAtIndex( index );
		serializedObject.ApplyModifiedProperties();

		GameObject newGameObject  = new GameObject();
		newGameObject.AddComponent<RectTransform>();
		newGameObject.AddComponent<CanvasRenderer>();
		newGameObject.AddComponent<Image>();

		if( targ.normalSprite != null )
		{
			newGameObject.GetComponent<Image>().sprite = targ.normalSprite;
			newGameObject.GetComponent<Image>().color = targ.normalColor;
		}
		else
			newGameObject.GetComponent<Image>().color = Color.clear;

		GameObject image = Instantiate( newGameObject.gameObject, Vector3.zero, Quaternion.identity );
		image.transform.SetParent( targ.transform );
		image.transform.SetSiblingIndex( targ.UltimateRadialButtonList[ targ.UltimateRadialButtonList.Count - 1 ].buttonTransform.GetSiblingIndex() + 1 );

		image.gameObject.name = "Radial Image " + ( targ.UltimateRadialButtonList.Count ).ToString( "00" );

		RectTransform trans = image.GetComponent<RectTransform>();
		
		trans.anchorMin = new Vector2( 0.5f, 0.5f );
		trans.anchorMax = new Vector2( 0.5f, 0.5f );
		trans.pivot = new Vector2( 0.5f, 0.5f );
		
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].radialMenu", index ) ).objectReferenceValue = targ;
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].buttonTransform", index ) ).objectReferenceValue = trans;
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].radialImage", index ) ).objectReferenceValue = trans.GetComponent<Image>();
		serializedObject.ApplyModifiedProperties();

		if( targ.useButtonIcon )
		{
			GameObject newIcon = new GameObject();
			newIcon.AddComponent<CanvasRenderer>();
			RectTransform iconTransform = newIcon.AddComponent<RectTransform>();
			Image iconImage = newIcon.AddComponent<Image>();

			newIcon.transform.SetParent( targ.UltimateRadialButtonList[ index ].buttonTransform );
			newIcon.name = "Icon";

			iconTransform.pivot = new Vector2( 0.5f, 0.5f );
			iconTransform.localScale = Vector3.one;

			if( iconPlaceholderSprite == null && targ.UltimateRadialButtonList.Count > 0 && targ.UltimateRadialButtonList[ 0 ].icon != null && targ.UltimateRadialButtonList[ 0 ].icon.sprite != null )
				iconPlaceholderSprite = targ.UltimateRadialButtonList[ 0 ].icon.sprite;

			iconImage.color = targ.iconNormalColor;
			if( iconPlaceholderSprite != null )
			{
				iconImage.sprite = iconPlaceholderSprite;
				iconImage.color = targ.iconNormalColor;
			}
			else
				iconImage.color = Color.clear;

			serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].icon", index ) ).objectReferenceValue = iconImage;
			serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].iconTransform", index ) ).objectReferenceValue = iconTransform;
			serializedObject.ApplyModifiedProperties();
		}

		if( targ.useButtonText )
		{
			GameObject newText = new GameObject();
			RectTransform textTransform = newText.AddComponent<RectTransform>();
			newText.AddComponent<CanvasRenderer>();
			Text textComponent = newText.AddComponent<Text>();
			newText.name = "Text";

			newText.transform.SetParent( targ.UltimateRadialButtonList[ index ].buttonTransform );
			newText.transform.SetAsLastSibling();

			textTransform.position = targ.UltimateRadialButtonList[ selectedRadialButtonIndex ].buttonTransform.position;
			textTransform.localScale = Vector3.one;
			textTransform.pivot = new Vector2( 0.5f, 0.5f );

			textComponent.text = "Text";
			textComponent.resizeTextForBestFit = true;
			textComponent.resizeTextMinSize = 0;
			textComponent.resizeTextMaxSize = 300;
			textComponent.alignment = TextAnchor.MiddleCenter;
			textComponent.color = targ.textNormalColor;

			if( targ.buttonTextFont != null )
				textComponent.font = targ.buttonTextFont;

			if( targ.buttonTextOutline )
			{
				UnityEngine.UI.Outline textOutline = textComponent.gameObject.AddComponent<UnityEngine.UI.Outline>();
				textOutline.effectColor = targ.buttonTextOutlineColor;
			}
			
			serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].text", index ) ).objectReferenceValue = textComponent;
			serializedObject.ApplyModifiedProperties();
		}

		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].useIconUnique", index ) ).boolValue = false;
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].buttonDisabled", index ) ).boolValue = targ.UltimateRadialButtonList[ 0 ].buttonDisabled;
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].name", index ) ).stringValue = string.Empty;
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].description", index ) ).stringValue = string.Empty;
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].iconSize", index ) ).floatValue = 0.0f;
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].iconHorizontalPosition", index ) ).floatValue = 0.0f;
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].iconVerticalPosition", index ) ).floatValue = 0.0f;
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].iconRotation", index ) ).floatValue = 0.0f;
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].id", index ) ).intValue = 0;
		serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].key", index ) ).stringValue = string.Empty;
		serializedObject.ApplyModifiedProperties();
		
		buttonInputAngle.floatValue = 1.0f;
		serializedObject.ApplyModifiedProperties();

		Undo.RegisterCreatedObjectUndo( image, "Create Radial Button Object" );

		StoreReferences();
		SelectedRadialButtonIndex = index;

		if( newGameObject != targ.UltimateRadialButtonList[ 0 ].buttonTransform.gameObject )
			DestroyImmediate( newGameObject );

		UpdateRadialButtonStyle();
	}

	void RemoveRadialButton ( int index )
	{
		GameObject objToDestroy = targ.UltimateRadialButtonList[ index ].radialImage.gameObject;
		serializedObject.FindProperty( "UltimateRadialButtonList" ).DeleteArrayElementAtIndex( index );
		buttonInputAngle.floatValue = 1.0f;
		serializedObject.ApplyModifiedProperties();

		Undo.DestroyObjectImmediate( objToDestroy );

		StoreReferences();

		if( index == targ.UltimateRadialButtonList.Count )
			SelectedRadialButtonIndex = targ.UltimateRadialButtonList.Count - 1;

		UpdateRadialButtonStyle();
	}
	
	void GenerateRadialImages ()
	{
		GameObject newGameObject = new GameObject();
		newGameObject.AddComponent<RectTransform>();
		newGameObject.AddComponent<CanvasRenderer>();
		Image img = newGameObject.AddComponent<Image>();

		img.color = targ.normalColor;

		if( targ.normalSprite != null )
			img.sprite = targ.normalSprite;
		else
			img.color = Color.clear;

		newGameObject.transform.SetParent( targ.transform );
		
		for( int i = 0; i < menuButtonCount; i++ )
		{
			GameObject image = Instantiate( newGameObject, Vector3.zero, Quaternion.identity );
			image.transform.SetParent( targ.transform );

			image.gameObject.name = "Radial Image " + i.ToString( "00" );

			RectTransform trans = image.GetComponent<RectTransform>();

			trans.anchorMin = new Vector2( 0.5f, 0.5f );
			trans.anchorMax = new Vector2( 0.5f, 0.5f );
			trans.pivot = new Vector2( 0.5f, 0.5f );

			serializedObject.FindProperty( "UltimateRadialButtonList" ).arraySize++;
			serializedObject.ApplyModifiedProperties();

			targ.UltimateRadialButtonList[ i ] = new UltimateRadialMenu.UltimateRadialButton();
			serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].radialMenu", i ) ).objectReferenceValue = targ;
			serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].buttonTransform", i ) ).objectReferenceValue = trans;
			serializedObject.FindProperty( string.Format( "UltimateRadialButtonList.Array.data[{0}].radialImage", i ) ).objectReferenceValue = trans.GetComponent<Image>();
			serializedObject.ApplyModifiedProperties();
			
			Undo.RegisterCreatedObjectUndo( image, "Create Radial Button Objects" );
		}
		
		buttonInputAngle.floatValue = 1.0f;
		serializedObject.ApplyModifiedProperties();
		StoreReferences();

		DestroyImmediate( newGameObject );

#if UNITY_2022_2_OR_NEWER
		EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
#else
		EventSystem eventSystem = FindObjectOfType<EventSystem>();
#endif
		if( eventSystem != null && !eventSystem.GetComponent<UltimateRadialMenuInputManager>() )
			Undo.AddComponent( eventSystem.gameObject, typeof( UltimateRadialMenuInputManager) );
	}

	void DeleteRadialImages ()
	{
		for( int i = targ.UltimateRadialButtonList.Count - 1; i >= 0; i-- )
			Undo.DestroyObjectImmediate( targ.UltimateRadialButtonList[ i ].radialImage.gameObject );

		serializedObject.FindProperty( "UltimateRadialButtonList" ).ClearArray();
		serializedObject.ApplyModifiedProperties();

		StoreReferences();
	}

	void UpdateRadialButtonStyle ()
	{
		if( targ.radialMenuStyle != null && targ.radialMenuStyle.RadialMenuStyles.Count > 0 )
		{
			int CurrentStyleIndex = targ.UltimateRadialButtonList.Count <= targ.radialMenuStyle.RadialMenuStyles[ 0 ].buttonCount ? 0 : targ.radialMenuStyle.RadialMenuStyles.Count - 1;
			for( int i = 0; i < targ.radialMenuStyle.RadialMenuStyles.Count; i++ )
			{
				float styleAngle = 360.0f / targ.radialMenuStyle.RadialMenuStyles[ i ].buttonCount;

				if( ( float )styleAngle <= targ.GetAnglePerButton )
				{
					CurrentStyleIndex = i;
					break;
				}
			}

			normalSprite.objectReferenceValue = targ.radialMenuStyle.RadialMenuStyles[ CurrentStyleIndex ].normalSprite;
			serializedObject.ApplyModifiedProperties();

			if( targ.spriteSwap )
			{
				highlightedSprite.objectReferenceValue = targ.radialMenuStyle.RadialMenuStyles[ CurrentStyleIndex ].highlightedSprite;
				pressedSprite.objectReferenceValue = targ.radialMenuStyle.RadialMenuStyles[ CurrentStyleIndex ].pressedSprite;
				selectedSprite.objectReferenceValue = targ.radialMenuStyle.RadialMenuStyles[ CurrentStyleIndex ].selectedSprite;
				disabledSprite.objectReferenceValue = targ.radialMenuStyle.RadialMenuStyles[ CurrentStyleIndex ].disabledSprite;
				serializedObject.ApplyModifiedProperties();
			}
			
			for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
			{
				Undo.RecordObject( targ.UltimateRadialButtonList[ i ].radialImage, "Update Radial Button Style" );

				if( targ.UltimateRadialButtonList[ i ].buttonDisabled && targ.spriteSwap && targ.disabledSprite != null )
					targ.UltimateRadialButtonList[ i ].radialImage.sprite = targ.disabledSprite;
				else
					targ.UltimateRadialButtonList[ i ].radialImage.sprite = targ.normalSprite;

				if( targ.UltimateRadialButtonList[ i ].buttonDisabled && targ.colorChange )
					targ.UltimateRadialButtonList[ i ].radialImage.color = targ.disabledColor;
				else
					targ.UltimateRadialButtonList[ i ].radialImage.color = targ.normalColor;
				
				if( prefabRootError )
					PrefabUtility.RecordPrefabInstancePropertyModifications( targ.UltimateRadialButtonList[ i ].radialImage );
			}
		}
		else
		{
			for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
			{
				if( targ.normalSprite != null )
				{
					if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.spriteSwap )
						targ.UltimateRadialButtonList[ i ].radialImage.sprite = targ.normalSprite;

					if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.colorChange )
						targ.UltimateRadialButtonList[ i ].radialImage.color = normalColor.colorValue;
				}
				else
				{
					if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.spriteSwap )
						targ.UltimateRadialButtonList[ i ].radialImage.sprite = null;

					if( !targ.UltimateRadialButtonList[ i ].buttonDisabled || !targ.colorChange )
						targ.UltimateRadialButtonList[ i ].radialImage.color = Color.clear;
				}
			}
		}
	}

	void CheckForDuplicateButtonCount ()
	{
		if( newStyleButtonCount <= 1 )
		{
			duplicateButtonCount = true;
			return;
		}

		for( int i = 0; i < targ.PointerStyles.Count; i++ )
		{
			if( newStyleButtonCount == targ.PointerStyles[ i ].buttonCount )
			{
				duplicateButtonCount = true;
				return;
			}
		}

		duplicateButtonCount = false;
	}
	
	void OnSceneGUI ()
	{
		if( Selection.activeGameObject == null || Application.isPlaying || Selection.objects.Length > 1 || parentCanvas == null )
		{
			if( parentCanvas == null )
			{
				CheckForParentCanvas();
				StoreReferences();
			}
			return;
		}
		
		RectTransform trans = targ.transform.GetComponent<RectTransform>();
		Vector3 center = trans.position;
		float sizeDeltaX = trans.sizeDelta.x * parentCanvasRectTrans.localScale.x;

		Handles.color = colorDefault;

		if( targ.UltimateRadialButtonList.Count == 0 )
		{
			Handles.color = colorDefault;
			Handles.DrawWireDisc( center, Selection.activeGameObject.transform.forward, ( sizeDeltaX / 2 ) );
			Handles.DrawWireDisc( center, Selection.activeGameObject.transform.forward, ( sizeDeltaX / 4 ) );

			float anglePerButton = 360f / menuButtonCount;
			float angleInRadians = anglePerButton * Mathf.Deg2Rad;
			float halfAngle = anglePerButton / 2 * Mathf.Deg2Rad;

			for( int i = 0; i < menuButtonCount; i++ )
			{
				Vector3 lineStart = Vector3.zero;
				lineStart.x += ( Mathf.Cos( ( angleInRadians * i ) + ( 90 * Mathf.Deg2Rad ) + halfAngle ) * ( trans.sizeDelta.x / 4 ) );
				lineStart.y += ( Mathf.Sin( ( angleInRadians * i ) + ( 90 * Mathf.Deg2Rad ) + halfAngle ) * ( trans.sizeDelta.x / 4 ) );
				Vector3 lineEnd = Vector3.zero;
				lineEnd.x += ( Mathf.Cos( ( angleInRadians * i ) + ( 90 * Mathf.Deg2Rad ) + halfAngle ) * ( trans.sizeDelta.x / 2 ) );
				lineEnd.y += ( Mathf.Sin( ( angleInRadians * i ) + ( 90 * Mathf.Deg2Rad ) + halfAngle ) * ( trans.sizeDelta.x / 2 ) );

				lineStart = targ.transform.TransformPoint( lineStart );
				lineEnd = targ.transform.TransformPoint( lineEnd );
				
				Handles.DrawLine( lineStart, lineEnd );
			}
			return;
		}

		if( EditorPrefs.GetBool( "URM_RadialMenuPositioning" ) )
		{
			Handles.color = colorDefault;
			if( DisplayMinRange.HighlightGizmo )
				Handles.color = colorValueChanged;

			Handles.DrawWireDisc( center, Selection.activeGameObject.transform.forward, ( sizeDeltaX / 2 ) * targ.minRange );
			Handles.Label( center + ( -trans.transform.up * ( ( sizeDeltaX / 2 ) * targ.minRange ) ), "Min Range: " + targ.minRange );

			Handles.color = colorDefault;
			if( DisplayMaxRange.HighlightGizmo )
				Handles.color = colorValueChanged;

			if( !targ.infiniteMaxRange )
			{
				Handles.DrawWireDisc( center, Selection.activeGameObject.transform.forward, ( sizeDeltaX / 2 ) * targ.maxRange );
				Handles.Label( center + ( -trans.transform.up * ( ( sizeDeltaX / 2 ) * targ.maxRange ) ), "Max Range: " + targ.maxRange.ToString() );
			}

			if( targ.UltimateRadialButtonList.Count > 0 )
			{
				float maxRange = targ.maxRange;
				if( targ.infiniteMaxRange )
					maxRange = 1.5f;

				Handles.color = colorDefault;
				if( DisplayInputAngle.HighlightGizmo )
					Handles.color = colorValueChanged;

				float minAngle = targ.UltimateRadialButtonList[ 0 ].angle + targ.UltimateRadialButtonList[ 0 ].angleRange;
				float maxAngle = targ.UltimateRadialButtonList[ 0 ].angle - targ.UltimateRadialButtonList[ 0 ].angleRange;

				Vector3 lineStart = Vector3.zero;
				lineStart.x += ( Mathf.Cos( minAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * targ.minRange ) );
				lineStart.y += ( Mathf.Sin( minAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * targ.minRange ) );
				Vector3 lineEnd = Vector3.zero;
				lineEnd.x += ( Mathf.Cos( minAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * maxRange ) );
				lineEnd.y += ( Mathf.Sin( minAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * maxRange ) );

				lineStart = targ.transform.TransformPoint( lineStart );
				lineEnd = targ.transform.TransformPoint( lineEnd );

				Handles.DrawLine( lineStart, lineEnd );

				lineStart = Vector3.zero;
				lineStart.x += ( Mathf.Cos( maxAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * targ.minRange ) );
				lineStart.y += ( Mathf.Sin( maxAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * targ.minRange ) );
				lineEnd = Vector3.zero;
				lineEnd.x += ( Mathf.Cos( maxAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * maxRange ) );
				lineEnd.y += ( Mathf.Sin( maxAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * maxRange ) );

				lineStart = targ.transform.TransformPoint( lineStart );
				lineEnd = targ.transform.TransformPoint( lineEnd );

				Handles.DrawLine( lineStart, lineEnd );
			}
		}

		if( EditorPrefs.GetBool( "URM_RadialMenuOptions" ) )
		{
			if( EditorPrefs.GetBool( "URM_MenuText" ) )
			{
				Handles.color = colorTextBox;
				if( targ.displayButtonName && targ.nameText != null )
					DisplayWireBox( targ.nameText.rectTransform.localPosition, targ.nameText.rectTransform.sizeDelta, targ.transform );

				if( targ.displayButtonDescription && targ.descriptionText != null )
					DisplayWireBox( targ.descriptionText.rectTransform.localPosition, targ.descriptionText.rectTransform.sizeDelta, targ.transform );
			}
			
			if( EditorPrefs.GetBool( "URM_ButtonText" ) && targ.useButtonText )
			{
				Handles.color = colorTextBox;
				for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
				{
					if( targ.UltimateRadialButtonList[ i ].text == null )
						continue;

					DisplayWireBox( targ.UltimateRadialButtonList[ i ].text.rectTransform.InverseTransformPoint( targ.UltimateRadialButtonList[ i ].text.rectTransform.position ), targ.UltimateRadialButtonList[ i ].text.rectTransform.sizeDelta, targ.UltimateRadialButtonList[ i ].text.rectTransform );
				}
			}
		}

		if( EditorPrefs.GetBool( "URM_RadialButtonList" ) )
		{
			for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
			{
				if( selectedRadialButtonIndex == i )
					Handles.color = colorButtonSelected;
				else
					Handles.color = colorButtonUnselected;

				float handleSize = sizeDeltaX / 10;
				float distanceMod = ( ( sizeDeltaX / 2 ) * ( targ.radialMenuButtonRadius ) ) + handleSize;

				Vector3 difference = center - targ.UltimateRadialButtonList[ i ].buttonTransform.position;
				
				Vector3 handlePos = center;
				handlePos += -difference.normalized * distanceMod;

				if( Handles.Button( handlePos, Quaternion.identity, handleSize, sizeDeltaX / 10, Handles.SphereHandleCap ) )
				{
					SelectedRadialButtonIndex = i;
					EditorGUIUtility.PingObject( targ.UltimateRadialButtonList[ i ].buttonTransform );
				}
				GUIStyle labelStyle = new GUIStyle( GUI.skin.label )
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Bold,
				};
				Handles.Label( handlePos, i.ToString( "00" ), labelStyle );
			}
		}

		if( targ.normalSprite == null )
		{
			Color halfColor = colorDefault;
			halfColor.a = 0.75f;
			Handles.color = halfColor;

			if( targ.followOrbitalRotation )
			{
				Handles.DrawWireDisc( center, Selection.activeGameObject.transform.forward, ( sizeDeltaX / 2 ) * targ.minRange );

				if( !targ.infiniteMaxRange )
					Handles.DrawWireDisc( center, Selection.activeGameObject.transform.forward, ( sizeDeltaX / 2 ) * targ.maxRange );

				float maxRange = targ.maxRange;
				if( targ.infiniteMaxRange )
					maxRange = 1.5f;

				for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
				{
					float minAngle = targ.UltimateRadialButtonList[ i ].angle + targ.UltimateRadialButtonList[ i ].angleRange;

					Vector3 lineStart = Vector3.zero;
					lineStart.x += Mathf.Cos( minAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * targ.minRange );
					lineStart.y += Mathf.Sin( minAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * targ.minRange );
					Vector3 lineEnd = Vector3.zero;
					lineEnd.x += Mathf.Cos( minAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * maxRange );
					lineEnd.y += Mathf.Sin( minAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * maxRange );

					lineStart = targ.transform.TransformPoint( lineStart );
					lineEnd = targ.transform.TransformPoint( lineEnd );

					Handles.DrawLine( lineStart, lineEnd );

					if( targ.overallAngle < 360.0f && i == targ.UltimateRadialButtonList.Count - 1 )
					{
						float maxAngle = targ.UltimateRadialButtonList[ i ].angle - targ.UltimateRadialButtonList[ i ].angleRange;

						lineStart = Vector3.zero;
						lineStart.x += Mathf.Cos( maxAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * targ.minRange );
						lineStart.y += Mathf.Sin( maxAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * targ.minRange );
						lineEnd = Vector3.zero;
						lineEnd.x += Mathf.Cos( maxAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * maxRange );
						lineEnd.y += Mathf.Sin( maxAngle * Mathf.Deg2Rad ) * ( ( trans.sizeDelta.x / 2 ) * maxRange );

						lineStart = targ.transform.TransformPoint( lineStart );
						lineEnd = targ.transform.TransformPoint( lineEnd );

						Handles.DrawLine( lineStart, lineEnd );
					}
				}
			}
			else
			{
				for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
					DisplayWireBox( targ.UltimateRadialButtonList[ i ].buttonTransform.localPosition, targ.UltimateRadialButtonList[ i ].buttonTransform.sizeDelta, targ.transform );
			}
		}

		if( ( EditorPrefs.GetBool( "URM_ButtonIcon" ) || EditorPrefs.GetBool( "URM_RadialButtonList" ) ) && targ.useButtonIcon )
			DrawEmptyIcons();
	}

	void DrawEmptyIcons ()
	{
		Handles.color = colorDefault;
		for( int i = 0; i < targ.UltimateRadialButtonList.Count; i++ )
		{
			if( targ.UltimateRadialButtonList[ i ].icon == null || targ.UltimateRadialButtonList[ i ].icon.sprite != null )
				continue;

			Handles.DrawWireDisc( targ.UltimateRadialButtonList[ i ].iconTransform.position, Selection.activeGameObject.transform.forward, ( targ.UltimateRadialButtonList[ i ].iconTransform.sizeDelta.x * parentCanvasRectTrans.localScale.x ) / 2 );

			GUIStyle labelStyle = new GUIStyle( GUI.skin.label )
			{
				alignment = TextAnchor.MiddleCenter,
				fontStyle = FontStyle.Bold,
			};
			Handles.Label( targ.UltimateRadialButtonList[ i ].iconTransform.position, "Icon", labelStyle );
		}
	}

	void DisplayWireBox ( Vector3 center, Vector2 sizeDelta, Transform trans )
	{
		float halfHeight = sizeDelta.y / 2;
		float halfWidth = sizeDelta.x / 2;

		Vector3 topLeft = center + new Vector3( -halfWidth, halfHeight, 0 );
		Vector3 topRight = center + new Vector3( halfWidth, halfHeight, 0 );
		Vector3 bottomRight = center + new Vector3( halfWidth, -halfHeight, 0 );
		Vector3 bottomLeft = center + new Vector3( -halfWidth, -halfHeight, 0 );

		topLeft = trans.TransformPoint( topLeft );
		topRight = trans.TransformPoint( topRight );
		bottomRight = trans.TransformPoint( bottomRight );
		bottomLeft = trans.TransformPoint( bottomLeft );

		Handles.DrawLine( topLeft, topRight );
		Handles.DrawLine( topRight, bottomRight );
		Handles.DrawLine( bottomRight, bottomLeft );
		Handles.DrawLine( bottomLeft, topLeft );
	}

	void CheckEventSystem ()
	{
#if UNITY_2022_2_OR_NEWER
		EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
#else
		EventSystem eventSystem = FindObjectOfType<EventSystem>();
#endif
		if( eventSystem == null )
		{
			GameObject eventSystemObject = new GameObject( "EventSystem" );
			eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
			eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
			eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
			eventSystemObject.AddComponent<UltimateRadialMenuInputManager>();

			Undo.RegisterCreatedObjectUndo( eventSystemObject, "Create EventSystem" );
		}
	}
	
	void RequestCanvas ( GameObject child, bool screenSpaceOverlay = true )
	{
		// Store all canvas objects to check the render mode options.
#if UNITY_2022_2_OR_NEWER
		UnityEngine.Canvas[] allCanvas = FindObjectsByType<UnityEngine.Canvas>( FindObjectsSortMode.None );
#else
		UnityEngine.Canvas[] allCanvas = FindObjectsOfType<UnityEngine.Canvas>();
#endif
		// Loop through each canvas.
		for( int i = 0; i < allCanvas.Length; i++ )
		{
			// If the user wants a screen space canvas...
			if( screenSpaceOverlay )
			{
				// Check to see if this canvas is set to Screen Space and it is enabled. Then set the parent and check for an event system.
				if( allCanvas[ i ].renderMode == RenderMode.ScreenSpaceOverlay && allCanvas[ i ].enabled == true )
				{
					Undo.SetTransformParent( child.transform, allCanvas[ i ].transform, "Update Radial Menu Parent" );
					CheckEventSystem();
					return;
				}
			}
			// Else the user wants a world space canvas...
			else
			{
				// Check to see if this canvas is set to World Space and see if it is enabled. Then set the parent and check for an event system.
				if( allCanvas[ i ].renderMode == RenderMode.WorldSpace && allCanvas[ i ].enabled == true )
				{
					Undo.SetTransformParent( child.transform, allCanvas[ i ].transform, "Update Radial Menu Parent" );
					
					if( !child.GetComponent<BoxCollider>() )
						Undo.AddComponent( child, typeof( BoxCollider ) );

					CheckEventSystem();
					return;
				}
			}
		}

		// If there have been no canvas objects found for this child, then create a new one.
		GameObject newCanvasObject = new GameObject( "Canvas" );
		newCanvasObject.layer = LayerMask.NameToLayer( "UI" );
		RectTransform canvasRectTransform = newCanvasObject.AddComponent<RectTransform>();
		UnityEngine.Canvas canvas = newCanvasObject.AddComponent<UnityEngine.Canvas>();
		newCanvasObject.AddComponent<GraphicRaycaster>();

		if( !screenSpaceOverlay )
		{
			canvas.renderMode = RenderMode.WorldSpace;
			canvasRectTransform.sizeDelta = Vector2.one * 1000;
			canvasRectTransform.localScale = Vector3.one * 0.01f;

			if( !child.GetComponent<BoxCollider>() )
				Undo.AddComponent( child, typeof( BoxCollider ) );
		}
		else
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		
		Undo.RegisterCreatedObjectUndo( newCanvasObject, "Create New Canvas" );
		Undo.SetTransformParent( child.transform, newCanvasObject.transform, "Create New Canvas" );
		CheckEventSystem();
	}

	[MenuItem( "GameObject/UI/Ultimate Radial Menu" )]
	public static void CreateUltimateRadialMenuFromScratch ()
	{
		GameObject ultimateRadialMenu = new GameObject( "Ultimate Radial Menu" );
		ultimateRadialMenu.layer = LayerMask.NameToLayer( "UI" );
		ultimateRadialMenu.AddComponent<RectTransform>();
		ultimateRadialMenu.AddComponent<CanvasGroup>();
		ultimateRadialMenu.AddComponent<UltimateRadialMenu>();

		Undo.RegisterCreatedObjectUndo( ultimateRadialMenu, "Create Ultimate Radial Menu" );

		Selection.activeGameObject = ultimateRadialMenu;
	}
}