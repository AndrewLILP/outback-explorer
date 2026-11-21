// PauseMenuSceneController.cs
// Location: Assets/Scripts/Core/PauseMenuSceneController.cs
//
// UPDATED (Nov 2026): two resume options - "Resume" (where the player left
// off) and "Return to start position" (RCC_Spawner). Esc / B and gamepad Start
// no longer resume from the main menu, so the player always picks one.
// Changes are marked "NEW (resume choice)".
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using RelaxingDrive.Core;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the UI inside PauseMenuScene, which is loaded non-additively
    /// (replacing the gameplay scene entirely - see PauseFlowManager).
    ///
    /// Navigation (keyboard / gamepad / mouse):
    ///   - The scene's EventSystem + InputSystemUIInputModule turns arrows/WASD,
    ///     d-pad and left stick into UI Toolkit navigation events; Enter/Space/A
    ///     into Submit (clicks the focused button); Esc/B into Cancel.
    ///   - Mouse hover FOCUSES the button, so mouse and gamepad share one
    ///     highlight (USS :focus) instead of showing two different ones.
    ///   - Cancel (Esc / B) goes back one level; on the main menu it does nothing.
    ///   - NEW (resume choice): neither Esc / B nor gamepad Start resumes - the
    ///     player picks "Resume" or "Return to start position", so they never
    ///     land somewhere they didn't choose.
    ///
    /// Sounds: navigate (focus moved), submit (button pressed), back (cancel).
    /// Scene changes wait a moment so the click sound isn't cut off.
    ///
    /// Settings panel: values are handled by SettingsPanelController + GameSettings.
    /// This class only shows/hides it, wires focus/hover/sounds for its controls,
    /// and saves settings when the panel closes.
    ///
    /// There is no cross-scene callback to PauseFlowManager: the gameplay scene
    /// is already unloaded by the time this script runs. Resume / New Game
    /// load a scene directly. Save talks to GameSaveManager, which
    /// survives the scene swap because it's DontDestroyOnLoad.
    ///
    /// NEW (resume choice): the car's position was captured into
    /// ResumeSpawnHandoff (static, survives scene loads) before this scene
    /// opened. The Resume buttons just set its mode; ResumeSpawnController in
    /// the gameplay scene does the actual move.
    ///
    /// CHANGE CAR is currently disabled (car selection scene is off). All of its
    /// code is commented out below and marked "CHANGE CAR (disabled)" - restore
    /// those blocks plus the button line in PauseMenu.uxml to bring it back.
    ///
    /// This scene is never time-scaled; timers below still use unscaled time
    /// so they keep working if that ever changes.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(AudioSource))]
    public class PauseMenuSceneController : MonoBehaviour
    {
        #region Inspector Settings

        [Header("Scene Names")]
        [Tooltip("Exact name of the main gameplay scene in Build Settings.")]
        [SerializeField] private string gameplaySceneName = "RCC_City_CarSelectionWithLoadedScene";

        // CHANGE CAR (disabled)
        // [Tooltip("Exact name of the car selection scene in Build Settings.")]
        // [SerializeField] private string carSelectionSceneName = "RCC_City_CarSelectionWithLoadScene";

        [Header("UI Sounds")]
        [Tooltip("Focus moved to another button (keyboard, gamepad or mouse hover).")]
        [SerializeField] private AudioClip navigateSound;
        [Tooltip("A button was pressed.")]
        [SerializeField] private AudioClip submitSound;
        [Tooltip("Back / cancel (Esc or gamepad B).")]
        [SerializeField] private AudioClip backSound;
        [Range(0f, 1f)]
        [SerializeField] private float uiVolume = 0.6f;
        [Tooltip("Minimum gap between slider ticks, so holding a direction doesn't machine-gun sounds.")]
        [SerializeField] private float tickMinInterval = 0.06f;

        [Header("Timing")]
        [Tooltip("Ignore Cancel input for this long after the scene opens, so the " +
                 "Esc press that opened the menu can't immediately act on it.")]
        [SerializeField] private float inputGraceSeconds = 0.25f;
        [Tooltip("Delay before loading another scene, so the click sound is heard.")]
        [SerializeField] private float sceneChangeDelay = 0.15f;
        [Tooltip("How long the Save button shows 'Saved!'.")]
        [SerializeField] private float savedFeedbackSeconds = 1.5f;

        [Header("References")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        private enum MenuPanel { Main, Instructions, Settings, ConfirmNewGame }

        // USS class added to a settings row while its control is focused
        // (USS has no :focus-within, so we toggle a class from code).
        private const string SettingsRowFocusedClass = "settings-row--focused";

        private VisualElement root;
        private VisualElement pauseContainer;
        private VisualElement instructionsPanel;
        private VisualElement settingsPanel;
        private VisualElement confirmPanel;

        private SettingsPanelController settingsController;

        private Button resumeButton;
        private Button resumeStartButton;   // NEW (resume choice)
        private Button saveButton;
        private Button newGameButton;
        // private Button changeCarsButton;   // CHANGE CAR (disabled)
        private Button instructionsButton;
        private Button settingsButton;
        private Button quitButton;
        private Button closeInstructionsButton;
        private Button closeSettingsButton;
        private Button confirmCancelButton;
        private Button confirmNewGameButton;

        private AudioSource audioSource;
        // NEW (resume choice): the gamepad Start "resume" action was removed.

        private MenuPanel currentPanel = MenuPanel.Main;
        private Button lastMainMenuButton;          // re-focused when returning from a sub-panel
        private VisualElement quietFocusTarget;     // focused by code - don't play the navigate sound
        private float lastTickTime = -1f;
        private float readyTime;            // unscaled time when input is accepted
        private bool isTransitioning;       // a scene load has started - ignore further input
        private string saveButtonText;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            // 2D UI sounds: no 3D falloff, never auto-play.
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            readyTime = Time.unscaledTime + inputGraceSeconds;

            StartCoroutine(InitializeWhenReady());
        }

        private void OnDisable()
        {
            // Leaving the scene (resume, quit...) - make sure settings reach disk.
            settingsController?.SaveSettings();
        }

        #endregion

        #region Initialization

        private IEnumerator InitializeWhenReady()
        {
            int framesWaited = 0;
            while ((uiDocument == null || uiDocument.rootVisualElement == null) && framesWaited < 60)
            {
                framesWaited++;
                yield return null;
            }

            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogError("[PauseMenuSceneController] rootVisualElement never became available - " +
                    "check that this UIDocument's Panel Settings AND Source Asset are both assigned.");
                yield break;
            }

            root = uiDocument.rootVisualElement;

            pauseContainer = root.Q<VisualElement>("PauseContainer");
            instructionsPanel = root.Q<VisualElement>("InstructionsPanel");
            settingsPanel = root.Q<VisualElement>("SettingsPanel");
            confirmPanel = root.Q<VisualElement>("ConfirmPanel");

            resumeButton = root.Q<Button>("ResumeButton");
            resumeStartButton = root.Q<Button>("ResumeStartButton");   // NEW (resume choice)
            saveButton = root.Q<Button>("SaveButton");
            newGameButton = root.Q<Button>("NewGameButton");
            // changeCarsButton = root.Q<Button>("ChangeCarsButton");   // CHANGE CAR (disabled)
            instructionsButton = root.Q<Button>("InstructionsButton");
            settingsButton = root.Q<Button>("SettingsButton");
            quitButton = root.Q<Button>("QuitButton");
            closeInstructionsButton = root.Q<Button>("CloseInstructionsButton");
            closeSettingsButton = root.Q<Button>("CloseSettingsButton");
            confirmCancelButton = root.Q<Button>("ConfirmCancelButton");
            confirmNewGameButton = root.Q<Button>("ConfirmNewGameButton");

            // Remember the Save label so "Saved!" can be swapped back.
            saveButtonText = saveButton != null ? saveButton.text : "Save game";

            WireButton(resumeButton, OnResumeClicked);
            WireButton(resumeStartButton, OnResumeStartClicked);   // NEW (resume choice)
            WireButton(saveButton, OnSaveClicked);
            WireButton(newGameButton, OnNewGameClicked);
            // WireButton(changeCarsButton, OnChangeCarsClicked);   // CHANGE CAR (disabled)
            WireButton(instructionsButton, OnInstructionsClicked);
            WireButton(settingsButton, OnSettingsClicked);
            WireButton(quitButton, OnQuitClicked);
            WireButton(closeInstructionsButton, ShowMainMenu);
            WireButton(closeSettingsButton, CloseSettings);

            // Settings: values are handled by SettingsPanelController; we add
            // focus, hover and sound behaviour to its controls here.
            if (settingsPanel != null)
            {
                settingsController = new SettingsPanelController(settingsPanel);
                settingsController.OnSettingChanged += PlayTick;
                settingsController.OnSfxVolumeChanged += PlaySfxPreview;

                foreach (VisualElement control in settingsController.Controls)
                {
                    WireSettingControl(control);
                }
            }
            WireButton(confirmCancelButton, ShowMainMenu);
            WireButton(confirmNewGameButton, OnConfirmNewGameClicked);

            // Cancel (Esc / gamepad B) bubbles up from the focused button to the root.
            root.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel);

            // Defensive guard: at non-1x Game view scale, UI Toolkit can
            // occasionally fail to clear a button's pointer capture on
            // PointerUp, leaving the panel stuck. Releasing capture on every
            // PointerUp is a cheap safety net (non-issue in a build).
            root.RegisterCallback<PointerUpEvent>(OnAnyPointerUp, TrickleDown.TrickleDown);

            // Focus after the first layout pass, so the button is ready to receive it.
            lastMainMenuButton = resumeButton;
            root.schedule.Execute(() => ShowPanel(MenuPanel.Main));

            Log($"Ready after {framesWaited} frame(s)");
        }

        /// <summary>
        /// Gives every button the same behaviour: click sound + action,
        /// mouse hover moves focus, and focus changes play the navigate sound.
        /// </summary>
        private void WireButton(Button button, Action onClick)
        {
            if (button == null)
            {
                Debug.LogWarning("[PauseMenuSceneController] A button is missing from PauseMenu.uxml - check its name.");
                return;
            }

            button.clicked += () =>
            {
                if (isTransitioning) return;
                PlaySound(submitSound);
                onClick();
            };

            // Hover = focus: mouse, keyboard and gamepad all share one highlight.
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!isTransitioning && button.enabledInHierarchy) button.Focus();
            });

            button.RegisterCallback<FocusInEvent>(OnControlFocused);
        }

        /// <summary>
        /// Sliders and toggles in the Settings panel: hover focuses, focus ticks,
        /// and the whole row highlights while its control is focused.
        /// </summary>
        private void WireSettingControl(VisualElement control)
        {
            control.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!isTransitioning && control.enabledInHierarchy) control.Focus();
            });

            control.RegisterCallback<FocusInEvent>(evt =>
            {
                control.parent?.AddToClassList(SettingsRowFocusedClass);
                OnControlFocused(evt);
            });

            control.RegisterCallback<FocusOutEvent>(_ =>
            {
                control.parent?.RemoveFromClassList(SettingsRowFocusedClass);
            });
        }

        #endregion

        #region Panels & Focus

        private void ShowPanel(MenuPanel panel)
        {
            // If a panel is missing from the UXML, stay on the main menu and say why,
            // rather than hiding everything and leaving a blank screen.
            if (panel == MenuPanel.Settings && settingsPanel == null)
            {
                Debug.LogError("[PauseMenuSceneController] No 'SettingsPanel' in PauseMenu.uxml - " +
                               "check the UIDocument is using the latest file.");
                panel = MenuPanel.Main;
            }

            currentPanel = panel;

            SetVisible(pauseContainer, panel == MenuPanel.Main);
            SetVisible(instructionsPanel, panel == MenuPanel.Instructions);
            SetVisible(settingsPanel, panel == MenuPanel.Settings);
            SetVisible(confirmPanel, panel == MenuPanel.ConfirmNewGame);

            switch (panel)
            {
                case MenuPanel.Main:
                    FocusQuietly(lastMainMenuButton != null ? lastMainMenuButton : resumeButton);
                    break;
                case MenuPanel.Instructions:
                    FocusQuietly(closeInstructionsButton);
                    break;
                case MenuPanel.Settings:
                    settingsController?.Refresh();
                    FocusQuietly(settingsController?.FirstFocusable ?? closeSettingsButton);
                    break;
                case MenuPanel.ConfirmNewGame:
                    // Safe default: a stray Submit press cancels instead of wiping progress.
                    FocusQuietly(confirmCancelButton);
                    break;
            }
        }

        private void ShowMainMenu() => ShowPanel(MenuPanel.Main);

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Focus a control from code without the navigate sound.</summary>
        private void FocusQuietly(VisualElement element)
        {
            if (element == null) return;
            quietFocusTarget = element;
            element.Focus();
        }

        private void OnControlFocused(FocusInEvent evt)
        {
            VisualElement element = evt.currentTarget as VisualElement;

            // Remember where the player was on the main menu.
            if (currentPanel == MenuPanel.Main && element is Button button)
                lastMainMenuButton = button;

            // Focus set by code (opening a panel) is silent; player-driven focus ticks.
            if (element != null && element == quietFocusTarget)
            {
                quietFocusTarget = null;
                return;
            }

            PlaySound(navigateSound);
        }

        #endregion

        #region Input Handlers

        private void OnNavigationCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            if (isTransitioning || Time.unscaledTime < readyTime) return;

            // NEW (resume choice): no back sound on the main menu, since nothing happens.
            if (currentPanel != MenuPanel.Main)
                PlaySound(backSound);

            switch (currentPanel)
            {
                case MenuPanel.Main:
                    // NEW (resume choice): no quick resume - the player picks a button.
                    break;
                case MenuPanel.Settings:
                    CloseSettings();
                    break;
                default:
                    ShowMainMenu();
                    break;
            }
        }

        private void OnAnyPointerUp(PointerUpEvent evt)
        {
            if (root.panel.GetCapturingElement(evt.pointerId) != null)
                root.panel.ReleasePointer(evt.pointerId);
        }

        #endregion

        #region Button Actions

        // NEW (resume choice): each button sets the mode, then reloads gameplay.
        private void OnResumeClicked() => Resume(ResumeSpawnMode.CurrentPosition);

        private void OnResumeStartClicked() => Resume(ResumeSpawnMode.StartPosition);

        private void Resume(ResumeSpawnMode mode)
        {
            ResumeSpawnHandoff.SetMode(mode);
            Log($"Resume ({mode}) - loading gameplay scene '{gameplaySceneName}'");
            LoadSceneAfterDelay(gameplaySceneName);
        }

        private void OnSaveClicked()
        {
            if (GameSaveManager.Instance == null)
            {
                Debug.LogError("[PauseMenuSceneController] GameSaveManager not found!");
                return;
            }

            GameSaveManager.Instance.Save();
            Log("Game saved");

            // Small, reassuring feedback - then restore the label.
            saveButton.text = "Saved!";
            saveButton.schedule.Execute(() => saveButton.text = saveButtonText)
                .ExecuteLater((long)(savedFeedbackSeconds * 1000f));
        }

        private void OnNewGameClicked() => ShowPanel(MenuPanel.ConfirmNewGame);

        private void OnConfirmNewGameClicked()
        {
            if (GameSaveManager.Instance == null)
            {
                Debug.LogError("[PauseMenuSceneController] GameSaveManager not found!");
                return;
            }

            // NEW (resume choice): forget the paused position, so the new game
            // starts at RCC_Spawner instead of where the old game was.
            ResumeSpawnHandoff.Clear();

            isTransitioning = true;
            StartCoroutine(RunAfterDelay(() =>
            {
                // Pass the gameplay scene explicitly - the active scene here is PauseMenuScene.
                GameSaveManager.Instance.StartNewGame(gameplaySceneName);
                Log("New game started - gameplay scene reloading");
            }));
        }

        // CHANGE CAR (disabled)
        // private void OnChangeCarsClicked()
        // {
        //     Log($"Change car - loading car selection scene '{carSelectionSceneName}'");
        //     LoadSceneAfterDelay(carSelectionSceneName);
        // }

        private void OnInstructionsClicked() => ShowPanel(MenuPanel.Instructions);

        private void OnSettingsClicked() => ShowPanel(MenuPanel.Settings);

        private void CloseSettings()
        {
            settingsController?.SaveSettings();
            ShowMainMenu();
        }

        private void OnQuitClicked()
        {
            Log("Quit to desktop");
            isTransitioning = true;

            // GameSaveManager saves in OnApplicationQuit, so progress is kept.
            StartCoroutine(RunAfterDelay(() =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }));
        }

        #endregion

        #region Helpers

        private void LoadSceneAfterDelay(string sceneName)
        {
            if (isTransitioning) return;
            isTransitioning = true;
            StartCoroutine(RunAfterDelay(() => SceneManager.LoadScene(sceneName, LoadSceneMode.Single)));
        }

        /// <summary>Waits (real time) so the click sound plays before the scene changes.</summary>
        private IEnumerator RunAfterDelay(Action action)
        {
            yield return new WaitForSecondsRealtime(sceneChangeDelay);
            action();
        }

        /// <summary>Slider/toggle changed: soft tick, rate-limited.</summary>
        private void PlayTick()
        {
            if (Time.unscaledTime - lastTickTime < tickMinInterval) return;
            lastTickTime = Time.unscaledTime;
            PlaySound(navigateSound);
        }

        /// <summary>
        /// SFX slider changed: play the submit sound so the player hears the new
        /// level. (This scene's AudioSource is routed to the SFX mixer group.)
        /// </summary>
        private void PlaySfxPreview()
        {
            if (Time.unscaledTime - lastTickTime < tickMinInterval) return;
            lastTickTime = Time.unscaledTime;
            PlaySound(submitSound);
        }

        private void PlaySound(AudioClip clip)
        {
            // PlayOneShot lets quick presses overlap instead of cutting each other off.
            if (clip != null && audioSource != null)
                audioSource.PlayOneShot(clip, uiVolume);
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[PauseMenuSceneController] {message}");
        }

        #endregion
    }
}