using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor-time scatter tool for trees/props alongside a SplineRoadGenerator road.
/// Consumes the road's public `waypoints` (position/forward/right) — it does not
/// touch SplineRoadGenerator internals, so it stays a separate, swappable component
/// per the "consume waypoints, don't edit the generator" pattern.
///
/// Excludes road width + shoulder + an extra buffer, then scatters outward to
/// scatterDistance using a density-falloff curve, a minimum spacing grid to avoid
/// clumping, slope rejection, and weighted prefab + scale variation.
/// Re-running clears and regenerates its own container, so it's safe to iterate on.
/// </summary>
public class RoadsideScatter : MonoBehaviour
{
    [Header("Source")]
    public SplineRoadGenerator road;

    [System.Serializable]
    public struct ScatterPrefab
    {
        public GameObject prefab;
        [Min(0f)] public float weight;
        public Vector2 uniformScaleRange;
    }
    [Header("Prefabs")]
    public List<ScatterPrefab> prefabs = new List<ScatterPrefab>();

    [Header("Placement Band")]
    [Tooltip("Extra clearance (metres) beyond road width + shoulder before scatter is allowed to start.")]
    public float extraBuffer = 1f;
    [Tooltip("How far (metres) beyond the buffer the scatter band extends.")]
    public float scatterDistance = 15f;
    [Tooltip("Density multiplier keyed by normalized distance across the scatter band: 0 = just past the buffer, 1 = outer edge. Default is denser near the road, thinning with distance.")]
    public AnimationCurve densityFalloff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f));

    [Header("Density & Spacing")]
    [Tooltip("Spacing (metres) between candidate rows marched along the road.")]
    public float rowSpacing = 3f;
    [Tooltip("Minimum spacing (metres) enforced between any two placed objects, via a spatial grid.")]
    public float minSpacingBetweenObjects = 2.5f;
    [Range(0f, 1f)] public float placementProbability = 0.6f;
    [Tooltip("Seeded RNG so repeated runs with the same settings are reproducible while you tune.")]
    public int randomSeed = 12345;

    [Header("Surface")]
    public LayerMask groundLayer;
    public float maxSlopeDegrees = 35f;
    public bool alignToSurfaceNormal = true;
    public float raycastHeight = 500f;
    public float raycastDistance = 2000f;

    [System.Serializable]
    public struct ExclusionZone
    {
        public Transform center;
        public float radius;
    }
    [Header("Exclusion Zones (optional)")]
    [Tooltip("Manual keep-out circles — e.g. VisitZones, buildings, existing landmarks — checked before every placement.")]
    public List<ExclusionZone> exclusionZones = new List<ExclusionZone>();

    [Header("Output")]
    [Tooltip("Container for spawned objects. Auto-created as a child if left empty. Cleared and rebuilt each run.")]
    public Transform scatterContainer;

    private readonly List<Vector3> _placedPositions = new List<Vector3>();
    private Dictionary<(int, int), List<int>> _spatialGrid;

    [ContextMenu("Scatter Roadside Objects")]
    public void Scatter()
    {
        if (road == null)
        {
            Debug.LogWarning("Assign a source SplineRoadGenerator before scattering.");
            return;
        }
        if (road.waypoints == null || road.waypoints.Count < 2)
        {
            Debug.LogWarning("Source road has no waypoints yet — run Generate Road on it first.");
            return;
        }
        if (prefabs == null || prefabs.Count == 0)
        {
            Debug.LogWarning("Add at least one prefab to scatter.");
            return;
        }

        PrepareContainer();
        _placedPositions.Clear();
        _spatialGrid = new Dictionary<(int, int), List<int>>();

        var rng = new System.Random(randomSeed);

        float bufferInner = road.roadWidth * 0.5f + road.shoulderWidth + extraBuffer;
        float bufferOuter = bufferInner + scatterDistance;

        List<(Vector3 pos, Vector3 forward, Vector3 right)> rows = MarchWaypoints(rowSpacing);

        int placedCount = 0;

        foreach (var row in rows)
        {
            foreach (int side in new[] { -1, 1 })
            {
                float offset = bufferInner;
                while (offset <= bufferOuter)
                {
                    float lateralJitter = ((float)rng.NextDouble() * 2f - 1f) * minSpacingBetweenObjects * 0.4f;
                    float alongJitter = ((float)rng.NextDouble() * 2f - 1f) * rowSpacing * 0.4f;
                    float actualOffset = offset + lateralJitter;

                    Vector3 candidate = row.pos
                        + row.right * side * actualOffset
                        + row.forward * alongJitter;

                    float t = Mathf.InverseLerp(bufferInner, bufferOuter, actualOffset);
                    float density = densityFalloff.Evaluate(Mathf.Clamp01(t));

                    bool pass = rng.NextDouble() <= density * placementProbability
                        && IsFarEnoughFromPlaced(candidate)
                        && !IsInsideExclusionZone(candidate);

                    if (pass && TryGetGroundPoint(candidate, out Vector3 groundPos, out Vector3 normal))
                    {
                        float slope = Vector3.Angle(normal, Vector3.up);
                        if (slope <= maxSlopeDegrees && TryPickWeightedPrefab(rng, out GameObject prefab, out Vector2 scaleRange))
                        {
                            SpawnInstance(prefab, groundPos, normal, scaleRange, rng);
                            RegisterPlacement(groundPos);
                            placedCount++;
                        }
                    }

                    offset += minSpacingBetweenObjects;
                }
            }
        }

        Debug.Log($"RoadsideScatter placed {placedCount} objects along {rows.Count} rows.");
    }

    [ContextMenu("Clear Scattered Objects")]
    public void Clear()
    {
        PrepareContainer(clearOnly: true);
    }

    private void PrepareContainer(bool clearOnly = false)
    {
        if (scatterContainer == null)
        {
            Transform existing = transform.Find("ScatterContainer");
            if (existing != null)
            {
                scatterContainer = existing;
            }
            else if (!clearOnly)
            {
                var go = new GameObject("ScatterContainer");
                go.transform.SetParent(transform, false);
                scatterContainer = go.transform;
            }
            else
            {
                return; // nothing to clear
            }
        }

        for (int i = scatterContainer.childCount - 1; i >= 0; i--)
        {
            var child = scatterContainer.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    // Same resampling approach as SplineRoadGenerator.MarchAlongRoad, but built from
    // the public RoadWaypoint list rather than the generator's private cross-sections,
    // since this component intentionally only consumes public road data.
    private List<(Vector3 pos, Vector3 forward, Vector3 right)> MarchWaypoints(float spacing)
    {
        var result = new List<(Vector3, Vector3, Vector3)>();
        var wps = road.waypoints;
        if (wps.Count < 2) return result;

        result.Add((wps[0].position, wps[0].forward, wps[0].right));
        float distanceSinceLast = 0f;

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
                Vector3 pos = Vector3.Lerp(a, b, t);
                Vector3 fwd = Vector3.Lerp(wps[i].forward, wps[i + 1].forward, t).normalized;
                Vector3 right = Vector3.Lerp(wps[i].right, wps[i + 1].right, t).normalized;
                result.Add((pos, fwd, right));
                distanceSinceLast = 0f;
            }
            distanceSinceLast += segLen - traveled;
        }
        return result;
    }

    private bool TryGetGroundPoint(Vector3 point, out Vector3 groundPos, out Vector3 normal)
    {
        Vector3 rayOrigin = point + Vector3.up * raycastHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
        {
            groundPos = hit.point;
            normal = hit.normal;
            return true;
        }
        groundPos = point;
        normal = Vector3.up;
        return false;
    }

    private bool TryPickWeightedPrefab(System.Random rng, out GameObject prefab, out Vector2 scaleRange)
    {
        float totalWeight = 0f;
        foreach (var p in prefabs)
            if (p.prefab != null) totalWeight += Mathf.Max(0f, p.weight);

        if (totalWeight <= 0f)
        {
            prefab = null;
            scaleRange = Vector2.one;
            return false;
        }

        float roll = (float)rng.NextDouble() * totalWeight;
        float accum = 0f;
        foreach (var p in prefabs)
        {
            if (p.prefab == null) continue;
            accum += Mathf.Max(0f, p.weight);
            if (roll <= accum)
            {
                prefab = p.prefab;
                scaleRange = p.uniformScaleRange == Vector2.zero ? new Vector2(1f, 1f) : p.uniformScaleRange;
                return true;
            }
        }

        // Fallback (float rounding) — use the last valid entry.
        for (int i = prefabs.Count - 1; i >= 0; i--)
        {
            if (prefabs[i].prefab != null)
            {
                prefab = prefabs[i].prefab;
                scaleRange = prefabs[i].uniformScaleRange == Vector2.zero ? new Vector2(1f, 1f) : prefabs[i].uniformScaleRange;
                return true;
            }
        }

        prefab = null;
        scaleRange = Vector2.one;
        return false;
    }

    private void SpawnInstance(GameObject prefab, Vector3 groundPos, Vector3 normal, Vector2 scaleRange, System.Random rng)
    {
        Quaternion rotation = alignToSurfaceNormal
            ? Quaternion.FromToRotation(Vector3.up, normal)
            : Quaternion.identity;

        float yaw = (float)rng.NextDouble() * 360f;
        rotation *= Quaternion.Euler(0f, yaw, 0f);

        var instance = Instantiate(prefab, groundPos, rotation, scatterContainer);

        float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());
        instance.transform.localScale *= scale;
    }

    // --- Minimum-spacing rejection via a coarse spatial hash (cell size = minSpacingBetweenObjects) ---
    private bool IsFarEnoughFromPlaced(Vector3 candidate)
    {
        if (_spatialGrid == null || minSpacingBetweenObjects <= 0f) return true;

        var cell = CellOf(candidate);
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                var key = (cell.Item1 + dx, cell.Item2 + dz);
                if (!_spatialGrid.TryGetValue(key, out List<int> indices)) continue;
                foreach (int idx in indices)
                {
                    if (Vector3.Distance(_placedPositions[idx], candidate) < minSpacingBetweenObjects)
                        return false;
                }
            }
        }
        return true;
    }

    private void RegisterPlacement(Vector3 pos)
    {
        _placedPositions.Add(pos);
        var cell = CellOf(pos);
        if (!_spatialGrid.TryGetValue(cell, out List<int> indices))
        {
            indices = new List<int>();
            _spatialGrid[cell] = indices;
        }
        indices.Add(_placedPositions.Count - 1);
    }

    private (int, int) CellOf(Vector3 pos)
    {
        float size = Mathf.Max(minSpacingBetweenObjects, 0.01f);
        return (Mathf.FloorToInt(pos.x / size), Mathf.FloorToInt(pos.z / size));
    }

    private bool IsInsideExclusionZone(Vector3 candidate)
    {
        if (exclusionZones == null) return false;
        foreach (var zone in exclusionZones)
        {
            if (zone.center == null) continue;
            if (Vector3.Distance(zone.center.position, candidate) <= zone.radius)
                return true;
        }
        return false;
    }
}
