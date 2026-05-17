// KangarooInteraction.cs
// Choreographed interaction between two kangaroos for the car selection scene.
// Attach to an empty GameObject. Assign both kangaroo references in the Inspector.
// Uses the legacy Animation component - no Animator Controller required.
//
// IMPORTANT - Inspector setup on each kangaroo's Rigidbody:
//   Constraints → Freeze Rotation X ✓
//   Constraints → Freeze Rotation Z ✓
//   (Leave Y free so they can turn naturally)
//
// Sequence:
//   Roo1 eats at start position → walks toward Roo2 → playful attack →
//   Roo2 reacts (hurt) → Roo1 walks back → both eat → repeat

using System.Collections;
using UnityEngine;

namespace RelaxingDrive.World
{
    public class KangarooInteraction : MonoBehaviour
    {
        // ── Inspector Fields ─────────────────────────────────────────────────

        [Header("Kangaroo References")]
        [Tooltip("Kangaroo 1 – the one that walks over and attacks")]
        [SerializeField] private GameObject kangaroo1;

        [Tooltip("Kangaroo 2 – the one that grazes and reacts")]
        [SerializeField] private GameObject kangaroo2;

        [Header("Timing (seconds)")]
        [Tooltip("How long each kangaroo eats before the sequence begins")]
        [SerializeField] private float initialEatDuration = 3f;

        [Tooltip("How long Roo1 eats at start before walking over")]
        [SerializeField] private float eatBeforeWalkDuration = 2f;

        [Tooltip("How long the attack animation plays")]
        [SerializeField] private float attackDuration = 1.8f;

        [Tooltip("How long Roo2's hurt reaction plays")]
        [SerializeField] private float hurtDuration = 1.2f;

        [Tooltip("How long both kangaroos eat before the loop repeats")]
        [SerializeField] private float eatAfterAttackDuration = 4f;

        [Tooltip("Pause before the whole sequence repeats")]
        [SerializeField] private float loopDelay = 1f;

        [Header("Movement")]
        [Tooltip("Walk speed toward target (metres per second)")]
        [SerializeField] private float walkSpeed = 1.5f;

        [Tooltip("How quickly kangaroos rotate to face their direction")]
        [SerializeField] private float rotationSpeed = 5f;

        [Tooltip("How close Roo1 needs to get before stopping and attacking")]
        [SerializeField] private float attackStopDistance = 1.8f;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // ── Private State ────────────────────────────────────────────────────

        private Animation anim1;
        private Animation anim2;

        private Vector3 roo1StartPosition;
        private Quaternion roo1StartRotation;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Start()
        {
            if (kangaroo1 == null || kangaroo2 == null)
            {
                Debug.LogError("KangarooInteraction: Both kangaroo references must be assigned!", this);
                enabled = false;
                return;
            }

            anim1 = kangaroo1.GetComponent<Animation>();
            anim2 = kangaroo2.GetComponent<Animation>();

            if (anim1 == null || anim2 == null)
            {
                Debug.LogError("KangarooInteraction: Both kangaroos need a legacy Animation component!", this);
                enabled = false;
                return;
            }

            roo1StartPosition = kangaroo1.transform.position;

            // Sanitise start rotations to Y-only — prevents storing any existing tilt
            roo1StartRotation = ClampToYRotation(kangaroo1.transform.rotation);
            kangaroo1.transform.rotation = roo1StartRotation;
            kangaroo2.transform.rotation = ClampToYRotation(kangaroo2.transform.rotation);

            StartCoroutine(InteractionLoop());
        }

        // ── Main Sequence ────────────────────────────────────────────────────

        private IEnumerator InteractionLoop()
        {
            while (true)
            {
                // ── Phase 1: Both kangaroos eat ───────────────────────────────
                Log("Phase 1: Both eating");
                PlayAnimation(anim1, "eat");
                PlayAnimation(anim2, "eat");
                yield return new WaitForSeconds(initialEatDuration);

                // ── Phase 2: Roo1 looks up, preparing to walk over ────────────
                Log("Phase 2: Roo1 looks up");
                PlayAnimation(anim1, "idle1");
                yield return new WaitForSeconds(eatBeforeWalkDuration);

                // ── Phase 3: Roo1 walks toward Roo2 ──────────────────────────
                Log("Phase 3: Roo1 walking toward Roo2");
                PlayAnimation(anim1, "run");
                yield return StartCoroutine(WalkToward(
                    kangaroo1.transform, kangaroo2.transform, attackStopDistance));

                yield return StartCoroutine(RotateTo(
                    kangaroo1.transform, kangaroo2.transform.position));

                // Roo2 looks up from eating
                PlayAnimation(anim2, "idle2");
                yield return new WaitForSeconds(0.4f);

                // ── Phase 4: Playful attack ───────────────────────────────────
                Log("Phase 4: Roo1 attacks!");
                PlayAnimation(anim1, "attack");
                yield return new WaitForSeconds(attackDuration * 0.4f);

                PlayAnimation(anim2, "hurt");
                yield return new WaitForSeconds(hurtDuration);

                // ── Phase 5: Roo1 walks back to start ────────────────────────
                Log("Phase 5: Roo1 walking back");
                PlayAnimation(anim1, "run");
                yield return StartCoroutine(WalkToPosition(
                    kangaroo1.transform, roo1StartPosition));
                yield return StartCoroutine(RotateToQuaternion(
                    kangaroo1.transform, roo1StartRotation));

                // ── Phase 6: Both settle into eating ─────────────────────────
                Log("Phase 6: Both eating again");
                PlayAnimation(anim1, "eat");
                PlayAnimation(anim2, "eat");
                yield return new WaitForSeconds(eatAfterAttackDuration);

                yield return new WaitForSeconds(loopDelay);
            }
        }

        // ── Movement Coroutines ──────────────────────────────────────────────

        private IEnumerator WalkToward(Transform mover, Transform target, float stopDistance)
        {
            while (true)
            {
                float distance = Vector3.Distance(mover.position, target.position);
                if (distance <= stopDistance) yield break;

                Vector3 direction = (target.position - mover.position).normalized;
                mover.position = Vector3.MoveTowards(
                    mover.position, target.position, walkSpeed * Time.deltaTime);

                SmoothRotateToward(mover, direction);
                yield return null;
            }
        }

        private IEnumerator WalkToPosition(Transform mover, Vector3 targetPosition)
        {
            while (Vector3.Distance(mover.position, targetPosition) > 0.15f)
            {
                Vector3 direction = (targetPosition - mover.position).normalized;
                mover.position = Vector3.MoveTowards(
                    mover.position, targetPosition, walkSpeed * Time.deltaTime);

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

            // Clamp X and Z to zero every frame — prevents Rigidbody tipping on X/Z
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
                    $"KangarooInteraction: Clip '{clipName}' not found on {anim.gameObject.name}");
            }
        }

        private void Log(string message)
        {
            if (showDebugMessages)
                Debug.Log($"[KangarooInteraction] {message}");
        }
    }
}