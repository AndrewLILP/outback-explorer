// PauseMenuSceneController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Core;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Controls the UI inside the separate, additively-loaded PauseMenuScene.
    /// Resume and New Game call back into PauseFlowManager.Instance (still
    /// alive in the gameplay scene underneath, since that scene stays
    /// loaded rather than being replaced). Save and Instructions are fully
    /// self-contained - no cross-scene call needed for either.
    ///
    /// Reminder for whoever builds the tutorial animation in the
    /// Instructions panel: Time.timeScale is 0 for the entire time this
    /// scene is loaded, so any animation driven from script must use
    /// Time.unscaledDeltaTime / Time.unscaledTime, not the scaled versions,
    /// or it will appear completely frozen.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PauseMenuSceneController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private bool showDebugLogs = true;

        private VisualElement root;
        private VisualElement instructionsPanel;
        private Button resumeButton;
        private Button saveButton;
        private Button newGameButton;
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
            resumeButton = root.Q<Button>("ResumeButton");
            saveButton = root.Q<Button>("SaveButton");
            newGameButton = root.Q<Button>("NewGameButton");
            instructionsButton = root.Q<Button>("InstructionsButton");
            closeInstructionsButton = root.Q<Button>("CloseInstructionsButton");

            if (resumeButton != null) resumeButton.clicked += OnResumeClicked;
            if (saveButton != null) saveButton.clicked += OnSaveClicked;
            if (newGameButton != null) newGameButton.clicked += OnNewGameClicked;
            if (instructionsButton != null) instructionsButton.clicked += OnInstructionsClicked;
            if (closeInstructionsButton != null) closeInstructionsButton.clicked += OnCloseInstructionsClicked;

            if (instructionsPanel != null) instructionsPanel.style.display = DisplayStyle.None;

            Log($"Ready after {framesWaited} frame(s)");
        }

        private void OnResumeClicked()
        {
            if (PauseFlowManager.Instance != null) PauseFlowManager.Instance.ExitPause();
            else Debug.LogError("[PauseMenuSceneController] PauseFlowManager.Instance not found - is it still loaded in the gameplay scene?");
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
            if (PauseFlowManager.Instance != null) PauseFlowManager.Instance.ExitPauseForNewGame();
            else Debug.LogError("[PauseMenuSceneController] PauseFlowManager.Instance not found - is it still loaded in the gameplay scene?");
        }

        private void OnInstructionsClicked()
        {
            if (instructionsPanel != null) instructionsPanel.style.display = DisplayStyle.Flex;
        }

        private void OnCloseInstructionsClicked()
        {
            if (instructionsPanel != null) instructionsPanel.style.display = DisplayStyle.None;
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[PauseMenuSceneController] {message}");
        }
    }
}
