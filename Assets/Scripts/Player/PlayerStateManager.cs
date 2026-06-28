// PlayerStateManager.cs
using UnityEngine;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// Manages player state transitions between Driving and OnFoot.
    /// Implements State Pattern - delegates behavior to current state.
    /// Singleton for easy access from other systems.
    ///
    /// Depends on IVehicleController, not a concrete car type, so this works
    /// for both PolyStang (Itch scene, car placed in-scene at design time -
    /// resolved immediately) and RCC (Steam scenes, car spawned dynamically
    /// by RCC_Spawner in Start() - resolved via RCC_SceneManager.OnVehicleChanged,
    /// since Awake() runs too early to see it).
    /// </summary>
    public class PlayerStateManager : MonoBehaviour
    {
        private static PlayerStateManager instance;
        public static PlayerStateManager Instance => instance;

        [Header("References")]
        [Tooltip("Leave empty for RCC scenes - resolved automatically once RCC_Spawner spawns the car. Assign directly for PolyStang scenes.")]
        [SerializeField] private GameObject carGameObject;
        [SerializeField] private GameObject playerCharacter;
        [SerializeField] private FollowCamera followCamera; // Can leave empty - will auto-find

        [Header("Camera Settings")]
        [SerializeField] private Vector3 drivingCameraOffset = new Vector3(0f, 3f, -7f);
        [SerializeField] private Vector3 walkingCameraOffset = new Vector3(0f, 2f, -5f);

        [Header("Player Spawn Settings")]
        [SerializeField] private Vector3 exitCarOffset = new Vector3(2f, 0f, 0f);

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool logStateChanges = true;

        private PlayerState currentState;
        private DrivingState drivingState;
        private OnFootState onFootState;

        private IVehicleController vehicleController;
        private bool vehicleReady = false;

        public GameObject CarGameObject => carGameObject;
        public GameObject PlayerCharacter => playerCharacter;
        public FollowCamera FollowCamera => followCamera;
        public IVehicleController VehicleController => vehicleController;
        public Vector3 DrivingCameraOffset => drivingCameraOffset;
        public Vector3 WalkingCameraOffset => walkingCameraOffset;
        public Vector3 ExitCarOffset => exitCarOffset;

        public bool IsDriving => currentState is DrivingState;
        public bool IsOnFoot => currentState is OnFootState;

        private void Awake()
        {
            // Singleton check must happen first - no point resolving cameras
            // or vehicles on an instance we're about to destroy.
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            if (followCamera == null)
            {
                followCamera = Object.FindFirstObjectByType<FollowCamera>();
                if (followCamera != null)
                {
                    Debug.Log($"[PlayerStateManager] Auto-found FollowCamera on GameObject: {followCamera.gameObject.name}");
                }
                else
                {
                    // Not fatal: scenes without a walking controller yet (like this one)
                    // don't need FollowCamera until OnFootState is actually reachable.
                    Debug.LogWarning("[PlayerStateManager] FollowCamera not found in scene - " +
                        "OnFoot transitions will be unavailable until both a walking controller and FollowCamera are added.");
                }
            }

            drivingState = new DrivingState(this);
            onFootState = new OnFootState(this);

            if (carGameObject != null)
            {
                // PolyStang path: car already exists in the scene right now.
                ResolveVehicleController(carGameObject);
                BeginDriving();
            }
            else
            {
                // RCC path: car doesn't exist yet - RCC_Spawner creates it in its
                // own Start(), which runs after this Awake(). Wait for RCC_SceneManager
                // to report the new vehicle instead of looking for it now.
                RCC_SceneManager.OnVehicleChanged += HandleRCCVehicleChanged;
                if (logStateChanges)
                    Debug.Log("[PlayerStateManager] No carGameObject assigned - waiting for RCC vehicle to spawn...");
            }
        }

        private void HandleRCCVehicleChanged()
        {
            if (RCC_SceneManager.Instance == null || RCC_SceneManager.Instance.activePlayerVehicle == null)
                return;

            carGameObject = RCC_SceneManager.Instance.activePlayerVehicle.gameObject;
            ResolveVehicleController(carGameObject);

            // Only needed for the initial spawn - not expecting in-scene car swaps here.
            RCC_SceneManager.OnVehicleChanged -= HandleRCCVehicleChanged;

            BeginDriving();

            if (logStateChanges)
                Debug.Log($"[PlayerStateManager] RCC vehicle resolved: {carGameObject.name}");
        }

        private void ResolveVehicleController(GameObject car)
        {
            vehicleController = car.GetComponent<IVehicleController>();
            if (vehicleController == null)
            {
                Debug.LogError($"[PlayerStateManager] No IVehicleController adapter found on '{car.name}'! " +
                    "Add PolyStangVehicleAdapter or RCCVehicleAdapter to the car.");
            }
        }

        private void BeginDriving()
        {
            if (vehicleReady) return; // guard against double-entry
            vehicleReady = true;
            ChangeState(drivingState);

            if (logStateChanges)
                Debug.Log("[PlayerStateManager] Initialized in Driving state");
        }

        private void Update() => currentState?.Update();
        private void FixedUpdate() => currentState?.FixedUpdate();

        public void ChangeState(PlayerState newState)
        {
            currentState?.Exit();

            string oldStateName = currentState?.GetType().Name ?? "None";
            string newStateName = newState?.GetType().Name ?? "None";

            currentState = newState;
            currentState?.Enter();

            if (logStateChanges)
                Debug.Log($"[PlayerStateManager] State changed: {oldStateName} → {newStateName}");
        }

        public void SwitchToDriving() => ChangeState(drivingState);

        public void SwitchToOnFoot()
        {
            // This scene may not have a walking controller set up yet -
            // fail safely rather than letting OnFootState crash on a null reference.
            if (playerCharacter == null)
            {
                Debug.LogWarning("[PlayerStateManager] Cannot switch to OnFoot - no playerCharacter assigned in this scene yet.");
                return;
            }
            ChangeState(onFootState);
        }

        private void OnDestroy()
        {
            RCC_SceneManager.OnVehicleChanged -= HandleRCCVehicleChanged;
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;
            GUI.Label(new Rect(10, 10, 300, 20), $"State: {currentState?.GetType().Name ?? "None"}");
            GUI.Label(new Rect(10, 30, 300, 20), $"Press E to {(IsDriving ? "Exit Car" : "Enter Car")}");
        }
    }
}