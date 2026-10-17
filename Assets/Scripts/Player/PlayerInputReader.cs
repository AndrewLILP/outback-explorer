// PlayerInputReader.cs
// Location: Assets/Scripts/Player/PlayerInputReader.cs
//
// The single place in the game that owns the SundayDriveControls input actions.
// Other scripts ask THIS class about input instead of reading keys themselves.
//
// Why one reader?
//   - Keyboard (E / Esc) and gamepad (A / Start) are already merged into one
//     action each by the Input Actions asset, so no other script needs to know
//     which device was used.
//   - Later, input-aware prompts ("E" vs "A") will ask this class which control
//     scheme is active - one source of truth.
//
// Two ways to read input, matching how each kind of caller works:
//   1. POLLING  - InteractPressedThisFrame. For plain C# classes that already run
//                 in an Update() loop, like DrivingState and OnFootState.
//   2. EVENTS   - OnPausePressed (Observer pattern). For MonoBehaviours that just
//                 want to be told "pause was pressed", like PauseFlowManager.
//
// Single Responsibility: this class reads input and reports it. It never
// enters cars, pauses the game or shows UI.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
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

        #endregion

        #region Inspector Settings

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        #endregion

        #region Private Fields

        // The C# class Unity generated from SundayDriveControls.inputactions.
        // Never edit that generated file - change the .inputactions asset instead.
        private SundayDriveControls controls;

        #endregion

        #region Public API

        /// <summary>
        /// True only on the frame Interact was pressed (E or A).
        /// Safe to call even if no reader exists in the scene - it just returns false.
        /// Same "one frame only" behaviour as the old Input.GetKeyDown.
        /// </summary>
        public static bool InteractPressedThisFrame =>
            Instance != null &&
            Instance.controls != null &&
            Instance.controls.GamePlay.Interact.WasPressedThisFrame();

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
            Log("Created SundayDriveControls");
        }

        private void OnEnable()
        {
            // Guard: if Awake destroyed a duplicate, controls is null here.
            if (controls == null) return;

            controls.GamePlay.Pause.performed += HandlePausePerformed;

            // Actions do nothing until enabled - the most common Input System gotcha.
            controls.GamePlay.Enable();
            Log("GamePlay actions enabled");
        }

        private void OnDisable()
        {
            if (controls == null) return;

            // Mirror OnEnable exactly: unsubscribe, then disable.
            controls.GamePlay.Pause.performed -= HandlePausePerformed;
            controls.GamePlay.Disable();
            Log("GamePlay actions disabled");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Frees the runtime copy of the actions asset. The generated class warns
            // about a leak if the map is still enabled, which is why OnDisable
            // (which always runs before OnDestroy) disables it first.
            controls?.Dispose();
            controls = null;
        }

        #endregion

        #region Input Callbacks

        private void HandlePausePerformed(InputAction.CallbackContext context)
        {
            Log($"Pause pressed ({context.control.device.displayName})");
            OnPausePressed?.Invoke();
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