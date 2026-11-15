// FuelMonitor.cs
// Location: Assets/Scripts/Player/FuelMonitor.cs
//
// NEW (Nov 2026 - fuel): watches the car's fuel level and raises OnFuelLow
// once each time the tank drops past a warning level (50%, 25%, 10%).
// Refuelling above a level re-arms it, so the warnings work again next tank.
//
// Observer pattern: this class doesn't know the UI exists - FuelWarningUI
// (and later maybe Murray, audio, Steam stats) just subscribe to the event.
//
// Put it on the same GameObject as PlayerStateManager.

using System;
using UnityEngine;

namespace RelaxingDrive.Player
{
    public class FuelMonitor : MonoBehaviour
    {
        [Header("Warnings")]
        [Tooltip("Fuel % levels that trigger a warning. Any order - sorted high to low on Awake.")]
        [SerializeField] private int[] warningThresholds = { 50, 25, 10 };

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        /// <summary>Fired once when fuel drops to/below a threshold. Passes the threshold (e.g. 25).</summary>
        public event Action<int> OnFuelLow;

        /// <summary>Current fuel %, or 100 if no tank has been found yet.</summary>
        public float FuelPercent => fuelTank != null ? fuelTank.FuelPercent : 100f;

        private IFuelTank fuelTank;
        private int nextThresholdIndex;   // index of the next warning still to fire
        private bool loggedNoFuelSystem;

        private void Awake()
        {
            // Sort high -> low (50, 25, 10) so the index walks downwards as fuel drops.
            Array.Sort(warningThresholds);
            Array.Reverse(warningThresholds);
        }

        private void Update()
        {
            if (!TryResolveTank())
                return;

            CheckThresholds(fuelTank.FuelPercent);
        }

        /// <summary>
        /// The RCC car is spawned in Start(), after this Awake() - so we look for
        /// the tank lazily each frame until PlayerStateManager has resolved the car.
        /// </summary>
        private bool TryResolveTank()
        {
            if (fuelTank == null)
            {
                PlayerStateManager psm = PlayerStateManager.Instance;
                if (psm == null || psm.VehicleController == null)
                    return false;

                // The adapter component implements BOTH interfaces, so this cast
                // works without knowing it's RCC underneath.
                fuelTank = psm.VehicleController as IFuelTank;
                if (fuelTank == null)
                {
                    if (!loggedNoFuelSystem)
                        Debug.LogWarning("[FuelMonitor] Vehicle adapter doesn't implement IFuelTank - fuel warnings disabled.");
                    loggedNoFuelSystem = true;
                    return false;
                }

                // Start armed at the right place, in case the tank isn't full at spawn.
                nextThresholdIndex = 0;
                while (nextThresholdIndex < warningThresholds.Length &&
                       fuelTank.FuelPercent <= warningThresholds[nextThresholdIndex])
                    nextThresholdIndex++;

                if (showDebugLogs)
                    Debug.Log($"[FuelMonitor] Tank found: {fuelTank.FuelLitres:0.0}/{fuelTank.FuelCapacity:0} L ({fuelTank.FuelPercent:0}%)");
            }

            if (!fuelTank.UsesFuel)
            {
                if (!loggedNoFuelSystem && showDebugLogs)
                    Debug.Log("[FuelMonitor] useFuelConsumption is OFF on this car - no warnings.");
                loggedNoFuelSystem = true;
                return false;
            }

            return true;
        }

        private void CheckThresholds(float percent)
        {
            // Dropping: walk past every threshold we've crossed, but only warn
            // about the LOWEST one (e.g. if fuel jumped from 60% to 20%, say 25%, not 50% too).
            int crossed = -1;
            while (nextThresholdIndex < warningThresholds.Length &&
                   percent <= warningThresholds[nextThresholdIndex])
            {
                crossed = warningThresholds[nextThresholdIndex];
                nextThresholdIndex++;
            }

            if (crossed >= 0)
            {
                if (showDebugLogs) Debug.Log($"[FuelMonitor] Fuel at/below {crossed}% ({percent:0.0}%)");
                OnFuelLow?.Invoke(crossed);
            }

            // Rising (refuelled): re-arm any thresholds we're now back above.
            while (nextThresholdIndex > 0 && percent > warningThresholds[nextThresholdIndex - 1])
                nextThresholdIndex--;
        }
    }
}