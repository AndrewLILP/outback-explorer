// DrivingState.cs
using UnityEngine;
using RelaxingDrive.World;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// State when player is driving the car.
    /// Handles car controls and camera positioning via the IVehicleController
    /// abstraction - works with any car asset that has a matching adapter.
    /// </summary>
    public class DrivingState : PlayerState
    {
        private IVehicleController vehicleController;

        public DrivingState(PlayerStateManager manager) : base(manager) { }

        public override void Enter()
        {
            Debug.Log("[DrivingState] ========== ENTERING DRIVING STATE ==========");

            if (!stateManager.CarGameObject.activeSelf)
            {
                stateManager.CarGameObject.SetActive(true);
                Debug.Log("[DrivingState] Re-enabled car GameObject");
            }

            vehicleController = stateManager.VehicleController;

            // Unfreeze physics (OnFootState froze it on parking)
            if (vehicleController?.Rigidbody != null)
            {
                vehicleController.Rigidbody.isKinematic = false;
                Debug.Log("[DrivingState] Car Rigidbody set to non-kinematic - physics active");
            }

            // Hand control back to whichever adapter is present
            if (vehicleController != null)
            {
                vehicleController.SetCanControl(true);
                Debug.Log("[DrivingState] Vehicle control enabled");
            }

            if (stateManager.PlayerCharacter != null)
            {
                stateManager.PlayerCharacter.SetActive(false);
                Debug.Log("[DrivingState] Player character disabled");
            }

            if (stateManager.FollowCamera != null)
            {
                stateManager.FollowCamera.SetTarget(stateManager.CarGameObject.transform);
                stateManager.FollowCamera.SetOffset(stateManager.DrivingCameraOffset);
                Debug.Log($"[DrivingState] Camera target set to car, offset: {stateManager.DrivingCameraOffset}");
            }

            Debug.Log("[DrivingState] Driving mode active - press E to exit car");
        }

        public override void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
                ExitCar();
        }

        public override void FixedUpdate()
        {
            // Car physics handled by the car asset's own controller
        }

        private void ExitCar()
        {
            Debug.Log("[DrivingState] Exiting car...");
            stateManager.SwitchToOnFoot();
        }

        public override void Exit()
        {
            Debug.Log("[DrivingState] ========== EXITING DRIVING STATE ==========");

            if (vehicleController != null)
            {
                vehicleController.SetCanControl(false);
                Debug.Log("[DrivingState] Vehicle control disabled");
            }

            Debug.Log("[DrivingState] Car will be frozen by OnFootState");
        }
    }
}