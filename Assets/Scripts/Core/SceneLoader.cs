// SceneLoader.cs
// Attach to a cylinder (or any GameObject) with a Trigger Collider.
// When the car drives into the trigger, the screen fades to black and loads a new scene.

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using RelaxingDrive.Player;

namespace RelaxingDrive.World
{
    /// <summary>
    /// Loads a new scene when the player's car drives into this trigger.
    /// Fades the screen to black before loading.
    ///
    /// Setup:
    ///   1. Add this script to a cylinder GameObject.
    ///   2. Ensure the cylinder's collider has "Is Trigger" ticked.
    ///   3. Assign the scene name to load in the Inspector.
    ///   4. Assign any UIDocument already in your scene (e.g. the HUD).
    ///   5. Make sure the target scene is added in File > Build Settings.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SceneLoader : MonoBehaviour
    {
        [Header("Scene Settings")]
        [Tooltip("Exact name of the scene to load (must be in Build Settings)")]
        [SerializeField] private string sceneToLoad;

        [Header("Fade Settings")]
        [Tooltip("How long the fade to black takes, in seconds")]
        [SerializeField] private float fadeDuration = 1.0f;

        [Header("References")]
        [Tooltip("Any UIDocument in your scene — the fade overlay is added to it at runtime")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // Prevents the trigger firing more than once if the car lingers
        private bool isLoading = false;

        private void Awake()
        {
            // Guarantee the collider on this object is a trigger
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Ignore if we've already started loading
            if (isLoading) return;

            // Only respond to the Player tag (the car carries this tag)
            if (!other.CompareTag("Player")) return;

            // Only trigger while the player is actually driving
            // (guards against the walking player accidentally activating this)
            if (PlayerStateManager.Instance == null || !PlayerStateManager.Instance.IsDriving) return;

            if (showDebugMessages)
                Debug.Log($"[SceneLoader] Car entered trigger — loading scene: '{sceneToLoad}'");

            if (string.IsNullOrEmpty(sceneToLoad))
            {
                Debug.LogError("[SceneLoader] No scene name set in the Inspector! Assign one to load.");
                return;
            }

            isLoading = true;
            StartCoroutine(FadeAndLoad());
        }

        /// <summary>
        /// Fades the screen to black over <see cref="fadeDuration"/> seconds,
        /// then loads the target scene.
        /// </summary>
        private IEnumerator FadeAndLoad()
        {
            // --- Create a full-screen black overlay using UI Toolkit ---

            VisualElement overlay = null;

            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                overlay = new VisualElement();

                // Style it to cover the entire screen
                overlay.style.position = Position.Absolute;
                overlay.style.left   = 0;
                overlay.style.top    = 0;
                overlay.style.right  = 0;
                overlay.style.bottom = 0;
                overlay.style.backgroundColor = new StyleColor(Color.black);
                overlay.style.opacity = 0f;

                // Add to the root so it sits on top of all other UI
                uiDocument.rootVisualElement.Add(overlay);
            }
            else
            {
                Debug.LogWarning("[SceneLoader] No UIDocument assigned — scene will load without a fade.");
            }

            // --- Animate opacity from 0 → 1 ---

            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);

                if (overlay != null)
                    overlay.style.opacity = t;

                yield return null;
            }

            // Ensure fully black before loading
            if (overlay != null)
                overlay.style.opacity = 1f;

            // Small buffer so the black frame is visible before the scene switches
            yield return new WaitForSeconds(0.1f);

            // --- Load the scene ---
            SceneManager.LoadScene(sceneToLoad);
        }

        // Draw a coloured gizmo in the Scene view so the trigger is easy to see
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            Gizmos.DrawSphere(transform.position, transform.localScale.x * 0.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, transform.localScale.x * 0.5f);
        }
    }
}
