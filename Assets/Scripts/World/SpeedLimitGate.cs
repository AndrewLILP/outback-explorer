// SpeedLimitGate.cs
// Location: Assets/Scripts/World/SpeedLimitGate.cs
//
// A trigger "gate" that switches the speed limit on or off when the player
// passes through it. Use them in pairs at each end of the bridge:
//
//   TOWN ----[Gate: Town End, ON]==== bridge ====[Gate: Highway End, OFF]---- HIGHWAY
//
// Why two gates instead of one toggle? A single toggle flips state every
// time you touch it, so reversing halfway, or clipping it twice, leaves the
// limit in the wrong state. With two gates, each one SETS a known state, so
// whichever gate you passed through last is always right, in either direction.
//
// Single responsibility: this only detects the player and sets
// SpeedLimitMonitor.LimitEnabled. It doesn't know about UI or respawning.

using UnityEngine;
using RelaxingDrive.Player;

namespace RelaxingDrive.World
{
    [RequireComponent(typeof(Collider))]
    public class SpeedLimitGate : MonoBehaviour
    {
        [Header("Behaviour")]
        [Tooltip("Ticked = speed limit ON after passing (town end).\n" +
                 "Unticked = speed limit OFF after passing (highway end).")]
        [SerializeField] private bool enableLimit = true;

        [Header("References")]
        [Tooltip("Leave empty to find it automatically in the scene.")]
        [SerializeField] private SpeedLimitMonitor monitor;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private void Awake()
        {
            if (monitor == null)
                monitor = Object.FindFirstObjectByType<SpeedLimitMonitor>();

            if (monitor == null)
                Debug.LogError($"[SpeedLimitGate] '{name}': no SpeedLimitMonitor found in the scene.");
        }

        private void Reset()
        {
            // Runs when the component is first added in the Editor:
            // make sure the collider is a trigger so the car drives through it.
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (monitor == null || !IsActivePlayer(other)) return;

            // Already in the right state (e.g. a second child collider of
            // the car entering) - nothing to do.
            if (monitor.LimitEnabled == enableLimit) return;

            monitor.LimitEnabled = enableLimit;

            if (showDebugLogs)
                Debug.Log($"[SpeedLimitGate] '{name}': speed limit {(enableLimit ? "ON" : "OFF")}");
        }

        /// <summary>
        /// True if the collider belongs to whatever the player is controlling
        /// right now (the car while driving, the character on foot).
        /// RCC cars have many child colliders, so we check IsChildOf rather
        /// than comparing the collider's own GameObject.
        /// </summary>
        private bool IsActivePlayer(Collider other)
        {
            PlayerStateManager psm = PlayerStateManager.Instance;
            if (psm == null) return false;

            Transform active = psm.ActivePlayerTransform;
            return active != null && other.transform.IsChildOf(active);
        }

        // Draws the gate in the Scene view: green = limit ON, red = limit OFF.
        // Makes it easy to check which gate is which at a glance.
        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.color = enableLimit
                ? new Color(0.3f, 0.8f, 0.3f, 0.35f)
                : new Color(0.9f, 0.3f, 0.2f, 0.35f);

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}