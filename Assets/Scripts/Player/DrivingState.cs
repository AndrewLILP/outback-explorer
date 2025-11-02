// DrivingState.cs
using UnityEngine;
using PolyStang;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// Player state when driving the car.
    /// Wraps the existing CarController functionality.
    /// Handles input for exiting the vehicle (E key).
    /// </summary>
    public class DrivingState : PlayerState
    {
        private CarController carController;
        private GameObject carGameObject;
        private GameObject playerCharacter;
        private FollowCamera camera;
        
        public DrivingState(PlayerStateManager manager) : base(manager)
        {
            carController = manager.CarController;
            carGameObject = manager.CarGameObject;
            playerCharacter = manager.PlayerCharacter;
            camera = manager.FollowCamera;
        }
        
        public override void Enter()
        {
            Debug.Log("Entered Driving State");
            
            // Enable car components
            if (carController != null)
            {
                carController.enabled = true;
            }
            
            // Enable car's rigidbody
            Rigidbody carRb = carGameObject.GetComponent<Rigidbody>();
            if (carRb != null)
            {
                carRb.isKinematic = false;
            }
            
            // Disable player character
            if (playerCharacter != null)
            {
                playerCharacter.SetActive(false);
            }
            
            // Setup camera for driving
            if (camera != null)
            {
                camera.SetTarget(carGameObject.transform);
                camera.SetOffset(stateManager.DrivingCameraOffset);
            }
        }
        
        public override void Update()
        {
            // Check for exit car input (E key)
            if (Input.GetKeyDown(KeyCode.E))
            {
                ExitCar();
            }
            
            // Car controller handles its own input/update
        }
        
        public override void Exit()
        {
            Debug.Log("Exited Driving State");
            
            // Disable car controller (prevent driving while on foot)
            if (carController != null)
            {
                carController.enabled = false;
            }
            
            // Make car static (optional - or let it be pushed around)
            Rigidbody carRb = carGameObject.GetComponent<Rigidbody>();
            if (carRb != null)
            {
                carRb.isKinematic = true; // Freeze car in place
                carRb.linearVelocity = Vector3.zero; // Stop any movement
                carRb.angularVelocity = Vector3.zero;
            }
        }
        
        /// <summary>
        /// Handles exiting the car
        /// </summary>
        private void ExitCar()
        {
            // Get exit position (beside car)
            Vector3 exitPosition = stateManager.GetExitPosition();
            
            // Position player character at exit point
            if (playerCharacter != null)
            {
                playerCharacter.transform.position = exitPosition;
                
                // Face same direction as car
                playerCharacter.transform.rotation = carGameObject.transform.rotation;
            }
            
            // Transition to on-foot state
            stateManager.TransitionToOnFoot();
        }
    }
}