// PlayerInteractionDetector.cs
using UnityEngine;
using System.Collections.Generic;

namespace RelaxingDrive.World
{
    /// <summary>
    /// Detects nearby interactable objects using trigger colliders.
    /// Attach to player character with a sphere collider.
    /// Maintains list of nearby interactables and finds closest one.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class PlayerInteractionDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private float detectionRadius = 3f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        
        private SphereCollider detectionCollider;
        private List<IInteractable> nearbyInteractables = new List<IInteractable>();
        private IInteractable closestInteractable;
        
        public IInteractable ClosestInteractable => closestInteractable;
        public bool HasInteractable => closestInteractable != null;
        
        private void Awake()
        {
            // Setup detection collider
            detectionCollider = GetComponent<SphereCollider>();
            detectionCollider.isTrigger = true;
            detectionCollider.radius = detectionRadius;
        }
        
        private void Update()
        {
            UpdateClosestInteractable();
        }
        
        /// <summary>
        /// Finds the closest interactable from the list
        /// </summary>
        private void UpdateClosestInteractable()
        {
            closestInteractable = null;
            float closestDistance = float.MaxValue;
            
            // Remove null entries (destroyed objects)
            nearbyInteractables.RemoveAll(item => item == null);
            
            foreach (var interactable in nearbyInteractables)
            {
                if (!interactable.CanInteract())
                    continue;
                
                // Get distance (need to cast to MonoBehaviour to access transform)
                if (interactable is MonoBehaviour mb)
                {
                    float distance = Vector3.Distance(transform.position, mb.transform.position);
                    
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestInteractable = interactable;
                    }
                }
            }
            
            if (showDebugInfo && closestInteractable != null)
            {
                Debug.Log($"Closest interactable: {closestInteractable.GetInteractionPrompt()}");
            }
        }
        
        /// <summary>
        /// Interact with the closest interactable
        /// </summary>
        public void InteractWithClosest()
        {
            if (closestInteractable != null && closestInteractable.CanInteract())
            {
                closestInteractable.Interact();
            }
            else
            {
                if (showDebugInfo)
                    Debug.Log("No interactable nearby");
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            IInteractable interactable = other.GetComponent<IInteractable>();
            
            if (interactable != null && !nearbyInteractables.Contains(interactable))
            {
                nearbyInteractables.Add(interactable);
                
                if (showDebugInfo)
                    Debug.Log($"Entered interaction range: {interactable.GetInteractionPrompt()}");
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            IInteractable interactable = other.GetComponent<IInteractable>();
            
            if (interactable != null)
            {
                nearbyInteractables.Remove(interactable);
                
                if (showDebugInfo)
                    Debug.Log($"Left interaction range: {interactable.GetInteractionPrompt()}");
            }
        }
        
        // Visualize detection range
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}