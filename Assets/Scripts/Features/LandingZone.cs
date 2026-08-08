using UnityEngine;

/// <summary>
/// A trigger volume that scores a car's landing based on how close it lands
/// to a "sweet spot" and how level (upright) the landing was.
/// Also tracks car entry/exit so CarJumpScorer knows which zone it's over.
/// </summary>
public class LandingZone : MonoBehaviour
{
    [Header("Scoring shape")]
    [Tooltip("Landing at or inside this radius from sweetSpot scores full points.")]
    [SerializeField] private float maxScoreRadius = 1f;

    [Tooltip("Landing at or beyond this radius from sweetSpot scores zero.")]
    [SerializeField] private float zoneRadius = 5f;

    [Tooltip("Points awarded for a perfect (centered, level) landing.")]
    [SerializeField] private int maxPoints = 100;

    [Header("References")]
    [Tooltip("The ideal landing point. Defaults to this transform if left empty.")]
    [SerializeField] private Transform sweetSpot;

    void Awake()
    {
        if (sweetSpot == null)
            sweetSpot = transform;
    }

    /// <summary>
    /// Computes score for a landing at the given transform. Called by CarJumpScorer
    /// when CarJumpTracker reports a landing while the car is over this zone.
    /// </summary>
    public LandingResult ScoreLanding(Transform car, float airTime)
    {
        float dist = Vector3.Distance(
            new Vector3(car.position.x, 0f, car.position.z),
            new Vector3(sweetSpot.position.x, 0f, sweetSpot.position.z));

        float positionScore = Mathf.Clamp01(1f - (dist - maxScoreRadius) / (zoneRadius - maxScoreRadius));

        float uprightDot = Vector3.Dot(car.up, Vector3.up); // 1 = flat/level landing
        float stabilityScore = Mathf.Clamp01(uprightDot);

        int points = Mathf.RoundToInt(maxPoints * positionScore * stabilityScore);

        return new LandingResult
        {
            points = points,
            distanceFromCenter = dist,
            airTime = airTime
        };
    }

    // --- Zone entry/exit tracking ---
    // A separate, larger trigger collider on this GameObject (or a child) drives these.
    // Kept independent of ground state: this only tracks "is the car currently over the zone."

    void OnTriggerEnter(Collider other)
    {
        var scorer = other.GetComponentInParent<CarJumpScorer>();
        if (scorer != null)
            scorer.SetCurrentZone(this);
    }

    void OnTriggerExit(Collider other)
    {
        var scorer = other.GetComponentInParent<CarJumpScorer>();
        if (scorer != null)
            scorer.SetCurrentZone(null);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Transform spot = sweetSpot != null ? sweetSpot : transform;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spot.position, maxScoreRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(spot.position, zoneRadius);
    }
#endif
}