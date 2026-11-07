// SightingManager.cs
// Location: Assets/Scripts/Animals/SightingManager.cs
//
// The single entry point for "the player saw an animal". Decides which award
// tier the sighting counts toward, records it, and tells observers.
//
// Tier rules (decided Nov 2026):
//   1. A species never seen before        -> First tier, new.
//   2. First tier not yet complete         -> First tier, repeat
//      (Local/Remote don't count until all 7 have a First sighting).
//   3. First tier complete                 -> the animal's region (Local or
//      Remote) tier; new if that species hasn't been seen in that region.
//
// AnimalDiscoveryManager is still the record for the First tier - we call it
// here so its existing event keeps driving the HUD strip and the
// ACH_FIRST_DISCOVERY achievement. This class only ADDS the Local/Remote lists.
//
// Lazy self-instantiating singleton + DontDestroyOnLoad, same pattern as
// AnimalDiscoveryManager.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RelaxingDrive.Animals
{
    public class SightingManager : MonoBehaviour
    {
        #region Singleton

        private static SightingManager instance;
        private static bool isQuitting = false;

        public static SightingManager Instance
        {
            get
            {
                if (isQuitting) return null;

                if (instance == null)
                {
                    instance = FindFirstObjectByType<SightingManager>();
                    if (instance == null)
                    {
                        GameObject managerObject = new GameObject("SightingManager");
                        instance = managerObject.AddComponent<SightingManager>();
                    }
                }
                return instance;
            }
        }

        #endregion

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // Species names seen in each region AFTER the First tier was complete.
        // Keyed by AnimalName, matching AnimalDiscoveryManager and the save file.
        private HashSet<string> localSightings = new HashSet<string>();
        private HashSet<string> remoteSightings = new HashSet<string>();

        private AnimalRoster roster;

        /// <summary>Fired on EVERY sighting (new or repeat). Award and HUD systems listen here.</summary>
        public event Action<SightingContext, SightingResult> OnSighting;

        public AnimalRoster Roster => roster;

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("[SightingManager] Duplicate instance detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            roster = AnimalRoster.Load();

            Log($"Initialized (roster: {(roster != null ? roster.Count : 0)} species)");
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Call this from any discovery trigger. Returns the verdict so the caller
        /// (e.g. the info card) can show the right text straight away.
        /// </summary>
        public SightingResult ReportSighting(SightingContext context)
        {
            if (context.Animal == null)
            {
                Debug.LogError("[SightingManager] ReportSighting called with null AnimalData.");
                return default;
            }

            AnimalDiscoveryManager discovery = AnimalDiscoveryManager.Instance;
            if (discovery == null) return default;

            // Rule 1 - DiscoverAnimal returns true only the very first time for this species.
            bool isNewSpecies = discovery.DiscoverAnimal(context.Animal);

            SightingResult result;
            if (isNewSpecies)
            {
                result = new SightingResult(SightingTier.First, true);
            }
            else if (!IsFirstTierComplete())
            {
                // Rule 2 - still working through the First tier.
                result = new SightingResult(SightingTier.First, false);
            }
            else
            {
                // Rule 3 - HashSet.Add returns false if the name was already there,
                // so it records AND tells us whether it's new in one call.
                bool isNewInRegion = GetSet(context.Region).Add(context.Animal.AnimalName);
                result = new SightingResult(ToTier(context.Region), isNewInRegion);
            }

            Log($"{context.Animal.AnimalName} [{context.Region}, {(context.PlayerOnFoot ? "on foot" : "driving")}] -> {result}");

            OnSighting?.Invoke(context, result);
            return result;
        }

        #endregion

        #region Queries

        /// <summary>True once every species in the roster has a First sighting.</summary>
        public bool IsFirstTierComplete()
        {
            AnimalDiscoveryManager discovery = AnimalDiscoveryManager.Instance;
            if (roster == null || roster.Count == 0 || discovery == null) return false;

            foreach (AnimalData animal in roster.Animals)
            {
                if (animal != null && !discovery.HasDiscovered(animal))
                    return false;
            }
            return true;
        }

        /// <summary>Has this species been seen in the given tier? (Used by HUD pips later.)</summary>
        public bool HasSighted(string animalName, SightingTier tier)
        {
            switch (tier)
            {
                case SightingTier.First:
                    return AnimalDiscoveryManager.Instance != null &&
                           AnimalDiscoveryManager.Instance.HasDiscovered(animalName);
                case SightingTier.Local:
                    return localSightings.Contains(animalName);
                case SightingTier.Remote:
                    return remoteSightings.Contains(animalName);
                default:
                    return false;
            }
        }

        public bool HasSighted(AnimalData animal, SightingTier tier) =>
            animal != null && HasSighted(animal.AnimalName, tier);

        /// <summary>How many roster species have been seen in this tier.</summary>
        public int GetSightingCount(SightingTier tier)
        {
            if (roster == null) return 0;

            int count = 0;
            foreach (AnimalData animal in roster.Animals)
            {
                if (HasSighted(animal, tier)) count++;
            }
            return count;
        }

        #endregion

        #region Save / Load

        /// <summary>Copy of one region's list, for GameSaveManager.</summary>
        public List<string> GetSightings(SightingRegion region) => new List<string>(GetSet(region));

        public void LoadSightingData(IEnumerable<string> local, IEnumerable<string> remote)
        {
            // Null-safe: saves from before this system have no lists.
            localSightings = local != null ? new HashSet<string>(local) : new HashSet<string>();
            remoteSightings = remote != null ? new HashSet<string>(remote) : new HashSet<string>();

            Log($"Loaded sightings - local: {localSightings.Count}, remote: {remoteSightings.Count}");
        }

        public void ResetAllSightings()
        {
            localSightings.Clear();
            remoteSightings.Clear();
            Log("All Local/Remote sightings reset.");
        }

        #endregion

        #region Helpers

        private HashSet<string> GetSet(SightingRegion region) =>
            region == SightingRegion.Local ? localSightings : remoteSightings;

        private static SightingTier ToTier(SightingRegion region) =>
            region == SightingRegion.Local ? SightingTier.Local : SightingTier.Remote;

        private void Log(string message)
        {
            if (showDebugMessages) Debug.Log($"[SightingManager] {message}");
        }

        #endregion

        #region Debug (right-click the component in the Inspector during Play)

        [ContextMenu("Print Sightings")]
        private void PrintSightings()
        {
            Debug.Log($"[SightingManager] First tier complete: {IsFirstTierComplete()}\n" +
                      $"First:  {GetSightingCount(SightingTier.First)}/{(roster != null ? roster.Count : 0)}\n" +
                      $"Local:  {string.Join(", ", localSightings)}\n" +
                      $"Remote: {string.Join(", ", remoteSightings)}");
        }

        // These go through ReportSighting() like a real sighting, so the HUD,
        // the info card and the award pop-up all react exactly as in play.

        [ContextMenu("Debug: Complete First Tier")]
        private void DebugCompleteFirstTier()
        {
            if (roster == null || AnimalDiscoveryManager.Instance == null) return;

            foreach (AnimalData animal in roster.Animals)
            {
                // Only report species not yet found - otherwise, once the tier
                // completes mid-loop, the remaining ones would count as Local.
                if (animal != null && !AnimalDiscoveryManager.Instance.HasDiscovered(animal))
                    ReportSighting(new SightingContext(animal, SightingRegion.Local, false, Vector3.zero));
            }
            Log("DEBUG: First tier completed.");
        }

        [ContextMenu("Debug: Sight All Local")]
        private void DebugSightAllLocal() => DebugSightAll(SightingRegion.Local);

        [ContextMenu("Debug: Sight All Remote")]
        private void DebugSightAllRemote() => DebugSightAll(SightingRegion.Remote);

        private void DebugSightAll(SightingRegion region)
        {
            if (roster == null) return;
            if (!IsFirstTierComplete())
            {
                Debug.LogWarning("[SightingManager] Complete the First tier first.");
                return;
            }

            foreach (AnimalData animal in roster.Animals)
            {
                if (animal != null)
                    ReportSighting(new SightingContext(animal, region, false, Vector3.zero));
            }
            Log($"DEBUG: all species sighted ({region}).");
        }

        #endregion
    }
}