// VisitZone.cs
using UnityEngine;
using RelaxingDrive.Core;

namespace RelaxingDrive.World
{
    /// <summary>
    /// Detects when the player enters a zone and notifies the VisitManager.
    /// Attach this to trigger colliders placed throughout the world.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class VisitZone : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [Tooltip("Unique identifier for this zone")]
        [SerializeField] private string zoneID;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        private BoxCollider triggerCollider;

        // Public property to access zone ID
        public string ZoneID => zoneID;

        private void Awake()
        {
            // Ensure we have a trigger collider
            triggerCollider = GetComponent<BoxCollider>();
            triggerCollider.isTrigger = true;

            // Generate unique ID if not set
            if (string.IsNullOrEmpty(zoneID))
            {
                zoneID = $"Zone_{transform.position.x}_{transform.position.z}";
                Debug.LogWarning($"VisitZone had no ID. Auto-generated: {zoneID}");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if the player entered (assuming player has "Player" tag)
            if (other.CompareTag("Player"))
            {
                if (showDebugMessages)
                {
                    Debug.Log($"Player entered zone: {zoneID}");
                }

                // Notify the VisitManager
                VisitManager.Instance.RecordVisit(zoneID);
            }
        }

        // Visualize the zone in the editor
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawCube(transform.position, transform.localScale);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
    }
}