// HUDController.cs
using UnityEngine;
using UnityEngine.UIElements;
using PolyStang; // Reference to your car controller namespace

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the HUD elements using UI Toolkit.
    /// Works with the existing CarController to display speed.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CarController carController;
        [SerializeField] private UIDocument uiDocument;
        
        [Header("Settings")]
        [SerializeField] private float speedMultiplier = 4f; // Same as car controller
        
        // UI Elements
        private Label speedLabel;
        private VisualElement speedometer;
        
        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
        }
        
        private void OnEnable()
        {
            SetupUIElements();
        }
        
        private void SetupUIElements()
        {
            var root = uiDocument.rootVisualElement;
            
            // Query UI elements
            speedLabel = root.Q<Label>("SpeedLabel");
            speedometer = root.Q<VisualElement>("Speedometer");
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
        /// Public method to set the car controller reference at runtime
        /// </summary>
        public void SetCarController(CarController controller)
        {
            carController = controller;
        }
    }
}