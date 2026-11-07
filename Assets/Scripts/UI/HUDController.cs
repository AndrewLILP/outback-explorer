// HUDController.cs
// Location: Assets/Scripts/UI/HUDController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Animals;
using RelaxingDrive.Core;
using RelaxingDrive.Player;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Slim HUD controller for the gameplay scene. Owns exactly two things:
    /// - The bottom-right speed readout: "Speed: 42 km/h (faster than a koala)".
    /// - The collapsible animal-discovery journal (bottom-left pill + stamp grid).
    ///
    /// Tier display (Nov 2026):
    ///  - Before the First Sighting Award: "n of 7 animals found (First Sighting Award)"
    ///  - After it: "n of 7 local animals found", switching to remote when the
    ///    latest sighting was Remote.
    ///  - The dots, the grid title and the filled stamps all show that SAME tier,
    ///    so the grid always matches the award the player is working on.
    ///  - Each stamp has 3 pips (First / Local / Remote); the current tier's pip
    ///    is drawn slightly larger.
    ///
    /// Toggling the grid (Nov 2026):
    ///  - The pill no longer takes focus. Before, once focused, Space/Enter (PC)
    ///    or A (Deck) "clicked" it - the handbrake and enter-car buttons - so the
    ///    grid opened at seemingly random times.
    ///  - It now toggles via the ToggleJournal input action (Tab / View button)
    ///    from PlayerInputReader, or a mouse click on the pill.
    ///  - It still auto-opens briefly on a NEW sighting (optional, see Inspector).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDController : MonoBehaviour
    {
        // Order controls left-to-right position in the dots and stamp grid.
        // Names must match AnimalData.AnimalName.
        private static readonly string[] AnimalOrder =
        {
            "Kangaroo", "Frillneck Lizard", "Emu", "Echidna", "Tasmanian Devil", "Koala", "Platypus"
        };

        // Pip element names inside each stamp, in SightingTier order (First, Local, Remote).
        private static readonly string[] PipNames = { "PipFirst", "PipLocal", "PipRemote" };

        private const string PipFilledClass = "pip--filled";
        private const string PipCurrentClass = "pip--current";

        [Header("References")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Settings")]
        [Tooltip("Open the grid briefly when the player makes a NEW sighting.")]
        [SerializeField] private bool autoExpandOnNewSighting = true;
        [Tooltip("How long the grid stays auto-expanded after a new sighting.")]
        [SerializeField] private float discoveryPopupSeconds = 3f;
        [SerializeField] private bool showDebugLogs = true;

        private VisualElement root;
        private Label speedComparisonLabel;

        private Button discoveryPill;
        private Label discoveryCountLabel;
        private VisualElement discoveryGrid;
        private Label gridTitleLabel;
        private VisualElement[] discoveryDots;
        private VisualElement[] stamps;
        private VisualElement[,] pips; // [animal, tier]

        // Last whole-number speed written to the label (-1 = nothing shown yet).
        private int lastShownSpeed = -1;

        private bool isDiscoveryExpanded;
        private Coroutine collapseRoutine;
        private bool initialized;

        // Cached so OnDisable never re-creates managers during scene teardown.
        private SightingManager sightings;
        private GameSaveManager saveManager;
        private PlayerInputReader inputReader;

        // Which region the HUD shows once the First tier is complete.
        // Defaults to Local; follows the region of the latest Local/Remote sighting.
        private SightingRegion displayedRegion = SightingRegion.Local;

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
            if (sightings != null) sightings.OnSighting -= HandleSighting;
            if (saveManager != null) saveManager.OnInitialLoadComplete -= UpdateDiscoveryDisplay;
            if (inputReader != null) inputReader.OnJournalTogglePressed -= ToggleDiscoveryGrid;
        }

        /// <summary>
        /// Waits until the UIDocument's panel has built a visual tree before
        /// querying it - guards against root.Q(...) on a null rootVisualElement.
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

            // Touching the Instance bootstraps AnimalDiscoveryManager (other systems,
            // e.g. the Steam achievement, rely on the HUD doing this).
            _ = AnimalDiscoveryManager.Instance;

            sightings = SightingManager.Instance;
            if (sightings != null) sightings.OnSighting += HandleSighting;

            // If the save hasn't loaded yet, refresh once it has.
            saveManager = GameSaveManager.Instance;
            if (saveManager != null && !saveManager.HasCompletedInitialLoad)
                saveManager.OnInitialLoadComplete += UpdateDiscoveryDisplay;

            // Tab / View button toggles the grid.
            inputReader = PlayerInputReader.Instance != null
                ? PlayerInputReader.Instance
                : FindFirstObjectByType<PlayerInputReader>();
            if (inputReader != null)
                inputReader.OnJournalTogglePressed += ToggleDiscoveryGrid;
            else
                Debug.LogWarning("[HUDController] No PlayerInputReader in scene - grid can only be opened by mouse.");

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
            gridTitleLabel = root.Q<Label>("GridTitle");

            discoveryDots = new VisualElement[AnimalOrder.Length];
            stamps = new VisualElement[AnimalOrder.Length];
            pips = new VisualElement[AnimalOrder.Length, PipNames.Length];

            for (int i = 0; i < AnimalOrder.Length; i++)
            {
                discoveryDots[i] = root.Q<VisualElement>($"Dot{i}");
                stamps[i] = root.Q<VisualElement>($"Stamp{ElementSuffixFor(AnimalOrder[i])}");

                // Pips share names across stamps, so query INSIDE each stamp.
                for (int t = 0; t < PipNames.Length; t++)
                    pips[i, t] = stamps[i]?.Q<VisualElement>(PipNames[t]);
            }

            if (discoveryPill != null)
            {
                // Never take focus - otherwise Space/Enter/A "click" it during play.
                discoveryPill.focusable = false;
                discoveryPill.clicked += ToggleDiscoveryGrid; // mouse click still works on PC
            }

            SetDiscoveryGridExpanded(false);

            Log("UI elements wired up");
        }

        private void Update()
        {
            if (!initialized) return;
            UpdateSpeedComparison();
        }

        /// <summary>
        /// UPDATED (Nov 2026): bottom-right readout, "Speed: 42 km/h (faster than a koala)".
        /// Only shown while driving. The text is rebuilt only when the whole-number
        /// speed changes - building a new string every frame creates garbage, which
        /// causes small hitches on the Steam Deck.
        /// </summary>
        private void UpdateSpeedComparison()
        {
            if (speedComparisonLabel == null) return;

            PlayerStateManager psm = PlayerStateManager.Instance;
            if (psm == null || !psm.IsDriving || psm.VehicleController == null)
            {
                speedComparisonLabel.style.display = DisplayStyle.None;
                lastShownSpeed = -1; // force a refresh when driving resumes
                return;
            }

            speedComparisonLabel.style.display = DisplayStyle.Flex;

            // Abs() so reversing shows a positive speed.
            int speed = Mathf.RoundToInt(Mathf.Abs(psm.VehicleController.Speed));
            if (speed == lastShownSpeed) return;
            lastShownSpeed = speed;

            string animal = FasterThan(speed);
            speedComparisonLabel.text = animal != null
                ? $"Speed: {speed} km/h (faster than {animal})"
                : $"Speed: {speed} km/h";
        }

        /// <summary>The fastest animal the player is beating, with its article - or null if none.</summary>
        private static string FasterThan(float speed)
        {
            if (speed >= KANGAROO_SPEED) return "a kangaroo";
            if (speed >= EMU_SPEED) return "an emu";
            if (speed >= FRILLNECK_LIZARD_SPEED) return "a frillneck lizard";
            if (speed >= TASMANIAN_DEVIL_SPEED) return "a Tasmanian devil";
            if (speed >= PLATYPUS_SPEED) return "a platypus";
            if (speed >= KOALA_SPEED) return "a koala";
            if (speed >= ECHIDNA_SPEED) return "an echidna";
            return null;
        }

        #region Grid open / close

        private void ToggleDiscoveryGrid()
        {
            // A manual toggle cancels any pending auto-collapse.
            if (collapseRoutine != null)
            {
                StopCoroutine(collapseRoutine);
                collapseRoutine = null;
            }
            SetDiscoveryGridExpanded(!isDiscoveryExpanded);
        }

        private void SetDiscoveryGridExpanded(bool expanded)
        {
            isDiscoveryExpanded = expanded;
            if (discoveryGrid == null) return;
            discoveryGrid.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private IEnumerator CollapseAfterDelay()
        {
            yield return new WaitForSeconds(discoveryPopupSeconds);
            SetDiscoveryGridExpanded(false);
            collapseRoutine = null;
        }

        #endregion

        /// <summary>
        /// Fires on every sighting (new or repeat). Switches the HUD to the
        /// region of a Local/Remote sighting, and pops the grid open on new ones.
        /// </summary>
        private void HandleSighting(SightingContext context, SightingResult result)
        {
            if (result.Tier != SightingTier.First)
                displayedRegion = context.Region;

            UpdateDiscoveryDisplay();

            // Only auto-open if the player hasn't already opened it themselves.
            if (autoExpandOnNewSighting && result.IsNew && !isDiscoveryExpanded)
            {
                SetDiscoveryGridExpanded(true);
                collapseRoutine = StartCoroutine(CollapseAfterDelay());
            }
        }

        /// <summary>The tier the pill, dots, title and stamps are showing.</summary>
        private SightingTier GetDisplayedTier()
        {
            if (sightings == null || !sightings.IsFirstTierComplete())
                return SightingTier.First;

            return displayedRegion == SightingRegion.Local ? SightingTier.Local : SightingTier.Remote;
        }

        private void UpdateDiscoveryDisplay()
        {
            if (sightings == null || root == null) return;

            SightingTier displayedTier = GetDisplayedTier();
            int total = AnimalOrder.Length;
            int found = 0;

            for (int i = 0; i < AnimalOrder.Length; i++)
            {
                bool sightedInDisplayedTier = sightings.HasSighted(AnimalOrder[i], displayedTier);
                if (sightedInDisplayedTier) found++;

                // Dot on the pill
                if (discoveryDots[i] != null)
                {
                    discoveryDots[i].EnableInClassList("discovery-dot--filled", sightedInDisplayedTier);
                    discoveryDots[i].EnableInClassList("discovery-dot--empty", !sightedInDisplayedTier);
                }

                // Stamp: inked when sighted in the tier the player is working on
                if (stamps[i] != null)
                {
                    stamps[i].EnableInClassList("discovered", sightedInDisplayedTier);
                    stamps[i].EnableInClassList("undiscovered", !sightedInDisplayedTier);
                }

                // Pips: all three tiers, current one emphasised
                for (int t = 0; t < PipNames.Length; t++)
                {
                    VisualElement pip = pips[i, t];
                    if (pip == null) continue;

                    SightingTier pipTier = (SightingTier)t;
                    pip.EnableInClassList(PipFilledClass, sightings.HasSighted(AnimalOrder[i], pipTier));
                    pip.EnableInClassList(PipCurrentClass, pipTier == displayedTier);
                }
            }

            if (discoveryCountLabel != null)
                discoveryCountLabel.text = CounterTextFor(displayedTier, found, total);

            if (gridTitleLabel != null)
                gridTitleLabel.text = $"{AwardNameFor(displayedTier)}  ·  {found} of {total}";
        }

        private static string CounterTextFor(SightingTier tier, int found, int total)
        {
            switch (tier)
            {
                case SightingTier.Local: return $"{found} of {total} local animals found";
                case SightingTier.Remote: return $"{found} of {total} remote animals found";
                default: return $"{found} of {total} animals found (First Sighting Award)";
            }
        }

        /// <summary>
        /// Award name for the grid title, read from the AwardData assets so the
        /// HUD and the pop-up always use the same wording.
        /// </summary>
        private static string AwardNameFor(SightingTier tier)
        {
            AwardManager awards = AwardManager.Instance;
            if (awards != null)
            {
                foreach (AwardData award in awards.Awards)
                {
                    if (award.Tier == tier) return award.DisplayName;
                }
            }
            return $"{tier} Sightings"; // fallback if no asset exists for this tier
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