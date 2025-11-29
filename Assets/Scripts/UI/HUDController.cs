// HUDController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using PolyStang; // Reference to your car controller namespace
using RelaxingDrive.Core;
using RelaxingDrive.Animals; // Required for AnimalData

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the HUD elements using UI Toolkit.
    /// Displays speed, animal discovery progress, and pause menu with Save/Load.
    /// Sprint 4: Added Save/Load button functionality.
    /// BUG FIX: Added Start() method to update discovery display after save data loads.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CarController carController;
        [SerializeField] private UIDocument uiDocument;

        [Header("Settings")]
        [SerializeField] private float speedMultiplier = 4f;
        [SerializeField] private bool showDebugLogs = true;

        // Speed UI Elements
        private Label speedLabel;
        private VisualElement speedometer;
        private Label speedComparisonLabel;

        // Animal Discovery UI Elements
        private Label progressText;
        private VisualElement kangarooIcon;
        private VisualElement emuIcon;
        private VisualElement echidnaIcon;
        private VisualElement devilIcon;
        private Label kangarooLabel;
        private Label emuLabel;
        private Label echidnaLabel;
        private Label devilLabel;

        // Pause Menu Elements
        private VisualElement pauseMenu;
        private Button resumeButton;
        private Button saveButton;
        private Button newGameButton;
        private Button instructionsButton;
        private VisualElement instructionsPanel;
        private Button closeInstructionsButton;

        private bool isPaused = false;
        private bool instructionsVisible = false;

        // Animal speed constants (km/h for comparison)
        private const float KANGAROO_SPEED = 60f;
        private const float EMU_SPEED = 50f;
        private const float ECHIDNA_SPEED = 2f;
        private const float TASMANIAN_DEVIL_SPEED = 13f;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            SetupUIElements();
            SubscribeToEvents();
        }

        // 🔧 FIX: Added Start() to update UI after save data loads
        private void Start()
        {
            // Delay one frame to ensure AnimalDiscoveryManager has loaded save data
            StartCoroutine(UpdateDiscoveryDisplayDelayed());
        }

        /// <summary>
        /// Waits one frame then updates discovery display (ensures save data is loaded)
        /// </summary>
        private IEnumerator UpdateDiscoveryDisplayDelayed()
        {
            yield return null; // Wait one frame
            
            Log("🔄 Updating discovery display after potential save load...");
            UpdateDiscoveryDisplay();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        /// <summary>
        /// Query and cache all UI elements from UXML
        /// </summary>
        private void SetupUIElements()
        {
            var root = uiDocument.rootVisualElement;

            // Speed elements
            speedLabel = root.Q<Label>("SpeedLabel");
            speedometer = root.Q<VisualElement>("Speedometer");
            speedComparisonLabel = root.Q<Label>("SpeedComparison");

            // Animal discovery progress elements
            progressText = root.Q<Label>("ProgressText");

            // Query animal icons
            kangarooIcon = root.Q<VisualElement>("KangarooIcon");
            emuIcon = root.Q<VisualElement>("EmuIcon");
            echidnaIcon = root.Q<VisualElement>("EchidnaIcon");
            devilIcon = root.Q<VisualElement>("DevilIcon");

            // Query animal labels
            kangarooLabel = root.Q<Label>("KangarooLabel");
            emuLabel = root.Q<Label>("EmuLabel");
            echidnaLabel = root.Q<Label>("EchidnaLabel");
            devilLabel = root.Q<Label>("DevilLabel");

            // Pause menu elements
            pauseMenu = root.Q<VisualElement>("PauseMenu");
            resumeButton = root.Q<Button>("ResumeButton");
            saveButton = root.Q<Button>("SaveButton");
            newGameButton = root.Q<Button>("NewGameButton");
            instructionsButton = root.Q<Button>("InstructionsButton");
            instructionsPanel = root.Q<VisualElement>("InstructionsPanel");
            closeInstructionsButton = root.Q<Button>("CloseInstructionsButton");

            // Initialize animal icon colors (add CSS classes)
            InitializeAnimalIcons();

            // Initialize discovery display
            UpdateDiscoveryDisplay();

            // Register button callbacks
            RegisterButtonCallbacks();

            // Hide pause menu initially
            if (pauseMenu != null)
            {
                pauseMenu.style.display = DisplayStyle.None;
            }

            // Hide instructions panel initially
            if (instructionsPanel != null)
            {
                instructionsPanel.style.display = DisplayStyle.None;
                instructionsVisible = false;
            }

            Log("✅ HUD UI elements setup complete");
        }

        /// <summary>
        /// Register all button click callbacks
        /// </summary>
        private void RegisterButtonCallbacks()
        {
            // Resume button
            if (resumeButton != null)
            {
                resumeButton.clicked += OnResumeClicked;
                Log("✅ Resume button callback registered");
            }
            else
            {
                LogError("❌ Resume button not found in UXML!");
            }

            // Save button
            if (saveButton != null)
            {
                saveButton.clicked += OnSaveClicked;
                Log("✅ Save button callback registered");
            }
            else
            {
                LogError("❌ Save button not found in UXML!");
            }

            // New Game button
            if (newGameButton != null)
            {
                newGameButton.clicked += OnNewGameClicked;
                Log("✅ New Game button callback registered");
            }
            else
            {
                LogError("❌ New Game button not found in UXML!");
            }

            // Instructions button
            if (instructionsButton != null)
            {
                instructionsButton.clicked += OnInstructionsClicked;
                Log("✅ Instructions button callback registered");
            }
            else
            {
                LogError("❌ Instructions button not found in UXML!");
            }

            // Close instructions button
            if (closeInstructionsButton != null)
            {
                closeInstructionsButton.clicked += OnCloseInstructionsClicked;
                Log("✅ Close Instructions button callback registered");
            }
            else
            {
                LogError("❌ Close Instructions button not found in UXML!");
            }
        }

        /// <summary>
        /// Adds color CSS classes to animal icons
        /// </summary>
        private void InitializeAnimalIcons()
        {
            if (kangarooIcon != null) kangarooIcon.AddToClassList("kangaroo");
            if (emuIcon != null) emuIcon.AddToClassList("emu");
            if (echidnaIcon != null) echidnaIcon.AddToClassList("echidna");
            if (devilIcon != null) devilIcon.AddToClassList("devil");
        }

        /// <summary>
        /// Subscribe to AnimalDiscoveryManager events
        /// </summary>
        private void SubscribeToEvents()
        {
            if (AnimalDiscoveryManager.Instance != null)
            {
                AnimalDiscoveryManager.Instance.OnAnimalDiscovered += HandleAnimalDiscovered;
            }
        }

        /// <summary>
        /// Unsubscribe from events to prevent memory leaks
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (AnimalDiscoveryManager.Instance != null)
            {
                AnimalDiscoveryManager.Instance.OnAnimalDiscovered -= HandleAnimalDiscovered;
            }
        }

        private void Update()
        {
            UpdateSpeed();
            HandlePauseInput();
        }

        /// <summary>
        /// Handles ESC key to toggle pause menu
        /// </summary>
        private void HandlePauseInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (isPaused)
                {
                    HidePauseMenu();
                }
                else
                {
                    ShowPauseMenu();
                }
            }
        }

        /// <summary>
        /// Shows the pause menu
        /// </summary>
        private void ShowPauseMenu()
        {
            if (pauseMenu != null)
            {
                pauseMenu.style.display = DisplayStyle.Flex;
                isPaused = true;
                Time.timeScale = 0f; // Freeze game
                Log("⏸️ Game paused");
            }
        }

        /// <summary>
        /// Hides the pause menu
        /// </summary>
        private void HidePauseMenu()
        {
            if (pauseMenu != null)
            {
                pauseMenu.style.display = DisplayStyle.None;
                isPaused = false;
                Time.timeScale = 1f; // Resume game

                // Also hide instructions if they were showing
                if (instructionsPanel != null)
                {
                    instructionsPanel.style.display = DisplayStyle.None;
                    instructionsVisible = false;
                }

                Log("▶️ Game resumed");
            }
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
                LogError("❌ GameSaveManager not found!");
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
                LogError("❌ GameSaveManager not found!");
            }
        }

        private void OnInstructionsClicked()
        {
            Log("🔘 Instructions button clicked!");

            if (instructionsPanel != null)
            {
                if (!instructionsVisible)
                {
                    instructionsPanel.style.display = DisplayStyle.Flex;
                    instructionsVisible = true;
                    Log("📖 Instructions panel shown");
                }
            }
            else
            {
                LogError("❌ Instructions panel not found!");
            }
        }

        private void OnCloseInstructionsClicked()
        {
            Log("🔘 Close Instructions button clicked!");

            if (instructionsPanel != null)
            {
                instructionsPanel.style.display = DisplayStyle.None;
                instructionsVisible = false;
                Log("📖 Instructions panel hidden");
            }
        }

        #endregion

        /// <summary>
        /// Updates the speed display based on car's rigidbody velocity
        /// </summary>
        private void UpdateSpeed()
        {
            if (carController == null || speedLabel == null)
                return;

            Rigidbody carRb = carController.GetComponent<Rigidbody>();

            if (carRb != null)
            {
                int roundedSpeed = (int)Mathf.Round(carRb.linearVelocity.magnitude * speedMultiplier);
                speedLabel.text = $"{roundedSpeed}";

                // Update speed comparison
                UpdateSpeedComparison(roundedSpeed);
            }
        }

        /// <summary>
        /// Compares player speed to animal speeds
        /// </summary>
        private void UpdateSpeedComparison(float playerSpeed)
        {
            if (speedComparisonLabel == null)
                return;

            if (playerSpeed >= KANGAROO_SPEED)
            {
                speedComparisonLabel.text = "Faster than a kangaroo! 🦘";
            }
            else if (playerSpeed >= EMU_SPEED)
            {
                speedComparisonLabel.text = "Faster than an emu! 🦤";
            }
            else if (playerSpeed >= TASMANIAN_DEVIL_SPEED)
            {
                speedComparisonLabel.text = "Faster than a Tasmanian devil! 😈";
            }
            else if (playerSpeed >= ECHIDNA_SPEED)
            {
                speedComparisonLabel.text = "Faster than an echidna! 🦔";
            }
            else
            {
                speedComparisonLabel.text = "Take your time... 🐌";
            }
        }

        /// <summary>
        /// Called when an animal is discovered
        /// </summary>
        private void HandleAnimalDiscovered(AnimalData animal, bool isFirstDiscovery)
        {
            UpdateDiscoveryDisplay();

            if (isFirstDiscovery)
            {
                // Pulse the newly discovered animal icon
                VisualElement icon = GetIconForAnimal(animal.AnimalName);
                if (icon != null)
                {
                    StartCoroutine(PulseIcon(icon));
                }
            }
        }

        /// <summary>
        /// Updates the discovery progress display
        /// </summary>
        private void UpdateDiscoveryDisplay()
        {
            if (AnimalDiscoveryManager.Instance == null)
                return;

            int discoveredCount = AnimalDiscoveryManager.Instance.GetDiscoveryCount();
            int totalCount = 4; // Kangaroo, Emu, Echidna, Tasmanian Devil (update when adding more animals)

            // Update progress text
            if (progressText != null)
            {
                progressText.text = $"{discoveredCount}/{totalCount} Animals Discovered";
            }

            // Update individual animal indicators
            UpdateAnimalIndicator("Kangaroo", kangarooIcon, kangarooLabel);
            UpdateAnimalIndicator("Emu", emuIcon, emuLabel);
            UpdateAnimalIndicator("Echidna", echidnaIcon, echidnaLabel);
            UpdateAnimalIndicator("Tasmanian Devil", devilIcon, devilLabel);
        }

        /// <summary>
        /// Updates a single animal's icon/label based on discovery status
        /// </summary>
        private void UpdateAnimalIndicator(string animalName, VisualElement icon, Label label)
        {
            if (AnimalDiscoveryManager.Instance == null || icon == null)
                return;

            bool discovered = AnimalDiscoveryManager.Instance.HasDiscovered(animalName);

            if (discovered)
            {
                icon.RemoveFromClassList("undiscovered");
                icon.AddToClassList("discovered");

                if (label != null)
                {
                    label.text = "✓";
                }
            }
            else
            {
                icon.RemoveFromClassList("discovered");
                icon.AddToClassList("undiscovered");

                if (label != null)
                {
                    label.text = "?";
                }
            }
        }

        /// <summary>
        /// Gets the icon element for a given animal name
        /// </summary>
        private VisualElement GetIconForAnimal(string animalName)
        {
            switch (animalName)
            {
                case "Kangaroo": return kangarooIcon;
                case "Emu": return emuIcon;
                case "Echidna": return echidnaIcon;
                case "Tasmanian Devil": return devilIcon;
                default: return null;
            }
        }

        /// <summary>
        /// Pulses an icon to draw attention (for newly discovered animals)
        /// Unity UI Toolkit doesn't support CSS @keyframes, so we handle it via code.
        /// </summary>
        private IEnumerator PulseIcon(VisualElement icon)
        {
            if (icon == null) yield break;

            float duration = 0.5f;
            float elapsed = 0f;

            Vector3 originalScale = icon.transform.scale;
            float maxScale = 1.3f;

            // Scale up and pulse
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Ease in-out curve
                float scale = Mathf.Lerp(1f, maxScale, Mathf.Sin(t * Mathf.PI));
                icon.transform.scale = new Vector3(scale, scale, 1f);

                yield return null;
            }

            // Reset to original scale
            icon.transform.scale = originalScale;
        }

        /// <summary>
        /// Public method to set the car controller reference at runtime
        /// </summary>
        public void SetCarController(CarController controller)
        {
            carController = controller;
        }

        #region Debug Logging

        private void Log(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[HUDController] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[HUDController] {message}");
        }

        #endregion
    }
}