// PauseMenuController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Core;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the pause menu UI - accessed via ESC key during gameplay.
    /// Provides options to Resume, Save, Start New Game, and view Instructions.
    /// 
    /// DESIGN PATTERN: UI Controller Pattern
    /// - Separates UI logic from game logic
    /// - Uses UI Toolkit for modern Unity UI
    /// 
    /// USAGE:
    /// - Attach to a GameObject in the scene
    /// - Assign the UI Document component in Inspector
    /// - Press ESC to toggle pause menu
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("UI Document")]
        [Tooltip("UI Document component containing the pause menu")]
        [SerializeField] private UIDocument pauseMenuDocument;

        [Header("Settings")]
        [Tooltip("Pause the game when menu is open (Time.timeScale = 0)")]
        [SerializeField] private bool pauseGameTime = true;

        [Tooltip("Key to toggle pause menu")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        [Header("Debug")]
        [Tooltip("Show detailed debug logs")]
        [SerializeField] private bool showDebugLogs = true;

        // UI Elements
        private VisualElement rootElement;
        private VisualElement pausePanel;
        private VisualElement instructionsPanel;
        private Button resumeButton;
        private Button saveButton;
        private Button newGameButton;
        private Button instructionsButton;
        private Button closeInstructionsButton;

        // State
        private bool isPaused = false;
        private bool instructionsVisible = false;
        private bool isInitialized = false;

        private void Awake()
        {
            Log("Awake() called");

            // Get UI Document if not assigned
            if (pauseMenuDocument == null)
            {
                pauseMenuDocument = GetComponent<UIDocument>();
                Log("UI Document was null, attempted GetComponent");
            }

            if (pauseMenuDocument == null)
            {
                LogError("No UI Document found! Please assign one in Inspector.");
                enabled = false;
                return;
            }

            Log("UI Document found: " + pauseMenuDocument.name);
        }

        private void OnEnable()
        {
            Log("OnEnable() called - starting initialization coroutine");
            // Start coroutine to initialize UI after it's ready
            StartCoroutine(InitializeUIWhenReady());
        }

        /// <summary>
        /// Waits for UI to be ready, then initializes
        /// </summary>
        private IEnumerator InitializeUIWhenReady()
        {
            Log("Waiting for UI to be ready...");

            // Wait until end of frame to ensure UI Document is ready
            yield return new WaitForEndOfFrame();

            // Try to get root element
            int attempts = 0;
            while (attempts < 10) // Try up to 10 frames
            {
                if (pauseMenuDocument != null && pauseMenuDocument.rootVisualElement != null)
                {
                    Log($"UI ready after {attempts} attempts!");
                    InitializeUI();
                    yield break;
                }

                attempts++;
                Log($"UI not ready yet, attempt {attempts}/10");
                yield return null; // Wait one frame
            }

            LogError("Failed to initialize UI after 10 attempts! rootVisualElement is still null.");
        }

        private void Update()
        {
            // Toggle pause menu with ESC key
            if (Input.GetKeyDown(pauseKey))
            {
                Log("Pause key pressed!");
                TogglePauseMenu();
            }
        }

        /// <summary>
        /// Initializes UI elements and button callbacks
        /// </summary>
        private void InitializeUI()
        {
            Log("InitializeUI() started");

            if (pauseMenuDocument == null)
            {
                LogError("pauseMenuDocument is null in InitializeUI!");
                return;
            }

            rootElement = pauseMenuDocument.rootVisualElement;
            
            if (rootElement == null)
            {
                LogError("rootVisualElement is STILL null in InitializeUI!");
                return;
            }

            Log("Root element found, children count: " + rootElement.childCount);

            // Debug: Print all root-level elements
            Log("=== Root Element Children ===");
            foreach (var child in rootElement.Children())
            {
                Log($"Child: '{child.name}' (Type: {child.GetType().Name})");
            }

            // Find UI elements
            pausePanel = rootElement.Q<VisualElement>("pause-panel");
            instructionsPanel = rootElement.Q<VisualElement>("instructions-panel");
            resumeButton = rootElement.Q<Button>("resume-button");
            saveButton = rootElement.Q<Button>("save-button");
            newGameButton = rootElement.Q<Button>("new-game-button");
            instructionsButton = rootElement.Q<Button>("instructions-button");
            closeInstructionsButton = rootElement.Q<Button>("close-instructions-button");

            // Validate elements with detailed logging
            Log($"pausePanel: {(pausePanel != null ? "✅ Found" : "❌ NULL")}");
            Log($"instructionsPanel: {(instructionsPanel != null ? "✅ Found" : "❌ NULL")}");
            Log($"resumeButton: {(resumeButton != null ? "✅ Found" : "❌ NULL")}");
            Log($"saveButton: {(saveButton != null ? "✅ Found" : "❌ NULL")}");
            Log($"newGameButton: {(newGameButton != null ? "✅ Found" : "❌ NULL")}");
            Log($"instructionsButton: {(instructionsButton != null ? "✅ Found" : "❌ NULL")}");
            Log($"closeInstructionsButton: {(closeInstructionsButton != null ? "✅ Found" : "❌ NULL")}");

            if (pausePanel == null || instructionsPanel == null)
            {
                LogError("Missing required panels! Check UXML structure.");
                LogError("Make sure your UXML has elements with names: 'pause-panel' and 'instructions-panel'");
                
                // Print ALL elements recursively to help debug
                Log("=== ALL UI Elements (Recursive) ===");
                PrintAllElements(rootElement, 0);
                return;
            }

            // Register button callbacks
            int registeredButtons = 0;

            if (resumeButton != null)
            {
                resumeButton.clicked += OnResumeClicked;
                registeredButtons++;
                Log("Resume button callback registered");
            }

            if (saveButton != null)
            {
                saveButton.clicked += OnSaveClicked;
                registeredButtons++;
                Log("Save button callback registered");
            }

            if (newGameButton != null)
            {
                newGameButton.clicked += OnNewGameClicked;
                registeredButtons++;
                Log("New Game button callback registered");
            }

            if (instructionsButton != null)
            {
                instructionsButton.clicked += OnInstructionsClicked;
                registeredButtons++;
                Log("Instructions button callback registered");
            }

            if (closeInstructionsButton != null)
            {
                closeInstructionsButton.clicked += OnCloseInstructionsClicked;
                registeredButtons++;
                Log("Close Instructions button callback registered");
            }

            // Initially hide the menu
            HidePauseMenuImmediate();

            isInitialized = true;
            Log($"✅ UI initialized successfully! {registeredButtons}/5 buttons registered");
        }

        /// <summary>
        /// Recursively prints all UI elements (for debugging)
        /// </summary>
        private void PrintAllElements(VisualElement element, int depth)
        {
            string indent = new string(' ', depth * 2);
            Log($"{indent}- {element.name} ({element.GetType().Name})");
            
            foreach (var child in element.Children())
            {
                PrintAllElements(child, depth + 1);
            }
        }

        /// <summary>
        /// Toggles the pause menu on/off
        /// </summary>
        public void TogglePauseMenu()
        {
            Log($"TogglePauseMenu() - Current state: isPaused={isPaused}");

            if (!isInitialized)
            {
                LogError("Cannot toggle pause menu - not initialized!");
                return;
            }

            if (isPaused)
            {
                HidePauseMenu();
            }
            else
            {
                ShowPauseMenu();
            }
        }

        /// <summary>
        /// Shows the pause menu
        /// </summary>
        public void ShowPauseMenu()
        {
            if (!isInitialized)
            {
                LogError("Cannot show pause menu - not initialized!");
                return;
            }

            if (pausePanel == null)
            {
                LogError("pausePanel is null! Cannot show menu.");
                return;
            }

            isPaused = true;
            pausePanel.style.display = DisplayStyle.Flex;

            if (pauseGameTime)
            {
                Time.timeScale = 0f;
            }

            Log("✅ Pause menu shown (Time.timeScale = " + Time.timeScale + ")");
        }

        /// <summary>
        /// Hides the pause menu
        /// </summary>
        public void HidePauseMenu()
        {
            if (!isInitialized)
            {
                LogError("Cannot hide pause menu - not initialized!");
                return;
            }

            HidePauseMenuImmediate();
        }

        /// <summary>
        /// Immediately hides menu without checks (for initialization)
        /// </summary>
        private void HidePauseMenuImmediate()
        {
            if (pausePanel == null || instructionsPanel == null)
                return;

            isPaused = false;
            pausePanel.style.display = DisplayStyle.None;
            instructionsPanel.style.display = DisplayStyle.None;
            instructionsVisible = false;

            if (pauseGameTime)
            {
                Time.timeScale = 1f;
            }

            Log("✅ Pause menu hidden (Time.timeScale = " + Time.timeScale + ")");
        }

        #region Button Callbacks

        private void OnResumeClicked()
        {
            Log("🔘 Resume button clicked!");
            HidePauseMenu();
        }

        private void OnSaveClicked()
        {
            Log("🔘 Save button clicked!");

            if (GameSaveManager.Instance != null)
            {
                GameSaveManager.Instance.Save();
                Log("✅ Game saved successfully!");
            }
            else
            {
                LogError("GameSaveManager not found!");
            }
        }

        private void OnNewGameClicked()
        {
            Log("🔘 New Game button clicked!");

            if (GameSaveManager.Instance != null)
            {
                HidePauseMenu();
                GameSaveManager.Instance.StartNewGame();
            }
            else
            {
                LogError("GameSaveManager not found!");
            }
        }

        private void OnInstructionsClicked()
        {
            Log("🔘 Instructions button clicked!");

            if (!instructionsVisible)
            {
                instructionsPanel.style.display = DisplayStyle.Flex;
                instructionsVisible = true;
                Log("Instructions panel shown");
            }
        }

        private void OnCloseInstructionsClicked()
        {
            Log("🔘 Close Instructions button clicked!");

            instructionsPanel.style.display = DisplayStyle.None;
            instructionsVisible = false;
            Log("Instructions panel hidden");
        }

        #endregion

        #region Public Methods

        public void OpenPauseMenu()
        {
            ShowPauseMenu();
        }

        public void ClosePauseMenu()
        {
            HidePauseMenu();
        }

        public bool IsPaused()
        {
            return isPaused;
        }

        #endregion

        #region Logging Helpers

        private void Log(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[PauseMenuController] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[PauseMenuController] ❌ {message}");
        }

        #endregion

        private void OnDisable()
        {
            Log("OnDisable() called - cleaning up");

            // Unregister button callbacks to prevent memory leaks
            if (resumeButton != null)
                resumeButton.clicked -= OnResumeClicked;

            if (saveButton != null)
                saveButton.clicked -= OnSaveClicked;

            if (newGameButton != null)
                newGameButton.clicked -= OnNewGameClicked;

            if (instructionsButton != null)
                instructionsButton.clicked -= OnInstructionsClicked;

            if (closeInstructionsButton != null)
                closeInstructionsButton.clicked -= OnCloseInstructionsClicked;

            // Reset time scale if paused
            if (isPaused && pauseGameTime)
            {
                Time.timeScale = 1f;
                Log("Restored Time.timeScale to 1.0");
            }
        }
    }
}