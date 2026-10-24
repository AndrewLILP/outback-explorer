// InteractionPromptUI.cs
// Location: Assets/Scripts/UI/InteractionPromptUI.cs
//
// Input-aware interaction prompt (UI Toolkit), e.g.  [E] Enter Car  /  (A) Enter Car
//
// How it works:
//   - Callers pass only the ACTION text:  ShowPrompt("Enter Car")
//   - This class adds the key/button "keycap" itself, asking PlayerInputReader
//     for the current device and the label bound to Interact.
//   - It listens to PlayerInputReader.OnDeviceChanged, so the keycap swaps live
//     (E <-> A) even while the prompt is on screen.
//
// Why callers don't write "Press E":
//   Only the prompt should know how keys are DISPLAYED. If every caller wrote
//   "Press E to ...", every caller would need changing for gamepad support.
//   (Single Responsibility + Open/Closed.)
//
// The elements are built in code and styled by InteractionPromptUI.uss, so this
// component works on any UIDocument (it doesn't need its own UXML).

using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Player;

namespace RelaxingDrive.UI
{
    public class InteractionPromptUI : MonoBehaviour
    {
        #region Inspector Settings

        [Header("UI Document")]
        [Tooltip("Leave empty to use the UIDocument on this GameObject.")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Style")]
        [Tooltip("Assign InteractionPromptUI.uss")]
        [SerializeField] private StyleSheet promptStyleSheet;

        [Tooltip("Shown on the keycap if no PlayerInputReader exists in the scene.")]
        [SerializeField] private string fallbackKeyLabel = "E";

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        #endregion

        #region USS Class Names
        // Kept as constants so the C# and the .uss file can't drift apart silently.

        private const string ContainerClass = "sd-prompt";
        private const string BoxClass = "sd-prompt__box";
        private const string KeyClass = "sd-prompt__key";
        private const string KeyKeyboardClass = "sd-prompt__key--keyboard";
        private const string KeyGamepadClass = "sd-prompt__key--gamepad";
        private const string TextClass = "sd-prompt__text";

        #endregion

        #region Private Fields

        private VisualElement promptContainer;
        private Label keyLabel;
        private Label actionLabel;

        private PlayerInputReader inputReader;
        private bool isVisible;

        #endregion

        #region Public API

        /// <summary>Whether the prompt is currently on screen.</summary>
        public bool IsVisible => isVisible;

        /// <summary>
        /// Shows the prompt. Pass only the action, e.g. "Enter Car" -
        /// the keycap (E or A) is added automatically.
        /// </summary>
        public void ShowPrompt(string actionText)
        {
            if (promptContainer == null)
            {
                Debug.LogWarning("[InteractionPromptUI] Cannot show prompt - UI not built yet.");
                return;
            }

            actionLabel.text = actionText;
            RefreshKeycap();

            promptContainer.style.display = DisplayStyle.Flex;
            isVisible = true;
        }

        /// <summary>Hides the prompt.</summary>
        public void HidePrompt()
        {
            if (promptContainer != null)
            {
                promptContainer.style.display = DisplayStyle.None;
            }

            isVisible = false;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                Debug.LogError("[InteractionPromptUI] No UIDocument assigned or found on this GameObject.");
            }
        }

        private void Start()
        {
            // Start (not Awake/OnEnable): the UIDocument has built its tree, and
            // PlayerInputReader.Awake has run, so Instance is set if it exists.
            BuildUI();
            HidePrompt();

            inputReader = PlayerInputReader.Instance;
            if (inputReader != null)
            {
                inputReader.OnDeviceChanged += HandleDeviceChanged;
            }
            else
            {
                Debug.LogWarning("[InteractionPromptUI] No PlayerInputReader in scene - keycap will always show the fallback label.");
            }
        }

        private void OnDestroy()
        {
            if (inputReader != null)
            {
                inputReader.OnDeviceChanged -= HandleDeviceChanged;
            }
        }

        #endregion

        #region Build

        /// <summary>
        /// Creates:  container (full width, near bottom)
        ///             └ box (dark rounded pill)
        ///                 ├ keycap label  [E] or (A)
        ///                 └ action label  "Enter Car"
        /// </summary>
        private void BuildUI()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            VisualElement root = uiDocument.rootVisualElement;

            if (promptStyleSheet != null)
            {
                root.styleSheets.Add(promptStyleSheet);
            }
            else
            {
                Debug.LogWarning("[InteractionPromptUI] No style sheet assigned - prompt will be unstyled.");
            }

            promptContainer = new VisualElement { name = "InteractionPrompt" };
            promptContainer.AddToClassList(ContainerClass);

            // The prompt must never block mouse clicks on other UI underneath it.
            promptContainer.pickingMode = PickingMode.Ignore;

            VisualElement box = new VisualElement();
            box.AddToClassList(BoxClass);
            box.pickingMode = PickingMode.Ignore;

            keyLabel = new Label(fallbackKeyLabel) { name = "PromptKey" };
            keyLabel.AddToClassList(KeyClass);
            keyLabel.pickingMode = PickingMode.Ignore;

            actionLabel = new Label { name = "PromptText" };
            actionLabel.AddToClassList(TextClass);
            actionLabel.pickingMode = PickingMode.Ignore;

            box.Add(keyLabel);
            box.Add(actionLabel);
            promptContainer.Add(box);
            root.Add(promptContainer);

            Log("Prompt UI built");
        }

        #endregion

        #region Keycap

        private void HandleDeviceChanged(InputDeviceType device)
        {
            // Only matters if the prompt is showing; otherwise ShowPrompt refreshes it.
            if (isVisible)
            {
                RefreshKeycap();
            }
        }

        /// <summary>Sets the keycap text and shape (square = keyboard, round = gamepad).</summary>
        private void RefreshKeycap()
        {
            if (keyLabel == null) return;

            bool gamepad = inputReader != null && inputReader.CurrentDevice == InputDeviceType.Gamepad;

            keyLabel.text = inputReader != null ? inputReader.InteractLabel : fallbackKeyLabel;

            // EnableInClassList adds the class when true, removes it when false.
            keyLabel.EnableInClassList(KeyGamepadClass, gamepad);
            keyLabel.EnableInClassList(KeyKeyboardClass, !gamepad);

            Log($"Keycap → {keyLabel.text} ({(gamepad ? "gamepad" : "keyboard")})");
        }

        #endregion

        #region Helpers

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[InteractionPromptUI] {message}");
        }

        #endregion
    }
}