// HUDController.cs
using UnityEngine;
using UnityEngine.UIElements;
using PolyStang; // Reference to your car controller namespace
using RelaxingDrive.Animals;
using System.Collections;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the HUD elements using UI Toolkit.
    /// Displays speed and tracks animal discovery progress.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CarController carController;
        [SerializeField] private UIDocument uiDocument;

        [Header("Settings")]
        [SerializeField] private float speedMultiplier = 4f; // Same as car controller
        [SerializeField] private int totalAnimalSpecies = 4; // Total unique animals

        // Speedometer UI Elements
        private Label speedLabel;
        private VisualElement speedometer;

        // Discovery Progress UI Elements
        private Label progressText;
        private VisualElement kangarooIcon;
        private VisualElement emuIcon;
        private VisualElement echidnaIcon;
        private VisualElement devilIcon;
        private Label kangarooLabel;
        private Label emuLabel;
        private Label echidnaLabel;
        private Label devilLabel;

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

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void SetupUIElements()
        {
            var root = uiDocument.rootVisualElement;

            // Query speedometer elements
            speedLabel = root.Q<Label>("SpeedLabel");
            speedometer = root.Q<VisualElement>("Speedometer");

            // Query discovery progress elements
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

            // Initialize animal icon colors (add CSS classes)
            InitializeAnimalIcons();

            // Initialize discovery display
            UpdateDiscoveryDisplay();
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
        }

        /// <summary>
        /// Updates the speed display based on car's rigidbody velocity
        /// </summary>
        private void UpdateSpeed()
        {
            if (carController == null || speedLabel == null)
                return;

            // Get the car's rigidbody (same logic as CarController)
            Rigidbody carRb = carController.GetComponent<Rigidbody>();

            if (carRb != null)
            {
                int roundedSpeed = (int)Mathf.Round(carRb.linearVelocity.magnitude * speedMultiplier);
                speedLabel.text = $"{roundedSpeed}";
            }
        }

        /// <summary>
        /// Handles when an animal is discovered
        /// </summary>
        private void HandleAnimalDiscovered(AnimalData animalData, bool isFirstDiscovery)
        {
            if (animalData == null) return;

            // Update the specific animal's UI
            UpdateAnimalUI(animalData.AnimalName, isFirstDiscovery);

            // Update overall progress display
            UpdateDiscoveryDisplay();
        }

        /// <summary>
        /// Updates UI for a specific animal (icon + label)
        /// </summary>
        private void UpdateAnimalUI(string animalName, bool isFirstDiscovery)
        {
            VisualElement icon = null;
            Label label = null;

            // Get the correct icon and label based on animal name
            switch (animalName.ToLower())
            {
                case "kangaroo":
                    icon = kangarooIcon;
                    label = kangarooLabel;
                    break;
                case "emu":
                    icon = emuIcon;
                    label = emuLabel;
                    break;
                case "echidna":
                    icon = echidnaIcon;
                    label = echidnaLabel;
                    break;
                case "tasmanian devil":
                    icon = devilIcon;
                    label = devilLabel;
                    break;
                default:
                    Debug.LogWarning($"HUDController: Unknown animal name '{animalName}'");
                    return;
            }

            if (icon != null)
            {
                // Add "discovered" class to make icon fully visible
                icon.AddToClassList("discovered");

                // Trigger pulse animation if first discovery
                if (isFirstDiscovery)
                {
                    StartCoroutine(PulseIcon(icon));
                }
            }

            if (label != null)
            {
                // Update label text and style
                label.text = $"✓ {animalName}";
                label.RemoveFromClassList("undiscovered");
                label.AddToClassList("discovered");
            }
        }

        /// <summary>
        /// Updates the progress text (e.g., "Discovered: 2/4 (50%)")
        /// </summary>
        private void UpdateDiscoveryDisplay()
        {
            if (progressText == null || AnimalDiscoveryManager.Instance == null)
                return;

            int discoveredCount = AnimalDiscoveryManager.Instance.GetDiscoveryCount();
            float percentage = (float)discoveredCount / totalAnimalSpecies * 100f;

            progressText.text = $"Discovered: {discoveredCount}/{totalAnimalSpecies} ({percentage:F0}%)";
        }

        /// <summary>
        /// Pulse animation for newly discovered animal icon.
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
    }
}