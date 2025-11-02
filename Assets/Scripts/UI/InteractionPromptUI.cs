// InteractionPromptUI.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the interaction prompt UI that shows messages like "Press E to Enter Car".
    /// Uses UI Toolkit for rendering.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("UI Element Names")]
        [SerializeField] private string promptContainerName = "interaction-prompt-container";
        [SerializeField] private string promptTextName = "prompt-text";

        private VisualElement promptContainer;
        private Label promptText;
        private bool isVisible = false;

        private void Awake()
        {
            // Get UI Document if not assigned
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                Debug.LogError("[InteractionPromptUI] No UIDocument found! Add a UIDocument component.");
                return;
            }
        }

        private void Start()
        {
            InitializeUI();
            HidePrompt(); // Start hidden
        }

        private void InitializeUI()
        {
            if (uiDocument == null) return;

            var root = uiDocument.rootVisualElement;

            // Try to find existing elements
            promptContainer = root.Q<VisualElement>(promptContainerName);
            promptText = root.Q<Label>(promptTextName);

            // If not found, create them programmatically
            if (promptContainer == null)
            {
                promptContainer = new VisualElement();
                promptContainer.name = promptContainerName;
                promptContainer.AddToClassList("interaction-prompt-container");
                root.Add(promptContainer);

                Debug.Log("[InteractionPromptUI] Created prompt container programmatically");
            }

            if (promptText == null)
            {
                promptText = new Label("Press E");
                promptText.name = promptTextName;
                promptText.AddToClassList("prompt-text");

                // Create prompt box
                var promptBox = new VisualElement();
                promptBox.AddToClassList("interaction-prompt");
                promptBox.Add(promptText);

                promptContainer.Add(promptBox);

                Debug.Log("[InteractionPromptUI] Created prompt text programmatically");
            }
        }

        /// <summary>
        /// Shows the interaction prompt with the specified message.
        /// </summary>
        public void ShowPrompt(string message)
        {
            if (promptText == null)
            {
                Debug.LogWarning("[InteractionPromptUI] Cannot show prompt - UI not initialized");
                return;
            }

            promptText.text = message;

            if (promptContainer != null)
            {
                promptContainer.style.display = DisplayStyle.Flex;
            }

            isVisible = true;
        }

        /// <summary>
        /// Hides the interaction prompt.
        /// </summary>
        public void HidePrompt()
        {
            if (promptContainer != null)
            {
                promptContainer.style.display = DisplayStyle.None;
            }

            isVisible = false;
        }

        /// <summary>
        /// Returns whether the prompt is currently visible.
        /// </summary>
        public bool IsVisible => isVisible;
    }
}