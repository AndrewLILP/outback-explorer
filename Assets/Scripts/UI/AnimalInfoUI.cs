// AnimalInfoUI.cs
// UI Controller that displays animal information when player is nearby
// Receives direct notifications from AnimalController

using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Animals;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the Animal Info Panel UI.
    /// Displays animal information when notified by AnimalController.
    /// Handles fade in/out animations.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class AnimalInfoUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("StyleSheet containing animations and styling")]
        [SerializeField] private StyleSheet animalInfoStyleSheet;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // UI Element references
        private UIDocument uiDocument;
        private VisualElement rootPanel;
        private VisualElement container;

        // UI Labels
        private Label animalNameLabel;
        private Label scientificNameLabel;
        private Label habitatValue;
        private Label dietValue;
        private Label funFactValue;
        private VisualElement discoveryBadge;
        private Label discoveryText;
        private VisualElement animalIcon;

        // State tracking
        private AnimalData currentAnimal;
        private bool isPanelVisible = false;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null)
            {
                Debug.LogError("AnimalInfoUI: UIDocument component not found!", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            // Wait one frame for UI to initialize
            Invoke(nameof(InitializeUI), 0.1f);
        }

        private void InitializeUI()
        {
            // Get root visual element
            rootPanel = uiDocument.rootVisualElement.Q<VisualElement>("animal-info-root");

            if (rootPanel == null)
            {
                Debug.LogError("AnimalInfoUI: Could not find 'animal-info-root' in UXML!", this);
                return;
            }

            // Apply stylesheet
            if (animalInfoStyleSheet != null)
            {
                rootPanel.styleSheets.Add(animalInfoStyleSheet);
            }

            // Cache UI element references
            container = rootPanel.Q<VisualElement>("animal-info-container");
            animalNameLabel = rootPanel.Q<Label>("animal-name");
            scientificNameLabel = rootPanel.Q<Label>("scientific-name");
            habitatValue = rootPanel.Q<Label>("habitat-value");
            dietValue = rootPanel.Q<Label>("diet-value");
            funFactValue = rootPanel.Q<Label>("fun-fact-value");
            discoveryBadge = rootPanel.Q<VisualElement>("discovery-badge");
            discoveryText = rootPanel.Q<Label>("discovery-text");
            animalIcon = rootPanel.Q<VisualElement>("animal-icon");

            // Start hidden
            HidePanel();

            Debug.Log("AnimalInfoUI: Initialized successfully");
        }

        /// <summary>
        /// Called by AnimalController when player enters animal's range.
        /// Shows the panel with animal information.
        /// </summary>
        public void SetCurrentAnimal(AnimalData animalData, Vector3 animalPosition)
        {
            if (animalData == null)
            {
                Debug.LogWarning("AnimalInfoUI: SetCurrentAnimal called with null data!");
                return;
            }

            currentAnimal = animalData;

            // Check if this is first time discovering this animal
            bool isFirstTime = !AnimalDiscoveryManager.Instance.HasDiscovered(animalData);

            // Update UI content
            UpdateAnimalInfo(animalData, isFirstTime);

            // Show panel
            ShowPanel();

            if (showDebugMessages)
            {
                Debug.Log($"AnimalInfoUI: Now showing {animalData.AnimalName} (First time: {isFirstTime})");
            }
        }

        /// <summary>
        /// Called by AnimalController when player exits animal's range.
        /// Hides the panel.
        /// </summary>
        public void OnAnimalRangeExit(AnimalData animalData)
        {
            // Only hide if this is the animal we're currently displaying
            if (currentAnimal != null && currentAnimal.AnimalName == animalData.AnimalName)
            {
                HidePanel();
                currentAnimal = null;

                if (showDebugMessages)
                {
                    Debug.Log($"AnimalInfoUI: Hiding panel (player left {animalData.AnimalName})");
                }
            }
        }

        /// <summary>
        /// Updates UI labels with animal data.
        /// </summary>
        private void UpdateAnimalInfo(AnimalData animalData, bool isFirstTime)
        {
            // Update text content
            animalNameLabel.text = animalData.AnimalName;
            scientificNameLabel.text = animalData.ScientificName;
            habitatValue.text = animalData.Habitat;
            dietValue.text = animalData.Diet;
            funFactValue.text = animalData.FunFact;

            // Update icon (if sprite exists)
            if (animalData.Icon != null)
            {
                animalIcon.style.backgroundImage = new StyleBackground(animalData.Icon);
            }
            else
            {
                // Use placeholder or keep default
                animalIcon.style.backgroundImage = StyleKeyword.None;
            }

            // Update discovery badge
            if (isFirstTime)
            {
                discoveryText.text = "🆕 Discovered!";
                discoveryBadge.RemoveFromClassList("discovery-badge--hidden");
                discoveryBadge.RemoveFromClassList("discovery-badge--already");
            }
            else
            {
                discoveryText.text = "✓ Already Discovered";
                discoveryBadge.RemoveFromClassList("discovery-badge--hidden");
                discoveryBadge.AddToClassList("discovery-badge--already");
            }

            if (showDebugMessages)
            {
                Debug.Log($"AnimalInfoUI: Updated info for {animalData.AnimalName}");
            }
        }

        /// <summary>
        /// Shows the panel with fade in animation.
        /// </summary>
        private void ShowPanel()
        {
            if (isPanelVisible) return;

            rootPanel.RemoveFromClassList("animal-panel--hidden");
            rootPanel.AddToClassList("animal-panel--visible");
            isPanelVisible = true;

            if (showDebugMessages)
            {
                Debug.Log("AnimalInfoUI: Panel visible");
            }
        }

        /// <summary>
        /// Hides the panel with fade out animation.
        /// </summary>
        private void HidePanel()
        {
            if (!isPanelVisible) return;

            rootPanel.RemoveFromClassList("animal-panel--visible");
            rootPanel.AddToClassList("animal-panel--hidden");
            isPanelVisible = false;

            if (showDebugMessages)
            {
                Debug.Log("AnimalInfoUI: Panel hidden");
            }
        }

        /// <summary>
        /// Manual method to toggle panel (for testing).
        /// </summary>
        [ContextMenu("Test Panel Visibility")]
        public void TestPanelVisibility()
        {
            if (isPanelVisible)
            {
                HidePanel();
            }
            else
            {
                ShowPanel();
            }
        }
    }
}