// SpeedGovernor.cs
// Caps the car's speed at whatever limit the current zone sets.
// It doesn't touch RCC's engine or gearing. It just removes horizontal speed above the limit, gently.
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpeedGovernor : MonoBehaviour
{
    [Tooltip("Limit applied at game start (km/h). Spawn is in town, so 60.")]
    [SerializeField] private float startLimitKmh = 60f;

    [Tooltip("How quickly excess speed is removed (m/s per second). ~6–10 feels like easing off, not slamming brakes.")]
    [SerializeField] private float easeOffRate = 8f;

    private float lastSpeed;   // horizontal speed after the previous governed step (m/s)


    public float CurrentLimitKmh { get; private set; }
    public bool IsLimiting { get; private set; }   // UI can read this, e.g. a "Take your time" hint

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        CurrentLimitKmh = startLimitKmh;
    }

    /// <summary>Called by SpeedZoneGate when the car drives through it.</summary>
    public void SetLimit(float limitKmh) => CurrentLimitKmh = limitKmh;

    // Physics runs in FixedUpdate, so velocity changes go here, not in Update.
    private void FixedUpdate()
    {
        Vector3 v = rb.linearVelocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        float speed = flat.magnitude;
        float limit = CurrentLimitKmh / 3.6f;

        // Ceiling = the limit, OR (if we're still easing down from a faster zone)
        // last step's speed minus the ease-off amount. Either way the car can't gain speed while over the limit.
        float ceiling = Mathf.Max(limit, lastSpeed - easeOffRate * Time.fixedDeltaTime);

        IsLimiting = speed > ceiling;
        if (IsLimiting)
        {
            flat = flat.normalized * ceiling;
            rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
            speed = ceiling;
        }

        lastSpeed = speed;
    }
}