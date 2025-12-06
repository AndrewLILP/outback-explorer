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
    /// FIXED VERSION - Properly re-enables car physics and cameras
    /// </summary>
    public class DrivingState : PlayerState
    {
        private CarController carController;

        public DrivingState(PlayerStateManager manager) : base(manager) { }

        public override void Enter()
        {
            Debug.Log("[DrivingState] ========== ENTERING DRIVING STATE ==========");

            // Car should already be active (never gets disabled)
            // But we ensure it just in case
            if (!stateManager.CarGameObject.activeSelf)
            {
                stateManager.CarGameObject.SetActive(true);
                Debug.Log("[DrivingState] Re-enabled car GameObject");
            }

            // 1. Re-enable car physics (make Rigidbody non-kinematic)
            Rigidbody carRigidbody = stateManager.CarGameObject.GetComponent<Rigidbody>();
            if (carRigidbody != null)
            {
                carRigidbody.isKinematic = false;
                Debug.Log("[DrivingState] Car Rigidbody set to non-kinematic - physics active");
            }

            // 2. Re-enable car cameras (children of car GameObject)
            Camera[] carCameras = stateManager.CarGameObject.GetComponentsInChildren<Camera>(true); // Include inactive
            foreach (Camera cam in carCameras)
            {
                cam.enabled = true;
                Debug.Log($"[DrivingState] Re-enabled car camera: {cam.gameObject.name}");
            }

            // 3. Enable car controller
            carController = stateManager.CarController;
            if (carController != null)
            {
                carController.enabled = true;
                Debug.Log("[DrivingState] Car controller enabled");
            }

            // Disable player character (if it exists)
            if (stateManager.PlayerCharacter != null)
            {
                stateManager.PlayerCharacter.SetActive(false);
                Debug.Log("[DrivingState] Player character disabled");
            }

            // Update camera to follow car
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
            Debug.Log("[DrivingState] ========== EXITING DRIVING STATE ==========");
            
            // Disable car controls
            if (carController != null)
            {
                carController.enabled = false;
                Debug.Log("[DrivingState] Car controller disabled");
            }

            // NOTE: We do NOT disable the car GameObject or Rigidbody here
            // That's handled by OnFootState.Enter() which freezes it
            Debug.Log("[DrivingState] Car will be frozen by OnFootState");
        }
    }
}