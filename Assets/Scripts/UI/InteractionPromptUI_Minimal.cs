// InteractionPromptUI_Minimal.cs
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Simplified version - easier to debug
/// Shows interaction prompts when near objects
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class InteractionPromptUI_Minimal : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool showDebugText = true;
    
    private UIDocument uiDocument;
    private Label promptLabel;
    private VisualElement promptContainer;
    private Label debugLabel;
    
    private void Awake()
    {
        Debug.Log("===== InteractionPromptUI_Minimal: AWAKE CALLED! =====");
        
        uiDocument = GetComponent<UIDocument>();
        
        if (uiDocument == null)
        {
            Debug.LogError("InteractionPromptUI_Minimal: No UIDocument component!");
            return;
        }
        
        Debug.Log("InteractionPromptUI_Minimal: UIDocument found ✓");
    }
    
    private void Start()
    {
        Debug.Log("===== InteractionPromptUI_Minimal: START CALLED! =====");
        SetupUI();
    }
    
    private void SetupUI()
    {
        if (uiDocument == null)
        {
            Debug.LogError("InteractionPromptUI_Minimal: UIDocument is null in SetupUI!");
            return;
        }
        
        var root = uiDocument.rootVisualElement;
        
        if (root == null)
        {
            Debug.LogError("InteractionPromptUI_Minimal: Root element is null!");
            return;
        }
        
        Debug.Log("InteractionPromptUI_Minimal: Root element found ✓");
        
        // Try to find elements from UXML
        promptContainer = root.Q<VisualElement>("InteractionPrompt");
        promptLabel = root.Q<Label>("PromptText");
        
        if (promptContainer == null)
        {
            Debug.LogWarning("InteractionPromptUI_Minimal: Could not find InteractionPrompt element. Creating one manually...");
            CreatePromptManually(root);
        }
        else
        {
            Debug.Log("InteractionPromptUI_Minimal: Found InteractionPrompt from UXML ✓");
            if (promptLabel != null)
            {
                Debug.Log("InteractionPromptUI_Minimal: Found PromptText label ✓");
            }
            else
            {
                Debug.LogWarning("InteractionPromptUI_Minimal: PromptText label not found in UXML");
            }
            
            // Hide initially
            promptContainer.style.display = DisplayStyle.None;
        }
        
        // Create debug display
        if (showDebugText)
        {
            CreateDebugDisplay(root);
        }
    }
    
    private void CreatePromptManually(VisualElement root)
    {
        // Create container
        promptContainer = new VisualElement();
        promptContainer.style.position = Position.Absolute;
        promptContainer.style.bottom = 150;
        promptContainer.style.left = new StyleLength(new Length(50, LengthUnit.Percent));
        promptContainer.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
        promptContainer.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        promptContainer.style.borderTopLeftRadius = 12;
        promptContainer.style.borderTopRightRadius = 12;
        promptContainer.style.borderBottomLeftRadius = 12;
        promptContainer.style.borderBottomRightRadius = 12;
        promptContainer.style.paddingTop = 20;
        promptContainer.style.paddingBottom = 20;
        promptContainer.style.paddingLeft = 30;
        promptContainer.style.paddingRight = 30;
        promptContainer.style.borderTopWidth = 3;
        promptContainer.style.borderBottomWidth = 3;
        promptContainer.style.borderLeftWidth = 3;
        promptContainer.style.borderRightWidth = 3;
        promptContainer.style.borderTopColor = new Color(0.4f, 0.8f, 1f);
        promptContainer.style.borderBottomColor = new Color(0.4f, 0.8f, 1f);
        promptContainer.style.borderLeftColor = new Color(0.4f, 0.8f, 1f);
        promptContainer.style.borderRightColor = new Color(0.4f, 0.8f, 1f);
        
        // Create label
        promptLabel = new Label("Press E to interact");
        promptLabel.style.fontSize = 24;
        promptLabel.style.color = Color.white;
        promptLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        promptLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        
        promptContainer.Add(promptLabel);
        root.Add(promptContainer);
        
        // Hide initially
        promptContainer.style.display = DisplayStyle.None;
        
        Debug.Log("InteractionPromptUI_Minimal: Created prompt manually ✓");
    }
    
    private void CreateDebugDisplay(VisualElement root)
    {
        debugLabel = new Label();
        debugLabel.style.position = Position.Absolute;
        debugLabel.style.top = 10;
        debugLabel.style.left = 10;
        debugLabel.style.color = Color.yellow;
        debugLabel.style.fontSize = 16;
        debugLabel.style.backgroundColor = new Color(0, 0, 0, 0.7f);
        debugLabel.style.paddingTop = 10;
        debugLabel.style.paddingBottom = 10;
        debugLabel.style.paddingLeft = 10;
        debugLabel.style.paddingRight = 10;
        debugLabel.text = "=== INTERACTION DEBUG ===\nUI System Active";
        
        root.Add(debugLabel);
        
        Debug.Log("InteractionPromptUI_Minimal: Debug display created ✓");
    }
    
    private void Update()
    {
        if (showDebugText && debugLabel != null)
        {
            UpdateDebugDisplay();
        }
        
        UpdatePrompt();
    }
    
    private void UpdateDebugDisplay()
    {
        string info = "=== INTERACTION DEBUG ===\n";
        info += "UI Script: RUNNING\n";
        info += $"Frame: {Time.frameCount}\n";
        
        // Try to find PlayerStateManager
        var playerStateManager = FindFirstObjectByType<RelaxingDrive.Player.PlayerStateManager>();
        
        if (playerStateManager != null)
        {
            info += $"State: {(playerStateManager.IsOnFoot ? "ON FOOT" : "DRIVING")}\n";
            
            if (playerStateManager.IsOnFoot && playerStateManager.PlayerCharacter != null)
            {
                // Check for interaction detector
                var detector = playerStateManager.PlayerCharacter.GetComponent<RelaxingDrive.World.PlayerInteractionDetector>();
                
                if (detector != null)
                {
                    info += $"Has Interactable: {detector.HasInteractable}\n";
                    
                    if (detector.HasInteractable)
                    {
                        info += $"Prompt: {detector.ClosestInteractable.GetInteractionPrompt()}\n";
                    }
                }
                else
                {
                    info += "⚠ No InteractionDetector\n";
                }
            }
        }
        else
        {
            info += "⚠ No PlayerStateManager\n";
        }
        
        debugLabel.text = info;
    }
    
    private void UpdatePrompt()
    {
        if (promptContainer == null || promptLabel == null)
            return;
        
        // Try to find PlayerStateManager
        var playerStateManager = FindFirstObjectByType<RelaxingDrive.Player.PlayerStateManager>();
        
        if (playerStateManager == null || !playerStateManager.IsOnFoot)
        {
            HidePrompt();
            return;
        }
        
        // Check for interactables
        if (playerStateManager.PlayerCharacter != null)
        {
            var detector = playerStateManager.PlayerCharacter.GetComponent<RelaxingDrive.World.PlayerInteractionDetector>();
            
            if (detector != null && detector.HasInteractable)
            {
                ShowPrompt(detector.ClosestInteractable.GetInteractionPrompt());
                return;
            }
            
            // Check distance to car
            if (playerStateManager.CarGameObject != null)
            {
                float distance = Vector3.Distance(
                    playerStateManager.PlayerCharacter.transform.position,
                    playerStateManager.CarGameObject.transform.position
                );
                
                if (distance <= 3f)
                {
                    ShowPrompt("Press E to enter vehicle");
                    return;
                }
            }
        }
        
        HidePrompt();
    }
    
    private void ShowPrompt(string text)
    {
        if (promptContainer != null && promptLabel != null)
        {
            promptLabel.text = text;
            promptContainer.style.display = DisplayStyle.Flex;
        }
    }
    
    private void HidePrompt()
    {
        if (promptContainer != null)
        {
            promptContainer.style.display = DisplayStyle.None;
        }
    }
}