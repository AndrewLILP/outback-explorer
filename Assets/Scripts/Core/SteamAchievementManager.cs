// SteamAchievementManager.cs
// Listens to existing game events (AnimalDiscoveryManager, VisitManager) and
// unlocks the corresponding Steam achievements. Self-instantiating singleton,
// following the same pattern as AnimalDiscoveryManager / GameSaveManager.
//
// Requires the Steamworks.NET wrapper in the project before SteamManager /
// SteamUserStats calls will compile. Achievement API names below must be
// created first in the Steamworks partner dashboard
// (App Admin > Stats and Achievements) — names are case-sensitive and must
// match exactly.

using UnityEngine;
using RelaxingDrive.Animals;
using Steamworks; // Steamworks.NET

namespace RelaxingDrive.Core
{
    /// <summary>
    /// Single Responsibility: bridges existing gameplay events to Steam's
    /// achievement API. Does not know about discovery or visit logic itself -
    /// it only reacts to events already fired by AnimalDiscoveryManager and
    /// VisitManager.
    /// </summary>
    public class SteamAchievementManager : MonoBehaviour
    {
        private static SteamAchievementManager _instance;

        // Achievement API names - must match the Steamworks partner dashboard
        // exactly (case-sensitive).
        private const string ACH_FIRST_DISCOVERY = "ACH_FIRST_DISCOVERY";
        private const string ACH_ALL_ANIMALS_DISCOVERED = "ACH_ALL_ANIMALS_DISCOVERED";
        private const string ACH_TEN_VISITS = "ACH_TEN_VISITS";

        private const int TOTAL_ANIMAL_SPECIES = 7; // kangaroo, emu, echidna, koala,
                                                      // Tasmanian devil, frillneck lizard, platypus

        // Self-instantiating lazy singleton, same pattern as AnimalDiscoveryManager
        public static SteamAchievementManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("SteamAchievementManager");
                    _instance = go.AddComponent<SteamAchievementManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            // AnimalDiscoveryManager.OnAnimalDiscovered is Action<AnimalData, bool>
            // where bool = isFirstTime.
            AnimalDiscoveryManager.Instance.OnAnimalDiscovered += HandleAnimalDiscovered;

            // VisitManager's event is OnZoneVisited, Action<string zoneID, int newCount>.
            VisitManager.Instance.OnZoneVisited += HandleZoneVisited;
        }

        private void OnDestroy()
        {
            if (AnimalDiscoveryManager.Instance != null)
            {
                AnimalDiscoveryManager.Instance.OnAnimalDiscovered -= HandleAnimalDiscovered;
            }

            if (VisitManager.Instance != null)
            {
                VisitManager.Instance.OnZoneVisited -= HandleZoneVisited;
            }
        }

        private void HandleAnimalDiscovered(AnimalData animal, bool isFirstTime)
        {
            // Repeat sightings of an already-discovered animal shouldn't
            // re-trigger achievement checks.
            if (!isFirstTime) return;

            // GetDiscoveryCount() is the source of truth - it's also what
            // GameSaveManager restores on load, so this stays correct even
            // after a save/load rather than drifting like a local counter would.
            int discoveredCount = AnimalDiscoveryManager.Instance.GetDiscoveryCount();

            if (discoveredCount == 1)
            {
                UnlockAchievement(ACH_FIRST_DISCOVERY);
            }

            if (discoveredCount >= TOTAL_ANIMAL_SPECIES)
            {
                UnlockAchievement(ACH_ALL_ANIMALS_DISCOVERED);
            }
        }

        private void HandleZoneVisited(string zoneID, int newCount)
        {
            if (newCount >= 10)
            {
                UnlockAchievement(ACH_TEN_VISITS);
            }
        }

        /// <summary>
        /// Wraps the Steamworks call so the rest of the class stays readable
        /// and this is the single point of contact with the SDK.
        /// </summary>
        private void UnlockAchievement(string achievementID)
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogWarning($"[SteamAchievementManager] Steam not initialized - skipped: {achievementID}");
                return;
            }

            if (!SteamUserStats.GetAchievement(achievementID, out bool alreadyUnlocked))
            {
                Debug.LogWarning($"[SteamAchievementManager] Unknown achievement ID: {achievementID}");
                return;
            }

            if (alreadyUnlocked) return;

            SteamUserStats.SetAchievement(achievementID);
            SteamUserStats.StoreStats();

            Debug.Log($"[SteamAchievementManager] Unlocked: {achievementID}");
        }
    }
}
