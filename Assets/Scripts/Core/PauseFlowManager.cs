// PauseFlowManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RelaxingDrive.UI
{
    /// <summary>
    /// Owns entry into the pause flow from the gameplay scene.
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
    /// Known, accepted tradeoff: player/car position resets on Resume, since
    /// the gameplay scene reloads fresh rather than being preserved frozen
    /// underneath. Revisit later if position preservation is wanted.
    /// </summary>
    public class PauseFlowManager : MonoBehaviour
    {
        [Header("Pause Scene")]
        [Tooltip("Must exactly match the scene name in Build Settings.")]
        [SerializeField] private string pauseSceneName = "PauseMenuScene";

        [SerializeField] private bool showDebugLogs = true;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                EnterPause();
        }

        public void EnterPause()
        {
            Log($"Entering pause - loading '{pauseSceneName}' (replaces gameplay scene)");
            SceneManager.LoadScene(pauseSceneName, LoadSceneMode.Single);
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[PauseFlowManager] {message}");
        }
    }
}
