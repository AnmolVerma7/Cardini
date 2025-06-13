// CardiniRadialInputManager.cs
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // For Gamepad class if using asset's default gamepad logic // Assuming InputBridge is in Cardini.Motion namespace
#endif

// Assuming your InputBridge and other Cardini scripts are in Cardini.Motion
namespace Cardini.Motion 
{
    
    // This class MUST inherit from the asset's input manager base class.
    // The original script was global, so ensure this can see UltimateRadialMenuInputManager.
    // If UltimateRadialMenuInputManager is in a namespace, add a 'using' for it.
    public class CardiniRadialInputManager : UltimateRadialMenuInputManager 
    {
        // Reference to our InputBridge
        [Header("Custom References")] // Add to top of class
        [SerializeField] private InputBridge _inputBridge; // Reference to the InputBridge instance

        // We can keep a reference to the last known joystick input for the radial menu
        private Vector2 _lastRadialLookInput = Vector2.zero;

        protected override void Awake()
        {
            // Call the base Awake to ensure its singleton logic and list setup runs
            base.Awake(); 

            // Find our InputBridge. Assuming it's on a player object or a global manager.
            // This might need adjustment based on your scene structure.
            // If CardiniPlayerObject is tagged "Player":
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player"); 
            if (playerObject != null)
            {
                _inputBridge = playerObject.GetComponent<InputBridge>();
            }

            if (_inputBridge == null)
            {
                Debug.LogError("CardiniRadialInputManager: Could not find InputBridge instance!", this);
                enabled = false; // Disable if InputBridge isn't found
            }
        }

        // Override the ControllerInput function to use our InputBridge for LookInput
        public override void ControllerInput(ref bool enableMenu, ref bool disableMenu, ref Vector2 input, ref float distance, ref bool inputDown, ref bool inputUp, int radialMenuIndex)
        {
            if (_inputBridge == null)
                return;

            // 1. Handle Enable/Disable Menu (from InputBridge.AbilitySelect)
            // The base class's Update() and CheckEnableMenu() might already handle this if 
            // we set enableMenuSetting correctly and provide the right button to its internal logic.
            // For "Hold" to enable:
            if (enableMenuSetting == EnableMenuSetting.Hold)
            {
                if (_inputBridge.AbilitySelect.IsHeld && !UltimateRadialMenuInformations[radialMenuIndex].radialMenu.IsEnabled)
                    enableMenu = true; // Signal to enable
                
                if (!_inputBridge.AbilitySelect.IsHeld && UltimateRadialMenuInformations[radialMenuIndex].radialMenu.IsEnabled)
                    disableMenu = true; // Signal to disable
            }
            // For "Toggle" to enable:
            else if (enableMenuSetting == EnableMenuSetting.Toggle)
            {
                if (_inputBridge.AbilitySelect.IsPressed) // Use IsPressed for toggle
                {
                    if (!UltimateRadialMenuInformations[radialMenuIndex].radialMenu.IsEnabled)
                        enableMenu = true;
                    else
                        disableMenu = true; // Toggle off if already on
                }
            }
            // If Manual, enableMenu/disableMenu are not set here. CardiniController/AbilityManager would call URM.Enable/Disable directly.

            // 2. Handle Navigation Input (from InputBridge.LookInput)
            Vector2 currentLookInput = _inputBridge.LookInput;

            // The asset's example uses a "retain last input" logic for controllers. Let's adapt that.
            // Only update _lastRadialLookInput if there's significant new input,
            // AND the menu is enabled (so we don't pick up look input when wheel is closed).
            if (UltimateRadialMenuInformations[radialMenuIndex].radialMenu.IsEnabled)
            {
                if (currentLookInput.sqrMagnitude >= (UltimateRadialMenuInformations[radialMenuIndex].radialMenu.minRange * UltimateRadialMenuInformations[radialMenuIndex].radialMenu.minRange)) // or some deadzone
                {
                    _lastRadialLookInput = currentLookInput;
                }
            }
            else // If menu is not enabled, reset last look input so it doesn't stick
            {
                _lastRadialLookInput = Vector2.zero;
            }
            
            // If there's a valid look input (either current or retained)
            if (_lastRadialLookInput.sqrMagnitude > 0.01f)
            {
                input = _lastRadialLookInput; // This is the Vector2 for radial navigation

                // The asset calculates distance to be "in the middle" for controllers if beyond minRange.
                // This makes selection less finicky than precise analog stick distance.
                float tempDist = _lastRadialLookInput.magnitude; // Use magnitude of our actual input
                if (tempDist >= UltimateRadialMenuInformations[radialMenuIndex].radialMenu.minRange)
                {
                    distance = Mathf.Lerp(UltimateRadialMenuInformations[radialMenuIndex].radialMenu.CalculatedMinRange, UltimateRadialMenuInformations[radialMenuIndex].radialMenu.CalculatedMaxRange, 0.5f);
                }
                else
                {
                    distance = tempDist; // If within minRange, use actual distance
                }
                CurrentInputDevice = InputDevice.Controller; // Assuming LookInput is from controller stick
            }
            else
            {
                // No significant look input, input remains zero, distance remains zero.
            }


            // 3. Handle Interact Input (Selection - e.g., from InputBridge.UseEquippedAbility or a dedicated UI Confirm)
            // The base UltimateRadialMenuInputManager uses Input.GetButtonDown/Up for its interactButtonController.
            // We need to feed it our New Input System state.
            // Let's assume "UseEquippedAbility" is our primary interact button for the wheel too.
            // if (_inputBridge.UseEquippedAbility.IsPressed)
            // {
            //     inputDown = true;
            //     CurrentInputDevice = InputDevice.Controller; // Or whatever device triggered UseEquippedAbility
            // }
            // if (_inputBridge.UseEquippedAbility.WasReleasedThisFrame)
            // {
            //     inputUp = true;
            //     CurrentInputDevice = InputDevice.Controller;
            // }

            // If you want to use the asset's built-in button checking (which uses old Input Manager strings by default, or New Input System Gamepad buttons if ENABLE_INPUT_SYSTEM is on)
            // you would call base.ControllerInput(...) AFTER setting up your custom parts, or selectively call its CheckControllerButtons.
            // For full control via InputBridge, the above is more direct.
            // base.ControllerInput(ref enableMenu, ref disableMenu, ref input, ref distance, ref inputDown, ref inputUp, radialMenuIndex);
        }

        // You might also want to override MouseAndKeyboardInput if you want mouse to drive the wheel
        // and have its enable/disable/interact also come from InputBridge actions.
        public override void MouseAndKeyboardInput(ref bool enableMenu, ref bool disableMenu, ref Vector2 input, ref float distance, ref bool inputDown, ref bool inputUp, int radialMenuIndex)
        {
            if (_inputBridge == null) return;

            // 1. Enable/Disable (same logic as ControllerInput, using AbilitySelect)
             if (enableMenuSetting == EnableMenuSetting.Hold)
            {
                if (_inputBridge.AbilitySelect.IsHeld && !UltimateRadialMenuInformations[radialMenuIndex].radialMenu.IsEnabled)
                    enableMenu = true;
                if (!_inputBridge.AbilitySelect.IsHeld && UltimateRadialMenuInformations[radialMenuIndex].radialMenu.IsEnabled)
                    disableMenu = true;
            }
            else if (enableMenuSetting == EnableMenuSetting.Toggle)
            {
                if (_inputBridge.AbilitySelect.IsPressed)
                {
                    if (!UltimateRadialMenuInformations[radialMenuIndex].radialMenu.IsEnabled) enableMenu = true; else disableMenu = true;
                }
            }

            // 2. Navigation Input (from InputBridge.LookInput, which already gets mouse delta)
            // The base class's MouseAndKeyboardInput calculates 'input' and 'distance' based on mousePosition relative to canvas.
            // If your InputBridge.LookInput provides raw mouse delta, it's not directly usable for radial position.
            // For mouse, it's often better to let the base class handle screen position to radial input conversion,
            // OR if your LookInput is *already* a screen position, you can use it.
            // For now, let's call the base to handle mouse-to-radial conversion:
            Vector2 mousePosition = Vector2.zero;
            #if ENABLE_INPUT_SYSTEM
                Mouse mouse = InputSystem.GetDevice<Mouse>();
                if(mouse != null) mousePosition = mouse.position.ReadValue();
            #else
                if(Input.mousePresent) mousePosition = Input.mousePosition;
            #endif

            if( UltimateRadialMenuInformations[ radialMenuIndex ].previousMouseInput != mousePosition || CurrentInputDevice == InputDevice.Mouse || inputDown || inputUp || enableMenu || disableMenu )
            {
                CurrentInputDevice = InputDevice.Mouse;
                UltimateRadialMenuInformations[ radialMenuIndex ].previousMouseInput = mousePosition;

                if( UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.IsWorldSpaceRadialMenu )
			        RaycastWorldSpaceRadialMenu( ref input, ref distance, mousePosition, radialMenuIndex );
                else
                {
                    Vector2 inputPositionOnCanvas = ( mousePosition / UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.ParentCanvas.scaleFactor ) - ( UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.ParentCanvas.GetComponent<RectTransform>().sizeDelta / 2 );
			        input = ( inputPositionOnCanvas - ( Vector2 )UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.BaseTransform.localPosition ) / ( UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.BaseTransform.sizeDelta.x / 2 );
			        distance = Vector2.Distance( UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.BaseTransform.localPosition, inputPositionOnCanvas );
                }
            }

if( UltimateRadialMenuInformations[ radialMenuIndex ].previousMouseInput != mousePosition || CurrentInputDevice == InputDevice.Mouse || inputDown || inputUp || enableMenu || disableMenu )
            {
                CurrentInputDevice = InputDevice.Mouse; // If mouse is being considered for input
                UltimateRadialMenuInformations[ radialMenuIndex ].previousMouseInput = mousePosition;
                // ... (rest of mouse position to radial input/distance conversion) ...
                if( UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.IsWorldSpaceRadialMenu )
			        RaycastWorldSpaceRadialMenu( ref input, ref distance, mousePosition, radialMenuIndex );
                else
                {
                    Vector2 inputPositionOnCanvas = ( mousePosition / UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.ParentCanvas.scaleFactor ) - ( UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.ParentCanvas.GetComponent<RectTransform>().sizeDelta / 2 );
			        input = ( inputPositionOnCanvas - ( Vector2 )UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.BaseTransform.localPosition ) / ( UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.BaseTransform.sizeDelta.x / 2 );
			        distance = Vector2.Distance( UltimateRadialMenuInformations[ radialMenuIndex ].radialMenu.BaseTransform.localPosition, inputPositionOnCanvas );
                }
            }
            // 3. Interact Input (e.g., from InputBridge.UseEquippedAbility, assuming it can be mouse click too)
            // if (_inputBridge.UseEquippedAbility.IsPressed) // If your "UseEquippedAbility" is also mapped to Left Mouse
            // {
            //     inputDown = true;
            //     CurrentInputDevice = InputDevice.Mouse;
            // }
            // if (_inputBridge.UseEquippedAbility.WasReleasedThisFrame)
            // {
            //     inputUp = true;
            //     CurrentInputDevice = InputDevice.Mouse;
            // }
        }
        
        // If you want to completely bypass the asset's internal input processing loop in Update()
        // and drive it entirely from your own system (e.g., CardiniPlayer or AbilityManager feeding inputs),
        // you might override Update() and directly call:
        // UltimateRadialMenuInformations[i].radialMenu.ProcessInput(calculatedInput, calculatedDistance, calculatedInputDown, calculatedInputUp);
        // And then:
        // UltimateRadialMenuInformations[i].radialMenu.Enable() / .Disable()
        // This gives maximum control but means you replicate some logic from the base Update().
        // For now, overriding ControllerInput and MouseAndKeyboardInput is safer.
    }
}