// VisitZone.cs
// Location: Assets/Scripts/World/VisitZone.cs
// Visit Zones 2.0 — Stories #2 (presence tracking), startup gating, #1 (ZoneData)
using System;
using UnityEngine;
using UnityEngine.Serialization;
using RelaxingDrive.Core;

namespace RelaxingDrive.World
{
    /// <summary>
    /// Detects when the ACTIVE player (car or walker) enters and leaves this zone,
    /// and records one visit per entry with the VisitManager.
    ///
    /// Key ideas:
    /// 1. POLLING instead of OnTriggerEnter — one player position, no multi-collider noise,
    ///    and a parked car can't keep the zone "occupied".
    /// 2. HYSTERESIS — you enter at the edge but only "leave" exitBuffer metres outside it.
    /// 3. STARTUP GATING — nothing is recorded until the save has loaded and the car has settled.
    /// 4. ZONEDATA — the zone's identity is an asset shared with the buildings it unlocks.
    ///    The old typed string is kept as a fallback until every zone is migrated.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class VisitZone : MonoBehaviour
    {
        [Header("Zone Identity")]
        [Tooltip("The zone's identity asset (ID, display name, region). " +
                 "Create via Assets > Create > Sunday Drive > Zone Data, or run Tools > Sunday Drive > Zones > Migrate.")]
        [SerializeField] private ZoneData zoneData;

        // FormerlySerializedAs tells Unity "this field used to be called zoneID" so the
        // value already typed into every zone in your scene carries over to the new name.
        [Tooltip("LEGACY — only used when Zone Data is empty. Will be removed once all zones are migrated.")]
        [FormerlySerializedAs("zoneID")]
        [SerializeField] private string legacyZoneID;

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
        public event Action<VisitZone> OnPlayerEntered;
        public event Action<VisitZone> OnPlayerExited;

        // --- Public API ---
        /// <summary>The ZoneData asset, or null if this zone still uses a legacy string ID.</summary>
        public ZoneData Data => zoneData;

        /// <summary>The save/visit key. Prefers the ZoneData asset; falls back to the legacy string.</summary>
        public string ZoneID => zoneData != null ? zoneData.ZoneID : legacyZoneID;

        /// <summary>Player-facing name, e.g. "Lake Hume".</summary>
        public string DisplayName => zoneData != null ? zoneData.DisplayName : legacyZoneID;

        public bool IsPlayerInside => isPlayerInside;

        /// <summary>Did the current (or most recent) entry count as a visit? Used by the on-foot bonus.</summary>
        public bool EntryCounted => entryCounted;

        private BoxCollider zoneCollider;
        private bool entryCounted;
        private IActivePlayerProvider playerProvider;
        private bool isPlayerInside;
        private bool hasCheckedOnce;
        private float checkTimer;
        private float nextProviderSearchTime;
        private bool warnedNoProvider;
        private float playerFirstSeenTime = -1f;

        private const float ProviderSearchRetrySeconds = 2f;

        #region Unity Lifecycle

        private void Awake()
        {
            zoneCollider = GetComponent<BoxCollider>();
            zoneCollider.isTrigger = true; // never physically blocks the car

            if (string.IsNullOrEmpty(ZoneID))
            {
                Debug.LogError($"[VisitZone] '{name}' has no Zone Data or ID. Visits will not be recorded.", this);
            }
            else if (zoneData == null && showDebugMessages)
            {
                Debug.LogWarning($"[VisitZone] '{name}' still uses legacy ID '{legacyZoneID}'. " +
                                 "Run Tools > Sunday Drive > Zones > Migrate.", this);
            }
        }

        private void Update()
        {
            checkTimer -= Time.deltaTime; // scaled time: pauses with the game
            if (checkTimer > 0f) return;

            checkTimer = checkInterval;
            CheckPresence();
        }

        private void OnDisable()
        {
            isPlayerInside = false;
            hasCheckedOnce = false;
            entryCounted = false;
            playerFirstSeenTime = -1f;
        }

        private void OnValidate()
        {
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
            // GATE 1 — don't record until the save is applied (else LoadVisitData overwrites visits).
            if (!IsSaveLoaded()) return;

            Transform player = GetActivePlayerTransform();
            if (player == null) return; // e.g. RCC car hasn't spawned yet

            // GATE 2 — let the spawned car settle before the very first check.
            if (!hasCheckedOnce)
            {
                if (playerFirstSeenTime < 0f) playerFirstSeenTime = Time.time;
                if (Time.time - playerFirstSeenTime < startupSettleTime) return;
            }

            float distanceOutside = DistanceOutsideZone(player.position);
            bool isWithin = distanceOutside <= 0f;

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
                Enter(recordVisit: true);
            else if (isPlayerInside && distanceOutside > exitBuffer)
                Exit();
        }

        /// <summary>0 if inside the box, otherwise the distance (metres) to its surface.</summary>
        private float DistanceOutsideZone(Vector3 worldPoint)
        {
            Vector3 closest = zoneCollider.ClosestPoint(worldPoint);
            return Vector3.Distance(closest, worldPoint);
        }

        private void Enter(bool recordVisit)
        {
            isPlayerInside = true;
            string id = ZoneID;

            // VisitManager applies the visit policy (e.g. cooldown) and tells us if it counted.
            bool counted = false;
            if (recordVisit && !string.IsNullOrEmpty(id) && VisitManager.Instance != null)
                counted = VisitManager.Instance.RecordVisit(id);

            entryCounted = counted; // set BEFORE firing OnPlayerEntered so listeners see it

            if (showDebugMessages)
            {
                string note = !recordVisit ? "already here at start, not counted"
                            : counted ? "visit recorded"
                            : "not counted, see VisitManager log";
                Debug.Log($"[VisitZone] Player entered '{DisplayName}' [{id}] ({note})", this);
            }

            OnPlayerEntered?.Invoke(this);
        }

        private void Exit()
        {
            isPlayerInside = false;

            if (showDebugMessages)
                Debug.Log($"[VisitZone] Player left '{DisplayName}' [{ZoneID}]", this);

            OnPlayerExited?.Invoke(this);
        }

        #endregion

        #region Startup Gating

        /// <summary>
        /// True once GameSaveManager has finished its startup load. Asking for the Instance also
        /// creates the manager if missing, which guarantees the save loads at startup.
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
            playerProvider = activePlayerSource as IActivePlayerProvider;
            if (playerProvider != null) return;

            // Search the scene at most every couple of seconds.
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
                Debug.LogWarning($"[VisitZone] '{ZoneID}': no IActivePlayerProvider in scene yet. Will keep looking.", this);
            }
        }

        #endregion

        #region Editor Gizmos

        // Real collider shape; gold while the player is inside; label shows the display name.
        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = (Application.isPlaying && isPlayerInside)
                ? new Color(1f, 0.8f, 0.1f, 0.35f)
                : new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawCube(box.center, box.size);

#if UNITY_EDITOR
            string label;
            if (string.IsNullOrEmpty(ZoneID)) label = "⚠ NO ZONE ID";
            else if (zoneData == null) label = $"{legacyZoneID}  (legacy)";
            else label = zoneData.DisplayName;

            if (Application.isPlaying && isPlayerInside) label += "  (inside)";

            // Show the cooldown countdown while testing (updates when the Scene view repaints).
            if (Application.isPlaying && VisitManager.ExistingInstance != null)
            {
                float wait = VisitManager.ExistingInstance.GetSecondsUntilCountable(ZoneID);
                if (wait > 0f) label += $"  ⏱ {wait:0}s";
            }

            UnityEditor.Handles.Label(transform.TransformPoint(box.center), label);
#endif
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(box.center, box.size); // entry edge

            Vector3 scale = transform.lossyScale;
            Vector3 bufferLocal = new Vector3(
                2f * exitBuffer / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                2f * exitBuffer / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                2f * exitBuffer / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireCube(box.center, box.size + bufferLocal); // exit edge (approx.)
        }

        #endregion
    }
}