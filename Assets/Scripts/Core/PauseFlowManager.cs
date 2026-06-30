// PauseFlowManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using RelaxingDrive.Core;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Owns the pause flow for the gameplay scene. Escape additively loads a
    /// separate PauseMenuScene on top of gameplay, instead of an overlapping
    /// UIDocument competing with RCC's uGUI Canvas for input - that approach
    /// hit a documented Unity limitation: once any uGUI EventSystem exists in
    /// the loaded scenes, it mediates ALL pointer input by comparing
    /// PanelSettings Sort Order against Canvas sortingOrder, and getting that
    /// to reliably favour our panel proved unreliable in practice.
    ///
    /// The gameplay scene stays loaded and frozen (Time.timeScale = 0)
    /// underneath while paused, so Resume returns to the exact same moment -
    /// no respawn, no lost car position. As a second, belt-and-suspenders
    /// safeguard against the input conflict, RCC's Canvas (and any other
    /// uGUI Canvas assigned below) is also disabled while paused.
    ///
    /// PauseMenuSceneController (living in PauseMenuScene) calls back into
    /// this singleton for Resume/New Game, since both scripts are alive
    /// simultaneously once that scene is loaded additively.
    /// </summary>
    public class PauseFlowManager : MonoBehaviour
    {
        public static PauseFlowManager Instance { get; private set; }

        [Header("Pause Scene")]
        [Tooltip("Must exactly match the scene name in Build Settings.")]
        [SerializeField] private string pauseSceneName = "PauseMenuScene";

        [Header("Canvases to disable while paused")]
        [Tooltip("RCC's dashboard Canvas (and any other uGUI Canvas) - belt-and-suspenders against the uGUI EventSystem/UI Toolkit Sort Order conflict.")]
        [SerializeField] private GameObject[] canvasesToDisableWhilePaused;

        [SerializeField] private bool showDebugLogs = true;

        private bool isPaused;
        public bool IsPaused => isPaused;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!isPaused && Input.GetKeyDown(KeyCode.Escape))
                EnterPause();
        }

        public void EnterPause()
        {
            if (isPaused) return;
            isPaused = true;

            SetCanvasesActive(false);
            Time.timeScale = 0f;
            SceneManager.LoadScene(pauseSceneName, LoadSceneMode.Additive);

            Log("Entered pause - loaded pause scene additively");
        }

        /// <summary>Called by PauseMenuSceneController's Resume button.</summary>
        public void ExitPause()
        {
            if (!isPaused) return;

            SceneManager.UnloadSceneAsync(pauseSceneName);
            Time.timeScale = 1f;
            SetCanvasesActive(true);
            isPaused = false;

            Log("Exited pause - unloaded pause scene");
        }

        /// <summary>
        /// Called by PauseMenuSceneController's New Game button.
        /// Time.timeScale MUST be restored before GameSaveManager reloads the
        /// gameplay scene (LoadSceneMode.Single under the hood), or the
        /// freshly-reloaded scene would start frozen at timeScale 0. That
        /// same scene reload also implicitly unloads PauseMenuScene - no
        /// separate UnloadSceneAsync call needed here.
        /// </summary>
        public void ExitPauseForNewGame()
        {
            Time.timeScale = 1f;
            isPaused = false;

            if (GameSaveManager.Instance != null)
            {
                GameSaveManager.Instance.StartNewGame();
                Log("New game started - gameplay scene reloading");
            }
            else
            {
                Debug.LogError("[PauseFlowManager] GameSaveManager not found!");
            }
        }

        private void SetCanvasesActive(bool active)
        {
            if (canvasesToDisableWhilePaused == null) return;
            foreach (GameObject canvas in canvasesToDisableWhilePaused)
            {
                if (canvas != null) canvas.SetActive(active);
            }
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[PauseFlowManager] {message}");
        }
    }
}
