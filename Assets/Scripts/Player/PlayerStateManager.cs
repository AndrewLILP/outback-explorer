// PlayerStateManager.cs
using UnityEngine;
using PolyStang; // Reference to your car controller namespace

namespace RelaxingDrive.Player
{
    /// <summary>
    /// Manages player state transitions between Driving and OnFoot.
    /// Implements State Pattern - delegates behavior to current state.
    /// Singleton for easy access from other systems.
    /// 
    /// FIXED VERSION - Auto-finds FollowCamera component
    /// </summary>
    public class PlayerStateManager : MonoBehaviour
    {
        // Singleton instance
        private static PlayerStateManager instance;
        public static PlayerStateManager Instance => instance;

        [Header("References")]
        [SerializeField] private GameObject carGameObject;
        [SerializeField] private GameObject playerCharacter; // Capsule or character model
        [SerializeField] private FollowCamera followCamera; // Can leave empty - will auto-find

        [Header("Camera Settings")]
        [SerializeField] private Vector3 drivingCameraOffset = new Vector3(0f, 3f, -7f);
        [SerializeField] private Vector3 walkingCameraOffset = new Vector3(0f, 2f, -5f);

        [Header("Player Spawn Settings")]
        [SerializeField] private Vector3 exitCarOffset = new Vector3(2f, 0f, 0f); // Spawn to the right of car

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool logStateChanges = true;

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

            // AUTO-FIND FollowCamera if not assigned
            if (followCamera == null)
            {
                followCamera = Object.FindFirstObjectByType<FollowCamera>();
                
                if (followCamera != null)
                {
                    Debug.Log($"[PlayerStateManager] Auto-found FollowCamera on GameObject: {followCamera.gameObject.name}");
                }
                else
                {
                    Debug.LogError("[PlayerStateManager] FollowCamera script not found in scene! Please add FollowCamera component to your Camera GameObject.");
                }
            }

            // Initialize states
            drivingState = new DrivingState(this);
            onFootState = new OnFootState(this);

            // Start in driving state
            ChangeState(drivingState);

            if (logStateChanges)
            {
                Debug.Log("[PlayerStateManager] Initialized in Driving state");
            }
        }

        private void Update()
        {
            currentState?.Update();
        }

        private void FixedUpdate()
        {
            currentState?.FixedUpdate();
        }

        /// <summary>
        /// Changes the current player state.
        /// Called by states themselves (e.g., OnFootState calls this to switch to DrivingState)
        /// </summary>
        public void ChangeState(PlayerState newState)
        {
            // Exit current state
            currentState?.Exit();

            // Store old state name for logging
            string oldStateName = currentState?.GetType().Name ?? "None";
            string newStateName = newState?.GetType().Name ?? "None";

            // Set new state
            currentState = newState;

            // Enter new state
            currentState?.Enter();

            // Log state change (only if enabled)
            if (logStateChanges)
            {
                Debug.Log($"[PlayerStateManager] State changed: {oldStateName} → {newStateName}");
            }
        }

        /// <summary>
        /// Switches to DrivingState (called from OnFootState)
        /// </summary>
        public void SwitchToDriving()
        {
            ChangeState(drivingState);
        }

        /// <summary>
        /// Switches to OnFootState (called from DrivingState)
        /// </summary>
        public void SwitchToOnFoot()
        {
            ChangeState(onFootState);
        }

        // Optional: GUI overlay for debugging (only if showDebugInfo is true)
        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUI.Label(new Rect(10, 10, 300, 20), $"State: {currentState?.GetType().Name ?? "None"}");
            GUI.Label(new Rect(10, 30, 300, 20), $"Press E to {(IsDriving ? "Exit Car" : "Enter Car")}");
        }
    }
}