// PlayerStateManager.cs
//
// UPDATED (Nov 2026, river floats): changes marked "NEW (river)".
//   - FloatingState added (riding a river craft)
//   - IsFloating, SwitchToFloating(craft), SwitchToOnFootAt(position, rotation)
//   - ActivePlayerTransform returns the character while floating, so VisitZones,
//     road names and sightings keep working on the river.
//
// UPDATED (Nov 2026, fuel): changes marked "NEW (fuel)".
//   - CarEntryLocked + SetCarEntryLocked(bool): FuelPump locks the car while
//     refuelling; OnFootState hides "Enter Car" and blocks entry until unlocked.
using UnityEngine;
using RelaxingDrive.Core;
using RelaxingDrive.World; // NEW (river): RiverCraftMover

namespace RelaxingDrive.Player
{
    /// <summary>
    /// Manages player state transitions between Driving, OnFoot and Floating.
    /// Implements State Pattern - delegates behavior to current state.
    /// Singleton for easy access from other systems.
    ///
    /// Depends on IVehicleController, not a concrete car type, so this works
    /// for both PolyStang (Itch scene, car placed in-scene at design time -
    /// resolved immediately) and RCC (Steam scenes, car spawned dynamically
    /// by RCC_Spawner in Start() - resolved via RCC_SceneManager.OnVehicleChanged,
    /// since Awake() runs too early to see it).
    ///
    /// Observer: raises OnStateChanged after every state change, so other systems
    /// (e.g. AudioStateMixer) can react without this class knowing they exist.
    /// </summary>
    public class PlayerStateManager : MonoBehaviour, IActivePlayerProvider
    {
        private static PlayerStateManager instance;
        public static PlayerStateManager Instance => instance;

        /// <summary>
        /// Fired AFTER the new state's Enter() has run, so listeners can safely
        /// check IsDriving / IsOnFoot / IsFloating. Passes the new state.
        /// </summary>
        public event System.Action<PlayerState> OnStateChanged;

        [Header("References")]
        [Tooltip("Leave empty for RCC scenes - resolved automatically once RCC_Spawner spawns the car. Assign directly for PolyStang scenes.")]
        [SerializeField] private GameObject carGameObject;
        [SerializeField] private GameObject playerCharacter;
        [SerializeField] private FollowCamera followCamera; // PolyStang/Itch scene only - can leave empty in RCC scene
        [Tooltip("Starter Assets' MainCamera (Camera + CinemachineBrain), used only when the walking controller owns its own camera. Leave empty in scenes without it (e.g. Itch).")]
        [SerializeField] private GameObject walkingCameraObject;

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
        private FloatingState floatingState; // NEW (river)

        private IVehicleController vehicleController;
        private bool vehicleReady = false;

        public GameObject CarGameObject => carGameObject;
        public GameObject PlayerCharacter => playerCharacter;
        public FollowCamera FollowCamera => followCamera;
        public GameObject WalkingCameraObject => walkingCameraObject;
        public IVehicleController VehicleController => vehicleController;
        public Vector3 DrivingCameraOffset => drivingCameraOffset;
        public Vector3 WalkingCameraOffset => walkingCameraOffset;
        public Vector3 ExitCarOffset => exitCarOffset;

        public bool IsDriving => currentState is DrivingState;
        public bool IsOnFoot => currentState is OnFootState;
        public bool IsFloating => currentState is FloatingState; // NEW (river)

        // NEW (fuel): while true, the player can't get into the car.
        // Set by FuelPump while refuelling; could be reused later (e.g. a cutscene).
        public bool CarEntryLocked { get; private set; }

        // IActivePlayerProvider: the transform the player is currently controlling.
        // VisitZone (and later systems) ask this instead of checking states themselves.
        // Returns null before the RCC car has spawned, which VisitZone safely ignores.
        // NEW (river): while floating, the character rides the board, so it's the answer.
        public Transform ActivePlayerTransform =>
            IsDriving ? (carGameObject != null ? carGameObject.transform : null) :
            (IsOnFoot || IsFloating) ? (playerCharacter != null ? playerCharacter.transform : null) :
            null;

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
                    // Not fatal: RCC scenes don't need FollowCamera at all now that both
                    // sides own their own camera (RCC's rig while driving, Starter Assets'
                    // Cinemachine rig while on foot). Only PolyStang/Itch-style scenes
                    // depend on FollowCamera being present.
                    Debug.Log("[PlayerStateManager] FollowCamera not found in scene - " +
                        "fine if this scene's vehicle and walking controller each own their own camera.");
                }
            }

            drivingState = new DrivingState(this);
            onFootState = new OnFootState(this);
            floatingState = new FloatingState(this); // NEW (river)

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

            // Tell listeners LAST - the new state is fully set up by now.
            OnStateChanged?.Invoke(currentState);
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

        /// <summary>
        /// NEW (river): go on foot at a given spot instead of beside the car
        /// (e.g. stepping off a river craft at the landing).
        /// </summary>
        public void SwitchToOnFootAt(Vector3 position, Quaternion rotation)
        {
            onFootState.SetArrivalPoint(position, rotation);
            SwitchToOnFoot();
        }

        /// <summary>
        /// NEW (river): hop onto a river craft. Only allowed on foot - you can't
        /// drive onto a paddle board.
        /// </summary>
        public void SwitchToFloating(RiverCraftMover craft)
        {
            if (!IsOnFoot || craft == null || playerCharacter == null)
            {
                Debug.LogWarning("[PlayerStateManager] Cannot switch to Floating - must be on foot with a valid craft.");
                return;
            }
            floatingState.SetCraft(craft);
            ChangeState(floatingState);
        }

        /// <summary>
        /// NEW (fuel): lock or unlock getting into the car.
        /// OnFootState reads CarEntryLocked every frame, so the "Enter Car"
        /// prompt appears by itself as soon as this is unlocked.
        /// </summary>
        public void SetCarEntryLocked(bool locked)
        {
            if (CarEntryLocked == locked) return;
            CarEntryLocked = locked;

            if (logStateChanges)
                Debug.Log($"[PlayerStateManager] Car entry {(locked ? "LOCKED" : "unlocked")}");
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