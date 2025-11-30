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
    /// Sprint 5: Updated to support 7 animals (Kangaroo, Emu, Echidna, Devil, Koala, Frillneck, Platypus)
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
        
        // Row 1 Animals (Easy finds)
        private VisualElement kangarooIcon;
        private VisualElement emuIcon;
        private VisualElement echidnaIcon;
        private VisualElement devilIcon;
        
        // Row 2 Animals (Hard finds)
        private VisualElement koalaIcon;
        private VisualElement frillneckIcon;
        private VisualElement platypusIcon;
        
        // Row 1 Labels
        private Label kangarooLabel;
        private Label emuLabel;
        private Label echidnaLabel;
        private Label devilLabel;
        
        // Row 2 Labels
        private Label koalaLabel;
        private Label frillneckLabel;
        private Label platypusLabel;

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
        private const float TASMANIAN_DEVIL_SPEED = 13f;
        private const float FRILLNECK_LIZARD_SPEED = 30f; // Can sprint quickly
        private const float KOALA_SPEED = 3f; // Very slow climber
        private const float ECHIDNA_SPEED = 2f;
        private const float PLATYPUS_SPEED = 7f; // Decent swimmer

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

            // Query Row 1 animal icons (easy finds)
            kangarooIcon = root.Q<VisualElement>("KangarooIcon");
            emuIcon = root.Q<VisualElement>("EmuIcon");
            echidnaIcon = root.Q<VisualElement>("EchidnaIcon");
            devilIcon = root.Q<VisualElement>("DevilIcon");
            
            // Query Row 2 animal icons (hard finds)
            koalaIcon = root.Q<VisualElement>("KoalaIcon");
            frillneckIcon = root.Q<VisualElement>("FrillneckIcon");
            platypusIcon = root.Q<VisualElement>("PlatypusIcon");

            // Query Row 1 animal labels
            kangarooLabel = root.Q<Label>("KangarooLabel");
            emuLabel = root.Q<Label>("EmuLabel");
            echidnaLabel = root.Q<Label>("EchidnaLabel");
            devilLabel = root.Q<Label>("DevilLabel");
            
            // Query Row 2 animal labels
            koalaLabel = root.Q<Label>("KoalaLabel");
            frillneckLabel = root.Q<Label>("FrillneckLabel");
            platypusLabel = root.Q<Label>("PlatypusLabel");

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

            Log("✅ HUD UI elements setup complete (7 animals supported)");
        }

        /// <summary>
        /// Register all button click callbacks
        /// </summary>
        private void RegisterButtonCallbacks()
        {
            if (resumeButton != null)
            {
                resumeButton.clicked += OnResumeClicked;
                Log("✅ Resume button callback registered");
            }

            if (saveButton != null)
            {
                saveButton.clicked += OnSaveClicked;
                Log("✅ Save button callback registered");
            }

            if (newGameButton != null)
            {
                newGameButton.clicked += OnNewGameClicked;
                Log("✅ New Game button callback registered");
            }

            if (instructionsButton != null)
            {
                instructionsButton.clicked += OnInstructionsClicked;
                Log("✅ Instructions button callback registered");
            }

            if (closeInstructionsButton != null)
            {
                closeInstructionsButton.clicked += OnCloseInstructionsClicked;
                Log("✅ Close Instructions button callback registered");
            }
        }

        /// <summary>
        /// Adds color CSS classes to animal icons
        /// </summary>
        private void InitializeAnimalIcons()
        {
            // Row 1 - Easy finds
            if (kangarooIcon != null) kangarooIcon.AddToClassList("kangaroo");
            if (emuIcon != null) emuIcon.AddToClassList("emu");
            if (echidnaIcon != null) echidnaIcon.AddToClassList("echidna");
            if (devilIcon != null) devilIcon.AddToClassList("devil");
            
            // Row 2 - Hard finds
            if (koalaIcon != null) koalaIcon.AddToClassList("koala");
            if (frillneckIcon != null) frillneckIcon.AddToClassList("frillneck");
            if (platypusIcon != null) platypusIcon.AddToClassList("platypus");
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
        /// Compares player speed to animal speeds (updated for 7 animals)
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
            else if (playerSpeed >= FRILLNECK_LIZARD_SPEED)
            {
                speedComparisonLabel.text = "Faster than a frillneck lizard! 🦎";
            }
            else if (playerSpeed >= TASMANIAN_DEVIL_SPEED)
            {
                speedComparisonLabel.text = "Faster than a Tasmanian devil! 😈";
            }
            else if (playerSpeed >= PLATYPUS_SPEED)
            {
                speedComparisonLabel.text = "Faster than a platypus! 🦆";
            }
            else if (playerSpeed >= KOALA_SPEED)
            {
                speedComparisonLabel.text = "Faster than a koala! 🐨";
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
        /// Updates the discovery progress display (supports 7 animals)
        /// </summary>
        private void UpdateDiscoveryDisplay()
        {
            if (AnimalDiscoveryManager.Instance == null)
                return;

            int discoveredCount = AnimalDiscoveryManager.Instance.GetDiscoveryCount();
            int totalCount = 7; // Updated from 4 to 7 animals

            // Update progress text
            if (progressText != null)
            {
                progressText.text = $"{discoveredCount}/{totalCount} Animals Discovered";
            }

            // Update Row 1 animal indicators (easy finds)
            UpdateAnimalIndicator("Kangaroo", kangarooIcon, kangarooLabel);
            UpdateAnimalIndicator("Emu", emuIcon, emuLabel);
            UpdateAnimalIndicator("Echidna", echidnaIcon, echidnaLabel);
            UpdateAnimalIndicator("Tasmanian Devil", devilIcon, devilLabel);
            
            // Update Row 2 animal indicators (hard finds)
            UpdateAnimalIndicator("Koala", koalaIcon, koalaLabel);
            UpdateAnimalIndicator("Frillneck Lizard", frillneckIcon, frillneckLabel);
            UpdateAnimalIndicator("Platypus", platypusIcon, platypusLabel);
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
        /// Gets the icon element for a given animal name (updated for 7 animals)
        /// </summary>
        private VisualElement GetIconForAnimal(string animalName)
        {
            switch (animalName)
            {
                case "Kangaroo": return kangarooIcon;
                case "Emu": return emuIcon;
                case "Echidna": return echidnaIcon;
                case "Tasmanian Devil": return devilIcon;
                case "Koala": return koalaIcon;
                case "Frillneck Lizard": return frillneckIcon;
                case "Platypus": return platypusIcon;
                default: return null;
            }
        }

        /// <summary>
        /// Pulses an icon to draw attention (for newly discovered animals)
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