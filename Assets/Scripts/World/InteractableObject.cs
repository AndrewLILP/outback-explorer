// InteractableObject.cs
using UnityEngine;
using RelaxingDrive.Core;
using RelaxingDrive.Data;

namespace RelaxingDrive.World
{
    /// <summary>
    /// Generic interactable object that can be attached to buildings, NPCs, etc.
    /// Triggers dialogue when interacted with.
    /// Uses trigger colliders to detect player proximity.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class InteractableObject : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] private string interactionPrompt = "Press E to interact";
        [SerializeField] private bool isInteractable = true;

        [Header("Dialogue")]
        [SerializeField] private DialogueData dialogueData;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmo = true;

        private Collider interactionCollider;

        private void Awake()
        {
            // Ensure collider is set to trigger
            interactionCollider = GetComponent<Collider>();
            interactionCollider.isTrigger = true;
        }

        /// <summary>
        /// Called when player presses E near this object
        /// </summary>
        public void Interact()
        {
            if (!CanInteract())
            {
                Debug.LogWarning($"Cannot interact with {gameObject.name}");
                return;
            }

            Debug.Log($"Interacting with: {gameObject.name}");

            // Trigger dialogue if we have dialogue data
            if (dialogueData != null && DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(dialogueData);
            }
            else
            {
                Debug.LogWarning($"{gameObject.name} has no DialogueData assigned!");
            }
        }

        /// <summary>
        /// Returns the interaction prompt text
        /// </summary>
        public string GetInteractionPrompt()
        {
            return interactionPrompt;
        }

        /// <summary>
        /// Check if currently interactable
        /// </summary>
        public bool CanInteract()
        {
            return isInteractable;
        }

        /// <summary>
        /// Enable/disable interaction at runtime
        /// </summary>
        public void SetInteractable(bool value)
        {
            isInteractable = value;
        }

        // Visualize interaction range in editor
        private void OnDrawGizmos()
        {
            if (!showDebugGizmo)
                return;

            Gizmos.color = isInteractable ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 3f); // Interaction range
        }
    }
}