// FrillneckInteraction.cs
// Choreographed territorial interaction between two frilled-neck lizards
// (Chlamydosaurus kingii) for the car selection scene.
// Attach to an empty GameObject. Assign both lizard references in the Inspector.
// Uses the legacy Animation component - no Animator Controller required.
//
// IMPORTANT - Inspector setup on each lizard's Rigidbody:
//   Constraints → Freeze Rotation X ✓
//   Constraints → Freeze Rotation Z ✓
//   (Leave Y free so they can turn naturally)
//
// Behaviour based on real Chlamydosaurus kingii:
//   - Territorial encounters are primarily DISPLAY-based, not physical
//   - Both lizards frill-up at each other in a mutual standoff (the iconic moment)
//   - The actual attack is a brief bluff lunge — fights risk injury so are avoided
//   - The loser runs away in the famous fast bipedal run
//   - The winner performs a short victory display before returning to normal
//
// Sequence:
//   Both eat → Lizard1 spots Lizard2 → walks toward it → Lizard1 frills up (aggressive) →
//   Lizard2 responds with its own display → mutual standoff → Lizard1 lunges (attack) →
//   Lizard2 runs away → Lizard1 victory display → both settle and eat → repeat

using System.Collections;
using UnityEngine;

namespace RelaxingDrive.World
{
    public class FrillneckInteraction : MonoBehaviour
    {
        // ── Inspector Fields ─────────────────────────────────────────────────

        [Header("Lizard References")]
        [Tooltip("Lizard 1 – the territorial one that initiates the display")]
        [SerializeField] private GameObject lizard1;

        [Tooltip("Lizard 2 – responds to the display then retreats")]
        [SerializeField] private GameObject lizard2;

        [Header("Timing (seconds)")]
        [Tooltip("How long both lizards eat at the start before the sequence begins")]
        [SerializeField] private float initialEatDuration = 4f;

        [Tooltip("How long Lizard 1 holds its frill display before Lizard 2 responds")]
        [SerializeField] private float lizard1DisplayDuration = 1.8f;

        [Tooltip("How long both lizards display at each other during the mutual standoff")]
        [SerializeField] private float mutualStandoffDuration = 2.5f;

        [Tooltip("How long the attack/lunge plays before Lizard 2 runs")]
        [SerializeField] private float attackDuration = 0.8f;

        [Tooltip("How long Lizard 1 holds its victory display after the chase")]
        [SerializeField] private float victoryDisplayDuration = 1.5f;

        [Tooltip("How long Lizard 2 idles nervously at its flee position")]
        [SerializeField] private float fleeIdleDuration = 3.5f;

        [Tooltip("How long both lizards eat together before the loop repeats")]
        [SerializeField] private float settleDuration = 5f;

        [Tooltip("Pause before the whole sequence repeats")]
        [SerializeField] private float loopDelay = 1f;

        [Header("Movement")]
        [Tooltip("Walk speed when approaching (metres per second)")]
        [SerializeField] private float walkSpeed = 1.2f;

        [Tooltip("Run speed when Lizard 2 flees — fast bipedal sprint (metres per second)")]
        [SerializeField] private float fleeRunSpeed = 5f;

        [Tooltip("Walk speed when Lizard 2 cautiously returns (metres per second)")]
        [SerializeField] private float returnWalkSpeed = 0.9f;

        [Tooltip("Walk speed when Lizard 1 returns to start (metres per second)")]
        [SerializeField] private float lizard1ReturnSpeed = 1.1f;

        [Tooltip("How quickly lizards rotate to face their direction")]
        [SerializeField] private float rotationSpeed = 7f;

        [Tooltip("Distance at which Lizard 1 stops walking and raises its frill")]
        [SerializeField] private float displayDistance = 3.0f;

        [Tooltip("How far Lizard 2 runs before stopping (metres)")]
        [SerializeField] private float fleeDistance = 10f;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // ── Private State ────────────────────────────────────────────────────

        private Animation anim1;
        private Animation anim2;

        private Vector3 lizard1StartPosition;
        private Quaternion lizard1StartRotation;
        private Vector3 lizard2StartPosition;
        private Quaternion lizard2StartRotation;

        private Vector3 lizard2FleePosition;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Start()
        {
            if (lizard1 == null || lizard2 == null)
            {
                Debug.LogError("FrillneckInteraction: Both lizard references must be assigned!", this);
                enabled = false;
                return;
            }

            anim1 = lizard1.GetComponent<Animation>();
            anim2 = lizard2.GetComponent<Animation>();

            if (anim1 == null || anim2 == null)
            {
                Debug.LogError("FrillneckInteraction: Both lizards need a legacy Animation component!", this);
                enabled = false;
                return;
            }

            // Cache starting transforms
            lizard1StartPosition = lizard1.transform.position;
            lizard2StartPosition = lizard2.transform.position;

            // Sanitise starting rotations to Y-only — prevents storing any existing tilt
            lizard1StartRotation = ClampToYRotation(lizard1.transform.rotation);
            lizard2StartRotation = ClampToYRotation(lizard2.transform.rotation);
            lizard1.transform.rotation = lizard1StartRotation;
            lizard2.transform.rotation = lizard2StartRotation;

            StartCoroutine(InteractionLoop());
        }

        // ── Main Sequence ────────────────────────────────────────────────────

        /// <summary>
        /// Runs the full frillneck territorial display sequence on a loop.
        /// </summary>
        private IEnumerator InteractionLoop()
        {
            while (true)
            {
                // ── Phase 1: Both lizards eat peacefully ─────────────────────
                Log("Phase 1: Both eating");
                PlayAnimation(anim1, "eat");
                PlayAnimation(anim2, "eat");
                yield return new WaitForSeconds(initialEatDuration);

                // ── Phase 2: Lizard 1 spots Lizard 2, idles briefly ──────────
                Log("Phase 2: Lizard 1 notices Lizard 2");
                PlayAnimation(anim1, "idle");
                yield return new WaitForSeconds(0.6f);

                // ── Phase 3: Lizard 1 walks toward Lizard 2 ──────────────────
                Log("Phase 3: Lizard 1 approaching");
                PlayAnimation(anim1, "walk");
                yield return StartCoroutine(WalkToward(
                    lizard1.transform, lizard2.transform, displayDistance, walkSpeed));

                // Face Lizard 2 squarely before displaying
                yield return StartCoroutine(RotateTo(
                    lizard1.transform, lizard2.transform.position));

                // ── Phase 4: Lizard 1 raises its frill — the iconic display! ─
                Log("Phase 4: Lizard 1 frill display!");
                PlayAnimation(anim1, "aggressive");
                yield return new WaitForSeconds(lizard1DisplayDuration);

                // ── Phase 5: Lizard 2 responds with its OWN frill display ────
                // This mutual standoff is the heart of real frillneck encounters
                Log("Phase 5: Lizard 2 responds — mutual standoff!");
                PlayAnimation(anim2, "aggressive");

                // Lizard 2 turns to face Lizard 1 during its display
                yield return StartCoroutine(RotateTo(
                    lizard2.transform, lizard1.transform.position));

                // Both hold their displays — the tension of the standoff
                PlayAnimation(anim1, "aggressive");
                yield return new WaitForSeconds(mutualStandoffDuration);

                // ── Phase 6: Lizard 1 lunges — a bluff attack ────────────────
                // Real frillnecks rarely make full contact; this is a threat lunge
                Log("Phase 6: Lizard 1 lunges!");
                PlayAnimation(anim1, "attack");

                // Calculate flee direction before Lizard 2 moves
                Vector3 fleeDirection = (lizard2.transform.position - lizard1.transform.position).normalized;
                lizard2FleePosition = lizard2.transform.position + fleeDirection * fleeDistance;

                yield return new WaitForSeconds(attackDuration);

                // ── Phase 7: Lizard 2 runs — the famous fast bipedal sprint ──
                Log("Phase 7: Lizard 2 fleeing fast!");
                PlayAnimation(anim2, "run");
                yield return StartCoroutine(WalkToPosition(
                    lizard2.transform, lizard2FleePosition, fleeRunSpeed));

                // ── Phase 8: Lizard 1 victory display ────────────────────────
                // Winner holds its frill up one more time to reinforce dominance
                Log("Phase 8: Lizard 1 victory display");
                PlayAnimation(anim1, "aggressive");
                yield return new WaitForSeconds(victoryDisplayDuration);

                // ── Phase 9: Lizard 1 returns to start ───────────────────────
                Log("Phase 9: Lizard 1 returning home");
                PlayAnimation(anim1, "walk");
                yield return StartCoroutine(WalkToPosition(
                    lizard1.transform, lizard1StartPosition, lizard1ReturnSpeed));
                yield return StartCoroutine(RotateToQuaternion(
                    lizard1.transform, lizard1StartRotation));

                // ── Phase 10: Lizard 1 eats; Lizard 2 idles nervously ────────
                Log("Phase 10: Lizard 1 settles; Lizard 2 nervous at flee position");
                PlayAnimation(anim1, "eat");
                PlayAnimation(anim2, "idle");
                yield return new WaitForSeconds(fleeIdleDuration);

                // ── Phase 11: Lizard 2 cautiously walks back ─────────────────
                Log("Phase 11: Lizard 2 cautiously returning");
                PlayAnimation(anim2, "walk");
                yield return StartCoroutine(WalkToPosition(
                    lizard2.transform, lizard2StartPosition, returnWalkSpeed));
                yield return StartCoroutine(RotateToQuaternion(
                    lizard2.transform, lizard2StartRotation));

                // ── Phase 12: Both eat — territory dispute resolved ───────────
                Log("Phase 12: Both eating again — peace restored");
                PlayAnimation(anim1, "eat");
                PlayAnimation(anim2, "eat");
                yield return new WaitForSeconds(settleDuration);

                yield return new WaitForSeconds(loopDelay);
            }
        }

        // ── Movement Coroutines ──────────────────────────────────────────────

        /// <summary>
        /// Moves toward a target transform, stopping within stopDistance.
        /// </summary>
        private IEnumerator WalkToward(Transform mover, Transform target,
                                       float stopDistance, float speed)
        {
            while (true)
            {
                float distance = Vector3.Distance(mover.position, target.position);
                if (distance <= stopDistance) yield break;

                Vector3 direction = (target.position - mover.position).normalized;
                mover.position = Vector3.MoveTowards(
                    mover.position, target.position, speed * Time.deltaTime);

                SmoothRotateToward(mover, direction);
                yield return null;
            }
        }

        /// <summary>
        /// Moves to a world position at a given speed.
        /// </summary>
        private IEnumerator WalkToPosition(Transform mover, Vector3 targetPosition, float speed)
        {
            while (Vector3.Distance(mover.position, targetPosition) > 0.1f)
            {
                Vector3 direction = (targetPosition - mover.position).normalized;
                mover.position = Vector3.MoveTowards(
                    mover.position, targetPosition, speed * Time.deltaTime);

                SmoothRotateToward(mover, direction);
                yield return null;
            }

            mover.position = targetPosition;
        }

        /// <summary>
        /// Rotates smoothly to face a world position (Y axis only).
        /// </summary>
        private IEnumerator RotateTo(Transform mover, Vector3 lookAtPosition)
        {
            Vector3 direction = (lookAtPosition - mover.position).normalized;
            direction.y = 0f;
            if (direction == Vector3.zero) yield break;

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            while (Quaternion.Angle(mover.rotation, targetRotation) > 1f)
            {
                mover.rotation = Quaternion.Slerp(
                    mover.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                mover.rotation = ClampToYRotation(mover.rotation);
                yield return null;
            }

            mover.rotation = ClampToYRotation(targetRotation);
        }

        /// <summary>
        /// Rotates smoothly back to a saved Quaternion (used to restore start rotation).
        /// </summary>
        private IEnumerator RotateToQuaternion(Transform mover, Quaternion targetRotation)
        {
            Quaternion sanitisedTarget = ClampToYRotation(targetRotation);

            while (Quaternion.Angle(mover.rotation, sanitisedTarget) > 1f)
            {
                mover.rotation = Quaternion.Slerp(
                    mover.rotation, sanitisedTarget, rotationSpeed * Time.deltaTime);

                mover.rotation = ClampToYRotation(mover.rotation);
                yield return null;
            }

            mover.rotation = sanitisedTarget;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Rotates to face direction of travel, Y axis only, clamping X and Z each frame.
        /// </summary>
        private void SmoothRotateToward(Transform mover, Vector3 direction)
        {
            direction.y = 0f;
            if (direction == Vector3.zero) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            mover.rotation = Quaternion.Slerp(
                mover.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            // Clamp X and Z to zero — prevents Rigidbody tipping into unnatural angles
            mover.rotation = ClampToYRotation(mover.rotation);
        }

        /// <summary>
        /// Strips X and Z rotation, leaving only Y (yaw).
        /// Shared by all rotation methods to prevent physics tipping.
        /// </summary>
        private Quaternion ClampToYRotation(Quaternion rotation)
        {
            return Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
        }

        /// <summary>
        /// Plays a named clip on a legacy Animation component.
        /// Logs a warning if the clip name is not found.
        /// </summary>
        private void PlayAnimation(Animation anim, string clipName)
        {
            if (anim == null) return;

            if (anim[clipName] != null)
            {
                anim.Play(clipName);
            }
            else
            {
                Debug.LogWarning(
                    $"FrillneckInteraction: Clip '{clipName}' not found on {anim.gameObject.name}");
            }
        }

        private void Log(string message)
        {
            if (showDebugMessages)
                Debug.Log($"[FrillneckInteraction] {message}");
        }
    }
}
