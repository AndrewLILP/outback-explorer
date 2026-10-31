// SpeedLimitMonitor.cs
// Location: Assets/Scripts/Player/SpeedLimitMonitor.cs
//
// Watches the player's driving speed. While they're above the limit a timer
// runs; drop below and it snaps back to 0. If the timer reaches the grace
// period, OnLimitExceeded fires.
//
// Single responsibility: this class only DETECTS. It doesn't show UI and
// doesn't move the car - SpeedWarningUI and SpeedLimitRespawner subscribe
// to its events and do those jobs (Observer pattern).

using System;
using UnityEngine;

namespace RelaxingDrive.Player
{
    public class SpeedLimitMonitor : MonoBehaviour
    {
        [Header("Limit")]
        [Tooltip("Speed (km/h) above which the warning starts.")]
        [SerializeField] private float speedLimitKmh = 65f;

        [Tooltip("How long (seconds) the player can stay over the limit before respawning.")]
        [SerializeField] private float gracePeriodSeconds = 10f;

        [Tooltip("Untick to allow any speed. Later, a trigger at the bridge can toggle this " +
                 "via the LimitEnabled property for the highways out of town.")]
        [SerializeField] private bool limitEnabled = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        // ---- Events (Observer pattern) ----
        /// <summary>Player has just gone over the limit - show the warning.</summary>
        public event Action OnWarningStarted;
        /// <summary>Player slowed down, left the car, or was respawned - hide the warning.</summary>
        public event Action OnWarningCleared;
        /// <summary>Player stayed over the limit for the full grace period - respawn them.</summary>
        public event Action OnLimitExceeded;

        private float timeOverLimit;
        private bool isWarning;

        /// <summary>
        /// Turn the limit on/off at runtime (e.g. from a highway zone trigger).
        /// Switching it off also clears any warning that's showing.
        /// </summary>
        public bool LimitEnabled
        {
            get => limitEnabled;
            set
            {
                limitEnabled = value;
                if (!limitEnabled) ResetTimer();
            }
        }

        /// <summary>Seconds left before respawn (handy if you later want a countdown).</summary>
        public float SecondsRemaining => Mathf.Max(0f, gracePeriodSeconds - timeOverLimit);

        private void Update()
        {
            if (!limitEnabled || !IsOverLimit())
            {
                ResetTimer();
                return;
            }

            // First frame over the limit -> tell listeners to show the warning.
            if (!isWarning)
            {
                isWarning = true;
                OnWarningStarted?.Invoke();
                Log($"Over {speedLimitKmh} km/h - warning started");
            }

            // Time.deltaTime (not unscaled) so the timer freezes if the game is paused.
            timeOverLimit += Time.deltaTime;

            if (timeOverLimit >= gracePeriodSeconds)
            {
                Log($"Over the limit for {gracePeriodSeconds}s - respawning");
                OnLimitExceeded?.Invoke();
                ResetTimer(); // also fires OnWarningCleared so the message fades out
            }
        }

        /// <summary>
        /// True only while driving AND above the limit. Reads speed through
        /// IVehicleController, so this never needs to know which car asset is used.
        /// </summary>
        private bool IsOverLimit()
        {
            PlayerStateManager psm = PlayerStateManager.Instance;
            if (psm == null || !psm.IsDriving || psm.VehicleController == null)
                return false;

            // Abs() so reversing fast counts too.
            return Mathf.Abs(psm.VehicleController.Speed) > speedLimitKmh;
        }

        private void ResetTimer()
        {
            timeOverLimit = 0f;

            if (isWarning)
            {
                isWarning = false;
                OnWarningCleared?.Invoke();
                Log("Warning cleared");
            }
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[SpeedLimitMonitor] {message}");
        }
    }
}