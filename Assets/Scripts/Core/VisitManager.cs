// VisitManager.cs
// Location: Assets/Scripts/Core/VisitManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RelaxingDrive.Core
{
    /// <summary>
    /// Singleton manager that tracks visit counts for all zones in the game.
    /// Uses the Observer pattern to notify listeners (buildings, achievements, UI)
    /// and a swappable VisitPolicy (Strategy pattern) to decide what counts as a visit.
    ///
    /// The policy is enforced HERE rather than in each VisitZone, so every source of
    /// visits obeys the same rules from one place.
    /// </summary>
    public class VisitManager : MonoBehaviour
    {
        #region Singleton

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
                    instance = FindFirstObjectByType<VisitManager>();

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

        /// <summary>
        /// Returns the manager only if it already exists — never creates one.
        /// Use in OnDisable/OnDestroy: creating a manager during scene teardown leaves orphans behind.
        /// </summary>
        public static VisitManager ExistingInstance => instance;

        #endregion

        #region Inspector

        [Header("Visit Rules")]
        [Tooltip("Decides whether entering a zone counts as a visit (e.g. VisitPolicy_Default = 60 s cooldown). " +
                 "Swap in VisitPolicy_QuickTest while testing. Empty = every entry counts.")]
        [SerializeField] private VisitPolicy visitPolicy;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        #endregion

        #region State & Events

        // Saved: total visits per zone
        private Dictionary<string, int> visitCounts = new Dictionary<string, int>();

        // Session only (not saved): Time.time of each zone's last COUNTED visit.
        // Not saving it is deliberate — startup presence never counts anyway.
        private readonly Dictionary<string, float> lastCountedTime = new Dictionary<string, float>();

        /// <summary>Fired when a visit COUNTS: (zoneID, newTotal).</summary>
        public event Action<string, int> OnZoneVisited;

        /// <summary>Fired after saved visit counts are applied, so listeners can re-check thresholds.</summary>
        public event Action OnVisitDataLoaded;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"Duplicate VisitManager found on {gameObject.name}. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject); // Persist between scenes

            if (visitPolicy == null)
                Debug.LogWarning("[VisitManager] No Visit Policy assigned - every zone entry will count. " +
                                 "Assign VisitPolicy_Default in the Inspector.", this);

            Debug.Log("VisitManager initialized successfully!");
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        #endregion

        #region Recording Visits

        /// <summary>
        /// Asks the visit policy whether this entry counts; if so, records it and notifies observers.
        /// </summary>
        /// <returns>True if the visit counted, false if the policy rejected it.</returns>
        public bool RecordVisit(string zoneID)
        {
            if (string.IsNullOrEmpty(zoneID))
                return false;

            float secondsSince = SecondsSinceLastCounted(zoneID);

            // Strategy pattern in action: we don't know or care which rule this is.
            if (visitPolicy != null && !visitPolicy.ShouldCount(zoneID, secondsSince, out string reason))
            {
                if (showDebugMessages)
                    Debug.Log($"Zone '{zoneID}' entered but NOT counted ({reason}).");
                return false;
            }

            lastCountedTime[zoneID] = Time.time;
            AddVisit(zoneID, "visited");
            return true;
        }

        /// <summary>
        /// Adds a bonus visit that BYPASSES the visit policy. Bonuses reward a visit that has
        /// already counted (e.g. walking around on foot), so a cooldown shouldn't block them.
        /// Does not reset the cooldown timer.
        /// </summary>
        public void RecordBonusVisit(string zoneID, string source)
        {
            if (string.IsNullOrEmpty(zoneID))
                return;

            AddVisit(zoneID, $"bonus visit ({source})");
        }

        /// <summary>Shared by normal and bonus visits: increment, log, notify observers.</summary>
        private void AddVisit(string zoneID, string logLabel)
        {
            visitCounts.TryGetValue(zoneID, out int count);
            count++;
            visitCounts[zoneID] = count;

            if (showDebugMessages)
                Debug.Log($"Zone '{zoneID}' {logLabel}. Total visits: {count}");

            // Notify all observers (buildings, achievements, UI...)
            OnZoneVisited?.Invoke(zoneID, count);
        }

        /// <summary>Seconds until an entry to this zone would count again (0 = now). For debug display.</summary>
        public float GetSecondsUntilCountable(string zoneID)
        {
            if (visitPolicy == null || string.IsNullOrEmpty(zoneID))
                return 0f;

            return visitPolicy.SecondsUntilCountable(SecondsSinceLastCounted(zoneID));
        }

        private float SecondsSinceLastCounted(string zoneID)
        {
            return lastCountedTime.TryGetValue(zoneID, out float time)
                ? Time.time - time
                : float.PositiveInfinity; // never counted this session
        }

        #endregion

        #region Queries

        /// <summary>Number of counted visits, or 0 if never visited.</summary>
        public int GetVisitCount(string zoneID)
        {
            return visitCounts.TryGetValue(zoneID, out int count) ? count : 0;
        }

        /// <summary>Checks if a zone has been visited at least once.</summary>
        public bool HasVisited(string zoneID)
        {
            return GetVisitCount(zoneID) > 0;
        }

        #endregion

        #region Save / Load / Reset

        /// <summary>Copy of all visit counts, for saving.</summary>
        public Dictionary<string, int> GetAllVisitData()
        {
            return new Dictionary<string, int>(visitCounts);
        }

        /// <summary>Replaces visit counts with saved data, then tells listeners to re-check.</summary>
        public void LoadVisitData(Dictionary<string, int> data)
        {
            visitCounts = new Dictionary<string, int>(data);

            if (showDebugMessages)
                Debug.Log($"Loaded visit data for {visitCounts.Count} zones.");

            OnVisitDataLoaded?.Invoke();
        }

        /// <summary>Clears all counts and cooldown timers (new game / testing).</summary>
        public void ResetAllVisits()
        {
            visitCounts.Clear();
            lastCountedTime.Clear();
            Debug.Log("All visit counts reset.");
        }

        #endregion
    }
}