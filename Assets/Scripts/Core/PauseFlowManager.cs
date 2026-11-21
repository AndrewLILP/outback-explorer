// PauseFlowManager.cs
// Location: Assets/Scripts/Core/PauseFlowManager.cs
//
// UPDATED (Nov 2026): captures the car's position before the gameplay scene
// unloads, so the pause menu can offer "Where I left off". Changes are marked
// "NEW (resume choice)".
using UnityEngine;
using UnityEngine.SceneManagement;
using RelaxingDrive.Core;      // NEW (resume choice)
using RelaxingDrive.Player;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Owns entry into the pause flow from the gameplay scene.
    ///
    /// Input: listens to PlayerInputReader.OnPausePressed (Esc on keyboard,
    /// Start / Menu on gamepad) instead of reading keys itself.
    ///
    /// Approach: scene REPLACEMENT, not an additive overlay. Pressing Pause
    /// loads PauseMenuScene non-additively, which unloads the gameplay scene
    /// entirely. This avoids the layering/input-routing conflicts hit when
    /// two scenes (each with their own Camera/UIDocument/PanelSettings) were
    /// active at once - see Pause_Menu_Scene_Summary for the abandoned
    /// additive-overlay attempt.
    ///
    /// Because the gameplay scene unloads on pause, this component is
    /// destroyed along with it - that's expected and fine. There is no
    /// cross-scene callback: PauseMenuSceneController (living in
    /// PauseMenuScene) doesn't call back into this class at all. It talks
    /// directly to SceneManager and to GameSaveManager, which stays alive
    /// because it's DontDestroyOnLoad.
    ///
    /// NEW (resume choice): because the scene reloads fresh on Resume, the car's
    /// position is snapshotted into ResumeSpawnHandoff (a static class that
    /// survives scene changes) just before unloading. ResumeSpawnController
    /// puts the car back if the player picks "Where I left off".
    /// </summary>
    public class PauseFlowManager : MonoBehaviour
    {
        [Header("Pause Scene")]
        [Tooltip("Must exactly match the scene name in Build Settings.")]
        [SerializeField] private string pauseSceneName = "PauseMenuScene";

        // NEW (resume choice)
        [Header("Resume Position")]
        [Tooltip("Captures the car's position before pausing. Leave empty to always resume at the start.")]
        [SerializeField] private ResumeSpawnController resumeSpawn;

        [SerializeField] private bool showDebugLogs = true;

        // The reader we subscribed to, kept so we unsubscribe from the SAME object.
        private PlayerInputReader inputReader;

        // Guard: stops a second press loading the pause scene twice
        // while the first load is still in progress.
        private bool isEnteringPause = false;

        private void Start()
        {
            // Subscribe in Start, not OnEnable: every Awake() in the scene has run
            // by now, so PlayerInputReader.Instance is guaranteed to be set
            // (if a reader exists in the scene at all).
            inputReader = PlayerInputReader.Instance;

            if (inputReader == null)
            {
                Debug.LogWarning("[PauseFlowManager] No PlayerInputReader in the scene - pause input won't work.");
                return;
            }

            inputReader.OnPausePressed += EnterPause;
        }

        private void OnDestroy()
        {
            // Always unsubscribe. If we didn't, the reader could call EnterPause
            // on this destroyed object later.
            if (inputReader != null)
            {
                inputReader.OnPausePressed -= EnterPause;
            }
        }

        public void EnterPause()
        {
            if (isEnteringPause) return;
            isEnteringPause = true;

            // NEW (resume choice): snapshot BEFORE the scene unloads - after
            // LoadScene the car and everything else here will be gone.
            if (resumeSpawn != null)
                resumeSpawn.Capture();
            else
                ResumeSpawnHandoff.Clear();

            Log($"Entering pause - loading '{pauseSceneName}' (replaces gameplay scene)");
            SceneManager.LoadScene(pauseSceneName, LoadSceneMode.Single);
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[PauseFlowManager] {message}");
        }
    }
}