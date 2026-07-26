using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor-time guardrail generator for a SplineRoadGenerator road.
/// Consumes the road's public `waypoints` only (same pattern as RoadsideScatter) —
/// no edits to SplineRoadGenerator required.
///
/// Unlike the road mesh (adaptive sampling, sparse on straights), this resamples
/// the route at a fixed, tighter spacing of its own: guardrails need consistent
/// post spacing and a straighter-reading rail line, not curvature-adaptive density.
///
/// Placement is conditional, per side, based on two independent triggers:
///   - a terrain drop-off/embankment beyond the shoulder edge
///   - a sharp curve (turn rate exceeding a threshold), which is symmetric (both sides)
/// Runs of consecutive "needs rail" samples are merged (with lead-in/lead-out padding
/// and small-gap bridging) into contiguous rail segments, each of which gets its own
/// extruded plank mesh(es) — same flat quad-strip technique as the road mesh, just
/// oriented vertically instead of laterally — plus optional posts at fixed spacing.
/// </summary>
public class GuardrailGenerator : MonoBehaviour
{
    [Header("Source")]
    public SplineRoadGenerator road;

    [Header("Own Sampling")]
    [Tooltip("Fixed spacing (metres) for guardrail sampling — independent of the road mesh's adaptive spacing.")]
    public float railSampleSpacing = 2f;

    [Header("Trigger: Terrain Drop-off")]
    [Tooltip("How far (metres) beyond the shoulder edge to probe for a drop.")]
    public float embankmentCheckDistance = 4f;
    [Tooltip("Height drop (metres) over the check distance that counts as a hazard requiring a rail.")]
    public float embankmentDropThreshold = 1.2f;

    [Header("Trigger: Sharp Curve")]
    [Tooltip("Turn rate (degrees per metre) beyond which a curve is considered sharp enough to warrant a rail on both sides, independent of drop-off.")]
    public float curvatureThreshold = 4f;

    [Header("Run Shaping")]
    [Tooltip("Extra samples of lead-in/lead-out padding added before/after a detected hazard, so the rail starts before the danger rather than exactly at it.")]
    public int padSamples = 3;
    [Tooltip("Gaps (in samples) shorter than this are bridged rather than split into separate runs, to avoid flickering short segments.")]
    public int gapToleranceSamples = 2;
    [Tooltip("Runs shorter than this (in samples) after padding/bridging are discarded as noise.")]
    public int minRunLengthSamples = 3;

    [Header("Rail Geometry")]
    [Tooltip("Lateral distance (metres) from the shoulder's outer edge to the rail line.")]
    public float railOffset = 0.3f;
    public float railHeight = 0.75f;
    public float plankThickness = 0.08f;
    public bool doubleRail = true;
    public float lowerRailHeight = 0.35f;
    public float uvTilesPerMeter = 0.5f;

    [Header("Posts (optional)")]
    public GameObject postPrefab;
    public float postSpacing = 2f;
    public float postGroundOffset = -0.05f;

    [Header("Surface")]
    public LayerMask groundLayer;
    public float raycastHeight = 500f;
    public float raycastDistance = 2000f;

    [Header("Output")]
    [Tooltip("Container for generated rail segments and posts. Auto-created as a child if left empty. Cleared and rebuilt each run.")]
    public Transform railContainer;

    private struct RailSample
    {
        public Vector3 pos;
        public Vector3 forward;
        public Vector3 right;
        public float distanceFromStart;
    }

    [ContextMenu("Generate Guardrails")]
    public void Generate()
    {
        if (road == null)
        {
            Debug.LogWarning("Assign a source SplineRoadGenerator before generating guardrails.");
            return;
        }
        if (road.waypoints == null || road.waypoints.Count < 2)
        {
            Debug.LogWarning("Source road has no waypoints yet — run Generate Road on it first.");
            return;
        }

        PrepareContainer();

        List<RailSample> samples = MarchWaypoints(railSampleSpacing);
        if (samples.Count < 2)
        {
            Debug.LogWarning("Not enough resampled points to build guardrails — check railSampleSpacing.");
            return;
        }

        float shoulderOuter = road.roadWidth * 0.5f + road.shoulderWidth;
        float railLateral = shoulderOuter + railOffset;

        bool[] dropoffLeft = new bool[samples.Count];
        bool[] dropoffRight = new bool[samples.Count];
        bool[] sharpCurve = new bool[samples.Count];

        for (int i = 0; i < samples.Count; i++)
        {
            dropoffLeft[i] = HasDropoff(samples[i], -1, shoulderOuter);
            dropoffRight[i] = HasDropoff(samples[i], 1, shoulderOuter);
            sharpCurve[i] = IsSharpCurve(samples, i);
        }

        bool[] needsLeft = new bool[samples.Count];
        bool[] needsRight = new bool[samples.Count];
        for (int i = 0; i < samples.Count; i++)
        {
            needsLeft[i] = dropoffLeft[i] || sharpCurve[i];
            needsRight[i] = dropoffRight[i] || sharpCurve[i];
        }

        List<(int start, int end)> leftRuns = BuildRuns(needsLeft);
        List<(int start, int end)> rightRuns = BuildRuns(needsRight);

        int segmentCount = 0;
        foreach (var run in leftRuns) { BuildRailSegment(samples, run, -1, railLateral, segmentCount++); }
        foreach (var run in rightRuns) { BuildRailSegment(samples, run, 1, railLateral, segmentCount++); }

        Debug.Log($"GuardrailGenerator built {leftRuns.Count} left run(s) and {rightRuns.Count} right run(s) " +
                  $"from {samples.Count} samples ({segmentCount} mesh segments).");
    }

    [ContextMenu("Clear Guardrails")]
    public void Clear()
    {
        PrepareContainer(clearOnly: true);
    }

    // --- Sampling ---

    private List<RailSample> MarchWaypoints(float spacing)
    {
        var result = new List<RailSample>();
        var wps = road.waypoints;
        if (wps.Count < 2) return result;

        result.Add(new RailSample { pos = wps[0].position, forward = wps[0].forward, right = wps[0].right, distanceFromStart = 0f });
        float distanceSinceLast = 0f;
        float totalDistance = 0f;

        for (int i = 0; i < wps.Count - 1; i++)
        {
            Vector3 a = wps[i].position;
            Vector3 b = wps[i + 1].position;
            float segLen = Vector3.Distance(a, b);
            if (segLen < 0.0001f) continue;

            float traveled = 0f;
            while (distanceSinceLast + (segLen - traveled) >= spacing)
            {
                traveled += spacing - distanceSinceLast;
                float t = traveled / segLen;
                totalDistance += spacing;
                result.Add(new RailSample
                {
                    pos = Vector3.Lerp(a, b, t),
                    forward = Vector3.Lerp(wps[i].forward, wps[i + 1].forward, t).normalized,
                    right = Vector3.Lerp(wps[i].right, wps[i + 1].right, t).normalized,
                    distanceFromStart = totalDistance
                });
                distanceSinceLast = 0f;
            }
            distanceSinceLast += segLen - traveled;
        }
        return result;
    }

    // --- Triggers ---

    private bool HasDropoff(RailSample sample, int side, float shoulderOuter)
    {
        Vector3 edgePos = sample.pos + sample.right * side * shoulderOuter;
        Vector3 farPos = sample.pos + sample.right * side * (shoulderOuter + embankmentCheckDistance);

        if (!TryGetGroundHeight(edgePos, out float edgeY)) return false;
        if (!TryGetGroundHeight(farPos, out float farY)) return false;

        float drop = edgeY - farY; // positive if terrain falls away on this side
        return drop >= embankmentDropThreshold;
    }

    private bool IsSharpCurve(List<RailSample> samples, int index)
    {
        int prev = Mathf.Max(index - 1, 0);
        int next = Mathf.Min(index + 1, samples.Count - 1);
        if (prev == next) return false;

        float angle = Vector3.Angle(samples[prev].forward, samples[next].forward);
        float distance = Mathf.Max(samples[next].distanceFromStart - samples[prev].distanceFromStart, 0.001f);
        float turnRatePerMetre = angle / distance;
        return turnRatePerMetre >= curvatureThreshold;
    }

    // --- Run merging: raw flags -> padded, gap-bridged, min-length-filtered index ranges ---

    private List<(int start, int end)> BuildRuns(bool[] flags)
    {
        var rawRuns = new List<(int start, int end)>();
        int runStart = -1;
        for (int i = 0; i < flags.Length; i++)
        {
            if (flags[i] && runStart < 0) runStart = i;
            if (!flags[i] && runStart >= 0)
            {
                rawRuns.Add((runStart, i - 1));
                runStart = -1;
            }
        }
        if (runStart >= 0) rawRuns.Add((runStart, flags.Length - 1));

        // Bridge small gaps between consecutive raw runs.
        var bridged = new List<(int start, int end)>();
        foreach (var run in rawRuns)
        {
            if (bridged.Count > 0 && run.start - bridged[^1].end - 1 <= gapToleranceSamples)
            {
                var last = bridged[^1];
                bridged[^1] = (last.start, run.end);
            }
            else
            {
                bridged.Add(run);
            }
        }

        // Apply lead-in/lead-out padding, clamp to valid range, then drop short runs.
        var final = new List<(int start, int end)>();
        foreach (var run in bridged)
        {
            int start = Mathf.Max(run.start - padSamples, 0);
            int end = Mathf.Min(run.end + padSamples, flags.Length - 1);
            if (end - start + 1 >= minRunLengthSamples)
                final.Add((start, end));
        }
        return final;
    }

    // --- Mesh construction ---

    private void BuildRailSegment(List<RailSample> samples, (int start, int end) run, int side, float railLateral, int segmentIndex)
    {
        int count = run.end - run.start + 1;
        if (count < 2) return;

        var container = new GameObject($"Rail_{(side < 0 ? "L" : "R")}_{segmentIndex}");
        container.transform.SetParent(railContainer, false);
        container.AddComponent<MeshFilter>();
        var mr = container.AddComponent<MeshRenderer>();

        BuildPlankMesh(samples, run, side, railLateral, railHeight, container);

        if (doubleRail)
        {
            var lowerGo = new GameObject($"Rail_{(side < 0 ? "L" : "R")}_{segmentIndex}_Lower");
            lowerGo.transform.SetParent(railContainer, false);
            lowerGo.AddComponent<MeshFilter>();
            lowerGo.AddComponent<MeshRenderer>();
            BuildPlankMesh(samples, run, side, railLateral, lowerRailHeight, lowerGo);
        }

        if (postPrefab != null && postSpacing > 0f)
        {
            SpawnPosts(samples, run, side, railLateral, container.transform);
        }
    }

    private void BuildPlankMesh(List<RailSample> samples, (int start, int end) run, int side, float railLateral, float plankTopHeight, GameObject target)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float distanceAccum = 0f;

        for (int s = run.start; s <= run.end; s++)
        {
            RailSample sample = samples[s];
            Vector3 anchor = sample.pos + sample.right * side * railLateral;

            float groundY = TryGetGroundHeight(anchor, out float gy) ? gy : sample.pos.y;
            Vector3 low = new Vector3(anchor.x, groundY + plankTopHeight - plankThickness, anchor.z);
            Vector3 high = new Vector3(anchor.x, groundY + plankTopHeight, anchor.z);

            verts.Add(target.transform.InverseTransformPoint(low));
            verts.Add(target.transform.InverseTransformPoint(high));

            if (s > run.start) distanceAccum += Vector3.Distance(samples[s].pos, samples[s - 1].pos);
            float v = distanceAccum * uvTilesPerMeter;
            uvs.Add(new Vector2(0, v));
            uvs.Add(new Vector2(1, v));

            int localIndex = s - run.start;
            if (localIndex > 0)
            {
                int baseIndex = (localIndex - 1) * 2;
                tris.Add(baseIndex);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);

                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 3);
            }
        }

        var mesh = new Mesh { name = target.name };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        target.GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    private void SpawnPosts(List<RailSample> samples, (int start, int end) run, int side, float railLateral, Transform parent)
    {
        float distanceSinceLastPost = 0f;
        Vector3 lastPos = samples[run.start].pos;

        for (int s = run.start; s <= run.end; s++)
        {
            if (s > run.start) distanceSinceLastPost += Vector3.Distance(samples[s].pos, lastPos);
            lastPos = samples[s].pos;

            if (s == run.start || distanceSinceLastPost >= postSpacing)
            {
                RailSample sample = samples[s];
                Vector3 anchor = sample.pos + sample.right * side * railLateral;
                float groundY = TryGetGroundHeight(anchor, out float gy) ? gy : sample.pos.y;
                Vector3 postPos = new Vector3(anchor.x, groundY + postGroundOffset, anchor.z);

                Quaternion rot = Quaternion.LookRotation(sample.forward, Vector3.up);
                Instantiate(postPrefab, postPos, rot, parent);

                distanceSinceLastPost = 0f;
            }
        }
    }

    // --- Shared helpers ---

    private bool TryGetGroundHeight(Vector3 point, out float height)
    {
        Vector3 rayOrigin = point + Vector3.up * raycastHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
        {
            height = hit.point.y;
            return true;
        }
        height = point.y;
        return false;
    }

    private void PrepareContainer(bool clearOnly = false)
    {
        if (railContainer == null)
        {
            Transform existing = transform.Find("RailContainer");
            if (existing != null)
            {
                railContainer = existing;
            }
            else if (!clearOnly)
            {
                var go = new GameObject("RailContainer");
                go.transform.SetParent(transform, false);
                railContainer = go.transform;
            }
            else
            {
                return; // nothing to clear
            }
        }

        for (int i = railContainer.childCount - 1; i >= 0; i--)
        {
            var child = railContainer.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }
}
