// RCCVehicleAdapter.cs
// Location: Assets/Scripts/Player/RCCVehicleAdapter.cs
//
// UPDATED (Nov 2026 - fuel):
// - Also implements IFuelTank, so fuel code never touches RCC directly.
// - SetCanControl(false) now kills the engine, SetCanControl(true) starts it.
//   RCC's own SetCanControl only blocks input, so the engine kept idling
//   (and burning fuel) the whole time the player was on foot.
// Changes are marked "NEW (fuel)".

using UnityEngine;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// Adapts RCC's RCC_CarControllerV4 to the IVehicleController and IFuelTank interfaces.
    /// Attach to the same GameObject as the RCC car controller (Steam scenes).
    /// </summary>
    [RequireComponent(typeof(RCC_CarControllerV4))]
    public class RCCVehicleAdapter : MonoBehaviour, IVehicleController, IFuelTank
    {
        private RCC_CarControllerV4 rccController;

        private void Awake()
        {
            rccController = GetComponent<RCC_CarControllerV4>();
        }

        #region IVehicleController

        public void SetCanControl(bool canControl)
        {
            rccController.SetCanControl(canControl);

            // NEW (fuel): engine follows the player. Getting out (E / A, or a
            // fuel pump forcing the exit) switches it off so fuel only burns
            // while driving. Getting back in starts it again - StartEngine()
            // plays RCC's start clip and gives power after ~1 s, and does
            // nothing if the engine is already running.
            if (canControl)
                rccController.StartEngine();
            else
                rccController.KillEngine();
        }

        public float Speed => rccController.speed; // RCC already exposes km/h

        public Rigidbody Rigidbody => rccController.Rigid;

        public bool UsesOwnCamera => true;

        public void SetOwnCameraActive(bool active)
        {
            if (RCC_SceneManager.Instance != null && RCC_SceneManager.Instance.activePlayerCamera != null)
                RCC_SceneManager.Instance.activePlayerCamera.gameObject.SetActive(active);
        }

        #endregion

        #region IFuelTank - NEW (fuel)

        public bool UsesFuel => rccController.useFuelConsumption;

        public float FuelCapacity => rccController.fuelTankCapacity;

        public float FuelLitres => rccController.fuelTank;

        public float FuelPercent => FuelCapacity > 0f ? (FuelLitres / FuelCapacity) * 100f : 100f;

        public void AddFuel(float litres)
        {
            // RCC's Fuel() only ever subtracts, so topping up is just writing the field.
            // If the tank had run dry, RCC set fuelInput = 0 - that's restored when the
            // player gets back in, because SetCanControl(true) -> StartEngine() sets
            // fuelInput = 1.
            rccController.fuelTank = Mathf.Clamp(rccController.fuelTank + litres, 0f, rccController.fuelTankCapacity);
        }

        #endregion
    }
}