// VisitZone.cs
// Location: Assets/Scripts/World/VisitZone.cs
// Visit Zones 2.0 — Story #2: presence tracking
using System;
using UnityEngine;
using RelaxingDrive.Core;

namespace RelaxingDrive.World
{
    /// <summary>
    /// Detects when the ACTIVE player (car or walker) enters and leaves this zone,
    /// and records one visit per entry with the VisitManager.
    ///
    /// What changed from the old version, and why:
    ///
    /// 1. POLLING instead of OnTriggerEnter.
    ///    Trigger events fire once per collider. An RCC car has several child colliders,
    ///    so one drive-through used to count as 3–4 visits. Also the parked car stays
    ///    active while you walk, so trigger-based occupancy could never "empty".
    ///    Instead, a few times per second we ask: "is the active player's position
    ///    inside my box?" One player, one answer, no noise.
    ///
    /// 2. HYSTERESIS (an exit buffer).
    ///    You're "inside" as soon as you cross the edge, but you only count as "left"
    ///    once you're exitBuffer metres OUTSIDE. Like a thermostat that switches on at
    ///    19° and off at 21°, the gap stops rapid flicker when you drive along an edge.
    ///
    /// 3. Start-of-scene presence doesn't count by default.
    ///    Spawning (or loading a save) inside a zone isn't a real visit, and counting it
    ///    would let players farm visits by reloading.
    ///
    /// Cooldowns and the on-foot bonus come in stories #3 and #4. This class only
    /// answers "did the player arrive / leave?" (Single Responsibility).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class VisitZone : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [Tooltip("Unique identifier for this zone. Must match ActivatableObject target IDs and existing save files (e.g. Zone_02_North).")]
        [SerializeField] private string zoneID;

        [Header("Player Detection")]
        [Tooltip("Optional. Drag in the PlayerStateManager (anything implementing IActivePlayerProvider). If empty, it's found automatically.")]
        [SerializeField] private MonoBehaviour activePlayerSource;

        [Tooltip("Seconds between presence checks. 0.2 = five checks per second: responsive and very cheap.")]
        [SerializeField, Range(0.05f, 1f)] private float checkInterval = 0.2f;

        [Tooltip("Metres OUTSIDE the zone the player must travel before they count as having left. Prevents edge flicker.")]
        [SerializeField, Min(0f)] private float exitBuffer = 3f;

        [Tooltip("If the player is already inside when the scene starts (spawn or save load), should that count as a visit?")]
        [SerializeField] private bool countPresenceAtStart = false;

        [Tooltip("Seconds to wait after the player first appears before the first presence check. " +
                 "Lets a freshly spawned car drop/settle so it isn't counted as 'driving in'. Raise if you still see a visit at startup.")]
        [SerializeField, Min(0f)] private float startupSettleTime = 1f;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // --- Events (Observer pattern) ---
        // Later stories subscribe here: the on-foot bonus timer starts on enter,
        // the growth-hint UI shows on enter, and so on.
        public event Action<VisitZone> OnPlayerEntered;
        public event Action<VisitZone> OnPlayerExited;

        public string ZoneID => zoneID;
        public bool IsPlayerInside => isPlayerInside;

        private BoxCollider zoneCollider;
        private IActivePlayerProvider playerProvider;
        private bool isPlayerInside;
        private bool hasCheckedOnce;       // false until the first successful presence check
        private float checkTimer;
        private float nextProviderSearchTime;
        private bool warnedNoProvider;
        private float playerFirstSeenTime = -1f; // -1 = player not seen yet

        private const float ProviderSearchRetrySeconds = 2f;

        #region Unity Lifecycle

        private void Awake()
        {
            zoneCollider = GetComponent<BoxCollider>();
            // Still a trigger so the box never physically blocks the car,
            // but we no longer rely on trigger callbacks.
            zoneCollider.isTrigger = true;

            if (string.IsNullOrEmpty(zoneID))
            {
                // Old version auto-generated an ID from position. That silently broke
                // saves whenever a zone was moved, so now we fail loudly instead.
                Debug.LogError($"[VisitZone] '{name}' has no Zone ID. Visits will not be recorded.", this);
            }
        }

        private void Update()
        {
            // Simple timer so we only check 5x per second, not every frame.
            // Uses scaled time, so checks pause while the game is paused.
            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f) return;

            checkTimer = checkInterval;
            CheckPresence();
        }

        private void OnDisable()
        {
            // Reset quietly. If the zone is re-enabled with the player inside,
            // the next check treats it like scene start (no free visit).
            isPlayerInside = false;
            hasCheckedOnce = false;
            playerFirstSeenTime = -1f;
        }

        private void OnValidate()
        {
            // Catch wrong drag-and-drops in the Inspector immediately.
            if (activePlayerSource != null && !(activePlayerSource is IActivePlayerProvider))
            {
                Debug.LogWarning($"[VisitZone] '{activePlayerSource.name}' doesn't implement IActivePlayerProvider. Cleared.", this);
                activePlayerSource = null;
            }
        }

        #endregion

        #region Presence Detection

        private void CheckPresence()
        {
            // GATE 1 — Initialisation order.
            // Don't record anything until the save file has been applied, otherwise
            // LoadVisitData() would overwrite (and lose) visits recorded before it.
            if (!IsSaveLoaded()) return;

            Transform player = GetActivePlayerTransform();
            if (player == null) return; // e.g. RCC car hasn't spawned yet

            // GATE 2 — Let the spawned car settle before the very first check.
            if (!hasCheckedOnce)
            {
                if (playerFirstSeenTime < 0f) playerFirstSeenTime = Time.time;
                if (Time.time - playerFirstSeenTime < startupSettleTime) return;
            }

            float distanceOutside = DistanceOutsideZone(player.position);
            bool isWithin = distanceOutside <= 0f;

            // First successful check: decide whether being here already counts.
            if (!hasCheckedOnce)
            {
                hasCheckedOnce = true;
                if (isWithin)
                {
                    Enter(countPresenceAtStart);
                    return;
                }
            }

            if (!isPlayerInside && isWithin)
            {
                Enter(recordVisit: true);
            }
            else if (isPlayerInside && distanceOutside > exitBuffer)
            {
                Exit();
            }
        }

        /// <summary>
        /// Returns 0 if the point is inside the box, otherwise the distance (metres) to its surface.
        /// Collider.ClosestPoint returns the point itself when it's already inside — handy!
        /// Works for rotated and scaled boxes.
        /// </summary>
        private float DistanceOutsideZone(Vector3 worldPoint)
        {
            Vector3 closest = zoneCollider.ClosestPoint(worldPoint);
            return Vector3.Distance(closest, worldPoint);
        }

        private void Enter(bool recordVisit)
        {
            isPlayerInside = true;

            if (recordVisit && !string.IsNullOrEmpty(zoneID) && VisitManager.Instance != null)
            {
                VisitManager.Instance.RecordVisit(zoneID);
            }

            if (showDebugMessages)
            {
                string note = recordVisit ? "visit recorded" : "already here at start, not counted";
                Debug.Log($"[VisitZone] Player entered '{zoneID}' ({note})", this);
            }

            OnPlayerEntered?.Invoke(this);
        }

        private void Exit()
        {
            isPlayerInside = false;

            if (showDebugMessages)
                Debug.Log($"[VisitZone] Player left '{zoneID}'", this);

            OnPlayerExited?.Invoke(this);
        }

        #endregion

        #region Startup Gating

        /// <summary>
        /// True once GameSaveManager has finished its startup load (save found, no save, or error).
        /// Note: asking for GameSaveManager.Instance also CREATES the manager if the scene doesn't
        /// have one, which guarantees the save actually loads at startup instead of whenever
        /// something happens to touch it first.
        /// </summary>
        private static bool IsSaveLoaded()
        {
            GameSaveManager saveManager = GameSaveManager.Instance;
            return saveManager != null && saveManager.HasCompletedInitialLoad;
        }

        #endregion

        #region Player Lookup

        private Transform GetActivePlayerTransform()
        {
            if (playerProvider == null)
                ResolveProvider();

            return playerProvider?.ActivePlayerTransform;
        }

        private void ResolveProvider()
        {
            // 1. Use the Inspector reference if one was given.
            playerProvider = activePlayerSource as IActivePlayerProvider;
            if (playerProvider != null) return;

            // 2. Otherwise search the scene — but only every couple of seconds,
            //    so a missing manager doesn't cost a full scene search 5x per second.
            if (Time.time < nextProviderSearchTime) return;
            nextProviderSearchTime = Time.time + ProviderSearchRetrySeconds;

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is IActivePlayerProvider found)
                {
                    playerProvider = found;
                    return;
                }
            }

            if (!warnedNoProvider)
            {
                warnedNoProvider = true;
                Debug.LogWarning($"[VisitZone] '{zoneID}': no IActivePlayerProvider in scene yet. Will keep looking.", this);
            }
        }

        #endregion

        #region Editor Gizmos

        // Draws the REAL collider shape (old version drew transform.localScale, which
        // ignored the collider's own center/size). Turns gold while the player is inside.
        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = (Application.isPlaying && isPlayerInside)
                ? new Color(1f, 0.8f, 0.1f, 0.35f)   // gold = player inside
                : new Color(0f, 1f, 0f, 0.2f);       // green = empty
            Gizmos.DrawCube(box.center, box.size);

#if UNITY_EDITOR
            string label = string.IsNullOrEmpty(zoneID) ? "⚠ NO ZONE ID" : zoneID;
            if (Application.isPlaying && isPlayerInside) label += "  (inside)";
            UnityEditor.Handles.Label(transform.TransformPoint(box.center), label);
#endif
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;

            // Solid outline = entry edge.
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(box.center, box.size);

            // Faint outline = exit edge (approximate: buffer converted from world metres to local units).
            Vector3 scale = transform.lossyScale;
            Vector3 bufferLocal = new Vector3(
                2f * exitBuffer / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                2f * exitBuffer / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                2f * exitBuffer / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireCube(box.center, box.size + bufferLocal);
        }

        #endregion
    }
}
