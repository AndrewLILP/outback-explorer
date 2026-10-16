// NewGameHotkey.cs
// Location: Assets/Scripts/Core/NewGameHotkey.cs
//
// Starts a new game from a hotkey, using the Input System (not legacy Input).
// Default bindings: HOLD "1" on keyboard, or HOLD View/Select on gamepad.
//
// Why HOLD instead of a single press?
//   StartNewGame() deletes the save file. In a cosy game, losing progress to a
//   stray key tap is the opposite of relaxing. A Hold interaction means the
//   action only "performs" after the button is held for holdDuration seconds.
//
// Single Responsibility: this class only listens for input and asks
// GameSaveManager to start a new game. It knows nothing about HOW saving works.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

namespace RelaxingDrive.Core
{
    public class NewGameHotkey : MonoBehaviour
    {
        #region Inspector Settings

        [Header("Input")]
        [Tooltip("The action that triggers a new game. Bindings and the Hold interaction can be edited here in the Inspector.")]
        [SerializeField] private InputAction newGameAction = new InputAction(
            name: "NewGame",
            type: InputActionType.Button,
            interactions: "hold(duration=1.5)");

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        // Guard so the action can't fire twice while the scene is reloading
        private bool isStarting = false;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            // Reset() runs when the component is first added (or right-click > Reset).
            // It gives us sensible default bindings without hard-coding them in Awake.
            newGameAction = new InputAction(
                name: "NewGame",
                type: InputActionType.Button,
                interactions: "hold(duration=1.5)");

            newGameAction.AddBinding("<Keyboard>/1");          // top-row 1
            newGameAction.AddBinding("<Keyboard>/numpad1");    // numpad 1
            newGameAction.AddBinding("<Gamepad>/select");      // View button on Xbox / Steam Deck
        }

        private void OnEnable()
        {
            // Input System callbacks:
            //   started   -> button went down (hold timer begins)
            //   performed -> held long enough (do the thing)
            //   canceled  -> released too early
            newGameAction.started   += OnHoldStarted;
            newGameAction.performed += OnNewGamePerformed;
            newGameAction.canceled  += OnHoldCanceled;

            // Actions do nothing until enabled — a very common "why isn't it working?" gotcha
            newGameAction.Enable();
        }

        private void OnDisable()
        {
            // Always mirror OnEnable: unsubscribe and disable to avoid leaks
            // and callbacks firing on a destroyed object after scene reload.
            newGameAction.started   -= OnHoldStarted;
            newGameAction.performed -= OnNewGamePerformed;
            newGameAction.canceled  -= OnHoldCanceled;

            newGameAction.Disable();
        }

        #endregion

        #region Input Callbacks

        private void OnHoldStarted(InputAction.CallbackContext context)
        {
            // Hook point: later you could show a UI Toolkit "Hold to start new game" ring here.
            Log("Hold started — keep holding to start a new game...");
        }

        private void OnHoldCanceled(InputAction.CallbackContext context)
        {
            Log("Released early — new game cancelled.");
        }

        private void OnNewGamePerformed(InputAction.CallbackContext context)
        {
            if (isStarting) return;

            GameSaveManager saveManager = GameSaveManager.Instance;
            if (saveManager == null)
            {
                Debug.LogError("[NewGameHotkey] GameSaveManager not found!");
                return;
            }

            isStarting = true;
            Log("Hold complete — starting new game.");

            // If the player triggers this while paused, timeScale would be 0
            // and the reloaded scene would start frozen. Reset it first.
            Time.timeScale = 1f;

            saveManager.StartNewGame();
        }

        #endregion

        #region Helpers

        private void Log(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[NewGameHotkey] {message}");
            }
        }

        #endregion
    }
}