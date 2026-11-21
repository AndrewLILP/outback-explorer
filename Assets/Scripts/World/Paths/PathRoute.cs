// PathRoute.cs
// Location: Assets/Scripts/World/Paths/PathRoute.cs
//
// A general route through the world, drawn by this object's CHILD empties in
// Hierarchy order (first child = start). Anything that "rides a line" can use
// it - the hot air balloon first; the river could move over to it later.
//
// Same idea as RiverPath: a Catmull-Rom curve through every point, "baked"
// into a lookup table measured in metres so riders move at a steady speed.
// What's new:
//   - Loop: joins the last point back to the first, smoothly (no corner).
//   - Real height: Evaluate returns the curve's actual Y, so a route can
//     climb and dip. The rider decides what that means (the balloon treats it
//     as the floor).
//   - GetPointBlend: "which two points am I between, and how far?" so a rider
//     can blend its OWN per-point settings. The route never needs to know
//     about balloons (Single Responsibility + Open/Closed).
//
// SETUP:
//   1. Empty GameObject "Balloon_Route", add PathRoute, tick Loop.
//   2. Add empty children Point_00, Point_01, ... in order.
//      Ctrl+D to add a point, drag in the Hierarchy to reorder.
//   The yellow line in the Scene view is the route; the label shows its length.

using System.Collections.Generic;
using UnityEngine;

namespace RelaxingDrive.World
{
    public class PathRoute : MonoBehaviour
    {
        [Tooltip("Join the last point back to the first. The route then starts AND ends at the first point.")]
        [SerializeField] private bool loop = true;

        [Tooltip("How many straight pieces each curve between two points is measured with. 12 is plenty.")]
        [SerializeField, Range(4, 32)] private int samplesPerSegment = 12;

        [Header("Gizmos")]
        [SerializeField] private Color pathColour = new Color(1f, 0.8f, 0.2f);

        private readonly List<Transform> points = new List<Transform>();
        private readonly List<Vector3> samples = new List<Vector3>();
        private readonly List<float> cumulative = new List<float>();

        // For each sample, where it sits counted in POINTS:
        // 2.25 = a quarter of the way from point 2 to point 3.
        private readonly List<float> pointParam = new List<float>();

        private int segmentCount;

        public bool Loop => loop;
        public float TotalLength { get; private set; }
        public bool IsValid => samples.Count >= 2;
        public IReadOnlyList<Transform> Points => points;
        public int PointCount => points.Count;

        private void Awake() => Rebuild();

        /// <summary>Re-reads the child points and re-bakes the lookup table.</summary>
        public void Rebuild()
        {
            points.Clear();
            foreach (Transform child in transform) points.Add(child);

            samples.Clear();
            cumulative.Clear();
            pointParam.Clear();
            TotalLength = 0f;
            segmentCount = 0;

            int n = points.Count;
            if (n < 2) return;

            // A loop has one extra segment: last point back to the first.
            segmentCount = loop ? n : n - 1;

            for (int seg = 0; seg < segmentCount; seg++)
            {
                Vector3 p0 = PointPosition(seg - 1);
                Vector3 p1 = PointPosition(seg);
                Vector3 p2 = PointPosition(seg + 1);
                Vector3 p3 = PointPosition(seg + 2);

                // Skip t = 0 after the first segment: it's where the last one ended.
                int first = seg == 0 ? 0 : 1;
                for (int i = first; i <= samplesPerSegment; i++)
                {
                    float t = i / (float)samplesPerSegment;
                    AddSample(CatmullRom(p0, p1, p2, p3, t), seg + t);
                }
            }
        }

        private Vector3 PointPosition(int index) => points[WrapIndex(index)].position;

        /// <summary>
        /// Loop: wrap like a clock face (-1 -> last point, n -> point 0).
        /// Open: hold the end points (Catmull-Rom just reuses them).
        /// The "+ n" makes negative numbers wrap properly, since -1 % n is -1 in C#.
        /// </summary>
        private int WrapIndex(int index)
        {
            int n = points.Count;
            return loop ? ((index % n) + n) % n : Mathf.Clamp(index, 0, n - 1);
        }

        private void AddSample(Vector3 point, float param)
        {
            if (samples.Count > 0)
                TotalLength += Vector3.Distance(samples[samples.Count - 1], point);

            samples.Add(point);
            cumulative.Add(TotalLength);
            pointParam.Add(param);
        }

        /// <summary>
        /// Position on the route after 'distance' metres (real height included),
        /// and the flat direction of travel there.
        /// </summary>
        public void Evaluate(float distance, out Vector3 position, out Vector3 forward)
        {
            if (!IsValid)
            {
                position = transform.position;
                forward = transform.forward;
                return;
            }

            int i = FindSampleIndex(Mathf.Clamp(distance, 0f, TotalLength), out float blend);
            position = Vector3.Lerp(samples[i], samples[i + 1], blend);

            forward = samples[i + 1] - samples[i];
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
        }

        /// <summary>
        /// Which two child points we're between at 'distance', and how far (t: 0..1).
        /// Riders use this to blend their own per-point settings.
        /// </summary>
        public void GetPointBlend(float distance, out int fromPoint, out int toPoint, out float t)
        {
            if (!IsValid)
            {
                fromPoint = toPoint = 0;
                t = 0f;
                return;
            }

            int i = FindSampleIndex(Mathf.Clamp(distance, 0f, TotalLength), out float blend);
            float param = Mathf.Lerp(pointParam[i], pointParam[i + 1], blend);

            int seg = Mathf.Min(Mathf.FloorToInt(param), segmentCount - 1);
            t = param - seg;
            fromPoint = WrapIndex(seg);
            toPoint = WrapIndex(seg + 1);
        }

        /// <summary>Binary search: the last sample at or before 'distance', plus the blend to the next.</summary>
        private int FindSampleIndex(float distance, out float blend)
        {
            int lo = 0;
            int hi = cumulative.Count - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cumulative[mid] <= distance) lo = mid;
                else hi = mid - 1;
            }

            float pieceLength = cumulative[lo + 1] - cumulative[lo];
            blend = pieceLength > 0.0001f ? (distance - cumulative[lo]) / pieceLength : 0f;
            return lo;
        }

        /// <summary>Catmull-Rom: a cubic curve through p1 (t=0) and p2 (t=1); p0 and p3 set the end directions.</summary>
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
            if (!Application.isPlaying) Rebuild();
            if (!IsValid) return;

            Gizmos.color = pathColour;
            for (int i = 0; i < samples.Count - 1; i++)
                Gizmos.DrawLine(samples[i], samples[i + 1]);

            for (int i = 0; i < points.Count; i++)
            {
                Gizmos.color = i == 0 ? Color.green : Color.white;
                Gizmos.DrawWireSphere(points[i].position, i == 0 ? 1f : 0.5f);
            }

#if UNITY_EDITOR
            string ending = loop ? "Start / End" : "Start";
            UnityEditor.Handles.Label(points[0].position + Vector3.up * 2f,
                $"{name} - {ending} - {TotalLength:0} m");
#endif
        }
    }
}