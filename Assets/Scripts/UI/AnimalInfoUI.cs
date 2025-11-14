// AnimalInfoUI.cs
// UI Controller that displays animal information when player is nearby
// Subscribes to AnimalDiscoveryManager events and triggers fade animations

using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Animals;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the Animal Info Panel UI.
    /// Subscribes to discovery events and updates UI with animal data.
    /// Handles fade in/out animations when player enters/exits range.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class AnimalInfoUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("StyleSheet containing animations and styling")]
        [SerializeField] private StyleSheet animalInfoStyleSheet;

        [Header("Fade Settings")]
        [Tooltip("Duration of fade in/out animation")]
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("Player Tracking")]
        [Tooltip("Player GameObject to track proximity")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Update interval for proximity checks")]
        [SerializeField] private float proximityCheckInterval = 0.5f;

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
        private Vector3 currentAnimalPosition;
        private bool isTrackingAnimal = false;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null)
            {
                Debug.LogError("AnimalInfoUI: UIDocument component not found!", this);
                enabled = false;
                return;
            }

            // Find player if not assigned
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
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

            // Subscribe to discovery events
            if (AnimalDiscoveryManager.Instance != null)
            {
                AnimalDiscoveryManager.Instance.OnAnimalDiscovered += OnAnimalDiscovered;
                Debug.Log("AnimalInfoUI: Subscribed to AnimalDiscoveryManager events");
            }
            else
            {
                Debug.LogWarning("AnimalInfoUI: AnimalDiscoveryManager not found!");
            }

            // Start proximity checking
            InvokeRepeating(nameof(CheckPlayerProximity), 1f, proximityCheckInterval);
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            if (AnimalDiscoveryManager.Instance != null)
            {
                AnimalDiscoveryManager.Instance.OnAnimalDiscovered -= OnAnimalDiscovered;
            }

            // Stop proximity checking
            CancelInvoke(nameof(CheckPlayerProximity));
        }

        /// <summary>
        /// Called when an animal is discovered (first time or repeat).
        /// This is triggered BY AnimalController when player enters range.
        /// </summary>
        private void OnAnimalDiscovered(AnimalData animalData, bool isFirstTime)
        {
            if (animalData == null) return;

            currentAnimal = animalData;

            // Find the animal's position in world (for proximity tracking)
            FindAnimalPosition(animalData);

            // Update UI content
            UpdateAnimalInfo(animalData, isFirstTime);

            // Show panel with fade in
            ShowPanel();
            isTrackingAnimal = true;

            if (showDebugMessages)
            {
                Debug.Log($"AnimalInfoUI: Displaying info for {animalData.AnimalName} (First time: {isFirstTime})");
            }
        }

        /// <summary>
        /// Finds the world position of the animal being displayed.
        /// </summary>
        private void FindAnimalPosition(AnimalData animalData)
        {
            // Find all AnimalControllers in scene
            AnimalController[] controllers = FindObjectsByType<AnimalController>(FindObjectsSortMode.None);

            foreach (AnimalController controller in controllers)
            {
                // Check if this controller has the same AnimalData
                // Note: We can't directly compare, so we'll use name matching
                if (controller.gameObject.name.Contains(animalData.AnimalName))
                {
                    currentAnimalPosition = controller.transform.position;
                    return;
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
        }

        /// <summary>
        /// Shows the panel with fade in animation.
        /// </summary>
        public void ShowPanel()
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
        public void HidePanel()
        {
            if (!isPanelVisible) return;

            rootPanel.RemoveFromClassList("animal-panel--visible");
            rootPanel.AddToClassList("animal-panel--hidden");
            isPanelVisible = false;
            isTrackingAnimal = false;

            if (showDebugMessages)
            {
                Debug.Log("AnimalInfoUI: Panel hidden");
            }
        }

        /// <summary>
        /// Checks if player is still in range of current animal.
        /// Called periodically via InvokeRepeating.
        /// </summary>
        private void CheckPlayerProximity()
        {
            if (!isTrackingAnimal || currentAnimal == null || playerTransform == null) return;

            float distance = Vector3.Distance(
                playerTransform.position,
                currentAnimalPosition
            );

            // Hide panel if player moved too far away (with 20% buffer to prevent flicker)
            if (distance > currentAnimal.DiscoveryRange * 1.2f)
            {
                HidePanel();
            }
        }

        /// <summary>
        /// Manual method to show specific animal info (for testing).
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

        private void OnDestroy()
        {
            CancelInvoke(nameof(CheckPlayerProximity));
        }
    }
}