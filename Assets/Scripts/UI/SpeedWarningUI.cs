// SpeedWarningUI.cs
// Location: Assets/Scripts/UI/SpeedWarningUI.cs
//
// Shows the centred "Take your time" message while SpeedLimitMonitor says
// the player is over the limit, and hides it when the warning clears.
//
// Lives on the HUD GameObject next to HUDController and reads the same
// UIDocument (HUD.uxml), but is its own class so HUDController keeps
// doing only its two jobs (speed comparison + discovery strip).
//
// The fade itself is done in USS (opacity transition) - this script only
// adds/removes a class, so look/feel can be tweaked without touching code.

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Player;

namespace RelaxingDrive.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class SpeedWarningUI : MonoBehaviour
    {
        private const string HiddenClass = "speed-warning--hidden";

        [Header("References")]
        [SerializeField] private UIDocument uiDocument;
        [Tooltip("Leave empty to find it automatically in the scene.")]
        [SerializeField] private SpeedLimitMonitor monitor;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private VisualElement warningPanel;

        private void Awake()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (monitor == null)
                monitor = Object.FindFirstObjectByType<SpeedLimitMonitor>();

            if (monitor == null)
                Debug.LogError("[SpeedWarningUI] No SpeedLimitMonitor found in the scene.");
        }

        private void OnEnable()
        {
            if (monitor != null)
            {
                monitor.OnWarningStarted += Show;
                monitor.OnWarningCleared += Hide;
            }

            StartCoroutine(InitializeWhenReady());
        }

        private void OnDisable()
        {
            if (monitor != null)
            {
                monitor.OnWarningStarted -= Show;
                monitor.OnWarningCleared -= Hide;
            }
        }

        /// <summary>
        /// Same wait-for-the-visual-tree pattern as HUDController and AnimalInfoUI.
        /// </summary>
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
                Debug.LogError("[SpeedWarningUI] rootVisualElement never became available.");
                yield break;
            }

            warningPanel = uiDocument.rootVisualElement.Q<VisualElement>("SpeedWarning");
            if (warningPanel == null)
            {
                Debug.LogError("[SpeedWarningUI] Could not find 'SpeedWarning' in HUD.uxml.");
                yield break;
            }

            Hide();
            if (showDebugLogs) Debug.Log($"[SpeedWarningUI] Ready after {framesWaited} frame(s)");
        }

        private void Show()
        {
            warningPanel?.RemoveFromClassList(HiddenClass);
        }

        private void Hide()
        {
            warningPanel?.AddToClassList(HiddenClass);
        }
    }
}