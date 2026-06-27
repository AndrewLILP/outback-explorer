// ChickenInteraction.cs
// Choreographed pecking-order interaction between two chickens for the main game world.
// Attach to an empty GameObject. Assign both chicken references in the Inspector.
// Uses the legacy Animation component - no Animator Controller required.
//
// IMPORTANT - Inspector setup on each chicken's Rigidbody:
//   Constraints → Freeze Rotation X ✓
//   Constraints → Freeze Rotation Z ✓
//   (Leave Y free so they can turn naturally)
//
// Animations available: idle, walk, run
//   idle = foraging / standing (pecking implied)
//   walk = casual approach, strut back
//   run  = charge (Chicken 1) or scatter/flee (Chicken 2)
//
// Behaviour based on real chicken pecking-order dynamics:
//   - Chickens establish a clear dominance hierarchy
//   - The dominant bird occasionally charges to reinforce its position
//   - The subordinate scatters, then cautiously returns to forage nearby
//   - Interactions are brief and frequent — part of everyday flock life
//
// Sequence:
//   Both forage (idle) → Chicken 1 walks toward Chicken 2 → brief standoff →
//   Chicken 1 charges (run) → Chicken 2 scatters (run) →
//   Chicken 1 struts back (walk) → Chicken 2 cautiously returns (walk) →
//   Both forage → repeat
//
// TODO (Phase 2): Add player proximity detection so both chickens scatter
//   when the player enters a set radius. Use a trigger collider on this
//   GameObject and call ScatterFromPlayer(playerPosition) to interrupt
//   the current coroutine and have both chickens run away briefly.

using System.Collections;
using UnityEngine;

namespace RelaxingDrive.World
{
    public class ChickenInteraction : MonoBehaviour
    {
        // ── Inspector Fields ─────────────────────────────────────────────────

        [Header("Chicken References")]
        [Tooltip("Chicken 1 – the dominant bird that initiates the charge")]
        [SerializeField] private GameObject chicken1;

        [Tooltip("Chicken 2 – the subordinate bird that scatters and returns")]
        [SerializeField] private GameObject chicken2;

        [Header("Timing (seconds)")]
        [Tooltip("How long both chickens forage before the sequence begins")]
        [SerializeField] private float initialForageDuration = 5f;

        [Tooltip("How long the standoff lasts before Chicken 1 charges")]
        [SerializeField] private float standoffDuration = 0.8f;

        [Tooltip("How long Chicken 2 idles nervously at scatter position before returning")]
        [SerializeField] private float scatterIdleDuration = 2.5f;

        [Tooltip("How long both chickens forage together before the loop repeats")]
        [SerializeField] private float togetherForageDuration = 6f;

        [Tooltip("Pause before the whole sequence repeats")]
        [SerializeField] private float loopDelay = 0.5f;

        [Header("Movement")]
        [Tooltip("Walk speed when approaching or returning (metres per second)")]
        [SerializeField] private float walkSpeed = 1.0f;

        [Tooltip("Run speed for Chicken 1 charge and Chicken 2 scatter (metres per second)")]
        [SerializeField] private float runSpeed = 3.5f;

        [Tooltip("Walk speed when Chicken 2 cautiously returns (metres per second)")]
        [SerializeField] private float cautiousReturnSpeed = 0.8f;

        [Tooltip("How quickly chickens rotate to face direction of travel")]
        [SerializeField] private float rotationSpeed = 8f;

        [Tooltip("Distance at which Chicken 1 stops walking and starts the standoff")]
        [SerializeField] private float standoffDistance = 1.5f;

        [Tooltip("How far Chicken 2 runs before stopping (metres)")]
        [SerializeField] private float scatterDistance = 6f;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // ── Private State ────────────────────────────────────────────────────

        private Animation anim1;
        private Animation anim2;

        private Vector3 chicken1StartPosition;
        private Quaternion chicken1StartRotation;
        private Vector3 chicken2StartPosition;
        private Quaternion chicken2StartRotation;

        private Vector3 chicken2ScatterPosition;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Start()
        {
            if (chicken1 == null || chicken2 == null)
            {
                Debug.LogError("ChickenInteraction: Both chicken references must be assigned!", this);
                enabled = false;
                return;
            }

            anim1 = chicken1.GetComponent<Animation>();
            anim2 = chicken2.GetComponent<Animation>();

            if (anim1 == null || anim2 == null)
            {
                Debug.LogError("ChickenInteraction: Both chickens need a legacy Animation component!", this);
                enabled = false;
                return;
            }

            // Cache starting transforms
            chicken1StartPosition = chicken1.transform.position;
            chicken2StartPosition = chicken2.transform.position;

            // Sanitise starting rotations to Y-only — prevents storing any existing tilt
            chicken1StartRotation = ClampToYRotation(chicken1.transform.rotation);
            chicken2StartRotation = ClampToYRotation(chicken2.transform.rotation);
            chicken1.transform.rotation = chicken1StartRotation;
            chicken2.transform.rotation = chicken2StartRotation;

            StartCoroutine(InteractionLoop());
        }

        // ── Main Sequence ────────────────────────────────────────────────────

        /// <summary>
        /// Runs the pecking-order interaction sequence on a loop.
        /// </summary>
        private IEnumerator InteractionLoop()
        {
            while (true)
            {
                // ── Phase 1: Both chickens forage peacefully ──────────────────
                Log("Phase 1: Both foraging");
                PlayAnimation(anim1, "idle");
                PlayAnimation(anim2, "idle");
                yield return new WaitForSeconds(initialForageDuration);

                // ── Phase 2: Chicken 1 walks toward Chicken 2 ─────────────────
                // Encroaching on Chicken 2's space — asserting dominance
                Log("Phase 2: Chicken 1 approaching");
                PlayAnimation(anim1, "walk");
                yield return StartCoroutine(WalkToward(
                    chicken1.transform, chicken2.transform, standoffDistance, walkSpeed));

                // Both face each other
                yield return StartCoroutine(RotateTo(
                    chicken1.transform, chicken2.transform.position));
                yield return StartCoroutine(RotateTo(
                    chicken2.transform, chicken1.transform.position));

                // ── Phase 3: Standoff — brief eye contact ─────────────────────
                Log("Phase 3: Standoff");
                PlayAnimation(anim1, "idle");
                PlayAnimation(anim2, "idle");
                yield return new WaitForSeconds(standoffDuration);

                // ── Phase 4: Chicken 1 charges ────────────────────────────────
                // Calculate scatter direction before Chicken 2 moves
                Vector3 scatterDirection = (chicken2.transform.position - chicken1.transform.position).normalized;
                chicken2ScatterPosition = chicken2.transform.position + scatterDirection * scatterDistance;

                Log("Phase 4: Chicken 1 charges!");
                PlayAnimation(anim1, "run");

                // Chicken 2 scatters immediately
                PlayAnimation(anim2, "run");

                // Run both simultaneously — charge and scatter overlap naturally
                yield return StartCoroutine(RunSimultaneous(
                    chicken1.transform, chicken2.transform,
                    chicken2.transform.position,  // Chicken 1 runs toward where Chicken 2 was
                    chicken2ScatterPosition,       // Chicken 2 runs away
                    standoffDistance * 0.5f        // Chicken 1 stops short — it was just a bluff
                ));

                // ── Phase 5: Chicken 1 struts back — job done ─────────────────
                Log("Phase 5: Chicken 1 struts back");
                PlayAnimation(anim1, "walk");
                yield return StartCoroutine(WalkToPosition(
                    chicken1.transform, chicken1StartPosition, walkSpeed));
                yield return StartCoroutine(RotateToQuaternion(
                    chicken1.transform, chicken1StartRotation));

                // ── Phase 6: Chicken 1 forages; Chicken 2 nervous at distance ─
                Log("Phase 6: Chicken 1 forages; Chicken 2 nervous");
                PlayAnimation(anim1, "idle");
                PlayAnimation(anim2, "idle");
                yield return new WaitForSeconds(scatterIdleDuration);

                // ── Phase 7: Chicken 2 cautiously returns ─────────────────────
                Log("Phase 7: Chicken 2 cautiously returning");
                PlayAnimation(anim2, "walk");
                yield return StartCoroutine(WalkToPosition(
                    chicken2.transform, chicken2StartPosition, cautiousReturnSpeed));
                yield return StartCoroutine(RotateToQuaternion(
                    chicken2.transform, chicken2StartRotation));

                // ── Phase 8: Both forage — order restored ─────────────────────
                Log("Phase 8: Both foraging together");
                PlayAnimation(anim1, "idle");
                PlayAnimation(anim2, "idle");
                yield return new WaitForSeconds(togetherForageDuration);

                yield return new WaitForSeconds(loopDelay);
            }
        }

        // ── Movement Coroutines ──────────────────────────────────────────────

        /// <summary>
        /// Moves a transform toward a target transform, stopping within stopDistance.
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
        /// Moves a transform to a world position at a given speed.
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
        /// Runs both chickens simultaneously — charge and scatter overlap naturally.
        /// Chicken 1 stops after a short burst; Chicken 2 runs to its scatter position.
        /// </summary>
        private IEnumerator RunSimultaneous(Transform charger, Transform scatterer,
                                            Vector3 chargeTarget, Vector3 scatterTarget,
                                            float chargerStopDistance)
        {
            bool chargerDone = false;
            bool scattererDone = false;

            while (!chargerDone || !scattererDone)
            {
                // Move charger (Chicken 1) — stops after a short burst
                if (!chargerDone)
                {
                    float dist = Vector3.Distance(charger.position, chargeTarget);
                    if (dist <= chargerStopDistance)
                    {
                        chargerDone = true;
                        PlayAnimation(anim1, "idle"); // Stops and watches
                    }
                    else
                    {
                        Vector3 dir = (chargeTarget - charger.position).normalized;
                        charger.position = Vector3.MoveTowards(
                            charger.position, chargeTarget, runSpeed * Time.deltaTime);
                        SmoothRotateToward(charger, dir);
                    }
                }

                // Move scatterer (Chicken 2) — runs to scatter position
                if (!scattererDone)
                {
                    float dist = Vector3.Distance(scatterer.position, scatterTarget);
                    if (dist <= 0.1f)
                    {
                        scatterer.position = scatterTarget;
                        scattererDone = true;
                    }
                    else
                    {
                        Vector3 dir = (scatterTarget - scatterer.position).normalized;
                        scatterer.position = Vector3.MoveTowards(
                            scatterer.position, scatterTarget, runSpeed * Time.deltaTime);
                        SmoothRotateToward(scatterer, dir);
                    }
                }

                yield return null;
            }
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
                    $"ChickenInteraction: Clip '{clipName}' not found on {anim.gameObject.name}");
            }
        }

        private void Log(string message)
        {
            if (showDebugMessages)
                Debug.Log($"[ChickenInteraction] {message}");
        }
    }
}