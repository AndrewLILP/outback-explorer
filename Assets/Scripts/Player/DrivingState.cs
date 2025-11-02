// DrivingState.cs
using UnityEngine;
using PolyStang;
using RelaxingDrive.World;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// State when player is driving the car.
    /// Handles car controls and camera positioning.
    /// Listens for E key to exit car and switch to OnFootState.
    /// 
    /// FIXED VERSION - Matches polished PlayerStateManager API
    /// </summary>
    public class DrivingState : PlayerState
    {
        private CarController carController;

        public DrivingState(PlayerStateManager manager) : base(manager) { }

        public override void Enter()
        {
            // Enable car
            stateManager.CarGameObject.SetActive(true);

            // Get car controller
            carController = stateManager.CarController;
            if (carController != null)
            {
                carController.enabled = true;
            }

            // Disable player character (if it exists)
            if (stateManager.PlayerCharacter != null)
            {
                stateManager.PlayerCharacter.SetActive(false);
            }

            // Update camera to follow car
            if (stateManager.FollowCamera != null)
            {
                stateManager.FollowCamera.SetTarget(stateManager.CarGameObject.transform);
                stateManager.FollowCamera.SetOffset(stateManager.DrivingCameraOffset);
            }

            Debug.Log("[DrivingState] Driving mode active - press E to exit car");
        }

        public override void Update()
        {
            // Check for exit car input
            if (Input.GetKeyDown(KeyCode.E))
            {
                ExitCar();
            }
        }

        public override void FixedUpdate()
        {
            // Car physics handled by CarController
        }

        private void ExitCar()
        {
            

            // Switch to OnFoot state
            Debug.Log("[DrivingState] Exiting car...");
            stateManager.SwitchToOnFoot();
        }

        public override void Exit()
        {
            // Disable car controls
            if (carController != null)
            {
                carController.enabled = false;
            }

            Debug.Log("[DrivingState] Car disabled - switching to walking mode");
        }
    }
}