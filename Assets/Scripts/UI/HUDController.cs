// HUDController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Core;
using RelaxingDrive.Animals;
using RelaxingDrive.Player;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Slim HUD controller for the RCC gameplay scene (also works in the Itch/
    /// PolyStang scene - speed comes from IVehicleController, not a concrete
    /// car type, so there's no PolyStang- or RCC-specific code here at all).
    ///
    /// Owns three things:
    /// - The "faster than a kangaroo" speed-comparison text (RCC's own dash
    ///   shows the actual number, so this HUD doesn't duplicate it).
    /// - The collapsible animal-discovery stamp strip.
    /// - The pause menu (Resume / Save / New Game / Instructions).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDController : MonoBehaviour
    {
        // Order controls left-to-right position in the stamp grid. The two
        // vertical-slice species lead so they're the first thing players see.
        private static readonly string[] AnimalOrder =
        {
            "Kangaroo", "Frillneck Lizard", "Emu", "Echidna", "Tasmanian Devil", "Koala", "Platypus"
        };

        [Header("References")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Settings")]
        [Tooltip("How long the stamp grid stays auto-expanded after a new discovery.")]
        [SerializeField] private float discoveryPopupSeconds = 3f;
        [SerializeField] private bool showDebugLogs = true;

        private VisualElement root;
        private Label speedComparisonLabel;

        private Button discoveryPill;
        private Label discoveryCountLabel;
        private VisualElement discoveryGrid;
        private VisualElement[] discoveryDots;

        private VisualElement pauseMenu;
        private Button resumeButton;
        private Button saveButton;
        private Button newGameButton;
        private Button instructionsButton;
        private VisualElement instructionsPanel;
        private Button closeInstructionsButton;

        private bool isPaused;
        private bool isDiscoveryExpanded;
        private Coroutine collapseRoutine;
        private bool initialized;

        // Animal speed constants (km/h), used for the comparison text.
        private const float KANGAROO_SPEED = 60f;
        private const float EMU_SPEED = 50f;
        private const float FRILLNECK_LIZARD_SPEED = 30f;
        private const float TASMANIAN_DEVIL_SPEED = 13f;
        private const float PLATYPUS_SPEED = 7f;
        private const float KOALA_SPEED = 3f;
        private const float ECHIDNA_SPEED = 2f;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            StartCoroutine(InitializeWhenReady());
        }

        private void OnDisable()
        {
            if (AnimalDiscoveryManager.Instance != null)
                AnimalDiscoveryManager.Instance.OnAnimalDiscovered -= HandleAnimalDiscovered;
        }

        /// <summary>
        /// Waits until the UIDocument's panel has actually built a visual tree
        /// before querying it. This is a deliberate guard against the previous
        /// session's unresolved bug: if rootVisualElement is null (e.g. the
        /// UIDocument's Panel Settings isn't assigned yet), root.Q(...) throws
        /// immediately and silently aborts setup before anything logs or
        /// subscribes to events - which would explain both a dead Escape key
        /// and a permanently frozen "0/7" display. This either prevents that
        /// failure outright, or makes it loud instead of silent.
        /// </summary>
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
                Debug.LogError("[HUDController] rootVisualElement never became available - " +
                    "check that this UIDocument's Panel Settings AND Source Asset are both assigned.");
                yield break;
            }

            root = uiDocument.rootVisualElement;
            SetupUIElements();

            if (AnimalDiscoveryManager.Instance != null)
                AnimalDiscoveryManager.Instance.OnAnimalDiscovered += HandleAnimalDiscovered;

            UpdateDiscoveryDisplay();
            initialized = true;
            Log($"Ready after {framesWaited} frame(s)");
        }

        private void SetupUIElements()
        {
            speedComparisonLabel = root.Q<Label>("SpeedComparison");

            discoveryPill = root.Q<Button>("DiscoveryPill");
            discoveryCountLabel = root.Q<Label>("DiscoveryCountLabel");
            discoveryGrid = root.Q<VisualElement>("DiscoveryGrid");

            discoveryDots = new VisualElement[AnimalOrder.Length];
            for (int i = 0; i < AnimalOrder.Length; i++)
                discoveryDots[i] = root.Q<VisualElement>($"Dot{i}");

            pauseMenu = root.Q<VisualElement>("PauseMenu");
            resumeButton = root.Q<Button>("ResumeButton");
            saveButton = root.Q<Button>("SaveButton");
            newGameButton = root.Q<Button>("NewGameButton");
            instructionsButton = root.Q<Button>("InstructionsButton");
            instructionsPanel = root.Q<VisualElement>("InstructionsPanel");
            closeInstructionsButton = root.Q<Button>("CloseInstructionsButton");

            if (discoveryPill != null) discoveryPill.clicked += ToggleDiscoveryGrid;
            if (resumeButton != null) resumeButton.clicked += OnResumeClicked;
            if (saveButton != null) saveButton.clicked += OnSaveClicked;
            if (newGameButton != null) newGameButton.clicked += OnNewGameClicked;
            if (instructionsButton != null) instructionsButton.clicked += OnInstructionsClicked;
            if (closeInstructionsButton != null) closeInstructionsButton.clicked += OnCloseInstructionsClicked;

            SetDiscoveryGridExpanded(false);

            if (pauseMenu != null) pauseMenu.style.display = DisplayStyle.None;
            if (instructionsPanel != null) instructionsPanel.style.display = DisplayStyle.None;

            Log("UI elements wired up");
        }

        private void Update()
        {
            if (!initialized) return;
            UpdateSpeedComparison();
            HandlePauseInput();
        }

        /// <summary>
        /// Updates the "faster than a kangaroo" text. Only shown while
        /// actually driving - on foot there's no speed worth comparing.
        /// </summary>
        private void UpdateSpeedComparison()
        {
            if (speedComparisonLabel == null) return;

            PlayerStateManager psm = PlayerStateManager.Instance;
            if (psm == null || !psm.IsDriving || psm.VehicleController == null)
            {
                speedComparisonLabel.style.display = DisplayStyle.None;
                return;
            }

            speedComparisonLabel.style.display = DisplayStyle.Flex;
            float speed = psm.VehicleController.Speed;

            if (speed >= KANGAROO_SPEED) speedComparisonLabel.text = "Faster than a kangaroo";
            else if (speed >= EMU_SPEED) speedComparisonLabel.text = "Faster than an emu";
            else if (speed >= FRILLNECK_LIZARD_SPEED) speedComparisonLabel.text = "Faster than a frillneck lizard";
            else if (speed >= TASMANIAN_DEVIL_SPEED) speedComparisonLabel.text = "Faster than a Tasmanian devil";
            else if (speed >= PLATYPUS_SPEED) speedComparisonLabel.text = "Faster than a platypus";
            else if (speed >= KOALA_SPEED) speedComparisonLabel.text = "Faster than a koala";
            else if (speed >= ECHIDNA_SPEED) speedComparisonLabel.text = "Faster than an echidna";
            else speedComparisonLabel.text = "Take your time...";
        }

        private void HandlePauseInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (isPaused) HidePauseMenu();
                else ShowPauseMenu();
            }
        }

        private void ShowPauseMenu()
        {
            if (pauseMenu == null) return;
            pauseMenu.style.display = DisplayStyle.Flex;
            isPaused = true;
            Time.timeScale = 0f;
            Log("Paused");
        }

        private void HidePauseMenu()
        {
            if (pauseMenu == null) return;
            pauseMenu.style.display = DisplayStyle.None;
            isPaused = false;
            Time.timeScale = 1f;
            if (instructionsPanel != null) instructionsPanel.style.display = DisplayStyle.None;
            Log("Resumed");
        }

        private void OnResumeClicked() => HidePauseMenu();

        private void OnSaveClicked()
        {
            if (GameSaveManager.Instance != null)
            {
                GameSaveManager.Instance.Save();
                Log("Game saved");
            }
            else
            {
                Debug.LogError("[HUDController] GameSaveManager not found!");
            }
        }

        private void OnNewGameClicked()
        {
            if (GameSaveManager.Instance == null)
            {
                Debug.LogError("[HUDController] GameSaveManager not found!");
                return;
            }

            HidePauseMenu();
            GameSaveManager.Instance.StartNewGame();
        }

        private void OnInstructionsClicked()
        {
            if (instructionsPanel != null) instructionsPanel.style.display = DisplayStyle.Flex;
        }

        private void OnCloseInstructionsClicked()
        {
            if (instructionsPanel != null) instructionsPanel.style.display = DisplayStyle.None;
        }

        private void ToggleDiscoveryGrid() => SetDiscoveryGridExpanded(!isDiscoveryExpanded);

        private void SetDiscoveryGridExpanded(bool expanded)
        {
            isDiscoveryExpanded = expanded;
            if (discoveryGrid == null) return;
            discoveryGrid.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Fires on every discovery attempt (including re-discovering an
        /// already-known animal). The grid only auto-pops on a genuinely new one.
        /// </summary>
        private void HandleAnimalDiscovered(AnimalData animal, bool isFirstDiscovery)
        {
            UpdateDiscoveryDisplay();

            if (isFirstDiscovery)
            {
                SetDiscoveryGridExpanded(true);
                if (collapseRoutine != null) StopCoroutine(collapseRoutine);
                collapseRoutine = StartCoroutine(CollapseAfterDelay());
            }
        }

        private IEnumerator CollapseAfterDelay()
        {
            yield return new WaitForSeconds(discoveryPopupSeconds);
            SetDiscoveryGridExpanded(false);
            collapseRoutine = null;
        }

        private void UpdateDiscoveryDisplay()
        {
            if (AnimalDiscoveryManager.Instance == null) return;

            int discoveredCount = AnimalDiscoveryManager.Instance.GetDiscoveryCount();

            if (discoveryCountLabel != null)
                discoveryCountLabel.text = $"{discoveredCount}/{AnimalOrder.Length} found";

            for (int i = 0; i < AnimalOrder.Length; i++)
            {
                bool discovered = AnimalDiscoveryManager.Instance.HasDiscovered(AnimalOrder[i]);

                if (discoveryDots[i] != null)
                {
                    discoveryDots[i].RemoveFromClassList(discovered ? "discovery-dot--empty" : "discovery-dot--filled");
                    discoveryDots[i].AddToClassList(discovered ? "discovery-dot--filled" : "discovery-dot--empty");
                }

                VisualElement stamp = root.Q<VisualElement>($"Stamp{ElementSuffixFor(AnimalOrder[i])}");
                if (stamp != null)
                {
                    stamp.RemoveFromClassList(discovered ? "undiscovered" : "discovered");
                    stamp.AddToClassList(discovered ? "discovered" : "undiscovered");
                }
            }
        }

        /// <summary>Maps an animal name to its UXML element-name suffix (StampKangaroo, StampDevil, etc).</summary>
        private string ElementSuffixFor(string animalName)
        {
            switch (animalName)
            {
                case "Tasmanian Devil": return "Devil";
                case "Frillneck Lizard": return "Frillneck";
                default: return animalName;
            }
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[HUDController] {message}");
        }
    }
}
