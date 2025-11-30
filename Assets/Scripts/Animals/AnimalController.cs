// AnimalController.cs
// Detects when player is near this animal and triggers discovery
// Attached to each animal GameObject in the scene
// UPDATED: Now detects both driving AND walking player states

using UnityEngine;
using RelaxingDrive.UI;

namespace RelaxingDrive.Animals
{
    /// <summary>
    /// Detects player proximity and triggers animal discovery.
    /// Works with both driving (car) and walking (on foot) player states.
    /// Attach this component to each animal GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AnimalController : MonoBehaviour
    {
        [Header("Animal Configuration")]
        [Tooltip("The ScriptableObject containing this animal's data")]
        [SerializeField] private AnimalData animalData;

        [Header("Detection Settings")]
        [Tooltip("How often to check for player proximity (in seconds)")]
        [SerializeField] private float detectionInterval = 0.5f;

        [Header("Player References")]
        [Tooltip("Reference to the car GameObject (AuStang)")]
        [SerializeField] private GameObject carObject;
        
        [Tooltip("Reference to the walking player GameObject (PlayerWalking)")]
        [SerializeField] private GameObject walkingPlayerObject;

        [Header("Debug Visualization")]
        [Tooltip("Show detection range in Scene view")]
        [SerializeField] private bool showDetectionGizmo = true;

        [SerializeField] private Color gizmoColor = new Color(0.3f, 1f, 0.3f, 0.3f);

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // Internal state
        private Transform activePlayerTransform;
        private bool hasBeenDiscovered = false;
        private bool playerInRange = false;
        private AnimalInfoUI animalInfoUI;

        private void Start()
        {
            // Validate configuration
            if (animalData == null)
            {
                Debug.LogError($"AnimalController on {gameObject.name}: AnimalData is not assigned!", this);
                enabled = false;
                return;
            }

            // Find player objects if not assigned
            if (carObject == null || walkingPlayerObject == null)
            {
                FindPlayerObjects();
            }

            // Find AnimalInfoUI
            animalInfoUI = FindFirstObjectByType<AnimalInfoUI>();
            if (animalInfoUI == null)
            {
                Debug.LogError($"AnimalController ({animalData.AnimalName}): Could not find AnimalInfoUI in scene! UI will not work!");
            }
            else
            {
                if (showDebugMessages)
                {
                    Debug.Log($"AnimalController ({animalData.AnimalName}): Found AnimalInfoUI successfully");
                }
            }

            // Start proximity detection loop
            InvokeRepeating(nameof(CheckPlayerProximity), 0.5f, detectionInterval);

            Debug.Log($"AnimalController initialized for {animalData.AnimalName} - Detection range: {animalData.DiscoveryRange}m");
        }

        /// <summary>
        /// Finds the car and walking player GameObjects in the scene.
        /// </summary>
        private void FindPlayerObjects()
        {
            // Try to find car by name (AuStang)
            if (carObject == null)
            {
                carObject = GameObject.Find("AuStang");
                if (carObject != null && showDebugMessages)
                {
                    Debug.Log($"AnimalController ({animalData.AnimalName}): Found car object: {carObject.name}");
                }
            }

            // Try to find walking player by name (PlayerWalking)
            if (walkingPlayerObject == null)
            {
                walkingPlayerObject = GameObject.Find("PlayerWalking");
                if (walkingPlayerObject != null && showDebugMessages)
                {
                    Debug.Log($"AnimalController ({animalData.AnimalName}): Found walking player object: {walkingPlayerObject.name}");
                }
            }

            // Fallback: try to find by tag
            if (carObject == null || walkingPlayerObject == null)
            {
                GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
                foreach (GameObject obj in playerObjects)
                {
                    if (obj.name.Contains("AuStang") || obj.name.Contains("Car"))
                    {
                        carObject = obj;
                    }
                    else if (obj.name.Contains("Walking") || obj.name.Contains("Player"))
                    {
                        walkingPlayerObject = obj;
                    }
                }
            }

            // Log warnings if still not found
            if (carObject == null)
            {
                Debug.LogWarning($"AnimalController ({animalData.AnimalName}): Could not find car GameObject!");
            }
            if (walkingPlayerObject == null)
            {
                Debug.LogWarning($"AnimalController ({animalData.AnimalName}): Could not find walking player GameObject!");
            }
        }

        /// <summary>
        /// Gets the currently active player transform (car or walking player).
        /// </summary>
        private Transform GetActivePlayerTransform()
        {
            // Check car first (if it exists and is active)
            if (carObject != null && carObject.activeInHierarchy)
            {
                return carObject.transform;
            }

            // Check walking player (if it exists and is active)
            if (walkingPlayerObject != null && walkingPlayerObject.activeInHierarchy)
            {
                return walkingPlayerObject.transform;
            }

            return null;
        }

        /// <summary>
        /// Checks if player is within discovery range.
        /// Called repeatedly via InvokeRepeating.
        /// </summary>
        private void CheckPlayerProximity()
        {
            // Get the currently active player (car or walking)
            activePlayerTransform = GetActivePlayerTransform();

            // If no player found, try to find them again
            if (activePlayerTransform == null)
            {
                FindPlayerObjects();
                return;
            }

            // Calculate distance to player
            float distance = Vector3.Distance(transform.position, activePlayerTransform.position);

            // Check if player entered range
            if (distance <= animalData.DiscoveryRange)
            {
                if (!playerInRange)
                {
                    // Player just entered range
                    playerInRange = true;

                    if (showDebugMessages)
                    {
                        string playerMode = activePlayerTransform == carObject?.transform ? "DRIVING" : "WALKING";
                        Debug.Log($"AnimalController ({animalData.AnimalName}): Player entered range ({playerMode}) (distance: {distance:F1}m)");
                    }

                    TriggerDiscovery();
                }
            }
            else
            {
                // Check if player left range (with buffer to prevent flicker)
                if (playerInRange && distance > animalData.DiscoveryRange * 1.2f)
                {
                    // Player left range
                    playerInRange = false;

                    if (showDebugMessages)
                    {
                        Debug.Log($"AnimalController ({animalData.AnimalName}): Player left range (distance: {distance:F1}m)");
                    }

                    NotifyPlayerLeftRange();
                }
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

            // Notify UI to show this animal
            if (animalInfoUI != null)
            {
                if (showDebugMessages)
                {
                    Debug.Log($"AnimalController ({animalData.AnimalName}): Calling UI.SetCurrentAnimal()");
                }

                animalInfoUI.SetCurrentAnimal(animalData, transform.position);
            }
            else
            {
                Debug.LogWarning($"AnimalController ({animalData.AnimalName}): animalInfoUI is null, cannot show UI!");
            }
        }

        /// <summary>
        /// Notifies UI that player left this animal's range.
        /// </summary>
        private void NotifyPlayerLeftRange()
        {
            if (animalInfoUI != null)
            {
                if (showDebugMessages)
                {
                    Debug.Log($"AnimalController ({animalData.AnimalName}): Calling UI.OnAnimalRangeExit()");
                }

                animalInfoUI.OnAnimalRangeExit(animalData);
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
            if (Application.isPlaying && activePlayerTransform != null)
            {
                float distance = Vector3.Distance(transform.position, activePlayerTransform.position);

                if (distance <= animalData.DiscoveryRange)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(transform.position, activePlayerTransform.position);
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
                playerInRange = true;
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