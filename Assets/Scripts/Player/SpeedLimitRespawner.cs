// SpeedLimitRespawner.cs
// Location: Assets/Scripts/Player/SpeedLimitRespawner.cs
//
// Listens for SpeedLimitMonitor.OnLimitExceeded and moves the player's car
// back to the respawn point using RCC's own transport call - the same one
// RCC_Teleporter uses for the sea.
//
// This is deliberately RCC-specific (like RCC_Teleporter). Teleporting isn't
// part of IVehicleController, and adding it there would force a change to
// PolyStangVehicleAdapter for a feature the Itch scene doesn't use.

using UnityEngine;

namespace RelaxingDrive.Player
{
    public class SpeedLimitRespawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Leave empty if the monitor is on this same GameObject.")]
        [SerializeField] private SpeedLimitMonitor monitor;

        [Tooltip("Where the car goes - drag in the same spawn point the sea teleporter uses (scene start).")]
        [SerializeField] private Transform respawnPoint;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private void Awake()
        {
            if (monitor == null)
                monitor = GetComponent<SpeedLimitMonitor>();

            if (monitor == null)
                Debug.LogError("[SpeedLimitRespawner] No SpeedLimitMonitor assigned or found on this GameObject.");

            if (respawnPoint == null)
                Debug.LogError("[SpeedLimitRespawner] No respawnPoint assigned.");
        }

        // Subscribe/unsubscribe in OnEnable/OnDisable so we never leave a
        // dangling listener behind if this object is disabled or destroyed.
        private void OnEnable()
        {
            if (monitor != null) monitor.OnLimitExceeded += Respawn;
        }

        private void OnDisable()
        {
            if (monitor != null) monitor.OnLimitExceeded -= Respawn;
        }

        private void Respawn()
        {
            if (respawnPoint == null) return;

            // Same lookup PlayerStateManager uses - RCC spawns the car at runtime,
            // so we ask RCC for it at the moment we need it rather than caching it.
            RCC_CarControllerV4 car = RCC_SceneManager.Instance != null
                ? RCC_SceneManager.Instance.activePlayerVehicle
                : null;

            if (car == null)
            {
                Debug.LogWarning("[SpeedLimitRespawner] No active RCC vehicle to respawn.");
                return;
            }

            RCC.Transport(car, respawnPoint.position, respawnPoint.rotation);

            if (showDebugLogs)
                Debug.Log($"[SpeedLimitRespawner] Respawned {car.name} at {respawnPoint.name}");
        }
    }
}