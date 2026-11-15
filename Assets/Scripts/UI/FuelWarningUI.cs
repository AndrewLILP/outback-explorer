// FuelWarningUI.cs
// Location: Assets/Scripts/UI/FuelWarningUI.cs
//
// NEW (Nov 2026 - fuel): shows fuel messages in a card styled exactly like
// the speed warning (it reuses the speed-warning USS classes), but it is its
// OWN element ("FuelWarning" in HUD.uxml) so the two scripts never fight over
// the same labels.
//
// Messages:
//   - Low fuel (from FuelMonitor):  "Fuel at 25%" / "Pull into a servo when you can"  (4 s)
//   - Refuelling (from FuelPump):   "Refuelling"  / "63%"                              (until full)
//   - Full (from FuelPump):         "Tank full"   / "Hop back in when you're ready"   (3 s)
//   - NEW (full) Parked with a full tank: "Tank's already full" / "98% - enjoy the drive" (3 s)
//
// Lives on the HUD GameObject next to HUDController and SpeedWarningUI.
//
// UPDATED (Nov 2026 - self-find): the panel is found lazily. If the assigned
// UIDocument doesn't contain "FuelWarning", every UIDocument in the scene is
// searched for it. Every step logs, so a silent failure is impossible.
// Changes marked "NEW (find)".

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Player;
using RelaxingDrive.World;

namespace RelaxingDrive.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class FuelWarningUI : MonoBehaviour
    {
        // Reused from the speed warning - same fade transition in HUD.uss.
        private const string HiddenClass = "speed-warning--hidden";

        [Header("References")]
        [SerializeField] private UIDocument uiDocument;
        [Tooltip("Leave empty to find it automatically in the scene.")]
        [SerializeField] private FuelMonitor monitor;

        [Header("Timing")]
        [SerializeField] private float lowFuelShowSeconds = 4f;
        [SerializeField] private float tankFullShowSeconds = 3f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private VisualElement panel;
        private Label titleLabel;
        private Label detailLabel;
        private Coroutine hideRoutine;
        private bool refuelling;

        private void Awake()
        {
            if (showDebugLogs) Debug.Log($"[FuelWarningUI] Awake on '{name}'"); // NEW (find)

            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (monitor == null)
                monitor = Object.FindFirstObjectByType<FuelMonitor>();

            if (monitor == null)
                Debug.LogWarning("[FuelWarningUI] No FuelMonitor in the scene - low-fuel warnings won't show (refuel messages still will).");
        }

        private void OnEnable()
        {
            if (monitor != null)
                monitor.OnFuelLow += HandleFuelLow;

            FuelPump.AnyRefuelStarted += HandleRefuelStarted;
            FuelPump.AnyRefuelProgress += HandleRefuelProgress;
            FuelPump.AnyRefuelCompleted += HandleRefuelCompleted;
            FuelPump.AnyTankAlreadyFull += HandleTankAlreadyFull; // NEW (full)

            StartCoroutine(InitializeWhenReady());
        }

        private void OnDisable()
        {
            if (monitor != null)
                monitor.OnFuelLow -= HandleFuelLow;

            // Static events outlive this object - ALWAYS unsubscribe, or a
            // destroyed UI keeps getting called after a scene reload.
            FuelPump.AnyRefuelStarted -= HandleRefuelStarted;
            FuelPump.AnyRefuelProgress -= HandleRefuelProgress;
            FuelPump.AnyRefuelCompleted -= HandleRefuelCompleted;
            FuelPump.AnyTankAlreadyFull -= HandleTankAlreadyFull; // NEW (full)
        }

        /// <summary>
        /// Same wait-for-the-visual-tree pattern as SpeedWarningUI and HUDController,
        /// then finds the panel (NEW (find): searching every UIDocument if needed).
        /// </summary>
        private IEnumerator InitializeWhenReady()
        {
            // Give every UIDocument a few frames to build its visual tree.
            for (int i = 0; i < 5; i++)
                yield return null;

            if (TryFindPanel())
                Hide();
        }

        /// <summary>
        /// NEW (find): looks in the assigned UIDocument first, then in every
        /// UIDocument in the scene. Safe to call again later - it does nothing
        /// once the panel has been found.
        /// </summary>
        private bool TryFindPanel()
        {
            if (panel != null) return true;

            // 1. The assigned document (normally the HUD).
            if (uiDocument != null && uiDocument.rootVisualElement != null)
                panel = uiDocument.rootVisualElement.Q<VisualElement>("FuelWarning");

            // 2. Fallback: any UIDocument in the scene that has a "FuelWarning" element.
            if (panel == null)
            {
                foreach (UIDocument doc in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                {
                    if (doc.rootVisualElement == null) continue;
                    VisualElement found = doc.rootVisualElement.Q<VisualElement>("FuelWarning");
                    if (found == null) continue;

                    uiDocument = doc;
                    panel = found;
                    if (showDebugLogs) Debug.Log($"[FuelWarningUI] Found 'FuelWarning' in UIDocument on '{doc.name}'");
                    break;
                }
            }

            if (panel == null)
            {
                Debug.LogError("[FuelWarningUI] No UIDocument in the scene contains an element named 'FuelWarning'. " +
                               "The HUD's UIDocument is still using a HUD.uxml without the fuel block - check its Source Asset.");
                return false;
            }

            titleLabel = panel.Q<Label>("FuelWarningText");
            detailLabel = panel.Q<Label>("FuelWarningDetail");

            if (showDebugLogs)
                Debug.Log($"[FuelWarningUI] Ready (panel in '{uiDocument.name}', labels found: {titleLabel != null}/{detailLabel != null})");
            return true;
        }

        // ---------------------------------------------------------------
        // Event handlers
        // ---------------------------------------------------------------
        private void HandleFuelLow(int threshold)
        {
            if (showDebugLogs) Debug.Log($"[FuelWarningUI] Fuel low event: {threshold}%"); // NEW (find)
            if (refuelling) return; // refuel messages take priority

            string detail = threshold <= 10 ? "Time to find a servo" : "Pull into a servo when you can";
            ShowFor($"Fuel at {threshold}%", detail, lowFuelShowSeconds);
        }

        private void HandleRefuelStarted()
        {
            if (showDebugLogs) Debug.Log("[FuelWarningUI] Refuel started event"); // NEW (find)
            refuelling = true;
            Show("Refuelling", "");
        }

        private void HandleRefuelProgress(float percent)
        {
            // Floor, so it reads 99% until it is truly full, then "Tank full".
            if (detailLabel != null)
                detailLabel.text = $"{Mathf.FloorToInt(percent)}%";
        }

        private void HandleRefuelCompleted()
        {
            if (showDebugLogs) Debug.Log("[FuelWarningUI] Refuel completed event"); // NEW (find)
            refuelling = false;
            ShowFor("Tank full", "Hop back in when you're ready", tankFullShowSeconds);
        }

        /// <summary>NEW (full): parked at the pump, but no fuel needed.</summary>
        private void HandleTankAlreadyFull(float percent)
        {
            if (showDebugLogs) Debug.Log($"[FuelWarningUI] Tank already full event: {percent:0}%"); // NEW (find)
            ShowFor("Tank's already full", $"{Mathf.FloorToInt(percent)}% - enjoy the drive", tankFullShowSeconds);
        }

        // ---------------------------------------------------------------
        // Show / hide
        // ---------------------------------------------------------------
        private void Show(string title, string detail)
        {
            // NEW (find): try again now, in case the HUD wasn't ready at start.
            if (!TryFindPanel()) return;

            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            if (titleLabel != null) titleLabel.text = title;
            if (detailLabel != null) detailLabel.text = detail;

            panel?.RemoveFromClassList(HiddenClass);
        }

        private void ShowFor(string title, string detail, float seconds)
        {
            Show(title, detail);
            hideRoutine = StartCoroutine(HideAfter(seconds));
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            hideRoutine = null;
            Hide();
        }

        private void Hide()
        {
            panel?.AddToClassList(HiddenClass);
        }
    }
}