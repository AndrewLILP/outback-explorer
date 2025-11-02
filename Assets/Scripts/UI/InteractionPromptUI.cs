// InteractionPromptUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.World;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the interaction prompt UI using UI Toolkit.
    /// Shows "Press E to [action]" when player is near an interactable object.
    /// Observes PlayerInteractionDetector to know when to show/hide.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteractionDetector playerDetector;
        [SerializeField] private UIDocument uiDocument;

        [Header("Settings")]
        [SerializeField] private string defaultPromptText = "Press E to interact";

        // UI Elements
        private VisualElement interactionPrompt;
        private Label interactionIcon;
        private Label interactionText;

        // State
        private IInteractable currentInteractable;

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

            // Query elements
            interactionPrompt = root.Q<VisualElement>("InteractionPrompt");
            interactionIcon = root.Q<Label>("InteractionIcon");
            interactionText = root.Q<Label>("InteractionText");

            // Start hidden
            HidePrompt();
        }

        private void Update()
        {
            if (playerDetector == null)
                return;

            // Check if player has an interactable nearby
            if (playerDetector.HasInteractable)
            {
                currentInteractable = playerDetector.ClosestInteractable;
                ShowPrompt(currentInteractable.GetInteractionPrompt());
            }
            else
            {
                currentInteractable = null;
                HidePrompt();
            }
        }

        /// <summary>
        /// Shows the prompt with custom text
        /// </summary>
        private void ShowPrompt(string promptText)
        {
            if (interactionPrompt == null)
                return;

            // Update text
            if (interactionText != null)
            {
                interactionText.text = promptText;
            }

            // Show prompt
            if (interactionPrompt.style.display == DisplayStyle.None)
            {
                interactionPrompt.style.display = DisplayStyle.Flex;
            }
        }

        /// <summary>
        /// Hides the interaction prompt
        /// </summary>
        private void HidePrompt()
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Public method to set the player detector reference
        /// </summary>
        public void SetPlayerDetector(PlayerInteractionDetector detector)
        {
            playerDetector = detector;
        }
    }
}