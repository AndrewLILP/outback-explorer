// PlayerInputReader.cs
// Location: Assets/Scripts/Player/PlayerInputReader.cs
//
// The single place in the game that owns the SundayDriveControls input actions.
// Other scripts ask THIS class about input instead of reading keys themselves.
//
// Why one reader?
//   - Keyboard (E / Esc / Tab) and gamepad (A / Start / View) are already merged
//     into one action each by the Input Actions asset, so no other script needs
//     to know which device was used.
//   - It also tracks which device the player is CURRENTLY using, so prompts
//     can show "E" or "A" (Steam Deck Verified requirement).
//
// Ways to read input, matching how each kind of caller works:
//   1. POLLING  - InteractPressedThisFrame. For plain C# classes that already run
//                 in an Update() loop, like DrivingState and OnFootState.
//   2. EVENTS   - OnPausePressed, OnJournalTogglePressed, OnDeviceChanged
//                 (Observer pattern). For MonoBehaviours that want to be told
//                 when something happens.
//
// UPDATED (Nov 2026): ToggleJournal action (Tab / gamepad View) -> OnJournalTogglePressed.
// Changes are marked "NEW (journal)".
//
// Single Responsibility: this class reads input and reports it. It never
// enters cars, pauses the game or shows UI.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using RelaxingDrive.Controls;

namespace RelaxingDrive.Player
{
    public class PlayerInputReader : MonoBehaviour
    {
        #region Singleton

        /// <summary>The reader in the current scene (null if none has been placed).</summary>
        public static PlayerInputReader Instance { get; private set; }

        #endregion

        #region Events

        /// <summary>Fired once when Pause is pressed (Esc on keyboard, Start / Menu on gamepad).</summary>
        public event Action OnPausePressed;

        /// <summary>NEW (journal): fired once when Toggle Journal is pressed (Tab on keyboard, View on gamepad).</summary>
        public event Action OnJournalTogglePressed;

        /// <summary>Fired when the player switches between keyboard/mouse and gamepad.</summary>
        public event Action<InputDeviceType> OnDeviceChanged;

        #endregion

        #region Inspector Settings

        [Header("Device Detection")]
        [Tooltip("If ON, moving the mouse switches prompts to keyboard. Leave OFF for Steam Deck: " +
                 "its trackpads can act as a mouse, and a light touch would flip prompts back and forth.")]
        [SerializeField] private bool mouseMovementSwitchesToKeyboard = false;

        [Tooltip("How far (pixels in one frame) the mouse must move to count, if the option above is ON.")]
        [SerializeField] private float mouseMoveThreshold = 5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        #endregion

        #region Private Fields

        // The C# class Unity generated from SundayDriveControls.inputactions.
        // Never edit that generated file - change the .inputactions asset instead.
        private SundayDriveControls controls;

        private InputDeviceType currentDevice;

        #endregion

        #region Public API

        /// <summary>
        /// True only on the frame Interact was pressed (E or A).
        /// Safe to call even if no reader exists in the scene - it just returns false.
        /// </summary>
        public static bool InteractPressedThisFrame =>
            Instance != null &&
            Instance.controls != null &&
            Instance.controls.GamePlay.Interact.WasPressedThisFrame();

        /// <summary>The device the player used most recently.</summary>
        public InputDeviceType CurrentDevice => currentDevice;

        /// <summary>
        /// The key/button name for Interact on the CURRENT device (e.g. "E" or "A").
        /// </summary>
        public string InteractLabel => GetBindingLabel(controls?.GamePlay.Interact);

        /// <summary>NEW (journal): key/button name for Toggle Journal on the current device (e.g. "Tab" or "View").</summary>
        public string JournalToggleLabel => GetBindingLabel(controls?.GamePlay.ToggleJournal);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Only one reader may exist. Destroy just this COMPONENT (not the whole
            // GameObject), in case it shares an object with other managers.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerInputReader] A second PlayerInputReader was found - removing it.");
                Destroy(this);
                return;
            }

            Instance = this;
            controls = new SundayDriveControls();

            // Starting guess: if a gamepad is connected, assume the player uses it.
            currentDevice = Gamepad.current != null ? InputDeviceType.Gamepad : InputDeviceType.KeyboardMouse;
            Log($"Created SundayDriveControls. Starting device: {currentDevice}");
        }

        private void OnEnable()
        {
            // Guard: if Awake destroyed a duplicate, controls is null here.
            if (controls == null) return;

            controls.GamePlay.Pause.performed += HandlePausePerformed;
            controls.GamePlay.ToggleJournal.performed += HandleJournalTogglePerformed; // NEW (journal)

            // Actions do nothing until enabled - the most common Input System gotcha.
            controls.GamePlay.Enable();
            Log("GamePlay actions enabled");
        }

        private void OnDisable()
        {
            if (controls == null) return;

            // Mirror OnEnable exactly: unsubscribe, then disable.
            controls.GamePlay.Pause.performed -= HandlePausePerformed;
            controls.GamePlay.ToggleJournal.performed -= HandleJournalTogglePerformed; // NEW (journal)
            controls.GamePlay.Disable();
            Log("GamePlay actions disabled");
        }

        private void Update()
        {
            if (controls == null) return;
            DetectActiveDevice();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Frees the runtime copy of the actions asset.
            controls?.Dispose();
            controls = null;
        }

        #endregion

        #region Device Detection

        /// <summary>
        /// Each frame, checks whether the player deliberately used a different device.
        /// Stick drift or a resting thumb on a trackpad must NOT flip the prompts.
        /// </summary>
        private void DetectActiveDevice()
        {
            InputDeviceType detected = currentDevice;

            if (GamepadWasUsedThisFrame())
            {
                detected = InputDeviceType.Gamepad;
            }
            else if (KeyboardOrMouseWasUsedThisFrame())
            {
                detected = InputDeviceType.KeyboardMouse;
            }

            if (detected == currentDevice) return;

            currentDevice = detected;
            Log($"Device changed → {currentDevice} (Interact = {InteractLabel})");
            OnDeviceChanged?.Invoke(currentDevice);
        }

        private bool GamepadWasUsedThisFrame()
        {
            Gamepad pad = Gamepad.current;

            // Cheap early-out: nothing on the pad changed this frame.
            if (pad == null || !pad.wasUpdatedThisFrame) return false;

            // Every button, trigger, d-pad and stick direction is a ButtonControl with
            // a press threshold, so small stick drift is ignored for free.
            foreach (InputControl control in pad.allControls)
            {
                if (control is ButtonControl button && button.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }

        private bool KeyboardOrMouseWasUsedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return false;

            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
            {
                return true;
            }

            if (mouseMovementSwitchesToKeyboard &&
                mouse.delta.ReadValue().sqrMagnitude > mouseMoveThreshold * mouseMoveThreshold)
            {
                return true;
            }

            return false;
        }

        #endregion

        #region Binding Labels

        /// <summary>
        /// Returns the display name of an action's binding for the current device,
        /// using the control scheme names from the asset ("Keyboard&Mouse" / "GamePad").
        /// </summary>
        private string GetBindingLabel(InputAction action)
        {
            if (action == null || controls == null) return "?";

            string group = currentDevice == InputDeviceType.Gamepad
                ? controls.GamePadScheme.bindingGroup
                : controls.KeyboardMouseScheme.bindingGroup;

            string label = action.GetBindingDisplayString(InputBinding.MaskByGroup(group));
            return string.IsNullOrEmpty(label) ? "?" : label;
        }

        #endregion

        #region Input Callbacks

        private void HandlePausePerformed(InputAction.CallbackContext context)
        {
            Log($"Pause pressed ({context.control.device.displayName})");
            OnPausePressed?.Invoke();
        }

        // NEW (journal)
        private void HandleJournalTogglePerformed(InputAction.CallbackContext context)
        {
            Log($"Toggle Journal pressed ({context.control.device.displayName})");
            OnJournalTogglePressed?.Invoke();
        }

        #endregion

        #region Helpers

        private void Log(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[PlayerInputReader] {message}");
            }
        }

        #endregion
    }
}