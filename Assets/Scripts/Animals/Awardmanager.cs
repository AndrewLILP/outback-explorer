// AwardManager.cs
// Location: Assets/Scripts/Animals/AwardManager.cs
//
// Listens to SightingManager and fires OnAwardEarned when a sighting completes
// an award. It stores nothing: whether an award is earned is worked out from
// the saved sightings, so there's no extra save data to keep in sync.
//
// Observers:
//   - AwardPopupUI shows the centred message.
//   - Later, a Steam bridge can subscribe and unlock AwardData.SteamAchievementId.
//
// Lifetime: unlike the other managers this is NOT DontDestroyOnLoad. It lives
// and dies with the gameplay scene, so after "hold 1 = new game" it is
// re-created and subscribes to the NEW SightingManager. (A persistent one
// would stay subscribed to the old, destroyed SightingManager.)
// AwardPopupUI bootstraps it by calling AwardManager.Instance.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RelaxingDrive.Animals
{
    public class AwardManager : MonoBehaviour
    {
        #region Singleton (scene lifetime)

        private static AwardManager instance;
        private static bool isQuitting = false;

        public static AwardManager Instance
        {
            get
            {
                if (isQuitting) return null;

                if (instance == null)
                {
                    instance = FindFirstObjectByType<AwardManager>();
                    if (instance == null)
                    {
                        GameObject managerObject = new GameObject("AwardManager");
                        instance = managerObject.AddComponent<AwardManager>();
                    }
                }
                return instance;
            }
        }

        #endregion

        /// <summary>Folder inside any Resources folder that holds the AwardData assets.</summary>
        public const string AwardsResourceFolder = "Awards";

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        private List<AwardData> awards = new List<AwardData>();

        // Cached so OnDisable never calls SightingManager.Instance during teardown
        // (that would create a fresh SightingManager mid-way through a new game).
        private SightingManager sightings;

        /// <summary>Fired once when an award is earned.</summary>
        public event Action<AwardData> OnAwardEarned;

        public IReadOnlyList<AwardData> Awards => awards;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            awards = Resources.LoadAll<AwardData>(AwardsResourceFolder)
                              .OrderBy(a => a.SortOrder)
                              .ToList();

            if (awards.Count == 0)
                Debug.LogWarning($"[AwardManager] No AwardData assets found in Resources/{AwardsResourceFolder}/");
            else
                Log($"Loaded {awards.Count} award(s): {string.Join(", ", awards.Select(a => a.DisplayName))}");
        }

        private void OnEnable()
        {
            sightings = SightingManager.Instance;
            if (sightings != null) sightings.OnSighting += HandleSighting;
        }

        private void OnDisable()
        {
            if (sightings != null) sightings.OnSighting -= HandleSighting;
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void HandleSighting(SightingContext context, SightingResult result)
        {
            foreach (AwardData award in awards)
            {
                if (award.WasJustEarnedBy(result, sightings))
                {
                    Log($"🏆 Earned: {award.DisplayName}");
                    OnAwardEarned?.Invoke(award);
                }
            }
        }

        /// <summary>For the HUD / journal: is this award already earned?</summary>
        public bool IsEarned(AwardData award) => award != null && award.IsEarned(sightings);

        /// <summary>Species count for {count} in messages.</summary>
        public int SpeciesCount => sightings != null && sightings.Roster != null ? sightings.Roster.Count : 0;

        private void Log(string message)
        {
            if (showDebugMessages) Debug.Log($"[AwardManager] {message}");
        }
    }
}