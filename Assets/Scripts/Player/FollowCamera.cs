// FollowCamera.cs
using UnityEngine;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// Simple third-person camera that smoothly follows a target.
    /// Can be used for both driving and on-foot modes.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("The transform to follow (vehicle or player)")]
        [SerializeField] private Transform target;

        [Header("Camera Position")]
        [Tooltip("Offset behind the target")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -7f);

        [Header("Camera Behavior")]
        [Tooltip("How quickly the camera follows the target")]
        [SerializeField] private float followSpeed = 10f;

        [Tooltip("How quickly the camera rotates to match target")]
        [SerializeField] private float rotationSpeed = 5f;

        [Tooltip("Should camera look at target?")]
        [SerializeField] private bool lookAtTarget = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        private void LateUpdate()
        {
            if (target == null)
            {
                Debug.LogWarning("FollowCamera has no target assigned!");
                return;
            }

            FollowTarget();
        }

        private void FollowTarget()
        {
            // Calculate desired position based on target's position and rotation
            Vector3 desiredPosition = target.position + target.rotation * offset;

            // Smoothly move camera to desired position
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                followSpeed * Time.deltaTime
            );

            // Handle camera rotation
            if (lookAtTarget)
            {
                // Look at the target
                Vector3 lookDirection = target.position - transform.position;
                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
            else
            {
                // Match target's rotation
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    target.rotation,
                    rotationSpeed * Time.deltaTime
                );
            }

            if (showDebugInfo)
            {
                Debug.DrawLine(transform.position, target.position, Color.cyan);
            }
        }

        /// <summary>
        /// Changes the camera target at runtime.
        /// Useful when switching between vehicle and on-foot modes.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Updates camera offset (useful for different camera modes).
        /// </summary>
        public void SetOffset(Vector3 newOffset)
        {
            offset = newOffset;
        }
    }
}