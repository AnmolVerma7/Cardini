/* RadialMenuPrefabDisplay.cs */
/* Written by Kaz */
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class RadialMenuPrefabDisplay : MonoBehaviour
{
	[Header( "Common Settings" )]
	public Text prefabNameText;
	public int radialMenuButtons = 4;
	public UltimateRadialButtonInfo radialMenuButtonInfo;

	[Header( "Prefabs" )]
	public List<GameObject> allPrefabs = new List<GameObject>();
	List<UltimateRadialMenu> allRadialMenus = new List<UltimateRadialMenu>();
	int currentRadialMenuIndex = 0;


	private void Start ()
	{
		// If there are no prefabs in the list, send an error and return.
		if( allPrefabs.Count == 0 )
		{
			Debug.LogError( "No radial menu prefabs are registered in the All Prefabs list." );
			return;
		}

		// Loop through all the prefabs in the list...
		for( int i = 0; i < allPrefabs.Count; i++ )
		{
			// Create and add the prefab to the list of radial menus.
			allRadialMenus.Add( Instantiate( allPrefabs[ i ], transform ).GetComponent<UltimateRadialMenu>() );

			// If this is not the first time through the loop, then disable the menu so that only the first menu will be visible to start.
			if( i > 0 )
				allRadialMenus[ allRadialMenus.Count - 1 ].Disable();
		}

		// Register dummy buttons on the first radial menu in the list.
		RegisterDummyRadialMenuButtons( allRadialMenus[ 0 ] );
	}

	public void NextRadialMenu ()
	{
		// Disable the current menu.
		allRadialMenus[ currentRadialMenuIndex ].Disable();

		// Configure the target index as one more than the current.
		int targetIndex = currentRadialMenuIndex + 1;

		// If the target is out of range, then set it to the 0 index.
		if( targetIndex >= allRadialMenus.Count )
			targetIndex = 0;
		
		// Register the dummy buttons to the target radial menu.
		RegisterDummyRadialMenuButtons( allRadialMenus[ targetIndex ] );

		// Update the current index.
		currentRadialMenuIndex = targetIndex;
	}


	public void PreviousRadialMenu ()
	{
		// Disable the current menu.
		allRadialMenus[ currentRadialMenuIndex ].Disable();

		// Configure the target index.
		int targetIndex = currentRadialMenuIndex - 1;

		// If the target index is out of range, then set it to the end of the list.
		if( targetIndex < 0 )
			targetIndex = allRadialMenus.Count - 1;
		
		// Register the dummy buttons to the target radial menu.
		RegisterDummyRadialMenuButtons( allRadialMenus[ targetIndex ] );

		// Update the current index.
		currentRadialMenuIndex = targetIndex;
	}

	public void SelectPrefabInProject ()
	{
		// If this is run in the editor, then select the prefab gameobject in the project window.
#if UNITY_EDITOR
		UnityEditor.Selection.activeGameObject = allPrefabs[ currentRadialMenuIndex ];
#endif
	}

	// Increase radial button count and re-register the radial menu buttons.
	public void IncreaseRadialButtonCount ()
	{
		radialMenuButtons++;
		RegisterDummyRadialMenuButtons( allRadialMenus[ currentRadialMenuIndex ] );
	}

	// Decrease the radial menu button count ONLY if it's over 2, and then re-register the buttons.
	public void DecreaseRadialButtonCount ()
	{
		if( radialMenuButtons <= 2 )
			return;

		radialMenuButtons--;
		RegisterDummyRadialMenuButtons( allRadialMenus[ currentRadialMenuIndex ] );
	}

	void RegisterDummyRadialMenuButtons ( UltimateRadialMenu radialMenu )
	{
		// Clear the radial menu.
		radialMenu.ClearMenu();

		// Loop through the amount of buttons to display, registered dummy info to them.
		for( int i = 0; i < radialMenuButtons; i++ )
			radialMenu.RegisterButton( DummyVoid, new UltimateRadialButtonInfo() { icon = radialMenuButtonInfo.icon } );

		// Enable the menu.
		radialMenu.Enable();

		// Update the name of the current prefab.
		prefabNameText.text = "Prefab name: " + allRadialMenus[ currentRadialMenuIndex ].name.Split( '(' )[ 0 ];
	}

	void DummyVoid ()
	{
		// Just a bank function to call for the radial menu buttons.
	}
}