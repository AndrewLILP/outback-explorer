// VisitManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RelaxingDrive.Core
{
    /// <summary>
    /// Singleton manager that tracks visit counts for all zones in the game.
    /// Uses Observer pattern to notify listeners when visit thresholds are reached.
    /// </summary>
    public class VisitManager : MonoBehaviour
    {
        // Singleton instance
        private static VisitManager instance;
        private static bool isQuitting = false;

        public static VisitManager Instance
        {
            get
            {
                // Don't try to create during shutdown
                if (isQuitting)
                    return null;

                // Auto-create if missing (helpful for testing)
                if (instance == null)
                {
                    // Try to find existing VisitManager in scene
                    instance = FindFirstObjectByType<VisitManager>();

                    // If still null, create one
                    if (instance == null)
                    {
                        GameObject managerObj = new GameObject("VisitManager (Auto-Created)");
                        instance = managerObj.AddComponent<VisitManager>();
                        Debug.LogWarning("VisitManager was missing - created automatically. Consider adding one to your scene manually.");
                    }
                }
                return instance;
            }
        }

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // Dictionary to store visit counts for each zone
        private Dictionary<string, int> visitCounts = new Dictionary<string, int>();

        // Event system for Observer pattern
        // Other systems can subscribe to be notified when a zone is visited
        public event Action<string, int> OnZoneVisited;

        private void Awake()
        {
            // Singleton pattern implementation
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"Duplicate VisitManager found on {gameObject.name}. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject); // Persist between scenes

            Debug.Log("VisitManager initialized successfully!");
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        /// <summary>
        /// Records a visit to a specific zone and notifies observers.
        /// </summary>
        /// <param name="zoneID">The unique identifier of the zone</param>
        public void RecordVisit(string zoneID)
        {
            // Initialize zone if first visit
            if (!visitCounts.ContainsKey(zoneID))
            {
                visitCounts[zoneID] = 0;
            }

            // Increment visit count
            visitCounts[zoneID]++;

            int currentCount = visitCounts[zoneID];

            if (showDebugMessages)
            {
                Debug.Log($"Zone '{zoneID}' visited. Total visits: {currentCount}");
            }

            // Notify all observers (buildings, NPCs, missions, etc.)
            OnZoneVisited?.Invoke(zoneID, currentCount);
        }

        /// <summary>
        /// Gets the current visit count for a specific zone.
        /// </summary>
        /// <param name="zoneID">The unique identifier of the zone</param>
        /// <returns>Number of visits, or 0 if never visited</returns>
        public int GetVisitCount(string zoneID)
        {
            return visitCounts.ContainsKey(zoneID) ? visitCounts[zoneID] : 0;
        }

        /// <summary>
        /// Checks if a zone has been visited at least once.
        /// </summary>
        public bool HasVisited(string zoneID)
        {
            return visitCounts.ContainsKey(zoneID) && visitCounts[zoneID] > 0;
        }

        /// <summary>
        /// Gets all visit data (useful for save/load system later).
        /// </summary>
        public Dictionary<string, int> GetAllVisitData()
        {
            return new Dictionary<string, int>(visitCounts);
        }

        /// <summary>
        /// Loads visit data (useful for save/load system later).
        /// </summary>
        public void LoadVisitData(Dictionary<string, int> data)
        {
            visitCounts = new Dictionary<string, int>(data);

            if (showDebugMessages)
            {
                Debug.Log($"Loaded visit data for {visitCounts.Count} zones.");
            }
        }

        /// <summary>
        /// Resets all visit counts (useful for testing).
        /// </summary>
        public void ResetAllVisits()
        {
            visitCounts.Clear();
            Debug.Log("All visit counts reset.");
        }
    }
}