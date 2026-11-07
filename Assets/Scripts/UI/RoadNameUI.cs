// RoadNameUI.cs
// Location: Assets/Scripts/UI/RoadNameUI.cs
//
// Shows the road name (e.g. "Koala Close") at the top-centre of the screen
// when a RoadNameTrigger announces it, then fades out after 3 seconds.
//
//  - Entering another road while one is showing swaps the name and restarts the timer.
//  - If the "Take your time" speed warning appears, the road name hides at once.
//
// Lives on the HUD GameObject next to HUDController, SpeedWarningUI and
// AwardPopupUI, and reads the same UIDocument (HUD.uxml, element "RoadName").
// The fade is a USS opacity transition - this script only adds/removes a class.

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Player;
using RelaxingDrive.World;

namespace RelaxingDrive.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class RoadNameUI : MonoBehaviour
    {
        private const string HiddenClass = "road-name--hidden";

        [Header("References")]
        [SerializeField] private UIDocument uiDocument;
        [Tooltip("Leave empty to find it automatically in the scene.")]
        [SerializeField] private SpeedLimitMonitor speedMonitor;

        [Header("Settings")]
        [Tooltip("How long the road name stays fully visible before fading")]
        [SerializeField] private float displaySeconds = 3f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private VisualElement panel;
        private Label nameLabel;
        private Coroutine hideRoutine;

        private void Awake()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (speedMonitor == null) speedMonitor = FindFirstObjectByType<SpeedLimitMonitor>();
        }

        private void OnEnable()
        {
            RoadNameTrigger.OnRoadEntered += Show;
            if (speedMonitor != null) speedMonitor.OnWarningStarted += HideNow;

            StartCoroutine(InitializeWhenReady());
        }

        private void OnDisable()
        {
            // Static events outlive the scene, so always unsubscribe.
            RoadNameTrigger.OnRoadEntered -= Show;
            if (speedMonitor != null) speedMonitor.OnWarningStarted -= HideNow;
        }

        /// <summary>Same wait-for-the-visual-tree pattern as the other HUD scripts.</summary>
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
                Debug.LogError("[RoadNameUI] rootVisualElement never became available.");
                yield break;
            }

            panel = uiDocument.rootVisualElement.Q<VisualElement>("RoadName");
            nameLabel = uiDocument.rootVisualElement.Q<Label>("RoadNameText");

            if (panel == null || nameLabel == null)
            {
                Debug.LogError("[RoadNameUI] Could not find 'RoadName' / 'RoadNameText' in HUD.uxml.");
                yield break;
            }

            panel.AddToClassList(HiddenClass);
            if (showDebugLogs) Debug.Log($"[RoadNameUI] Ready after {framesWaited} frame(s)");
        }

        private void Show(RoadData road)
        {
            if (panel == null || road == null) return;

            // Don't cover the speed warning - it's the more important message.
            if (IsSpeedWarningShowing()) return;

            nameLabel.text = road.DisplayName;
            panel.RemoveFromClassList(HiddenClass);

            // Restart the 3 s timer if a road name was already showing.
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            // Realtime so it still clears if the game is paused.
            yield return new WaitForSecondsRealtime(displaySeconds);
            panel.AddToClassList(HiddenClass);
            hideRoutine = null;
        }

        private void HideNow()
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
            panel?.AddToClassList(HiddenClass);
        }

        /// <summary>True while the "Take your time" card is visible in the HUD.</summary>
        private bool IsSpeedWarningShowing()
        {
            VisualElement warning = uiDocument.rootVisualElement.Q<VisualElement>("SpeedWarning");
            return warning != null && !warning.ClassListContains("speed-warning--hidden");
        }
    }
}