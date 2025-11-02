// PlayerStateManager.cs
using UnityEngine;
using PolyStang; // Reference to your car controller namespace

namespace RelaxingDrive.Player
{
    /// <summary>
    /// Manages player state transitions between Driving and OnFoot.
    /// Implements State Pattern - delegates behavior to current state.
    /// Singleton for easy access from other systems.
    /// </summary>
    public class PlayerStateManager : MonoBehaviour
    {
        // Singleton instance
        private static PlayerStateManager instance;
        public static PlayerStateManager Instance => instance;
        
        [Header("References")]
        [SerializeField] private GameObject carGameObject;
        [SerializeField] private GameObject playerCharacter; // Capsule or character model
        [SerializeField] private FollowCamera followCamera;
        
        [Header("Camera Settings")]
        [SerializeField] private Vector3 drivingCameraOffset = new Vector3(0f, 3f, -7f);
        [SerializeField] private Vector3 walkingCameraOffset = new Vector3(0f, 2f, -5f);
        
        [Header("Player Spawn Settings")]
        [SerializeField] private Vector3 exitCarOffset = new Vector3(2f, 0f, 0f); // Spawn to the right of car
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        
        // States
        private PlayerState currentState;
        private DrivingState drivingState;
        private OnFootState onFootState;
        
        // Components
        private CarController carController;
        
        // Public accessors
        public GameObject CarGameObject => carGameObject;
        public GameObject PlayerCharacter => playerCharacter;
        public FollowCamera FollowCamera => followCamera;
        public CarController CarController => carController;
        public Vector3 DrivingCameraOffset => drivingCameraOffset;
        public Vector3 WalkingCameraOffset => walkingCameraOffset;
        public Vector3 ExitCarOffset => exitCarOffset;
        
        // Current state info
        public bool IsDriving => currentState is DrivingState;
        public bool IsOnFoot => currentState is OnFootState;
        
        private void Awake()
        {
            // Singleton setup
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            
            // Get car controller reference
            if (carGameObject != null)
            {
                carController = carGameObject.GetComponent<CarController>();
            }
            
            // Initialize states
            drivingState = new DrivingState(this);
            onFootState = new OnFootState(this);
            
            // Start in driving state
            ChangeState(drivingState);
        }
        
        private void Update()
        {
            currentState?.Update();
            
            if (showDebugInfo)
            {
                string stateInfo = IsDriving ? "DRIVING" : "ON FOOT";
                Debug.Log($"Player State: {stateInfo}");
            }
        }
        
        private void FixedUpdate()
        {
            currentState?.FixedUpdate();
        }
        
        /// <summary>
        /// Changes to a new state. Calls Exit on old state, Enter on new state.
        /// </summary>
        public void ChangeState(PlayerState newState)
        {
            if (currentState == newState)
                return;
            
            if (showDebugInfo)
            {
                string oldStateName = currentState?.GetType().Name ?? "None";
                string newStateName = newState?.GetType().Name ?? "None";
                Debug.Log($"State Transition: {oldStateName} → {newStateName}");
            }
            
            currentState?.Exit();
            currentState = newState;
            currentState?.Enter();
        }
        
        /// <summary>
        /// Transition to driving state
        /// </summary>
        public void TransitionToDriving()
        {
            ChangeState(drivingState);
        }
        
        /// <summary>
        /// Transition to on-foot state
        /// </summary>
        public void TransitionToOnFoot()
        {
            ChangeState(onFootState);
        }
        
        /// <summary>
        /// Get position to spawn player when exiting car
        /// </summary>
        public Vector3 GetExitPosition()
        {
            if (carGameObject == null)
                return Vector3.zero;
            
            // Spawn player to the right of the car (driver's side)
            Vector3 exitPos = carGameObject.transform.position + 
                              carGameObject.transform.right * exitCarOffset.x +
                              carGameObject.transform.up * exitCarOffset.y +
                              carGameObject.transform.forward * exitCarOffset.z;
            
            return exitPos;
        }
    }
}