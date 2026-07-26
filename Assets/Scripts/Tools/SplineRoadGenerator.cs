using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal spline-to-drivable-road generator.
/// Attach to an empty GameObject, assign control points, hit Generate.
/// Produces: road mesh, collider, and a list of world-space waypoints
/// (usable for AI traffic, lane-keeping, or minimap logic).
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class SplineRoadGenerator : MonoBehaviour
{
    [Header("Path")]
    public List<Transform> controlPoints = new List<Transform>();

    [Header("Adaptive Sampling")]
    [Tooltip("Max allowed deviation (metres) between the true curve and a straight chord. Lower = smoother curves, more samples.")]
    public float curveDeviationTolerance = 0.05f;
    [Tooltip("Max distance (metres) allowed between samples even on dead-straight sections. Prevents overly sparse geometry.")]
    public float maxSampleSpacing = 25f;
    [Range(1, 12)] public int maxSubdivisionDepth = 8;

    [Header("Road Shape")]
    public float roadWidth = 10f;
    public float uvTilesPerMeter = 0.25f;

    [Header("Terrain Conform (optional)")]
    public bool snapToTerrain = false;
    public LayerMask terrainLayer;
    public float heightOffset = 0.02f;

    [Header("Terrain Deformation (grades terrain to match road)")]
    [Tooltip("The Unity Terrain to sculpt under the road. Leave empty to skip deformation entirely.")]
    public Terrain targetTerrain;
    [Tooltip("Flat extra width (metres) beyond the road edge, before the falloff blend starts. Gives mowers/verges/guardrail footings room and hides any residual seam.")]
    public float shoulderWidth = 2f;
    [Tooltip("How far (metres) the shoulder sits below the road edge height. Small values (0.02-0.05) read as a subtle verge without a visible cliff.")]
    public float shoulderDrop = 0.03f;
    [Tooltip("Depth (metres) to carve terrain below the road SURFACE, keyed by normalized offset from centerline (0) to road edge (1). Default dips the centerline slightly while keeping edges flush, since that's normally where terrain pokes through — tune per-project.")]
    public AnimationCurve carveDepth = new AnimationCurve(new Keyframe(0f, 0.08f), new Keyframe(1f, 0f));
    [Tooltip("Distance (metres) beyond the shoulder over which terrain blends back to its original height.")]
    public float terrainFalloffDistance = 8f;
    [Tooltip("Marching interval (metres) along the road when writing terrain heights. Smaller = more accurate but slower. Should be roughly your terrain's metres-per-heightmap-pixel or finer.")]
    public float terrainSampleSpacing = 2f;

    private Mesh _mesh;

    // Waypoint = position + forward direction + right direction (for lanes)
    [System.Serializable]
    public struct RoadWaypoint
    {
        public Vector3 position;
        public Vector3 forward;
        public Vector3 right;
    }

    // Cross-section = the actual post-snap mesh edge positions at a centerPoints index.
    // This is the ground-truth surface DeformTerrainUnderRoad carves to — it's built from
    // the SAME snapped left/right verts BuildMesh puts in the mesh, so the terrain carve
    // can never disagree with what's actually rendered (the old centerline-clipping bug
    // came from the carve using a different, unsnapped height source than the mesh).
    private struct RoadCrossSection
    {
        public Vector3 center;
        public Vector3 right;   // lateral direction, unit length
        public Vector3 left;    // snapped left edge, world space
        public Vector3 rightEdge; // snapped right edge, world space
    }

    [HideInInspector] public List<RoadWaypoint> waypoints = new List<RoadWaypoint>();
    private List<RoadCrossSection> _crossSections = new List<RoadCrossSection>();

    [ContextMenu("Generate Road")]
    public void Generate()
    {
        if (controlPoints.Count < 2)
        {
            Debug.LogWarning("Need at least 2 control points.");
            return;
        }

        waypoints.Clear();
        _crossSections.Clear();
        List<Vector3> centerPoints = SampleCatmullRom();

        BuildMesh(centerPoints);
        BuildWaypoints();
    }

    // --- Catmull-Rom spline sampling (curvature-adaptive) ---
    private List<Vector3> SampleCatmullRom()
    {
        List<Vector3> points = new List<Vector3>();
        int count = controlPoints.Count;

        // Seed the very first point; every subsequent point is emitted by the
        // recursive subdivision below (each leaf's "end" becomes the next leaf's "start").
        points.Add(controlPoints[0].position);

        for (int i = 0; i < count - 1; i++)
        {
            Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)].position;
            Vector3 p1 = controlPoints[i].position;
            Vector3 p2 = controlPoints[i + 1].position;
            Vector3 p3 = controlPoints[Mathf.Min(i + 2, count - 1)].position;

            SubdivideCatmullRom(p0, p1, p2, p3, 0f, 1f, points, 0);
        }
        return points;
    }

    // Recursively splits a segment in half until the curve is "flat enough"
    // (within curveDeviationTolerance of a straight chord) or maxSampleSpacing
    // is satisfied. This concentrates samples on tight curves and thins them
    // out on straights, instead of using a fixed count everywhere.
    private void SubdivideCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        float t0, float t1, List<Vector3> points, int depth)
    {
        Vector3 start = CatmullRomPoint(p0, p1, p2, p3, t0);
        Vector3 end = CatmullRomPoint(p0, p1, p2, p3, t1);
        Vector3 mid = CatmullRomPoint(p0, p1, p2, p3, (t0 + t1) * 0.5f);

        Vector3 chord = end - start;
        float chordLength = chord.magnitude;

        float deviation;
        if (chordLength < 0.0001f)
        {
            deviation = Vector3.Distance(mid, start);
        }
        else
        {
            Vector3 chordDir = chord / chordLength;
            float projected = Vector3.Dot(mid - start, chordDir);
            Vector3 closestPointOnChord = start + chordDir * projected;
            deviation = Vector3.Distance(mid, closestPointOnChord);
        }

        bool curveTooSharp = deviation > curveDeviationTolerance;
        bool chordTooLong = chordLength > maxSampleSpacing;

        if (depth < maxSubdivisionDepth && (curveTooSharp || chordTooLong))
        {
            float tMid = (t0 + t1) * 0.5f;
            SubdivideCatmullRom(p0, p1, p2, p3, t0, tMid, points, depth + 1);
            SubdivideCatmullRom(p0, p1, p2, p3, tMid, t1, points, depth + 1);
        }
        else
        {
            // Flat/short enough — accept this chord as-is.
            // "start" was already emitted by the previous leaf (or the initial seed point),
            // so only emit "end" here to avoid duplicate points.
            points.Add(end);
        }
    }

    private Vector3 CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    // --- Mesh extrusion along the sampled path ---
    private void BuildMesh(List<Vector3> centerPoints)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float distanceAccum = 0f;

        for (int i = 0; i < centerPoints.Count; i++)
        {
            Vector3 forward;
            if (i == 0) forward = (centerPoints[1] - centerPoints[0]).normalized;
            else if (i == centerPoints.Count - 1) forward = (centerPoints[i] - centerPoints[i - 1]).normalized;
            else forward = (centerPoints[i + 1] - centerPoints[i - 1]).normalized;

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 center = centerPoints[i];

            if (snapToTerrain)
                center = SnapToGround(center);

            Vector3 left = center - right * roadWidth * 0.5f;
            Vector3 rightEdge = center + right * roadWidth * 0.5f;

            if (snapToTerrain)
            {
                left = SnapToGround(left);
                rightEdge = SnapToGround(rightEdge);
            }

            // Cache the ground-truth cross-section (same points going into the mesh)
            // so DeformTerrainUnderRoad can carve to exactly what gets rendered,
            // instead of re-deriving a different height independently.
            _crossSections.Add(new RoadCrossSection
            {
                center = center,
                right = right,
                left = left,
                rightEdge = rightEdge
            });

            verts.Add(transform.InverseTransformPoint(left));
            verts.Add(transform.InverseTransformPoint(rightEdge));

            if (i > 0) distanceAccum += Vector3.Distance(centerPoints[i], centerPoints[i - 1]);
            float v = distanceAccum * uvTilesPerMeter;
            uvs.Add(new Vector2(0, v));
            uvs.Add(new Vector2(1, v));

            if (i > 0)
            {
                int baseIndex = (i - 1) * 2;
                // two triangles per slice
                tris.Add(baseIndex);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 1);

                tris.Add(baseIndex + 1);
                tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 3);
            }
        }

        _mesh = new Mesh { name = "GeneratedRoad" };
        _mesh.SetVertices(verts);
        _mesh.SetUVs(0, uvs);
        _mesh.SetTriangles(tris, 0);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;
        GetComponent<MeshCollider>().sharedMesh = _mesh;
    }

    private Vector3 SnapToGround(Vector3 point)
    {
        Vector3 rayOrigin = point + Vector3.up * 500f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2000f, terrainLayer))
        {
            return hit.point + Vector3.up * heightOffset;
        }
        return point;
    }

    // --- Waypoints for driving AI / lane logic ---
    // Now just reprojects _crossSections (built once in BuildMesh) instead of
    // recomputing forward/right from centerPoints a second time — previously
    // BuildMesh and BuildWaypoints each derived their own forward/right from
    // centerPoints independently, which could drift by float rounding even
    // though they're meant to describe the same road.
    private void BuildWaypoints()
    {
        for (int i = 0; i < _crossSections.Count; i++)
        {
            var cs = _crossSections[i];
            Vector3 forward = (i < _crossSections.Count - 1)
                ? (_crossSections[i + 1].center - cs.center).normalized
                : (cs.center - _crossSections[i - 1].center).normalized;

            waypoints.Add(new RoadWaypoint
            {
                position = cs.center,
                forward = forward,
                right = cs.right
            });
        }
    }

    private void OnDrawGizmos()
    {
        if (controlPoints == null) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            if (controlPoints[i] != null && controlPoints[i + 1] != null)
                Gizmos.DrawLine(controlPoints[i].position, controlPoints[i + 1].position);
        }

        if (waypoints != null)
        {
            Gizmos.color = Color.cyan;
            foreach (var wp in waypoints)
                Gizmos.DrawSphere(wp.position, 0.15f);
        }
    }

    // ============================================================
    // TERRAIN DEFORMATION
    // Grades the terrain heightmap to match the ACTUAL road mesh
    // surface (interpolated between the same snapped left/right
    // verts BuildMesh produced), applies a carve-depth curve under
    // the road bed, a flat dropped shoulder beyond the road edge,
    // then blends back to original terrain over terrainFalloffDistance.
    // Run this AFTER Generate() so _crossSections exist.
    // This is an editor-time authoring tool, not a runtime operation —
    // it directly rewrites terrain heightmap data.
    // ============================================================

    [ContextMenu("Deform Terrain Under Road")]
    public void DeformTerrainUnderRoad()
    {
        if (targetTerrain == null)
        {
            Debug.LogWarning("Assign a Target Terrain before deforming.");
            return;
        }
        if (_crossSections == null || _crossSections.Count < 2)
        {
            Debug.LogWarning("No road cross-sections yet — run Generate Road first.");
            return;
        }

        TerrainData data = targetTerrain.terrainData;
        int res = data.heightmapResolution; // Unity terrains use a square heightmap
        Vector3 terrainPos = targetTerrain.transform.position;
        Vector3 terrainSize = data.size;

        // Unity's heightmap array is indexed [z, x] (row = z, column = x) — easy to
        // get backwards, so it's called out explicitly here.
        float[,] heights = data.GetHeights(0, 0, res, res);
        float[,] weights = new float[res, res];
        float[,] targetHeights = new float[res, res];

        float halfWidth = roadWidth * 0.5f;
        float shoulderOuter = halfWidth + shoulderWidth;
        float fullInfluence = shoulderOuter + terrainFalloffDistance;

        float metresPerPixelX = terrainSize.x / (res - 1);
        float metresPerPixelZ = terrainSize.z / (res - 1);
        // Sample the cross-section finer than a single heightmap pixel so we don't
        // skip pixels and leave untouched gaps in the corridor.
        float crossSectionStep = Mathf.Min(metresPerPixelX, metresPerPixelZ) * 0.5f;

        List<MarchSample> marchSamples = MarchAlongRoad(terrainSampleSpacing);

        foreach (var sample in marchSamples)
        {
            for (float offset = -fullInfluence; offset <= fullInfluence; offset += crossSectionStep)
            {
                Vector3 worldPos = sample.pos + sample.right * offset;

                float normX = (worldPos.x - terrainPos.x) / terrainSize.x;
                float normZ = (worldPos.z - terrainPos.z) / terrainSize.z;
                if (normX < 0f || normX > 1f || normZ < 0f || normZ > 1f) continue;

                int px = Mathf.RoundToInt(normX * (res - 1));
                int pz = Mathf.RoundToInt(normZ * (res - 1));

                float absOffset = Mathf.Abs(offset);
                float weight;
                float surfaceY;

                if (absOffset <= halfWidth)
                {
                    // On the road bed itself: interpolate the ACTUAL mesh height between
                    // the left and right snapped edges (this is what fixes centerline
                    // clipping — previously this used one flat unsnapped height for the
                    // whole bed, which only happened to agree with the mesh at the two edges).
                    float lateralT = (offset + halfWidth) / roadWidth; // 0 = left edge, 1 = right edge
                    surfaceY = Mathf.Lerp(sample.leftY, sample.rightY, lateralT);

                    float normOffset = absOffset / Mathf.Max(halfWidth, 0.0001f); // 0 center .. 1 edge
                    float depth = carveDepth.Evaluate(normOffset);
                    surfaceY -= depth;
                    weight = 1f;
                }
                else if (absOffset <= shoulderOuter)
                {
                    // Flat dropped shoulder, referenced off whichever edge this offset is on.
                    float edgeY = offset < 0f ? sample.leftY : sample.rightY;
                    surfaceY = edgeY - shoulderDrop;
                    weight = 1f;
                }
                else if (absOffset <= fullInfluence)
                {
                    float edgeY = offset < 0f ? sample.leftY : sample.rightY;
                    surfaceY = edgeY - shoulderDrop;
                    float t = (absOffset - shoulderOuter) / terrainFalloffDistance;
                    weight = 1f - Mathf.SmoothStep(0f, 1f, t); // smooth blend back to original terrain
                }
                else
                {
                    continue;
                }

                float targetNormHeight = Mathf.Clamp01((surfaceY - terrainPos.y) / terrainSize.y);

                // Multiple cross-sections can touch the same pixel (e.g. tight curves).
                // Keep whichever write pulls hardest toward the road (highest weight).
                if (weight > weights[pz, px])
                {
                    weights[pz, px] = weight;
                    targetHeights[pz, px] = targetNormHeight;
                }
            }
        }

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                if (weights[z, x] <= 0f) continue;
                heights[z, x] = Mathf.Lerp(heights[z, x], targetHeights[z, x], weights[z, x]);
            }
        }

        data.SetHeights(0, 0, heights);
        Debug.Log("Terrain deformed under road.");
    }

    private struct MarchSample
    {
        public Vector3 pos;
        public Vector3 right;
        public float leftY;
        public float rightY;
    }

    // Resamples _crossSections at a fixed world-space distance interval, carrying
    // the interpolated left/right edge HEIGHTS along with position — this is what
    // lets DeformTerrainUnderRoad reconstruct the true mesh surface at any point
    // along the road, not just at the sparse adaptive-sampling vertices.
    private List<MarchSample> MarchAlongRoad(float spacing)
    {
        var result = new List<MarchSample>();
        if (_crossSections.Count < 2) return result;

        result.Add(new MarchSample
        {
            pos = _crossSections[0].center,
            right = _crossSections[0].right,
            leftY = _crossSections[0].left.y,
            rightY = _crossSections[0].rightEdge.y
        });
        float distanceSinceLast = 0f;

        for (int i = 0; i < _crossSections.Count - 1; i++)
        {
            var csA = _crossSections[i];
            var csB = _crossSections[i + 1];

            float segLen = Vector3.Distance(csA.center, csB.center);
            if (segLen < 0.0001f) continue;

            float traveled = 0f;
            while (distanceSinceLast + (segLen - traveled) >= spacing)
            {
                traveled += spacing - distanceSinceLast;
                float t = traveled / segLen;

                result.Add(new MarchSample
                {
                    pos = Vector3.Lerp(csA.center, csB.center, t),
                    right = Vector3.Lerp(csA.right, csB.right, t).normalized,
                    leftY = Mathf.Lerp(csA.left.y, csB.left.y, t),
                    rightY = Mathf.Lerp(csA.rightEdge.y, csB.rightEdge.y, t)
                });
                distanceSinceLast = 0f;
            }
            distanceSinceLast += segLen - traveled;
        }
        return result;
    }
}
