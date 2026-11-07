// AwardPopupUI.cs
// Location: Assets/Scripts/UI/AwardPopupUI.cs
//
// Shows a centred award card (title + message) when AwardManager fires
// OnAwardEarned, then fades it out. If two awards land together they queue
// and show one after the other.
//
// Lives on the HUD GameObject next to HUDController and SpeedWarningUI and
// reads the same UIDocument (HUD.uxml, element "AwardPopup").
// Like SpeedWarningUI, the fade is a USS opacity transition - this script
// only adds/removes the hidden class.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using RelaxingDrive.Animals;

namespace RelaxingDrive.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class AwardPopupUI : MonoBehaviour
    {
        private const string HiddenClass = "award-popup--hidden";
        private const float FadeSeconds = 0.6f; // keep in sync with HUD.uss transition-duration

        [Header("References")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Settings")]
        [Tooltip("How long each award stays fully visible")]
        [SerializeField] private float displaySeconds = 4f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private VisualElement popup;
        private Label titleLabel;
        private Label messageLabel;

        private AwardManager awardManager;
        private readonly Queue<AwardData> pending = new Queue<AwardData>();
        private bool isShowing;

        private void Awake()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // Accessing Instance here also bootstraps AwardManager for the scene.
            awardManager = AwardManager.Instance;
            if (awardManager != null) awardManager.OnAwardEarned += Enqueue;

            StartCoroutine(InitializeWhenReady());
        }

        private void OnDisable()
        {
            if (awardManager != null) awardManager.OnAwardEarned -= Enqueue;
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
                Debug.LogError("[AwardPopupUI] rootVisualElement never became available.");
                yield break;
            }

            popup = uiDocument.rootVisualElement.Q<VisualElement>("AwardPopup");
            titleLabel = uiDocument.rootVisualElement.Q<Label>("AwardTitle");
            messageLabel = uiDocument.rootVisualElement.Q<Label>("AwardMessage");

            if (popup == null)
            {
                Debug.LogError("[AwardPopupUI] Could not find 'AwardPopup' in HUD.uxml.");
                yield break;
            }

            popup.AddToClassList(HiddenClass);
            if (showDebugLogs) Debug.Log($"[AwardPopupUI] Ready after {framesWaited} frame(s)");

            // An award might have arrived before the UI was ready.
            if (pending.Count > 0 && !isShowing) StartCoroutine(ShowQueue());
        }

        private void Enqueue(AwardData award)
        {
            pending.Enqueue(award);
            if (popup != null && !isShowing) StartCoroutine(ShowQueue());
        }

        private IEnumerator ShowQueue()
        {
            isShowing = true;

            while (pending.Count > 0)
            {
                AwardData award = pending.Dequeue();

                if (titleLabel != null) titleLabel.text = award.DisplayName;
                if (messageLabel != null) messageLabel.text = award.GetPopupMessage(awardManager.SpeciesCount);

                popup.RemoveFromClassList(HiddenClass);
                if (showDebugLogs) Debug.Log($"[AwardPopupUI] Showing {award.DisplayName}");

                // Realtime so the card still clears if the game is paused.
                yield return new WaitForSecondsRealtime(displaySeconds);

                popup.AddToClassList(HiddenClass);
                yield return new WaitForSecondsRealtime(FadeSeconds);
            }

            isShowing = false;
        }
    }
}