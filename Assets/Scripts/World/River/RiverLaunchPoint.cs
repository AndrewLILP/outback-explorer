// RiverLaunchPoint.cs
// Location: Assets/Scripts/World/River/RiverLaunchPoint.cs
//
// Marks a craft as something the player can hop onto. Put it on the same
// GameObject as RiverCraftMover (SUP_Board). It does NOT read input or show
// prompts itself: OnFootState already owns the on-foot prompt and the
// Interact button (for the car), so it asks this class "is a board in reach?"
// and decides between car and board in one place. Two scripts both listening
// for E would fight over the same button press and the same prompt.
//
// Every launch point registers itself in a static list, so OnFootState can
// find the nearest one without a scene search or hard references
// (works for any number of rivers).

using System.Collections.Generic;
using UnityEngine;

namespace RelaxingDrive.World
{
    [RequireComponent(typeof(RiverCraftMover))]
    public class RiverLaunchPoint : MonoBehaviour
    {
        [Tooltip("How close (metres, from the board's centre) the player must stand to hop on. " +
                 "Big enough to reach from the bank.")]
        [SerializeField, Min(0.5f)] private float range = 4f;

        [Tooltip("Action text for the prompt. InteractionPromptUI adds the E / A keycap itself.")]
        [SerializeField] private string promptText = "Hop on board";

        private static readonly List<RiverLaunchPoint> registered = new List<RiverLaunchPoint>();

        private RiverCraftMover craft;

        public RiverCraftMover Craft => craft;
        public string PromptText => promptText;

        /// <summary>Only offered while the board is waiting at the launch.</summary>
        public bool CanLaunch => craft != null && craft.IsDocked;

        private void Awake() => craft = GetComponent<RiverCraftMover>();
        private void OnEnable() => registered.Add(this);
        private void OnDisable() => registered.Remove(this);

        /// <summary>
        /// The closest launchable board within its range of 'playerPosition', or null.
        /// </summary>
        public static RiverLaunchPoint FindInRange(Vector3 playerPosition)
        {
            RiverLaunchPoint best = null;
            float bestSqr = float.MaxValue;

            foreach (RiverLaunchPoint point in registered)
            {
                if (!point.CanLaunch) continue;

                // Compare squared distances: same answer, no square root.
                float sqr = (point.transform.position - playerPosition).sqrMagnitude;
                if (sqr <= point.range * point.range && sqr < bestSqr)
                {
                    best = point;
                    bestSqr = sqr;
                }
            }

            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}