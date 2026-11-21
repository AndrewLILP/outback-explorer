// BalloonMover.cs
// Location: Assets/Scripts/World/Balloon/BalloonMover.cs
//
// Carries a hot air balloon round a looping PathRoute. The player controls
// HEIGHT only, the real way:
//   Burner (W / Up / RT)   - heats the air, the balloon climbs
//   Vent   (S / Down / LT) - lets hot air out, the balloon sinks
//   Hands off              - the air cools, the balloon drifts slowly down
// Speed comes from the wind, and the wind is stronger higher up. So:
//   climb to go faster, sink low to slow down, sit at the bottom to stop.
// No sideways control: the route decides where you go.
//
// Life cycle (enum state machine, like RiverCraftMover):
//   Docked     - on the ground at the route's first point. BalloonLaunchPoint offers "Climb in".
//   Ascending  - automatic lift-off to cruising height. No player control.
//   Flying     - the player controls height (and so speed).
//   Descending - automatic landing over the last stretch back to the start.
//   Landed     - the player steps off; after a short pause it's ready again.
//
// No Rigidbody, so nothing can tip or crash - cosy, and cheap on Steam Deck.
//
// SETUP:
//   HotAirBalloon        <- empty, scale 1,1,1, pivot = bottom of the basket.
//                           Add BalloonMover (Route = Balloon_Route) and BalloonLaunchPoint.
//     Sway               <- empty. Drag into 'Sway Visual'
//       Basket           <- cube (1.4, 1.1, 1.4) at local (0, 0.55, 0)
//       Envelope         <- sphere (8, 9, 8) at local (0, 8, 0). REMOVE its collider
//       BurnerFlame      <- small orange sphere at (0, 3, 0), inactive. Drag into 'Burner Effect'
//       StandPoint       <- empty at local (0, 0.2, 0). Drag into 'Stand Point'
//   Optional: an empty "Balloon_StepOff" on the ground beside the launch pad
//   (NOT a child of the balloon). Drag into 'Step Off Spot'.
//   With the balloon selected, the orange line shows the height ceiling.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RelaxingDrive.World
{
    public class BalloonMover : MonoBehaviour
    {
        public enum BalloonMode { Docked, Ascending, Flying, Descending, Landed }

        [Header("Route")]
        [SerializeField] private PathRoute route;

        [Header("Balloon")]
        [Tooltip("Where the player's feet go. Put it under the Sway Visual so the player sways with the basket.")]
        [SerializeField] private Transform standPoint;
        [Tooltip("Optional. Where the player stands after landing, blue arrow facing the way to walk. Empty = beside the basket.")]
        [SerializeField] private Transform stepOffSpot;

        [Header("Height")]
        [Tooltip("Metres the balloon may climb above each route point. A BalloonPathPoint on a point overrides this.")]
        [SerializeField, Min(1f)] private float defaultMaxHeight = 15f;
        [Tooltip("Upward push at full burner (m/s²).")]
        [SerializeField, Min(0f)] private float burnerLift = 1.2f;
        [Tooltip("Downward push with the vent fully open (m/s²).")]
        [SerializeField, Min(0f)] private float ventSink = 1.5f;
        [Tooltip("Gentle downward push when hands off - the air cooling (m/s²).")]
        [SerializeField, Min(0f)] private float coolingSink = 0.25f;
        [Tooltip("Air resistance on climbing/sinking. Top vertical speed ≈ push ÷ drag.")]
        [SerializeField, Min(0.01f)] private float verticalDrag = 0.6f;

        [Header("Wind (speed)")]
        [Tooltip("Wind speed at the ceiling, in m/s.")]
        [SerializeField, Min(0f)] private float maxWindSpeed = 6f;
        [Tooltip("How the wind grows with height. X: 0 = floor, 1 = ceiling. Y: 0 = calm, 1 = Max Wind Speed. " +
                 "Starting at 0 means sitting at the bottom = stopped.")]
        [SerializeField] private AnimationCurve windByHeight = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("How quickly the balloon's speed catches up with the wind. Lower = lazier.")]
        [SerializeField, Min(0.01f)] private float speedSmoothing = 0.5f;

        [Header("Take-off and landing (automatic)")]
        [Tooltip("Lift-off climbs to this fraction of the allowed height, then hands control to the player.")]
        [SerializeField, Range(0.1f, 1f)] private float takeOffHeight = 0.5f;
        [Tooltip("Landing starts this many metres before the end of the route.")]
        [SerializeField, Min(5f)] private float landingDistance = 40f;
        [Tooltip("Speed as the basket touches down, in m/s.")]
        [SerializeField, Min(0.1f)] private float touchDownSpeed = 1.5f;
        [Tooltip("Seconds after landing before 'Climb in' is offered again.")]
        [SerializeField, Min(0f)] private float readyAgainDelay = 3f;

        [Header("Feel")]
        [Tooltip("How quickly the balloon turns to follow the route. Lower = lazier.")]
        [SerializeField, Min(0.05f)] private float turnSmoothing = 0.5f;
        [SerializeField, Min(0f)] private float bobHeight = 0.15f;
        [SerializeField, Min(0f)] private float bobCyclesPerSecond = 0.15f;

        [Header("Sway (visual only)")]
        [SerializeField] private Transform swayVisual;
        [SerializeField, Min(0f)] private float rollDegrees = 1f;
        [SerializeField, Min(0f)] private float pitchDegrees = 0.6f;
        [SerializeField, Min(0f)] private float swayCyclesPerSecond = 0.12f;

        [Header("Burner feedback (optional)")]
        [Tooltip("Shown while the burner is on (flame mesh or particle object).")]
        [SerializeField] private GameObject burnerEffect;
        [Tooltip("Burner roar. Loop ON, Play On Awake OFF - this script fades it.")]
        [SerializeField] private AudioSource burnerAudio;
        [SerializeField, Range(0f, 1f)] private float burnerVolume = 0.6f;

        [Header("Prototype input")]
        [Tooltip("TEMPORARY: read burner/vent directly while flying. Later replaced by PlayerInputReader calling SetLiftInput().")]
        [SerializeField] private bool debugReadInput = true;

        /// <summary>Raised when lift-off begins (hook for flight awards later).</summary>
        public event Action OnFlightStarted;
        /// <summary>Raised once when the basket touches down - BallooningState steps the player off.</summary>
        public event Action OnFlightFinished;

        private BalloonMode mode = BalloonMode.Docked;
        private float distance;          // metres travelled along the route
        private float altitude;          // metres above the route (the floor)
        private float verticalVelocity;  // m/s, + = climbing
        private float speed;             // m/s along the route
        private float liftInput;         // -1 (vent) .. +1 (burner)
        private float descentStartAltitude;
        private float descentStartSpeed;
        private float descentStartRemaining;
        private float readyTimer;
        private float[] pointMaxHeights; // cached per-point ceilings
        private Transform rider;

        public BalloonMode Mode => mode;
        public bool IsDocked => mode == BalloonMode.Docked;
        public PathRoute Route => route;
        public Transform StandPoint => standPoint != null ? standPoint : transform;
        public float Altitude => altitude; // for a HUD altimeter later
        public float Speed => speed;

        private void Start()
        {
            if (route == null || !route.IsValid)
            {
                Debug.LogWarning($"[BalloonMover] '{name}' has no valid PathRoute (needs at least 2 child points).");
                enabled = false;
                return;
            }

            if (!route.Loop)
                Debug.LogWarning($"[BalloonMover] '{route.name}' isn't set to Loop - the balloon will jump back to the start after landing.");

            // Read each point's ceiling once, instead of GetComponent every frame.
            pointMaxHeights = new float[route.PointCount];
            for (int i = 0; i < route.PointCount; i++)
                pointMaxHeights[i] = ReadMaxHeight(route.Points[i]);

            // Sit on the ground at the first point, facing along the route.
            route.Evaluate(0f, out Vector3 position, out Vector3 forward);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));

            if (burnerEffect != null) burnerEffect.SetActive(false);
            if (burnerAudio != null) burnerAudio.volume = 0f;
            mode = BalloonMode.Docked;
        }

        /// <summary>Called by BallooningState once the player is in the basket.</summary>
        public void Launch()
        {
            if (mode != BalloonMode.Docked) return;

            distance = 0f;
            altitude = 0f;
            verticalVelocity = 0f;
            speed = 0f;
            liftInput = 0f;
            mode = BalloonMode.Ascending;
            OnFlightStarted?.Invoke();
        }

        /// <summary>-1 = vent fully open, +1 = full burner. Whoever owns input calls this each frame.</summary>
        public void SetLiftInput(float value) => liftInput = Mathf.Clamp(value, -1f, 1f);

        public void SetRider(Transform newRider) => rider = newRider;
        public void ClearRider() => rider = null;

        private void Update()
        {
            float dt = Time.deltaTime;
            bool burning = false;

            switch (mode)
            {
                case BalloonMode.Ascending:  burning = UpdateAscending(dt); break;
                case BalloonMode.Flying:     burning = UpdateFlying(dt);    break;
                case BalloonMode.Descending: UpdateDescending(dt);          break;
                case BalloonMode.Landed:     UpdateLanded(dt);              break;
                // Docked: sits still on the ground.
            }

            PlaceOnRoute(dt);
            ApplySway();
            UpdateBurnerFeedback(burning, dt);
            CarryRider();
        }

        // ---------- Modes ----------

        private bool UpdateAscending(float dt)
        {
            // Full burner up to cruising height. The wind is calm near the ground,
            // so it rises almost straight up, then starts to drift.
            float maxHeight = MaxHeightAt(distance);
            ApplyVertical(1f, maxHeight, dt);
            ApplyWind(maxHeight, dt);

            if (altitude >= maxHeight * takeOffHeight)
                mode = BalloonMode.Flying;

            return true;
        }

        private bool UpdateFlying(float dt)
        {
            if (debugReadInput) SetLiftInput(ReadDebugLift());

            float maxHeight = MaxHeightAt(distance);
            ApplyVertical(liftInput, maxHeight, dt);
            ApplyWind(maxHeight, dt);

            if (route.TotalLength - distance <= landingDistance)
                BeginDescent();

            return liftInput > 0f;
        }

        private void BeginDescent()
        {
            descentStartAltitude = altitude;
            descentStartSpeed = Mathf.Max(speed, touchDownSpeed); // never stall short of the pad
            descentStartRemaining = Mathf.Max(route.TotalLength - distance, 0.01f);
            verticalVelocity = 0f;
            liftInput = 0f;
            mode = BalloonMode.Descending;
        }

        private void UpdateDescending(float dt)
        {
            // progress: 0 where the landing began, 1 at the pad.
            float remaining = route.TotalLength - distance;
            float progress = 1f - Mathf.Clamp01(remaining / descentStartRemaining);

            // SmoothStep eases in AND out, so the basket settles gently.
            altitude = Mathf.SmoothStep(descentStartAltitude, 0f, progress);
            speed = Mathf.Lerp(descentStartSpeed, touchDownSpeed, progress);
            distance += speed * dt;

            if (distance >= route.TotalLength) Land();
        }

        private void Land()
        {
            distance = route.TotalLength;
            altitude = 0f;
            speed = 0f;
            verticalVelocity = 0f;
            readyTimer = readyAgainDelay;
            mode = BalloonMode.Landed;
            OnFlightFinished?.Invoke();
        }

        private void UpdateLanded(float dt)
        {
            readyTimer -= dt;
            if (readyTimer > 0f) return;

            // On a loop the end IS the start, so resetting distance doesn't move it.
            distance = 0f;
            mode = BalloonMode.Docked;
        }

        // ---------- Flight model ----------

        /// <summary>
        /// Push (burner, vent or cooling) changes vertical speed; drag pulls it back
        /// towards zero. Together they settle at a steady "terminal" speed = push ÷ drag.
        /// </summary>
        private void ApplyVertical(float lift, float maxHeight, float dt)
        {
            float push = lift > 0f ? lift * burnerLift
                       : lift < 0f ? lift * ventSink   // lift is negative here, so this pushes down
                       : -coolingSink;

            verticalVelocity += push * dt;
            verticalVelocity *= Mathf.Exp(-verticalDrag * dt); // frame-rate independent drag
            altitude += verticalVelocity * dt;

            // Floor: the route points themselves.
            if (altitude <= 0f)
            {
                altitude = 0f;
                if (verticalVelocity < 0f) verticalVelocity = 0f;
            }

            // Ceiling: if it drops ahead (a lower Max Height), ease down rather than snap.
            if (altitude > maxHeight)
            {
                altitude = Mathf.Lerp(altitude, maxHeight, 1f - Mathf.Exp(-2f * dt));
                if (verticalVelocity > 0f) verticalVelocity = 0f;
            }
        }

        /// <summary>Wind speed depends on height (0..1 of the allowed band); speed eases towards it.</summary>
        private void ApplyWind(float maxHeight, float dt)
        {
            float height01 = maxHeight > 0f ? Mathf.Clamp01(altitude / maxHeight) : 0f;
            float targetSpeed = maxWindSpeed * Mathf.Max(0f, windByHeight.Evaluate(height01));

            speed = Mathf.Lerp(speed, targetSpeed, 1f - Mathf.Exp(-speedSmoothing * dt));
            distance = Mathf.Min(distance + speed * dt, route.TotalLength);
        }

        // ---------- Per-point ceiling ----------

        private float ReadMaxHeight(Transform point)
        {
            BalloonPathPoint settings = point.GetComponent<BalloonPathPoint>();
            return settings != null && settings.MaxHeight > 0f ? settings.MaxHeight : defaultMaxHeight;
        }

        private float PointMaxHeight(int index) =>
            Application.isPlaying && pointMaxHeights != null && index < pointMaxHeights.Length
                ? pointMaxHeights[index]
                : ReadMaxHeight(route.Points[index]); // editor gizmos: read live

        /// <summary>The ceiling at 'atDistance', blended between the two nearest points.</summary>
        private float MaxHeightAt(float atDistance)
        {
            route.GetPointBlend(atDistance, out int from, out int to, out float t);
            return Mathf.Lerp(PointMaxHeight(from), PointMaxHeight(to), t);
        }

        // ---------- Placement and visuals ----------

        private void PlaceOnRoute(float dt)
        {
            route.Evaluate(distance, out Vector3 floor, out Vector3 forward);

            // Bob only when airborne (fades in over the first 2 m) so it sits still on the ground.
            float airborne = Mathf.Clamp01(altitude / 2f);
            float bob = Mathf.Sin(Time.time * bobCyclesPerSecond * Mathf.PI * 2f) * bobHeight * airborne;
            transform.position = floor + Vector3.up * (altitude + bob);

            if (mode == BalloonMode.Docked || mode == BalloonMode.Landed) return;

            // 1 - e^(-k*dt): same smoothness at 30 FPS (Deck) and 60+ FPS (PC).
            Quaternion target = Quaternion.LookRotation(forward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSmoothing * dt));
        }

        private void ApplySway()
        {
            if (swayVisual == null) return;

            float airborne = Mathf.Clamp01(altitude / 2f);
            float phase = Time.time * swayCyclesPerSecond * Mathf.PI * 2f;
            float roll = Mathf.Sin(phase) * rollDegrees * airborne;
            float pitch = Mathf.Sin(phase * 0.73f + 1.3f) * pitchDegrees * airborne;
            swayVisual.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        private void UpdateBurnerFeedback(bool burning, float dt)
        {
            if (burnerEffect != null && burnerEffect.activeSelf != burning)
                burnerEffect.SetActive(burning);

            if (burnerAudio == null) return;

            // ~0.5 s fade in/out so the roar doesn't click on and off.
            float target = burning ? burnerVolume : 0f;
            burnerAudio.volume = Mathf.MoveTowards(burnerAudio.volume, target, dt * 2f);

            if (burnerAudio.volume > 0.001f && !burnerAudio.isPlaying) burnerAudio.Play();
            else if (burnerAudio.volume <= 0.001f && burnerAudio.isPlaying) burnerAudio.Stop();
        }

        /// <summary>Same as the SUP: feet on the StandPoint, body upright, yaw only.</summary>
        private void CarryRider()
        {
            if (rider == null) return;
            rider.SetPositionAndRotation(StandPoint.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        }

        /// <summary>Where the player stands after landing, snapped onto the ground.</summary>
        public void GetStepOffPose(out Vector3 position, out Quaternion rotation)
        {
            if (stepOffSpot != null)
            {
                position = stepOffSpot.position;
                Vector3 facing = stepOffSpot.forward;
                facing.y = 0f;
                rotation = Quaternion.LookRotation(facing.sqrMagnitude > 0.0001f ? facing : transform.forward, Vector3.up);
            }
            else
            {
                position = transform.position + transform.right * 2.5f;
                rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            }

            if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
            }
        }

        /// <summary>TEMPORARY prototype input (Input System, not legacy). Triggers are analogue: squeeze gently for a little heat.</summary>
        private float ReadDebugLift()
        {
            float value = 0f;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) value += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) value -= 1f;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float pad = gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();
                if (Mathf.Abs(pad) > Mathf.Abs(value)) value = pad;
            }

            return Mathf.Abs(value) < 0.05f ? 0f : Mathf.Clamp(value, -1f, 1f);
        }

        // ---------- Editor visuals ----------

        private void OnDrawGizmosSelected()
        {
            if (route == null) return;
            if (!Application.isPlaying) route.Rebuild();
            if (!route.IsValid) return;

            // Orange line = the ceiling along the whole route.
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
            const float step = 5f;
            Vector3 previous = Vector3.zero;
            for (float d = 0f; ; d += step)
            {
                float clamped = Mathf.Min(d, route.TotalLength);
                route.Evaluate(clamped, out Vector3 floor, out _);
                Vector3 ceiling = floor + Vector3.up * MaxHeightAt(clamped);
                if (d > 0f) Gizmos.DrawLine(previous, ceiling);
                previous = ceiling;
                if (clamped >= route.TotalLength) break;
            }

            // Posts from each point (floor) up to its ceiling.
            for (int i = 0; i < route.PointCount; i++)
            {
                Vector3 p = route.Points[i].position;
                Gizmos.DrawLine(p, p + Vector3.up * PointMaxHeight(i));
            }
        }
    }
}