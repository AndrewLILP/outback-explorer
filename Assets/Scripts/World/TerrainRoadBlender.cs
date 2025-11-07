// TerrainRoadBlender.cs
using UnityEngine;

/// <summary>
/// Blends terrain height with a road mesh by sampling the road's vertices
/// and adjusting the terrain heightmap underneath.
/// Attach this to a GameObject in your scene and assign the road and terrain.
/// </summary>
public class TerrainRoadBlender : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The road mesh to sample heights from")]
    public MeshFilter roadMesh;

    [Tooltip("The terrain to adjust")]
    public Terrain terrain;

    [Header("Blend Settings")]
    [Tooltip("How far from the road edge to blend (in meters)")]
    [Range(1f, 50f)]
    public float blendDistance = 10f;

    [Tooltip("Smoothness of the blend (higher = smoother)")]
    [Range(0.1f, 1f)]
    public float blendSmoothness = 0.5f;

    [Tooltip("Click to apply the blend")]
    public bool applyBlend = false;

    private void Update()
    {
        // Only run in editor when button is clicked
        if (applyBlend)
        {
            applyBlend = false;
            BlendTerrainToRoad();
        }
    }

    /// <summary>
    /// Main method that blends the terrain to match the road height
    /// </summary>
    public void BlendTerrainToRoad()
    {
        if (roadMesh == null || terrain == null)
        {
            Debug.LogError("TerrainRoadBlender: Missing road mesh or terrain reference!");
            return;
        }

        Debug.Log("Starting terrain blend...");

        TerrainData terrainData = terrain.terrainData;
        int heightmapResolution = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);

        // Get road mesh data in world space
        Mesh mesh = roadMesh.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Transform roadTransform = roadMesh.transform;

        // Convert road vertices to world space
        Vector3[] worldVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            worldVertices[i] = roadTransform.TransformPoint(vertices[i]);
        }

        // Process each heightmap point
        for (int y = 0; y < heightmapResolution; y++)
        {
            for (int x = 0; x < heightmapResolution; x++)
            {
                // Convert heightmap coordinates to world position
                Vector3 worldPos = HeightmapToWorld(x, y, terrainData, terrain.transform.position);

                // Find closest point on road and distance to it
                float minDistance = float.MaxValue;
                float roadHeight = 0f;

                foreach (Vector3 vertex in worldVertices)
                {
                    float distance = Vector3.Distance(new Vector3(worldPos.x, 0, worldPos.z),
                                                     new Vector3(vertex.x, 0, vertex.z));

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        roadHeight = vertex.y;
                    }
                }

                // Calculate blend factor based on distance
                if (minDistance < blendDistance)
                {
                    float blendFactor = Mathf.SmoothStep(0, 1, minDistance / blendDistance);
                    blendFactor = Mathf.Pow(blendFactor, blendSmoothness);

                    // Convert road world height to terrain height (0-1 normalized)
                    float targetHeight = (roadHeight - terrain.transform.position.y) / terrainData.size.y;

                    // Blend current height with road height
                    heights[y, x] = Mathf.Lerp(targetHeight, heights[y, x], blendFactor);
                }
            }
        }

        // Apply the modified heights back to terrain
        terrainData.SetHeights(0, 0, heights);

        Debug.Log("Terrain blend complete!");
    }

    /// <summary>
    /// Converts heightmap coordinates to world position
    /// </summary>
    private Vector3 HeightmapToWorld(int x, int y, TerrainData terrainData, Vector3 terrainPosition)
    {
        float xNormalized = (float)x / (terrainData.heightmapResolution - 1);
        float yNormalized = (float)y / (terrainData.heightmapResolution - 1);

        Vector3 worldPos = new Vector3(
            terrainPosition.x + xNormalized * terrainData.size.x,
            0,
            terrainPosition.z + yNormalized * terrainData.size.z
        );

        return worldPos;
    }

    // Draw gizmos to visualize blend distance
    private void OnDrawGizmosSelected()
    {
        if (roadMesh != null)
        {
            Gizmos.color = Color.yellow;
            Mesh mesh = roadMesh.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Transform roadTransform = roadMesh.transform;

            // Draw a few sample points with blend radius
            for (int i = 0; i < vertices.Length; i += 10)
            {
                Vector3 worldPos = roadTransform.TransformPoint(vertices[i]);
                Gizmos.DrawWireSphere(worldPos, blendDistance);
            }
        }
    }
}
