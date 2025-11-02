// ActivatableObject.cs
using System.Collections;
using UnityEngine;
using RelaxingDrive.Core;

namespace RelaxingDrive.World
{
    /// <summary>
    /// Makes a GameObject activate when a zone reaches a specific visit threshold.
    /// Uses Observer Pattern - subscribes to VisitManager events.
    /// Attach this to any building, NPC, or object that should appear based on visits.
    /// </summary>
    public class ActivatableObject : MonoBehaviour, IActivatable
    {
        [Header("Activation Settings")]
        [Tooltip("The zone ID that triggers this activation")]
        [SerializeField] private string targetZoneID;

        [Tooltip("How many visits required before activation")]
        [SerializeField] private int requiredVisits = 1;

        [Header("Activation Behavior")]
        [Tooltip("Type of activation animation")]
        [SerializeField] private ActivationType activationType = ActivationType.FadeIn;

        [Tooltip("Duration of activation animation in seconds")]
        [SerializeField] private float activationDuration = 1.0f;

        [Tooltip("Should this object deactivate if player leaves?")]
        [SerializeField] private bool isPermanent = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugMessages = true;

        // State
        private bool isActive = false;
        private bool isActivating = false;
        private static bool isQuitting = false; // Track if application is quitting

        // Components
        private MeshRenderer[] meshRenderers;
        private Collider[] colliders;

        public bool IsActive => isActive;

        /// <summary>
        /// Defines how the object appears when activated.
        /// </summary>
        public enum ActivationType
        {
            Instant,      // Appears immediately
            FadeIn,       // Fades in gradually
            ScaleUp,      // Scales from small to normal
            FadeAndScale  // Both fade and scale
        }

        private void Awake()
        {
            // Cache components
            meshRenderers = GetComponentsInChildren<MeshRenderer>();
            colliders = GetComponentsInChildren<Collider>();

            // Start disabled
            SetObjectVisibility(false);
        }

        private void OnEnable()
        {
            // Subscribe to VisitManager events (Observer Pattern)
            // Note: VisitManager.Instance will auto-create if missing
            if (VisitManager.Instance != null)
            {
                VisitManager.Instance.OnZoneVisited += HandleZoneVisited;

                if (showDebugMessages)
                    Debug.Log($"{gameObject.name} subscribed to VisitManager");
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            // Check both instance and if application is quitting
            if (!isQuitting && VisitManager.Instance != null)
            {
                VisitManager.Instance.OnZoneVisited -= HandleZoneVisited;

                if (showDebugMessages)
                    Debug.Log($"{gameObject.name} unsubscribed from VisitManager");
            }
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        private void Start()
        {
            if (showDebugMessages)
            {
                Debug.Log($"{gameObject.name} - Target Zone: '{targetZoneID}', Required Visits: {requiredVisits}, Type: {activationType}");
            }

            // Check if already activated (in case game was saved/loaded)
            if (VisitManager.Instance != null)
            {
                int currentVisits = VisitManager.Instance.GetVisitCount(targetZoneID);

                if (showDebugMessages)
                    Debug.Log($"{gameObject.name} - Current visits for '{targetZoneID}': {currentVisits}");

                if (currentVisits >= requiredVisits)
                {
                    // Already should be active - activate instantly
                    ActivateInstant();
                }
            }
        }

        /// <summary>
        /// Called when any zone is visited (Observer Pattern callback).
        /// </summary>
        private void HandleZoneVisited(string zoneID, int visitCount)
        {
            // Only respond to our target zone
            if (zoneID != targetZoneID)
                return;

            // Already active or activating
            if (isActive || isActivating)
                return;

            // Check if threshold met
            if (visitCount >= requiredVisits)
            {
                if (showDebugMessages)
                {
                    Debug.Log($"{gameObject.name} activating! Zone '{zoneID}' reached {visitCount} visits (required: {requiredVisits})");
                }

                Activate();
            }
        }

        /// <summary>
        /// Activates the object with the chosen animation type.
        /// </summary>
        public void Activate()
        {
            if (isActive || isActivating)
                return;

            switch (activationType)
            {
                case ActivationType.Instant:
                    ActivateInstant();
                    break;
                case ActivationType.FadeIn:
                    StartCoroutine(FadeInCoroutine());
                    break;
                case ActivationType.ScaleUp:
                    StartCoroutine(ScaleUpCoroutine());
                    break;
                case ActivationType.FadeAndScale:
                    StartCoroutine(FadeAndScaleCoroutine());
                    break;
            }
        }

        /// <summary>
        /// Deactivates the object (if not permanent).
        /// </summary>
        public void Deactivate()
        {
            if (isPermanent)
            {
                if (showDebugMessages)
                    Debug.LogWarning($"{gameObject.name} is permanent and cannot be deactivated!");
                return;
            }

            isActive = false;
            SetObjectVisibility(false);

            if (showDebugMessages)
                Debug.Log($"{gameObject.name} deactivated");
        }

        /// <summary>
        /// Instant activation - no animation.
        /// </summary>
        private void ActivateInstant()
        {
            isActive = true;
            SetObjectVisibility(true);
            SetObjectAlpha(1f);

            if (showDebugMessages)
                Debug.Log($"{gameObject.name} activated instantly");
        }

        /// <summary>
        /// Fade in activation animation.
        /// </summary>
        private IEnumerator FadeInCoroutine()
        {
            isActivating = true;
            SetObjectVisibility(true);
            SetObjectAlpha(0f);

            float elapsed = 0f;

            while (elapsed < activationDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / activationDuration);
                SetObjectAlpha(alpha);
                yield return null;
            }

            SetObjectAlpha(1f);
            isActive = true;
            isActivating = false;

            if (showDebugMessages)
                Debug.Log($"{gameObject.name} fade-in complete");
        }

        /// <summary>
        /// Scale up activation animation.
        /// </summary>
        private IEnumerator ScaleUpCoroutine()
        {
            isActivating = true;
            SetObjectVisibility(true);

            Vector3 originalScale = transform.localScale;
            Vector3 startScale = originalScale * 0.1f;
            transform.localScale = startScale;

            float elapsed = 0f;

            while (elapsed < activationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / activationDuration);
                // Use smooth step for nice easing
                t = t * t * (3f - 2f * t);
                transform.localScale = Vector3.Lerp(startScale, originalScale, t);
                yield return null;
            }

            transform.localScale = originalScale;
            isActive = true;
            isActivating = false;

            if (showDebugMessages)
                Debug.Log($"{gameObject.name} scale-up complete");
        }

        /// <summary>
        /// Combined fade and scale activation.
        /// </summary>
        private IEnumerator FadeAndScaleCoroutine()
        {
            isActivating = true;
            SetObjectVisibility(true);
            SetObjectAlpha(0f);

            Vector3 originalScale = transform.localScale;
            Vector3 startScale = originalScale * 0.1f;
            transform.localScale = startScale;

            float elapsed = 0f;

            while (elapsed < activationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / activationDuration);
                // Smooth step easing
                float smoothT = t * t * (3f - 2f * t);

                SetObjectAlpha(t);
                transform.localScale = Vector3.Lerp(startScale, originalScale, smoothT);
                yield return null;
            }

            SetObjectAlpha(1f);
            transform.localScale = originalScale;
            isActive = true;
            isActivating = false;

            if (showDebugMessages)
                Debug.Log($"{gameObject.name} fade and scale complete");
        }

        /// <summary>
        /// Sets visibility of all mesh renderers and colliders.
        /// </summary>
        private void SetObjectVisibility(bool visible)
        {
            // Enable/disable renderers
            foreach (var renderer in meshRenderers)
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }

            // Enable/disable colliders
            foreach (var collider in colliders)
            {
                if (collider != null)
                    collider.enabled = visible;
            }
        }

        /// <summary>
        /// Sets alpha of all materials (for fade effect).
        /// Works with Standard shader and most transparent shaders.
        /// </summary>
        private void SetObjectAlpha(float alpha)
        {
            foreach (var renderer in meshRenderers)
            {
                if (renderer == null)
                    continue;

                foreach (var material in renderer.materials)
                {
                    if (material.HasProperty("_Color"))
                    {
                        Color color = material.color;
                        color.a = alpha;
                        material.color = color;

                        // Enable transparency rendering
                        if (alpha < 1f)
                        {
                            material.SetFloat("_Mode", 3); // Transparent mode
                            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            material.SetInt("_ZWrite", 0);
                            material.DisableKeyword("_ALPHATEST_ON");
                            material.EnableKeyword("_ALPHABLEND_ON");
                            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            material.renderQueue = 3000;
                        }
                        else
                        {
                            // Back to opaque
                            material.SetFloat("_Mode", 0);
                            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                            material.SetInt("_ZWrite", 1);
                            material.DisableKeyword("_ALPHATEST_ON");
                            material.DisableKeyword("_ALPHABLEND_ON");
                            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            material.renderQueue = -1;
                        }
                    }
                }
            }
        }

        // Editor helper - visualize in scene view
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isActive ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(transform.position, transform.localScale * 1.1f);
        }
    }
}