// OnFootVisitBonus.cs
// Location: Assets/Scripts/World/OnFootVisitBonus.cs
// Put ONE of these in the gameplay scene (e.g. on an empty "OnFootVisitBonus" GameObject).
using System;
using System.Collections.Generic;
using UnityEngine;
using RelaxingDrive.Core;

namespace RelaxingDrive.World
{
    /// <summary>
    /// Rewards getting out of the car: spend enough time ON FOOT inside a zone during
    /// one stay and that zone earns +1 bonus visit ("Took the scenic route ✓").
    ///
    /// Design notes:
    /// - OBSERVER: listens to every VisitZone's OnPlayerEntered / OnPlayerExited events
    ///   (added in story #2), so VisitZone needed almost no changes (Single Responsibility).
    /// - Walking time ACCUMULATES during a stay: hop back in the car and the timer pauses;
    ///   get out again and it resumes. Leaving the zone resets it.
    /// - One bonus per stay, and by default only on stays whose entry counted — otherwise
    ///   "walk out, walk back in" would sidestep the cooldown.
    /// - The bonus BYPASSES the visit policy: it rewards a visit that already counted,
    ///   so the cooldown shouldn't block it.
    ///
    /// Settings live on this scene component, so Play-mode edits REVERT when you stop
    /// (the opposite of ScriptableObject assets like the visit policy). Great for quick tests.
    /// </summary>
    public class OnFootVisitBonus : MonoBehaviour
    {
        [Header("Bonus Rules")]
        [Tooltip("Total seconds ON FOOT inside one zone (during one stay) to earn the bonus. " +
                 "20 = default; 3 is handy for quick testing.")]
        [SerializeField, Min(0.5f)] private float requiredWalkSeconds = 20f;

        [Tooltip("Only award a bonus on stays whose entry counted as a visit (stops cooldown farming). " +
                 "Untick for quick testing.")]
        [SerializeField] private bool requireCountedEntry = true;

        [Header("References")]
        [Tooltip("Optional. PlayerStateManager (anything implementing IActivePlayerProvider). Found automatically if empty.")]
        [SerializeField] private MonoBehaviour activePlayerSource;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        [Tooltip("Read-only: live walk progress while in Play mode (Editor only).")]
        [SerializeField] private string debugStatus = "";

        /// <summary>Fired when a bonus is awarded. The growth-hint UI (story #5) can listen here.</summary>
        public event Action<VisitZone> OnBonusAwarded;

        /// <summary>Per-stay progress for one zone the player is currently inside.</summary>
        private class Stay
        {
            public float walkedSeconds;
            public bool eligible;
            public bool awarded;
        }

        private readonly Dictionary<VisitZone, Stay> activeStays = new Dictionary<VisitZone, Stay>();
        private readonly List<VisitZone> subscribedZones = new List<VisitZone>();
        private IActivePlayerProvider playerProvider;
        private float nextProviderSearchTime;

        #region Unity Lifecycle

        private void OnEnable()
        {
            // Subscribe to every zone in the scene (Observer pattern).
            foreach (VisitZone zone in FindObjectsByType<VisitZone>(FindObjectsSortMode.None))
            {
                zone.OnPlayerEntered += HandleZoneEntered;
                zone.OnPlayerExited += HandleZoneExited;
                subscribedZones.Add(zone);
            }

            if (showDebugMessages)
                Debug.Log($"[OnFootVisitBonus] Watching {subscribedZones.Count} zones (bonus after {requiredWalkSeconds}s on foot).");
        }

        private void OnDisable()
        {
            foreach (VisitZone zone in subscribedZones)
            {
                if (zone == null) continue; // zone may already be destroyed during scene unload
                zone.OnPlayerEntered -= HandleZoneEntered;
                zone.OnPlayerExited -= HandleZoneExited;
            }

            subscribedZones.Clear();
            activeStays.Clear();
        }

        private void OnValidate()
        {
            if (activePlayerSource != null && !(activePlayerSource is IActivePlayerProvider))
            {
                Debug.LogWarning($"[OnFootVisitBonus] '{activePlayerSource.name}' doesn't implement IActivePlayerProvider. Cleared.", this);
                activePlayerSource = null;
            }
        }

        private void Update()
        {
            if (activeStays.Count == 0)
            {
                SetDebugStatus("Not inside a zone");
                return;
            }

            bool onFoot = IsPlayerOnFoot();

#if UNITY_EDITOR
            // Built only in the Editor: string building every frame creates garbage,
            // which we don't want in a Steam Deck build.
            var status = new System.Text.StringBuilder();
#endif

            foreach (KeyValuePair<VisitZone, Stay> pair in activeStays)
            {
                VisitZone zone = pair.Key;
                Stay stay = pair.Value;
                if (zone == null) continue;

                if (stay.eligible && !stay.awarded)
                {
                    if (onFoot)
                        stay.walkedSeconds += Time.deltaTime; // scaled time: pauses with the game

                    if (stay.walkedSeconds >= requiredWalkSeconds)
                        Award(zone, stay);
                }

#if UNITY_EDITOR
                if (!stay.eligible) status.Append($"{zone.DisplayName}: entry didn't count, no bonus   ");
                else if (stay.awarded) status.Append($"{zone.DisplayName}: bonus earned ✓   ");
                else status.Append($"{zone.DisplayName}: {stay.walkedSeconds:0.0}/{requiredWalkSeconds:0}s on foot{(onFoot ? "" : " (in car, paused)")}   ");
#endif
            }

#if UNITY_EDITOR
            debugStatus = status.ToString();
#endif
        }

        #endregion

        #region Zone Events

        private void HandleZoneEntered(VisitZone zone)
        {
            activeStays[zone] = new Stay
            {
                eligible = !requireCountedEntry || zone.EntryCounted
            };
        }

        private void HandleZoneExited(VisitZone zone)
        {
            activeStays.Remove(zone); // leaving resets progress for that zone
        }

        #endregion

        #region Award

        private void Award(VisitZone zone, Stay stay)
        {
            stay.awarded = true;

            VisitManager manager = VisitManager.Instance;
            if (manager != null)
                manager.RecordBonusVisit(zone.ZoneID, "on-foot bonus");

            if (showDebugMessages)
                Debug.Log($"[OnFootVisitBonus] Took the scenic route ✓ +1 visit at '{zone.DisplayName}' [{zone.ZoneID}]", zone);

            OnBonusAwarded?.Invoke(zone);
        }

        #endregion

        #region Helpers

        private bool IsPlayerOnFoot()
        {
            if (playerProvider == null)
            {
                playerProvider = activePlayerSource as IActivePlayerProvider;

                // Search at most every 2 s if nothing was assigned.
                if (playerProvider == null && Time.time >= nextProviderSearchTime)
                {
                    nextProviderSearchTime = Time.time + 2f;
                    foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    {
                        if (behaviour is IActivePlayerProvider found)
                        {
                            playerProvider = found;
                            break;
                        }
                    }
                }
            }

            return playerProvider != null && playerProvider.IsOnFoot;
        }

        private void SetDebugStatus(string text)
        {
#if UNITY_EDITOR
            debugStatus = text;
#endif
        }

        #endregion
    }
}