// FuelPump.cs
// Location: Assets/Scripts/World/FuelPump.cs
//
// NEW (Nov 2026 - fuel): park-and-fill servo bay.
//
// Flow:
//   1. Car stops inside the bay (box) for a moment while the tank is below 95%
//   2. Player is taken out of the car - same path as pressing E / A
//      (DrivingState.Exit -> adapter.SetCanControl(false) -> engine off)
//   3. Car entry is locked; the tank fills (60 s from empty, less if part full)
//   4. At 100% the lock is released and the normal "Enter Car" prompt returns
//
// Put this on an empty "FuelBay" GameObject with a BoxCollider sized to the
// parking spot (e.g. size 3 x 3 x 6, centre y = 1.5). The pump model and the
// painted cube pad are separate children/objects - this script only needs the box.
//
// Static events: any number of pumps can exist, and the UI listens to all of
// them at once without needing references to each one.
//
// UPDATED (Nov 2026 - debug): logs WHY the bay isn't starting (only when the
// reason changes, so the console isn't spammed) and shows it in the Inspector
// as "Status". Changes marked "NEW (debug)".
//
// UPDATED (Nov 2026 - full tank): parking in the bay with a (nearly) full
// tank now raises AnyTankAlreadyFull once per visit, so the UI can say so
// instead of silently doing nothing. Changes marked "NEW (full)".

using System;
using UnityEngine;
using RelaxingDrive.Player;

namespace RelaxingDrive.World
{
    [RequireComponent(typeof(BoxCollider))]
    public class FuelPump : MonoBehaviour
    {
        // ===== Events (Observer) =====
        /// <summary>Any pump started filling.</summary>
        public static event Action AnyRefuelStarted;
        /// <summary>Fill progress each frame, 0-100.</summary>
        public static event Action<float> AnyRefuelProgress;
        /// <summary>Any pump finished - the tank is full and the car is unlocked.</summary>
        public static event Action AnyRefuelCompleted;
        /// <summary>NEW (full): car parked in a bay but the tank didn't need filling. Passes fuel %.</summary>
        public static event Action<float> AnyTankAlreadyFull;

        [Header("Parking")]
        [Tooltip("Car counts as parked below this speed (km/h).")]
        [SerializeField] private float parkedSpeedKmh = 1f;
        [Tooltip("How long the car must sit still in the bay before filling starts.")]
        [SerializeField] private float parkedSeconds = 1f;
        [Tooltip("Don't start if the tank is already at or above this % (stops the bay grabbing a nearly-full car).")]
        [SerializeField] private float minPercentToStart = 95f;

        [Header("Filling")]
        [Tooltip("Seconds to fill an EMPTY tank. A part-full tank takes proportionally less.")]
        [SerializeField] private float emptyToFullSeconds = 60f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        [Tooltip("Read-only: why the bay is or isn't filling right now.")]
        [SerializeField] private string status = "Idle"; // NEW (debug)

        private enum PumpState { Idle, Filling }

        private PumpState state = PumpState.Idle;
        private BoxCollider bay;
        private float parkedTimer;
        private IFuelTank tank;
        private bool toldTankFull; // NEW (full): only say it once per visit to the bay

        private void Awake()
        {
            bay = GetComponent<BoxCollider>();
            bay.isTrigger = true; // the car must be able to drive into it
        }

        private void Update()
        {
            PlayerStateManager psm = PlayerStateManager.Instance;
            if (psm == null) return;

            switch (state)
            {
                case PumpState.Idle:    CheckForParkedCar(psm); break;
                case PumpState.Filling: Fill(psm);              break;
            }
        }

        // ---------------------------------------------------------------
        // Idle: wait for the car to stop in the bay
        // ---------------------------------------------------------------
        private void CheckForParkedCar(PlayerStateManager psm)
        {
            if (!psm.IsDriving || psm.CarGameObject == null || psm.VehicleController == null)
            {
                ReportStatus("Waiting - player not driving");
                ResetVisit();
                return;
            }

            Vector3 carPos = psm.CarGameObject.transform.position;
            if (!IsInsideBay(carPos))
            {
                // Only worth logging when the car is close - otherwise it's just "driving around".
                float distance = Vector3.Distance(carPos, transform.position);
                ReportStatus(distance < 15f
                    ? $"Car NOT inside bay box (car pivot {distance:0.0} m from bay centre, local {transform.InverseTransformPoint(carPos)})"
                    : "Waiting - car not nearby");
                ResetVisit();
                return;
            }

            IFuelTank carTank = psm.VehicleController as IFuelTank;
            if (carTank == null)
            {
                ReportStatus("Car adapter does not implement IFuelTank - is the new RCCVehicleAdapter in?");
                parkedTimer = 0f;
                return;
            }
            if (!carTank.UsesFuel)
            {
                ReportStatus("useFuelConsumption is OFF on the RCC car");
                parkedTimer = 0f;
                return;
            }

            // CHANGED (full): check "stopped" BEFORE the fuel level, so driving
            // through the bay says nothing - only actually parking does.
            float speed = Mathf.Abs(psm.VehicleController.Speed);
            if (speed > parkedSpeedKmh)
            {
                ReportStatus($"In bay, still moving ({speed:0.0} km/h > {parkedSpeedKmh} km/h)");
                parkedTimer = 0f;
                return;
            }

            parkedTimer += Time.deltaTime;
            if (parkedTimer < parkedSeconds)
            {
                ReportStatus("In bay and stopped - checking tank");
                return;
            }

            // NEW (full): parked, but the tank doesn't need filling - say so once.
            if (carTank.FuelPercent >= minPercentToStart)
            {
                ReportStatus($"Tank already {carTank.FuelPercent:0}% (needs < {minPercentToStart:0}%)");
                if (!toldTankFull)
                {
                    toldTankFull = true;
                    AnyTankAlreadyFull?.Invoke(carTank.FuelPercent);
                }
                return;
            }

            StartRefuel(psm, carTank);
        }

        /// <summary>
        /// NEW (full): the car left the bay (or the player got out) - next visit
        /// starts fresh, so the "already full" message can show again.
        /// </summary>
        private void ResetVisit()
        {
            parkedTimer = 0f;
            toldTankFull = false;
        }

        /// <summary>
        /// NEW (debug): logs only when the reason changes, so the console stays readable.
        /// </summary>
        private void ReportStatus(string newStatus)
        {
            if (newStatus == status) return;
            status = newStatus;
            if (showDebugLogs) Debug.Log($"[FuelPump] {name}: {status}");
        }

        /// <summary>
        /// Is a world point inside the bay box? Converting the point into the bay's
        /// LOCAL space means this still works if the bay is rotated or scaled -
        /// unlike collider.bounds, which is an axis-aligned box in world space.
        /// We check the car's root position (its pivot) rather than using
        /// OnTriggerEnter, because RCC cars have many child colliders that would
        /// each fire their own enter/exit events.
        /// </summary>
        private bool IsInsideBay(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint) - bay.center;
            Vector3 half = bay.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x &&
                   Mathf.Abs(local.y) <= half.y &&
                   Mathf.Abs(local.z) <= half.z;
        }

        private void StartRefuel(PlayerStateManager psm, IFuelTank carTank)
        {
            parkedTimer = 0f;

            // Graceful exit - exactly what pressing E / A does.
            psm.SwitchToOnFoot();
            if (!psm.IsOnFoot)
            {
                Debug.LogWarning("[FuelPump] Couldn't switch the player to OnFoot - refuel cancelled.");
                return;
            }

            tank = carTank;
            psm.SetCarEntryLocked(true);
            state = PumpState.Filling;
            status = "Filling"; // NEW (debug)

            if (showDebugLogs)
                Debug.Log($"[FuelPump] {name}: refuelling from {tank.FuelPercent:0}%");

            AnyRefuelStarted?.Invoke();
            AnyRefuelProgress?.Invoke(tank.FuelPercent);
        }

        // ---------------------------------------------------------------
        // Filling: top up at a steady rate, then unlock the car
        // ---------------------------------------------------------------
        private void Fill(PlayerStateManager psm)
        {
            // Same litres per second for every fill, so an empty tank takes the
            // full emptyToFullSeconds and a half tank takes half as long.
            float litresPerSecond = tank.FuelCapacity / Mathf.Max(1f, emptyToFullSeconds);
            tank.AddFuel(litresPerSecond * Time.deltaTime);

            AnyRefuelProgress?.Invoke(tank.FuelPercent);

            // AddFuel clamps at capacity, so this comparison is exact.
            if (tank.FuelLitres >= tank.FuelCapacity)
                CompleteRefuel(psm);
        }

        private void CompleteRefuel(PlayerStateManager psm)
        {
            state = PumpState.Idle;
            tank = null;
            status = "Idle"; // NEW (debug)
            psm.SetCarEntryLocked(false);

            if (showDebugLogs)
                Debug.Log($"[FuelPump] {name}: tank full - car unlocked");

            AnyRefuelCompleted?.Invoke();

            // TODO (day cycle): when the time-of-day system exists, advance the
            // in-game clock here (e.g. +15 in-game minutes per stop) so a servo
            // break moves the day towards golden hour.

            // TODO (servo hub): the servo will also cover water (Care System -
            // fill containers for bird baths / troughs) and food/snacks (food
            // scraps). Those belong in their own components on the servo, not
            // here - this class only does fuel.
        }

        private void OnDisable()
        {
            // Safety: never leave the player locked out of their car if this
            // pump gets disabled or the scene unloads mid-fill.
            if (state == PumpState.Filling && PlayerStateManager.Instance != null)
                PlayerStateManager.Instance.SetCarEntryLocked(false);

            state = PumpState.Idle;
            tank = null;
        }

        // Draws the bay in the Scene view so it's easy to line up with the pad.
        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.25f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}