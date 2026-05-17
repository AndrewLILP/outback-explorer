// EmuInteraction.cs
// Choreographed interaction between two emus for the car selection scene.
// Attach to an empty GameObject. Assign both emu references in the Inspector.
// Uses the legacy Animation component - no Animator Controller required.
//
// IMPORTANT - Inspector setup on each emu's Rigidbody:
//   Constraints → Freeze Rotation X ✓
//   Constraints → Freeze Rotation Z ✓
//   (Leave Y free so they can turn naturally)
//
// Sequence:
//   Both graze → Emu1 approaches → standoff → Emu1 attacks →
//   Emu2 runs away → Emu1 grazes alone → Emu2 cautiously returns → repeat

using System.Collections;
using UnityEngine;

namespace RelaxingDrive.World
{
    public class EmuInteraction : MonoBehaviour
    {
        // ── Inspector Fields ─────────────────────────────────────────────────

        [Header("Emu References")]
        [Tooltip("Emu 1 – the dominant one that approaches and attacks")]
        [SerializeField] private GameObject emu1;

        [Tooltip("Emu 2 – the subordinate one that flees when attacked")]
        [SerializeField] private GameObject emu2;

        [Header("Timing (seconds)")]
        [Tooltip("How long both emus graze before the sequence begins")]
        [SerializeField] private float initialGrazeDuration = 4f;

        [Tooltip("Duration of the face-off standoff before attack")]
        [SerializeField] private float standoffDuration = 1.5f;

        [Tooltip("How long the attack clip plays")]
        [SerializeField] private float attackDuration = 1.4f;

        [Tooltip("How far Emu 2 runs before stopping (metres)")]
        [SerializeField] private float fleeDistance = 8f;

        [Tooltip("How long Emu 2 waits nervously at flee position before returning")]
        [SerializeField] private float fleeIdleDuration = 3f;

        [Tooltip("How long Emu 1 grazes alone after the chase before Emu 2 returns")]
        [SerializeField] private float soloGrazeDuration = 3f;

        [Tooltip("How long both emus graze together before the loop repeats")]
        [SerializeField] private float togetherGrazeDuration = 5f;

        [Tooltip("Pause before the whole sequence repeats")]
        [SerializeField] private float loopDelay = 1f;

        [Header("Movement")]
        [Tooltip("Emu 1 walk speed when approaching (metres per second)")]
        [SerializeField] private float approachSpeed = 1.4f;

        [Tooltip("Emu 2 run speed when fleeing (metres per second)")]
        [SerializeField] private float fleeRunSpeed = 4f;

        [Tooltip("Emu 2 walk speed when cautiously returning (metres per second)")]
        [SerializeField] private float returnWalkSpeed = 1.1f;

        [Tooltip("Emu 1 walk speed when returning to start (metres per second)")]
        [SerializeField] private float emu1ReturnSpeed = 1.3f;

        [Tooltip("How quickly emus rotate to face direction of travel")]
        [SerializeField] private float rotationSpeed = 6f;

        [Tooltip("Distance at which Emu 1 stops and begins the standoff")]
        [SerializeField] private float standoffDistance = 2.5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // ── Private State ────────────────────────────────────────────────────

        private Animation anim1;
        private Animation anim2;

        private Vector3 emu1StartPosition;
        private Quaternion emu1StartRotation;
        private Vector3 emu2StartPosition;
        private Quaternion emu2StartRotation;

        private Vector3 emu2FleePosition;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Start()
        {
            if (emu1 == null || emu2 == null)
            {
                Debug.LogError("EmuInteraction: Both emu references must be assigned!", this);
                enabled = false;
                return;
            }

            anim1 = emu1.GetComponent<Animation>();
            anim2 = emu2.GetComponent<Animation>();

            if (anim1 == null || anim2 == null)
            {
                Debug.LogError("EmuInteraction: Both emus need a legacy Animation component!", this);
                enabled = false;
                return;
            }

            emu1StartPosition = emu1.transform.position;
            emu2StartPosition = emu2.transform.position;

            // Sanitise start rotations to Y-only — prevents storing any existing tilt
            emu1StartRotation = ClampToYRotation(emu1.transform.rotation);
            emu2StartRotation = ClampToYRotation(emu2.transform.rotation);
            emu1.transform.rotation = emu1StartRotation;
            emu2.transform.rotation = emu2StartRotation;

            StartCoroutine(InteractionLoop());
        }

        // ── Main Sequence ────────────────────────────────────────────────────

        private IEnumerator InteractionLoop()
        {
            while (true)
            {
                // ── Phase 1: Both emus graze peacefully ──────────────────────
                Log("Phase 1: Both grazing");
                PlayAnimation(anim1, "eat");
                PlayAnimation(anim2, "eat");
                yield return new WaitForSeconds(initialGrazeDuration);

                // ── Phase 2: Emu1 looks up — notices Emu2 ───────────────────
                Log("Phase 2: Emu1 looks up");
                PlayAnimation(anim1, "idle");
                yield return new WaitForSeconds(0.8f);

                // ── Phase 3: Emu1 approaches with purpose ────────────────────
                Log("Phase 3: Emu1 approaching");
                PlayAnimation(anim1, "walk");
                yield return StartCoroutine(WalkToward(
                    emu1.transform, emu2.transform, standoffDistance, approachSpeed));

                // Emu2 looks up — senses the approach
                PlayAnimation(anim2, "idle");
                yield return StartCoroutine(RotateTo(emu1.transform, emu2.transform.position));
                yield return StartCoroutine(RotateTo(emu2.transform, emu1.transform.position));

                // ── Phase 4: Standoff — both idle, sizing each other up ──────
                Log("Phase 4: Standoff");
                PlayAnimation(anim1, "idle");
                PlayAnimation(anim2, "idle");
                yield return new WaitForSeconds(standoffDuration);

                // ── Phase 5: Emu1 attacks ─────────────────────────────────────
                Log("Phase 5: Emu1 attacks!");
                PlayAnimation(anim1, "attack");

                // Calculate flee direction before Emu2 moves
                Vector3 fleeDirection = (emu2.transform.position - emu1.transform.position).normalized;
                emu2FleePosition = emu2.transform.position + fleeDirection * fleeDistance;

                yield return new WaitForSeconds(attackDuration * 0.5f);

                // ── Phase 6: Emu2 runs away ───────────────────────────────────
                Log("Phase 6: Emu2 fleeing!");
                PlayAnimation(anim2, "run");
                yield return StartCoroutine(WalkToPosition(
                    emu2.transform, emu2FleePosition, fleeRunSpeed));

                yield return new WaitForSeconds(attackDuration * 0.5f);

                // ── Phase 7: Emu1 watches, then returns to start ─────────────
                Log("Phase 7: Emu1 returns");
                PlayAnimation(anim1, "idle");
                yield return new WaitForSeconds(1.0f);

                PlayAnimation(anim1, "walk");
                yield return StartCoroutine(WalkToPosition(
                    emu1.transform, emu1StartPosition, emu1ReturnSpeed));
                yield return StartCoroutine(RotateToQuaternion(emu1.transform, emu1StartRotation));

                // ── Phase 8: Emu1 grazes; Emu2 idles nervously ───────────────
                Log("Phase 8: Emu1 grazes alone, Emu2 nervous");
                PlayAnimation(anim1, "eat");
                PlayAnimation(anim2, "idle");
                yield return new WaitForSeconds(soloGrazeDuration);

                // ── Phase 9: Emu2 cautiously walks back ─────────────────────
                Log("Phase 9: Emu2 cautiously returning");
                PlayAnimation(anim2, "walk");
                yield return StartCoroutine(WalkToPosition(
                    emu2.transform, emu2StartPosition, returnWalkSpeed));
                yield return StartCoroutine(RotateToQuaternion(emu2.transform, emu2StartRotation));

                // ── Phase 10: Both graze — peace restored ────────────────────
                Log("Phase 10: Both grazing together");
                PlayAnimation(anim1, "eat");
                PlayAnimation(anim2, "eat");
                yield return new WaitForSeconds(togetherGrazeDuration);

                yield return new WaitForSeconds(loopDelay);
            }
        }

        // ── Movement Coroutines ──────────────────────────────────────────────

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

        private IEnumerator WalkToPosition(Transform mover, Vector3 targetPosition, float speed)
        {
            while (Vector3.Distance(mover.position, targetPosition) > 0.15f)
            {
                Vector3 direction = (targetPosition - mover.position).normalized;
                mover.position = Vector3.MoveTowards(
                    mover.position, targetPosition, speed * Time.deltaTime);

                SmoothRotateToward(mover, direction);
                yield return null;
            }

            mover.position = targetPosition;
        }

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

                // Clamp X and Z to zero — prevent Rigidbody tipping
                mover.rotation = ClampToYRotation(mover.rotation);
                yield return null;
            }

            mover.rotation = ClampToYRotation(targetRotation);
        }

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

        private void SmoothRotateToward(Transform mover, Vector3 direction)
        {
            direction.y = 0f;
            if (direction == Vector3.zero) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            mover.rotation = Quaternion.Slerp(
                mover.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            // Clamp X and Z to zero every frame — prevents Rigidbody tipping
            mover.rotation = ClampToYRotation(mover.rotation);
        }

        /// <summary>
        /// Strips X and Z rotation from a Quaternion, leaving only the Y (yaw) component.
        /// This prevents physics from tilting animals into unnatural poses.
        /// </summary>
        private Quaternion ClampToYRotation(Quaternion rotation)
        {
            return Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
        }

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
                    $"EmuInteraction: Clip '{clipName}' not found on {anim.gameObject.name}");
            }
        }

        private void Log(string message)
        {
            if (showDebugMessages)
                Debug.Log($"[EmuInteraction] {message}");
        }
    }
}
