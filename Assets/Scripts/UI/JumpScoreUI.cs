using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Displays running total score and a brief popup for each scored landing.
/// Subscribe this to a CarJumpScorer via the Inspector (drag the car's CarJumpScorer
/// into the scorer field, or call Bind() at runtime for spawned vehicles).
/// </summary>
public class JumpScoreUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarJumpScorer scorer;
    [SerializeField] private TMP_Text totalScoreText;
    [SerializeField] private TMP_Text popupText;

    [Header("Popup behaviour")]
    [SerializeField] private float popupDuration = 1.5f;
    [SerializeField] private string missedMessage = "Missed!";

    private Coroutine popupRoutine;

    void OnEnable()
    {
        RCC_SceneManager.OnVehicleChanged += HandleVehicleChanged;

    if (RCC_SceneManager.Instance.activePlayerVehicle != null)
        HandleVehicleChanged();
    }

    private void HandleVehicleChanged()
{
    var activeVehicle = RCC_SceneManager.Instance.activePlayerVehicle;
    Debug.Log($"[JumpScoreUI] OnVehicleChanged fired. activeVehicle = {activeVehicle}");

    if (activeVehicle == null)
        return;

    var newScorer = activeVehicle.GetComponent<CarJumpScorer>();
    Debug.Log($"[JumpScoreUI] Found CarJumpScorer: {newScorer}");

    if (newScorer != null)
        Bind(newScorer);
}

    void OnDisable()
    {
        if (scorer != null)
        {
            scorer.OnScored.RemoveListener(HandleScored);
            scorer.OnMissedLanding.RemoveListener(HandleMissed);
        }
    }

    /// <summary>
    /// Attaches this UI to a CarJumpScorer at runtime (e.g. if the player vehicle
    /// is spawned rather than placed in the scene).
    /// </summary>
    public void Bind(CarJumpScorer target)
    {
        if (scorer != null)
        {
            scorer.OnScored.RemoveListener(HandleScored);
            scorer.OnMissedLanding.RemoveListener(HandleMissed);
        }

        scorer = target;
        scorer.OnScored.AddListener(HandleScored);
        scorer.OnMissedLanding.AddListener(HandleMissed);

        UpdateTotal();
    }

    private void HandleScored(LandingResult result)
    {
        UpdateTotal();
        ShowPopup($"+{result.points}  ({result.distanceFromCenter:F1}m, {result.airTime:F2}s air)");
    }

    private void HandleMissed()
    {
        ShowPopup(missedMessage);
    }

    private void UpdateTotal()
    {
        if (totalScoreText != null)
            totalScoreText.text = $"Score: {scorer.TotalScore}";
    }

    private void ShowPopup(string message)
    {
        if (popupText == null)
            return;

        if (popupRoutine != null)
            StopCoroutine(popupRoutine);

        popupRoutine = StartCoroutine(PopupCoroutine(message));
    }

    private IEnumerator PopupCoroutine(string message)
    {
        popupText.text = message;
        popupText.alpha = 1f;

        yield return new WaitForSeconds(popupDuration);

        // Simple fade out
        float t = 0f;
        const float fadeTime = 0.4f;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            popupText.alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            yield return null;
        }

        popupText.text = "";
    }
}