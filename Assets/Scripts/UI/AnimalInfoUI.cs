// AnimalInfoUI.cs
// Location: Assets/Scripts/UI/AnimalInfoUI.cs
//
// UI controller for the compact top-left "field journal" card that shows
// animal facts when the player is near a discoverable animal.
//
// UPDATED (Nov 2026): Sighting tiers.
//  - SetCurrentAnimal now takes the SightingResult from SightingManager instead
//    of asking AnimalDiscoveryManager itself. The old version checked
//    HasDiscovered() AFTER the animal had already been recorded, so it always
//    said "Already discovered" - passing the result in fixes that.
//  - Badge text: "New discovery" (first ever), then "First sighting ✓",
//    "Local Sighting ✓" or "Remote Sighting ✓".
//  - The badge keeps the "new" styling for a NEW Local/Remote sighting, and
//    switches to the muted "--already" styling for any repeat.

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Animals;

namespace RelaxingDrive.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class AnimalInfoUI : MonoBehaviour
    {
        private const string BadgeAlreadyClass = "discovery-badge--already";

        [Header("UI References")]
        [Tooltip("Optional - only needed if the USS isn't already linked via the UXML's <Style> tag")]
        [SerializeField] private StyleSheet animalInfoStyleSheet;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        private UIDocument uiDocument;
        private VisualElement rootPanel;

        private Label animalNameLabel;
        private Label scientificNameLabel;
        private Label habitatValue;
        private Label dietValue;
        private Label funFactValue;
        private VisualElement discoveryBadge;
        private Label discoveryText;
        private VisualElement animalIcon;

        private AnimalData currentAnimal;
        private bool isPanelVisible;
        private bool initialized;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            StartCoroutine(InitializeWhenReady());
        }

        /// <summary>
        /// Waits until the UIDocument's visual tree exists before querying it,
        /// then hides the panel unconditionally.
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
                Debug.LogError("[AnimalInfoUI] rootVisualElement never became available - " +
                    "check that this UIDocument's Panel Settings AND Source Asset are both assigned.");
                yield break;
            }

            rootPanel = uiDocument.rootVisualElement.Q<VisualElement>("animal-info-root");
            if (rootPanel == null)
            {
                Debug.LogError("[AnimalInfoUI] Could not find 'animal-info-root' in the UXML!");
                yield break;
            }

            if (animalInfoStyleSheet != null && !rootPanel.styleSheets.Contains(animalInfoStyleSheet))
                rootPanel.styleSheets.Add(animalInfoStyleSheet);

            animalNameLabel = rootPanel.Q<Label>("animal-name");
            scientificNameLabel = rootPanel.Q<Label>("scientific-name");
            habitatValue = rootPanel.Q<Label>("habitat-value");
            dietValue = rootPanel.Q<Label>("diet-value");
            funFactValue = rootPanel.Q<Label>("fun-fact-value");
            discoveryBadge = rootPanel.Q<VisualElement>("discovery-badge");
            discoveryText = rootPanel.Q<Label>("discovery-text");
            animalIcon = rootPanel.Q<VisualElement>("animal-icon");

            HidePanel();

            initialized = true;
            Log($"Initialized after {framesWaited} frame(s)");
        }

        /// <summary>
        /// Called by AnimalController when the player enters an animal's range.
        /// </summary>
        public void SetCurrentAnimal(AnimalData animalData, SightingResult result)
        {
            if (!initialized || animalData == null) return;

            currentAnimal = animalData;
            UpdateAnimalInfo(animalData, result);
            ShowPanel();

            Log($"Showing {animalData.AnimalName} ({result})");
        }

        /// <summary>
        /// Called by AnimalController when the player exits an animal's range.
        /// </summary>
        public void OnAnimalRangeExit(AnimalData animalData)
        {
            if (!initialized) return;

            if (currentAnimal != null && animalData != null && currentAnimal.AnimalName == animalData.AnimalName)
            {
                HidePanel();
                currentAnimal = null;
            }
        }

        private void UpdateAnimalInfo(AnimalData animalData, SightingResult result)
        {
            if (animalNameLabel != null) animalNameLabel.text = animalData.AnimalName;
            if (scientificNameLabel != null) scientificNameLabel.text = animalData.ScientificName;
            if (habitatValue != null) habitatValue.text = animalData.Habitat;
            if (dietValue != null) dietValue.text = animalData.Diet;
            if (funFactValue != null) funFactValue.text = animalData.FunFact;

            if (animalIcon != null)
            {
                animalIcon.style.backgroundImage = animalData.Icon != null
                    ? new StyleBackground(animalData.Icon)
                    : new StyleBackground(StyleKeyword.None);
            }

            if (discoveryBadge != null && discoveryText != null)
            {
                discoveryText.text = BadgeTextFor(result);

                // EnableInClassList adds the class when true, removes it when false.
                discoveryBadge.EnableInClassList(BadgeAlreadyClass, !result.IsNew);
            }
        }

        /// <summary>Maps a sighting result to the badge wording.</summary>
        private static string BadgeTextFor(SightingResult result)
        {
            if (result.Tier == SightingTier.First && result.IsNew)
                return "New discovery";

            switch (result.Tier)
            {
                case SightingTier.Local: return "Local Sighting ✓";
                case SightingTier.Remote: return "Remote Sighting ✓";
                default: return "First sighting ✓";
            }
        }

        private void ShowPanel()
        {
            if (isPanelVisible) return;
            rootPanel.RemoveFromClassList("animal-panel--hidden");
            rootPanel.AddToClassList("animal-panel--visible");
            isPanelVisible = true;
        }

        private void HidePanel()
        {
            rootPanel.RemoveFromClassList("animal-panel--visible");
            rootPanel.AddToClassList("animal-panel--hidden");
            isPanelVisible = false;
        }

        [ContextMenu("Test Panel Visibility")]
        public void TestPanelVisibility()
        {
            if (isPanelVisible) HidePanel();
            else ShowPanel();
        }

        private void Log(string message)
        {
            if (showDebugMessages) Debug.Log($"[AnimalInfoUI] {message}");
        }
    }
}