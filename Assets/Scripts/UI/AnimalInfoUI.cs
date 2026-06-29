// AnimalInfoUI.cs
// UI Controller for the compact "field journal" card that shows animal facts
// when the player is near a discoverable animal. Redesigned for the RCC
// scene: a smaller bottom-center card instead of the old large left panel.
//
// Public contract (SetCurrentAnimal / OnAnimalRangeExit) is unchanged, so
// AnimalController needs no changes to keep working with this rewrite.

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Animals;

namespace RelaxingDrive.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class AnimalInfoUI : MonoBehaviour
    {
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
        /// Waits until the UIDocument's visual tree actually exists before
        /// querying it, then hides the panel unconditionally. The previous
        /// version's HidePanel() had `if (!isPanelVisible) return;` as its
        /// first line - since isPanelVisible defaults to false, the very
        /// first call (from initialization) returned immediately and never
        /// applied the hidden CSS class, leaving the panel visible with raw
        /// placeholder UXML text from scene start. Fixed below by removing
        /// that guard entirely; re-applying "hidden" when already hidden is
        /// a harmless no-op in UI Toolkit.
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
        /// Called by AnimalController when player enters animal's range.
        /// </summary>
        public void SetCurrentAnimal(AnimalData animalData, Vector3 animalPosition)
        {
            if (!initialized || animalData == null) return;

            currentAnimal = animalData;
            bool isFirstTime = AnimalDiscoveryManager.Instance != null &&
                !AnimalDiscoveryManager.Instance.HasDiscovered(animalData);

            UpdateAnimalInfo(animalData, isFirstTime);
            ShowPanel();

            Log($"Showing {animalData.AnimalName} (first time: {isFirstTime})");
        }

        /// <summary>
        /// Called by AnimalController when player exits animal's range.
        /// </summary>
        public void OnAnimalRangeExit(AnimalData animalData)
        {
            if (!initialized) return;

            if (currentAnimal != null && animalData != null && currentAnimal.AnimalName == animalData.AnimalName)
            {
                HidePanel();
                currentAnimal = null;
                Log($"Hiding panel ({animalData.AnimalName} left range)");
            }
        }

        private void UpdateAnimalInfo(AnimalData animalData, bool isFirstTime)
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
                if (isFirstTime)
{
    discoveryText.text = "New discovery";
    discoveryBadge.RemoveFromClassList("discovery-badge--already");
}
else
{
    discoveryText.text = "Already discovered";
    discoveryBadge.AddToClassList("discovery-badge--already");
}
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

        /// <summary>Manual toggle, for testing from the Inspector.</summary>
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
