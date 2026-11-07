// AnimalController.cs
// Location: Assets/Scripts/Animals/AnimalController.cs
//
// Detects when the player is near this animal and reports a sighting.
// Attached to each animal GameObject in the scene.
//
// UPDATED (Nov 2026): Sighting tiers.
//  - New Inspector field "Region" (Local / Remote) - set it on every placed animal.
//  - Proximity now reports to SightingManager (which calls AnimalDiscoveryManager
//    for the First tier), and passes the SightingResult to the info card.
//  - Proximity is just ONE discovery trigger. Future triggers (stop, photo, honk)
//    build a SightingContext and call SightingManager.ReportSighting() the same way.
//  - Gizmo colour shows the region: green = Local, orange = Remote.

using UnityEngine;
using RelaxingDrive.UI;
using RelaxingDrive.Player;

namespace RelaxingDrive.Animals
{
    [RequireComponent(typeof(Collider))]
    public class AnimalController : MonoBehaviour
    {
        [Header("Animal Configuration")]
        [Tooltip("The ScriptableObject containing this animal's data")]
        [SerializeField] private AnimalData animalData;

        [Tooltip("Local = start area (NSW). Remote = wider world (rest of Australia).")]
        [SerializeField] private SightingRegion region = SightingRegion.Local;

        [Header("Detection Settings")]
        [Tooltip("How often to check for player proximity (in seconds)")]
        [SerializeField] private float detectionInterval = 0.5f;

        [Header("Player References")]
        [Tooltip("Reference to the car GameObject (leave empty to get it from PlayerStateManager)")]
        [SerializeField] private GameObject carObject;

        [Tooltip("Reference to the walking player GameObject (leave empty to get it from PlayerStateManager)")]
        [SerializeField] private GameObject walkingPlayerObject;

        [Header("Debug Visualization")]
        [SerializeField] private bool showDetectionGizmo = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // Gizmo colours per region, so Local/Remote setup is visible in the Scene view.
        private static readonly Color LocalGizmoColor = new Color(0.3f, 1f, 0.3f, 0.3f);
        private static readonly Color RemoteGizmoColor = new Color(1f, 0.6f, 0.2f, 0.3f);

        // Internal state
        private Transform activePlayerTransform;
        private bool playerInRange = false;
        private AnimalInfoUI animalInfoUI;
        private PlayerStateManager playerStateManager;

        public AnimalData Data => animalData;
        public SightingRegion Region => region;

        private void Start()
        {
            if (animalData == null)
            {
                Debug.LogError($"AnimalController on {gameObject.name}: AnimalData is not assigned!", this);
                enabled = false;
                return;
            }

            playerStateManager = PlayerStateManager.Instance;
            if (playerStateManager == null)
            {
                Debug.LogError($"AnimalController ({animalData.AnimalName}): Could not find PlayerStateManager!");
            }

            if (carObject == null || walkingPlayerObject == null)
            {
                FindPlayerObjects();
            }

            animalInfoUI = FindFirstObjectByType<AnimalInfoUI>();
            if (animalInfoUI == null)
            {
                Debug.LogError($"AnimalController ({animalData.AnimalName}): Could not find AnimalInfoUI in scene!");
            }

            InvokeRepeating(nameof(CheckPlayerProximity), 0.5f, detectionInterval);

            Log($"Initialized - {region}, range {animalData.DiscoveryRange}m");
        }

        /// <summary>
        /// Gets car and walking player from PlayerStateManager, falling back to names.
        /// </summary>
        private void FindPlayerObjects()
        {
            if (playerStateManager != null)
            {
                if (carObject == null && playerStateManager.CarGameObject != null)
                    carObject = playerStateManager.CarGameObject;

                if (walkingPlayerObject == null && playerStateManager.PlayerCharacter != null)
                    walkingPlayerObject = playerStateManager.PlayerCharacter;
            }

            if (carObject == null) carObject = GameObject.Find("AuStang");
            if (walkingPlayerObject == null) walkingPlayerObject = GameObject.Find("PlayerWalking");

            if (carObject == null)
                Debug.LogWarning($"AnimalController ({animalData.AnimalName}): Could not find car GameObject!");
            if (walkingPlayerObject == null)
                Debug.LogWarning($"AnimalController ({animalData.AnimalName}): Could not find walking player GameObject!");
        }

        /// <summary>
        /// Uses PlayerStateManager.IsDriving rather than activeInHierarchy, because
        /// the car stays active (just frozen) while the player walks.
        /// </summary>
        private Transform GetActivePlayerTransform()
        {
            if (playerStateManager != null)
            {
                if (playerStateManager.IsDriving && carObject != null) return carObject.transform;
                if (playerStateManager.IsOnFoot && walkingPlayerObject != null) return walkingPlayerObject.transform;
                return null;
            }

            // Fallback if PlayerStateManager is missing.
            if (walkingPlayerObject != null && walkingPlayerObject.activeInHierarchy) return walkingPlayerObject.transform;
            if (carObject != null && carObject.activeInHierarchy) return carObject.transform;
            return null;
        }

        /// <summary>Called repeatedly via InvokeRepeating.</summary>
        private void CheckPlayerProximity()
        {
            activePlayerTransform = GetActivePlayerTransform();
            if (activePlayerTransform == null)
            {
                FindPlayerObjects();
                return;
            }

            float distance = Vector3.Distance(transform.position, activePlayerTransform.position);

            if (distance <= animalData.DiscoveryRange)
            {
                if (!playerInRange)
                {
                    playerInRange = true;
                    TriggerSighting();
                }
            }
            else if (playerInRange && distance > animalData.DiscoveryRange * 1.2f)
            {
                // 20% buffer stops the card flickering at the edge of the range.
                playerInRange = false;
                NotifyPlayerLeftRange();
            }
        }

        /// <summary>
        /// Builds a SightingContext and hands it to SightingManager, which decides
        /// the tier. The result goes straight to the info card.
        /// </summary>
        private void TriggerSighting()
        {
            SightingManager sightings = SightingManager.Instance;
            if (sightings == null) return; // Only null while the game is quitting.

            bool onFoot = playerStateManager != null && playerStateManager.IsOnFoot;
            var context = new SightingContext(animalData, region, onFoot, transform.position);

            SightingResult result = sightings.ReportSighting(context);

            if (animalInfoUI != null)
            {
                animalInfoUI.SetCurrentAnimal(animalData, result);
            }
        }

        private void NotifyPlayerLeftRange()
        {
            if (animalInfoUI != null)
            {
                animalInfoUI.OnAnimalRangeExit(animalData);
            }
        }

        private void Log(string message)
        {
            if (showDebugMessages && animalData != null)
                Debug.Log($"[AnimalController:{animalData.AnimalName}] {message}");
        }

        #region Gizmos & Debug

        private void OnDrawGizmos()
        {
            if (!showDetectionGizmo || animalData == null) return;

            Gizmos.color = region == SightingRegion.Local ? LocalGizmoColor : RemoteGizmoColor;
            Gizmos.DrawWireSphere(transform.position, animalData.DiscoveryRange);

            if (Application.isPlaying && activePlayerTransform != null &&
                Vector3.Distance(transform.position, activePlayerTransform.position) <= animalData.DiscoveryRange)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, activePlayerTransform.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (animalData == null) return;

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"{animalData.AnimalName} ({region})\nRange: {animalData.DiscoveryRange}m");
#endif
        }

        /// <summary>Right-click the component during Play to test without driving there.</summary>
        [ContextMenu("Force Sighting")]
        public void ForceSighting()
        {
            if (animalData == null) return;
            playerInRange = true;
            TriggerSighting();
        }

        private void OnDestroy()
        {
            CancelInvoke(nameof(CheckPlayerProximity));
        }

        #endregion
    }
}