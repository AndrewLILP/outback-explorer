// RiverPath.cs
// Location: Assets/Scripts/World/River/RiverPath.cs
//
// One per river. The river's centre line is drawn by this object's CHILD
// GameObjects (plain empty transforms), in Hierarchy order:
//   first child = launch end, last child = landing end.
//
// No Splines package: the curve is a Catmull-Rom spline that passes through
// every point, then "baked" into a lookup table measured in metres, so the
// tube moves at a steady speed however unevenly the points are spaced.
//
// SETUP:
//   1. Create an empty GameObject "River_01" and add RiverPath.
//   2. Add 5-10 empty children (Point_00, Point_01, ...). Place each one on the
//      water surface, down the middle of the river, from start to end.
//   3. Ctrl+D a point to add another; drag in the Hierarchy to reorder.
//   4. Optional: an empty "River_01_Landing" on the bank (NOT a child of River_01),
//      blue arrow facing the way to walk. Drag into 'Landing Spot'.
//   The cyan line in the Scene view is the path; the blue lines are the banks
//   the tube can steer between. The end label shows length and float time.

using System.Collections.Generic;
using UnityEngine;

namespace RelaxingDrive.World
{
    public class RiverPath : MonoBehaviour
    {
        [Header("Current")]
        [Tooltip("How fast the current carries the tube, in metres per second. 2-3 feels lazy.")]
        [SerializeField, Min(0.1f)] private float flowSpeed = 2.5f;

        [Header("Shape")]
        [Tooltip("Metres from the centre line to each bank. The tube can steer this far (minus its own radius).")]
        [SerializeField, Min(0.5f)] private float halfWidth = 3f;

        [Tooltip("How many straight pieces each curve between two points is measured with. 12 is plenty.")]
        [SerializeField, Range(4, 32)] private int samplesPerSegment = 12;

        [Header("Landing")]
        [Tooltip("Empty on the bank where the player steps off, facing the way they should walk. " +
                 "Keep it OUTSIDE this object's children (children are path points). " +
                 "Empty = automatic spot on the right bank past the last point.")]
        [SerializeField] private Transform landingSpot;

        [Header("Gizmos")]
        [SerializeField] private Color pathColour = Color.cyan;
        [SerializeField] private Color bankColour = new Color(0f, 0.6f, 1f, 0.6f);

        // The baked lookup table: points along the curve, and the distance
        // travelled from the start to reach each one.
        private readonly List<Vector3> samples = new List<Vector3>();
        private readonly List<float> cumulative = new List<float>();
        private readonly List<Vector3> controlPoints = new List<Vector3>();

        public float FlowSpeed => flowSpeed;
        public float HalfWidth => halfWidth;
        public float TotalLength { get; private set; }
        public bool IsValid => samples.Count >= 2;

        /// <summary>Seconds to float from start to end - handy for tuning the 1-minute target.</summary>
        public float EstimatedDuration => TotalLength / flowSpeed;

        private void Awake() => Rebuild();

        /// <summary>
        /// Re-reads the child points and re-bakes the lookup table.
        /// Runs in Awake for play, and every Scene-view repaint in the editor
        /// so the gizmo follows you as you drag points around.
        /// </summary>
        public void Rebuild()
        {
            controlPoints.Clear();
            foreach (Transform child in transform)
                controlPoints.Add(child.position);

            samples.Clear();
            cumulative.Clear();
            TotalLength = 0f;

            if (controlPoints.Count < 2) return;

            // Each segment runs from point[seg] to point[seg+1]. Catmull-Rom also
            // needs the point before and after to shape the curve; at the two ends
            // we reuse the end point itself.
            for (int seg = 0; seg < controlPoints.Count - 1; seg++)
            {
                Vector3 p0 = controlPoints[Mathf.Max(seg - 1, 0)];
                Vector3 p1 = controlPoints[seg];
                Vector3 p2 = controlPoints[seg + 1];
                Vector3 p3 = controlPoints[Mathf.Min(seg + 2, controlPoints.Count - 1)];

                // Skip t = 0 after the first segment: it's the same point the
                // previous segment ended on.
                int first = seg == 0 ? 0 : 1;
                for (int i = first; i <= samplesPerSegment; i++)
                {
                    float t = i / (float)samplesPerSegment;
                    AddSample(CatmullRom(p0, p1, p2, p3, t));
                }
            }
        }

        private void AddSample(Vector3 point)
        {
            if (samples.Count > 0)
                TotalLength += Vector3.Distance(samples[samples.Count - 1], point);

            samples.Add(point);
            cumulative.Add(TotalLength);
        }

        /// <summary>
        /// Where is the river's centre line after travelling 'distance' metres from
        /// the start, and which way is the current flowing there (flat, no tilt)?
        /// </summary>
        public void Evaluate(float distance, out Vector3 position, out Vector3 forward)
        {
            if (!IsValid)
            {
                position = transform.position;
                forward = transform.forward;
                return;
            }

            distance = Mathf.Clamp(distance, 0f, TotalLength);
            int i = FindSampleIndex(distance);

            // Blend between the two samples either side of this distance.
            float pieceLength = cumulative[i + 1] - cumulative[i];
            float blend = pieceLength > 0.0001f ? (distance - cumulative[i]) / pieceLength : 0f;
            position = Vector3.Lerp(samples[i], samples[i + 1], blend);

            forward = samples[i + 1] - samples[i];
            forward.y = 0f; // keep the tube level even where the river drops
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
        }

        /// <summary>
        /// Where the player stands after stepping off at the end of the river.
        /// Snapped down onto the ground so the walking controller doesn't start mid-air.
        /// </summary>
        public void GetLandingPose(out Vector3 position, out Quaternion rotation)
        {
            Evaluate(TotalLength, out Vector3 end, out Vector3 forward);

            if (landingSpot != null)
            {
                position = landingSpot.position;
                Vector3 facing = landingSpot.forward;
                facing.y = 0f;
                rotation = Quaternion.LookRotation(facing.sqrMagnitude > 0.0001f ? facing : forward, Vector3.up);
            }
            else
            {
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                position = end + right * (halfWidth + 1.5f);
                rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            // Find the ground under the spot (ignores trigger colliders).
            if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
            }
        }

        /// <summary>
        /// Binary search: the last sample whose distance is at or before 'distance'.
        /// Same idea as finding a word in a dictionary - halve the range each step.
        /// </summary>
        private int FindSampleIndex(float distance)
        {
            int lo = 0;
            int hi = cumulative.Count - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cumulative[mid] <= distance) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>
        /// Catmull-Rom spline: a cubic curve that passes THROUGH p1 (t = 0) and
        /// p2 (t = 1), using p0 and p3 only to set the direction at each end.
        /// </summary>
        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ---------- Editor visuals ----------

        private void OnDrawGizmos()
        {
            // Children may have been dragged since the last frame.
            if (!Application.isPlaying) Rebuild();
            if (!IsValid) return;

            // Centre line
            Gizmos.color = pathColour;
            for (int i = 0; i < samples.Count - 1; i++)
                Gizmos.DrawLine(samples[i], samples[i + 1]);

            // Banks (how far the tube can steer)
            Gizmos.color = bankColour;
            Vector3 prevLeft = Vector3.zero, prevRight = Vector3.zero;
            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 dir = samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(i - 1, 0)];
                dir.y = 0f;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                Vector3 left = samples[i] - right * halfWidth;
                Vector3 rightEdge = samples[i] + right * halfWidth;
                if (i > 0)
                {
                    Gizmos.DrawLine(prevLeft, left);
                    Gizmos.DrawLine(prevRight, rightEdge);
                }
                prevLeft = left;
                prevRight = rightEdge;
            }

            // Control points: green = launch, red = landing
            for (int i = 0; i < controlPoints.Count; i++)
            {
                Gizmos.color = i == 0 ? Color.green : i == controlPoints.Count - 1 ? Color.red : Color.white;
                Gizmos.DrawWireSphere(controlPoints[i], 0.4f);
            }

            // Landing spot (yellow)
            if (landingSpot != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(landingSpot.position, 0.5f);
                Gizmos.DrawLine(landingSpot.position, landingSpot.position + landingSpot.forward * 1.5f);
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(controlPoints[0] + Vector3.up, $"{name} - Launch");
            UnityEditor.Handles.Label(controlPoints[controlPoints.Count - 1] + Vector3.up,
                $"Landing - {TotalLength:0} m, ~{EstimatedDuration:0} s");
#endif
        }
    }
}