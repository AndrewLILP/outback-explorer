// BalloonLaunchPoint.cs
// Location: Assets/Scripts/World/Balloon/BalloonLaunchPoint.cs
//
// Marks a balloon as something the player can climb into. Same pattern as
// RiverLaunchPoint: it doesn't read input or show prompts itself - OnFootState
// owns the Interact button and asks "is a balloon in reach?". Every launch
// point registers in a static list, so there are no scene searches.

using System.Collections.Generic;
using UnityEngine;

namespace RelaxingDrive.World
{
    [RequireComponent(typeof(BalloonMover))]
    public class BalloonLaunchPoint : MonoBehaviour
    {
        [Tooltip("How close (metres, from the balloon's pivot) the player must stand to climb in.")]
        [SerializeField, Min(0.5f)] private float range = 5f;

        [Tooltip("Action text for the prompt. InteractionPromptUI adds the E / A keycap itself.")]
        [SerializeField] private string promptText = "Climb in";

        private static readonly List<BalloonLaunchPoint> registered = new List<BalloonLaunchPoint>();

        private BalloonMover balloon;

        public BalloonMover Balloon => balloon;
        public string PromptText => promptText;

        /// <summary>Only offered while the balloon is waiting on the ground.</summary>
        public bool CanLaunch => balloon != null && balloon.IsDocked;

        private void Awake() => balloon = GetComponent<BalloonMover>();
        private void OnEnable() => registered.Add(this);
        private void OnDisable() => registered.Remove(this);

        /// <summary>The closest ready balloon within its range of 'playerPosition', or null.</summary>
        public static BalloonLaunchPoint FindInRange(Vector3 playerPosition)
        {
            BalloonLaunchPoint best = null;
            float bestSqr = float.MaxValue;

            foreach (BalloonLaunchPoint point in registered)
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
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}