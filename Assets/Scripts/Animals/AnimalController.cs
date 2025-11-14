// AnimalController.cs
// Detects when player is near this animal and triggers discovery
// Attached to each animal GameObject in the scene

using UnityEngine;

namespace RelaxingDrive.Animals
{
    /// <summary>
    /// Detects player proximity and triggers animal discovery.
    /// Attach this component to each animal GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AnimalController : MonoBehaviour
    {
        [Header("Animal Configuration")]
        [Tooltip("The ScriptableObject containing this animal's data")]
        [SerializeField] private AnimalData animalData;

        [Header("Detection Settings")]
        [Tooltip("Tag of the player GameObject (default: 'Player')")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("How often to check for player proximity (in seconds)")]
        [SerializeField] private float detectionInterval = 0.5f;

        [Header("Debug Visualization")]
        [Tooltip("Show detection range in Scene view")]
        [SerializeField] private bool showDetectionGizmo = true;

        [SerializeField] private Color gizmoColor = new Color(0.3f, 1f, 0.3f, 0.3f);

        // Internal state
        private Transform playerTransform;
        private bool hasBeenDiscovered = false;

        private void Start()
        {
            // Validate configuration
            if (animalData == null)
            {
                Debug.LogError($"AnimalController on {gameObject.name}: AnimalData is not assigned!", this);
                enabled = false;
                return;
            }

            // Find player
            FindPlayer();

            // Start proximity detection loop
            InvokeRepeating(nameof(CheckPlayerProximity), 0.5f, detectionInterval);

            Debug.Log($"AnimalController initialized for {animalData.AnimalName} - Detection range: {animalData.DiscoveryRange}m");
        }

        /// <summary>
        /// Finds the player GameObject in the scene.
        /// </summary>
        private void FindPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);

            if (player != null)
            {
                playerTransform = player.transform;
                Debug.Log($"AnimalController ({animalData.AnimalName}): Found player at {player.name}");
            }
            else
            {
                Debug.LogWarning($"AnimalController ({animalData.AnimalName}): Could not find GameObject with tag '{playerTag}'");
            }
        }

        /// <summary>
        /// Checks if player is within discovery range.
        /// Called repeatedly via InvokeRepeating.
        /// </summary>
        private void CheckPlayerProximity()
        {
            // If player not found yet, try to find them
            if (playerTransform == null)
            {
                FindPlayer();
                return;
            }

            // Calculate distance to player
            float distance = Vector3.Distance(transform.position, playerTransform.position);

            // Check if player is within discovery range
            if (distance <= animalData.DiscoveryRange)
            {
                TriggerDiscovery();
            }
        }

        /// <summary>
        /// Triggers the animal discovery through AnimalDiscoveryManager.
        /// </summary>
        private void TriggerDiscovery()
        {
            if (AnimalDiscoveryManager.Instance == null)
            {
                Debug.LogError("AnimalController: AnimalDiscoveryManager not found in scene!");
                return;
            }

            // Attempt discovery (manager handles duplicate detection)
            bool isFirstTime = AnimalDiscoveryManager.Instance.DiscoverAnimal(animalData);

            // Track local state for this specific animal instance
            if (isFirstTime && !hasBeenDiscovered)
            {
                hasBeenDiscovered = true;
                OnFirstDiscovery();
            }
        }

        /// <summary>
        /// Called when this specific animal instance is discovered for the first time.
        /// Can be used for visual effects, animations, etc.
        /// </summary>
        private void OnFirstDiscovery()
        {
            Debug.Log($"🎉 First time discovering {animalData.AnimalName} at this location!");

            // TODO: Add visual feedback here in future
            // - Play particle effect
            // - Trigger animation
            // - Play sound effect
        }

        /// <summary>
        /// Draws detection range in Scene view (editor only).
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showDetectionGizmo || animalData == null) return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, animalData.DiscoveryRange);

            // Draw a line to player if in range (during play mode)
            if (Application.isPlaying && playerTransform != null)
            {
                float distance = Vector3.Distance(transform.position, playerTransform.position);

                if (distance <= animalData.DiscoveryRange)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(transform.position, playerTransform.position);
                }
            }
        }

        /// <summary>
        /// Draws more prominent detection range when object is selected.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (animalData == null) return;

            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, animalData.DiscoveryRange);

            // Draw distance label in scene view
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"{animalData.AnimalName}\nRange: {animalData.DiscoveryRange}m"
            );
#endif
        }

        /// <summary>
        /// Manual trigger for testing (can be called from Inspector button).
        /// </summary>
        [ContextMenu("Force Discovery")]
        public void ForceDiscovery()
        {
            if (animalData != null)
            {
                TriggerDiscovery();
            }
            else
            {
                Debug.LogWarning("Cannot force discovery - AnimalData not assigned!");
            }
        }

        private void OnDestroy()
        {
            // Clean up InvokeRepeating
            CancelInvoke(nameof(CheckPlayerProximity));
        }
    }
}