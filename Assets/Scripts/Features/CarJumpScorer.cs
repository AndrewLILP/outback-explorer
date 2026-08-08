using UnityEngine;

/// <summary>
/// Tracks which LandingZone the car is currently over, and scores a landing
/// (via CarJumpTracker's OnLanded event) if the car was over a zone when it touched down.
/// </summary>
[RequireComponent(typeof(CarJumpTracker))]
public class CarJumpScorer : MonoBehaviour
{
    [Header("Scoring")]
    [Tooltip("If true, logs each scored landing to the console for debugging.")]
    [SerializeField] private bool debugLog = false;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent<LandingResult> OnScored;
    public UnityEngine.Events.UnityEvent OnMissedLanding; // landed outside any zone

    private CarJumpTracker tracker;
    private LandingZone currentZone;

    // Exposed for UI/other systems that want the last result without subscribing to the event.
    public LandingResult LastResult { get; private set; }
    public int TotalScore { get; private set; }

    void Awake()
    {
        tracker = GetComponent<CarJumpTracker>();
    }

    void OnEnable()
    {
        tracker.OnLanded += HandleLanded;
    }

    void OnDisable()
    {
        tracker.OnLanded -= HandleLanded;
    }

    /// <summary>
    /// Called by LandingZone's trigger when the car enters/exits its bounds.
    /// This just tracks "am I currently over a zone" — actual scoring happens on landing.
    /// </summary>
    public void SetCurrentZone(LandingZone zone)
    {
        currentZone = zone;
    }

    private void HandleLanded(float airTime)
    {
        if (currentZone == null)
        {
            OnMissedLanding?.Invoke();

            if (debugLog)
                Debug.Log($"{name} landed outside any zone. Airtime: {airTime:F2}s");

            return;
        }

        LandingResult result = currentZone.ScoreLanding(transform, airTime);
        LastResult = result;
        TotalScore += result.points;

        OnScored?.Invoke(result);

        if (debugLog)
            Debug.Log($"{name} scored {result.points} pts | dist {result.distanceFromCenter:F2}m | airtime {result.airTime:F2}s");
    }
}

/// <summary>
/// Data describing a single scored landing. Plain struct so it can be passed
/// through UnityEvents and read by UI/scoring systems without extra plumbing.
/// </summary>
[System.Serializable]
public struct LandingResult
{
    public int points;
    public float distanceFromCenter;
    public float airTime;
}