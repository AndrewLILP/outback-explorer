// DialogueUIController.cs
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Core;
using RelaxingDrive.Data;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the dialogue UI using UI Toolkit.
    /// Observes DialogueManager and updates visual elements.
    /// Follows Single Responsibility - only handles UI updates.
    /// Unity 6 compatible - uses FindFirstObjectByType instead of singleton.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DialogueUIController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        // UI Toolkit elements (queried from UXML)
        private VisualElement dialoguePanel;
        private Label speakerNameLabel;
        private Label dialogueTextLabel;
        private Button continueButton;
        private VisualElement portraitImage;

        private void Awake()
        {
            // Get UIDocument component
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // Subscribe to DialogueManager events - use FindFirstObjectByType for Unity 6
            var dialogueManager = FindFirstObjectByType<DialogueManager>();

            if (dialogueManager != null)
            {
                dialogueManager.OnDialogueStarted += ShowDialogue;
                dialogueManager.OnDialogueLineChanged += UpdateDialogueLine;
                dialogueManager.OnDialogueEnded += HideDialogue;

                Debug.Log("DialogueUIController: Subscribed to DialogueManager events ✓");
            }
            else
            {
                Debug.LogWarning("DialogueUIController: DialogueManager not found in scene! Make sure DialogueManager GameObject exists.");
            }

            // Query UI elements from UXML
            SetupUIElements();
        }

        private void OnDisable()
        {
            // Unsubscribe from events - use FindFirstObjectByType for Unity 6
            var dialogueManager = FindFirstObjectByType<DialogueManager>();

            if (dialogueManager != null)
            {
                dialogueManager.OnDialogueStarted -= ShowDialogue;
                dialogueManager.OnDialogueLineChanged -= UpdateDialogueLine;
                dialogueManager.OnDialogueEnded -= HideDialogue;
            }
        }

        /// <summary>
        /// Query and cache UI elements from UXML
        /// </summary>
        private void SetupUIElements()
        {
            if (uiDocument == null)
            {
                Debug.LogError("DialogueUIController: UIDocument is null!");
                return;
            }

            var root = uiDocument.rootVisualElement;

            if (root == null)
            {
                Debug.LogError("DialogueUIController: Root visual element is null!");
                return;
            }

            // Query elements by name (must match UXML)
            dialoguePanel = root.Q<VisualElement>("DialoguePanel");
            speakerNameLabel = root.Q<Label>("SpeakerName");
            dialogueTextLabel = root.Q<Label>("DialogueText");
            continueButton = root.Q<Button>("ContinueButton");
            portraitImage = root.Q<VisualElement>("PortraitImage");

            // Verify elements were found
            if (dialoguePanel == null)
            {
                Debug.LogError("DialogueUIController: Could not find 'DialoguePanel' in UXML!");
                return;
            }

            if (speakerNameLabel == null)
            {
                Debug.LogWarning("DialogueUIController: Could not find 'SpeakerName' label");
            }

            if (dialogueTextLabel == null)
            {
                Debug.LogError("DialogueUIController: Could not find 'DialogueText' label!");
                return;
            }

            if (continueButton == null)
            {
                Debug.LogWarning("DialogueUIController: Could not find 'ContinueButton'");
            }

            Debug.Log("DialogueUIController: UI elements found ✓");

            // Setup button click event
            if (continueButton != null)
            {
                continueButton.clicked += OnContinueClicked;
            }

            // Hide dialogue panel initially
            if (dialoguePanel != null)
            {
                dialoguePanel.style.display = DisplayStyle.None;
                Debug.Log("DialogueUIController: Dialogue panel hidden initially ✓");
            }
        }

        /// <summary>
        /// Shows the dialogue panel
        /// </summary>
        private void ShowDialogue(DialogueData dialogueData)
        {
            Debug.Log($"DialogueUIController: ShowDialogue called for {dialogueData.speakerName}");

            if (dialoguePanel != null)
            {
                dialoguePanel.style.display = DisplayStyle.Flex;
                Debug.Log("DialogueUIController: Dialogue panel shown ✓");
            }
            else
            {
                Debug.LogError("DialogueUIController: Cannot show dialogue - panel is null!");
            }

            // Set portrait if available
            if (portraitImage != null && dialogueData.speakerPortrait != null)
            {
                portraitImage.style.backgroundImage = new StyleBackground(dialogueData.speakerPortrait);
            }
        }

        /// <summary>
        /// Updates the dialogue text and speaker name
        /// </summary>
        private void UpdateDialogueLine(string speakerName, string lineText)
        {
            Debug.Log($"DialogueUIController: UpdateDialogueLine - {speakerName}: {lineText}");

            if (speakerNameLabel != null)
            {
                speakerNameLabel.text = speakerName;
            }

            if (dialogueTextLabel != null)
            {
                dialogueTextLabel.text = lineText;
                Debug.Log("DialogueUIController: Dialogue text updated ✓");
            }
            else
            {
                Debug.LogError("DialogueUIController: Cannot update text - label is null!");
            }
        }

        /// <summary>
        /// Hides the dialogue panel
        /// </summary>
        private void HideDialogue()
        {
            Debug.Log("DialogueUIController: HideDialogue called");

            if (dialoguePanel != null)
            {
                dialoguePanel.style.display = DisplayStyle.None;
                Debug.Log("DialogueUIController: Dialogue panel hidden ✓");
            }
        }

        /// <summary>
        /// Called when continue button is clicked
        /// </summary>
        private void OnContinueClicked()
        {
            var dialogueManager = FindFirstObjectByType<DialogueManager>();

            if (dialogueManager != null)
            {
                dialogueManager.AdvanceDialogue();
            }
        }

        /// <summary>
        /// Alternative: Advance dialogue with keyboard (Space or E)
        /// Call this from Update() if you want keyboard support
        /// </summary>
        private void Update()
        {
            var dialogueManager = FindFirstObjectByType<DialogueManager>();

            if (dialogueManager != null && dialogueManager.IsDialogueActive)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
                {
                    dialogueManager.AdvanceDialogue();
                }
            }
        }
    }
}