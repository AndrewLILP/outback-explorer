// ResumeSpawnController.cs
// Location: Assets/Scripts/Player/ResumeSpawnController.cs
//
// Lives in the GAMEPLAY scene. Two jobs, both RCC-specific (like
// SpeedLimitRespawner):
//   1. Capture()  - called by PauseFlowManager just before the pause scene
//                   loads. Records the car's position + speed limit state.
//   2. Start()    - when the gameplay scene loads again, if the player chose
//                   "Where I left off", moves the car back with RCC.Transport.
//
// If the player chose "Return to start position" (or this is a fresh launch /
// new game), it does nothing and RCC_Spawner's normal spawn is used.
//
// Setup: add to any GameObject in the gameplay scene (e.g. the same object as
// SpeedLimitMonitor), then drag it into PauseFlowManager's "Resume Spawn" slot.

using System.Collections;
using UnityEngine;
using RelaxingDrive.Core;

namespace RelaxingDrive.Player
{
    public class ResumeSpawnController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Leave empty to find it automatically.")]
        [SerializeField] private SpeedLimitMonitor speedLimitMonitor;

        [Header("Restore")]
        [Tooltip("Lift the car slightly so its wheels don't start inside the road.")]
        [SerializeField] private float liftOnRestore = 0.3f;

        [Tooltip("RCC spawns the car at runtime - wait up to this long for it.")]
        [SerializeField] private float maxWaitForCarSeconds = 5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private void Awake()
        {
            if (speedLimitMonitor == null)
                speedLimitMonitor = FindFirstObjectByType<SpeedLimitMonitor>();
        }

        /// <summary>Snapshot the car before the gameplay scene unloads.</summary>
        public void Capture()
        {
            RCC_CarControllerV4 car = GetCar();

            if (car == null)
            {
                // Nothing to restore - the menu will just offer a normal resume.
                ResumeSpawnHandoff.Clear();
                Log("No active car to capture - resume will use the start position.");
                return;
            }

            bool limitOn = speedLimitMonitor == null || speedLimitMonitor.LimitEnabled;
            float limitKmh = speedLimitMonitor != null ? speedLimitMonitor.SpeedLimitKmh : 65f;

            ResumeSpawnHandoff.Capture(car.transform.position, car.transform.rotation, limitOn, limitKmh);
            Log($"Captured car at {car.transform.position} (limit {(limitOn ? "on" : "off")})");
        }

        // Start can be a coroutine: Unity runs it over several frames for us.
        private IEnumerator Start()
        {
            if (!ResumeSpawnHandoff.ShouldRestore)
            {
                ResumeSpawnHandoff.Clear();
                yield break;
            }

            // Copy the values, then clear straight away so a later reload
            // (New Game, hold-1) can never reuse an old position.
            Vector3 position = ResumeSpawnHandoff.Position + Vector3.up * liftOnRestore;
            Quaternion rotation = ResumeSpawnHandoff.Rotation;
            bool limitOn = ResumeSpawnHandoff.SpeedLimitEnabled;
            float limitKmh = ResumeSpawnHandoff.SpeedLimitKmh;
            ResumeSpawnHandoff.Clear();

            // Wait for RCC to spawn the player car.
            float waited = 0f;
            RCC_CarControllerV4 car = GetCar();
            while (car == null && waited < maxWaitForCarSeconds)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
                car = GetCar();
            }

            if (car == null)
            {
                Debug.LogWarning("[ResumeSpawnController] Car never spawned - staying at the start position.");
                yield break;
            }

            RCC.Transport(car, position, rotation);
            Log($"Restored car to {position}");

            // Let physics run one step first: if the start spawn sits inside a
            // SpeedLimitGate trigger, that gate fires now and we overwrite it after.
            yield return new WaitForFixedUpdate();

            if (speedLimitMonitor != null)
            {
                speedLimitMonitor.SpeedLimitKmh = limitKmh;
                speedLimitMonitor.LimitEnabled = limitOn;
            }
        }

        private static RCC_CarControllerV4 GetCar()
        {
            return RCC_SceneManager.Instance != null
                ? RCC_SceneManager.Instance.activePlayerVehicle
                : null;
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[ResumeSpawnController] {message}");
        }
    }
}