// PauseMenuSceneController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using RelaxingDrive.Core;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the UI inside PauseMenuScene, which is loaded non-additively
    /// (replacing the gameplay scene entirely - see PauseFlowManager).
    ///
    /// There is no cross-scene callback to PauseFlowManager: the gameplay
    /// scene - and PauseFlowManager with it - is already unloaded by the
    /// time this script runs. Resume / New Game / Change Car all load a
    /// scene directly. Save talks to GameSaveManager, which survives the
    /// scene swap because it's DontDestroyOnLoad.
    ///
    /// Reminder for whoever builds the tutorial animation in the
    /// Instructions panel: this scene is never time-scaled (nothing needs
    /// freezing once gameplay has fully unloaded), so no special
    /// unscaledDeltaTime handling is required here.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PauseMenuSceneController : MonoBehaviour
    {
        [Header("Scene Names")]
        [Tooltip("Exact name of the main gameplay scene in Build Settings.")]
        [SerializeField] private string gameplaySceneName = "RCC_City_CarSelectionWithLoadedScene";

        [Tooltip("Exact name of the car selection scene in Build Settings.")]
        [SerializeField] private string carSelectionSceneName = "RCC_City_CarSelectionWithLoadScene";

        [Header("References")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private bool showDebugLogs = true;

        private VisualElement root;
        private VisualElement pauseContainer;
        private VisualElement instructionsPanel;
        private Button resumeButton;
        private Button saveButton;
        private Button newGameButton;
        private Button changeCarsButton;
        private Button instructionsButton;
        private Button closeInstructionsButton;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            StartCoroutine(InitializeWhenReady());
        }

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
                Debug.LogError("[PauseMenuSceneController] rootVisualElement never became available - " +
                    "check that this UIDocument's Panel Settings AND Source Asset are both assigned.");
                yield break;
            }

            root = uiDocument.rootVisualElement;

            instructionsPanel = root.Q<VisualElement>("InstructionsPanel");
            pauseContainer = root.Q<VisualElement>("PauseContainer");
            resumeButton = root.Q<Button>("ResumeButton");
            saveButton = root.Q<Button>("SaveButton");
            newGameButton = root.Q<Button>("NewGameButton");
            changeCarsButton = root.Q<Button>("ChangeCarsButton");
            instructionsButton = root.Q<Button>("InstructionsButton");
            closeInstructionsButton = root.Q<Button>("CloseInstructionsButton");

            if (resumeButton != null) resumeButton.clicked += OnResumeClicked;
            if (saveButton != null) saveButton.clicked += OnSaveClicked;
            if (newGameButton != null) newGameButton.clicked += OnNewGameClicked;
            if (changeCarsButton != null) changeCarsButton.clicked += OnChangeCarsClicked;
            if (instructionsButton != null) instructionsButton.clicked += OnInstructionsClicked;
            if (closeInstructionsButton != null) closeInstructionsButton.clicked += OnCloseInstructionsClicked;

            if (instructionsPanel != null) instructionsPanel.style.display = DisplayStyle.None;

            // Defensive guard: at non-1x Game view scale, UI Toolkit can
            // occasionally fail to clear a button's pointer capture on
            // PointerUp, leaving the panel stuck and unresponsive until
            // Escape force-resets focus. Releasing capture explicitly on
            // every PointerUp (regardless of which element raised it) is a
            // cheap safety net. Should be a non-issue in an actual build,
            // where there is no Editor zoom to introduce the mismatch.
            root.RegisterCallback<PointerUpEvent>(OnAnyPointerUp, TrickleDown.TrickleDown);

            Log($"Ready after {framesWaited} frame(s)");
        }

        private void OnAnyPointerUp(PointerUpEvent evt)
        {
            if (root.panel.GetCapturingElement(evt.pointerId) != null)
                root.panel.ReleasePointer(evt.pointerId);
        }

        private void OnResumeClicked()
        {
            Log($"Resume - loading gameplay scene '{gameplaySceneName}'");
            SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }

        private void OnSaveClicked()
        {
            if (GameSaveManager.Instance != null)
            {
                GameSaveManager.Instance.Save();
                Log("Game saved");
            }
            else
            {
                Debug.LogError("[PauseMenuSceneController] GameSaveManager not found!");
            }
        }

        private void OnNewGameClicked()
        {
            if (GameSaveManager.Instance != null)
            {
                // NOTE: GameSaveManager.StartNewGame() currently reloads
                // SceneManager.GetActiveScene().name. Since the active scene
                // at this point is PauseMenuScene (not gameplay), that method
                // needs updating to reload gameplaySceneName explicitly
                // rather than "whatever scene is currently active".
                GameSaveManager.Instance.StartNewGame();
                Log("New game started - gameplay scene reloading");
            }
            else
            {
                Debug.LogError("[PauseMenuSceneController] GameSaveManager not found!");
            }
        }

        private void OnChangeCarsClicked()
        {
            Log($"Change car - loading car selection scene '{carSelectionSceneName}'");
            SceneManager.LoadScene(carSelectionSceneName, LoadSceneMode.Single);
        }

        private void OnInstructionsClicked()
        {
            if (instructionsPanel != null) instructionsPanel.style.display = DisplayStyle.Flex;
            if (pauseContainer != null) pauseContainer.style.display = DisplayStyle.None;
        }

        private void OnCloseInstructionsClicked()
        {
            if (instructionsPanel != null) instructionsPanel.style.display = DisplayStyle.None;
            if (pauseContainer != null) pauseContainer.style.display = DisplayStyle.Flex;
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[PauseMenuSceneController] {message}");
        }
    }
}
