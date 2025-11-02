// OnFootState.cs
using UnityEngine;
using RelaxingDrive.World;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// State when player is walking around (not in car).
    /// Handles character controller movement and interaction detection.
    /// Shows "Press E to Enter Car" prompt when near car.
    /// 
    /// POLISHED VERSION - Proper UI prompt integration, reduced debug spam
    /// </summary>
    public class OnFootState : PlayerState
    {
        private CharacterController characterController;
        private PlayerInteractionDetector interactionDetector;
        private UI.InteractionPromptUI interactionPrompt;

        // Movement settings
        private float moveSpeed = 5f;
        private float turnSpeed = 10f;
        private float gravity = -9.81f;
        private Vector3 velocity;

        // Car interaction
        private float carInteractionRange = 3f;
        private bool isNearCar = false;

        public OnFootState(PlayerStateManager manager) : base(manager) { }

        public override void Enter()
        {
            // Get or add CharacterController
            characterController = stateManager.PlayerCharacter.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = stateManager.PlayerCharacter.AddComponent<CharacterController>();
                characterController.height = 2f;
                characterController.radius = 0.5f;
            }

            // Get interaction detector
            interactionDetector = stateManager.PlayerCharacter.GetComponent<PlayerInteractionDetector>();

            // Get interaction prompt UI
            interactionPrompt = Object.FindFirstObjectByType<UI.InteractionPromptUI>();

            // Position player next to car
            Vector3 exitPosition = stateManager.CarGameObject.transform.position +
                                  stateManager.CarGameObject.transform.right * stateManager.ExitCarOffset.x;
            stateManager.PlayerCharacter.transform.position = exitPosition;

            // Enable player character
            stateManager.PlayerCharacter.SetActive(true);
            characterController.enabled = true;

            // Disable car
            stateManager.CarGameObject.SetActive(false);

            // Update camera
            if (stateManager.FollowCamera != null)
            {
                stateManager.FollowCamera.SetTarget(stateManager.PlayerCharacter.transform);
                stateManager.FollowCamera.SetOffset(stateManager.WalkingCameraOffset);
            }

            Debug.Log("[OnFootState] Player exited car - walking mode active");
        }

        public override void Update()
        {
            HandleMovement();
            CheckCarProximity();
            HandleCarInteraction();
        }

        private void HandleMovement()
        {
            // Get input
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            // Movement direction (relative to camera)
            Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

            if (moveDirection.magnitude >= 0.1f)
            {
                // Rotate player to face movement direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                stateManager.PlayerCharacter.transform.rotation =
                    Quaternion.Slerp(stateManager.PlayerCharacter.transform.rotation,
                                    targetRotation,
                                    turnSpeed * Time.deltaTime);

                // Move player
                characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
            }

            // Apply gravity
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            // Reset vertical velocity if grounded
            if (characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // Small negative value to keep grounded
            }
        }

        private void CheckCarProximity()
        {
            float distanceToCar = Vector3.Distance(
                stateManager.PlayerCharacter.transform.position,
                stateManager.CarGameObject.transform.position
            );

            bool wasNearCar = isNearCar;
            isNearCar = distanceToCar <= carInteractionRange;

            // Show/hide interaction prompt
            if (isNearCar && !wasNearCar)
            {
                ShowCarInteractionPrompt();
            }
            else if (!isNearCar && wasNearCar)
            {
                HideCarInteractionPrompt();
            }
        }

        private void HandleCarInteraction()
        {
            // Press E to enter car
            if (isNearCar && Input.GetKeyDown(KeyCode.E))
            {
                HideCarInteractionPrompt();
                stateManager.SwitchToDriving();
            }
        }

        private void ShowCarInteractionPrompt()
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.ShowPrompt("Press E to Enter Car");
            }
        }

        private void HideCarInteractionPrompt()
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.HidePrompt();
            }
        }

        public override void Exit()
        {
            // Hide prompt when exiting state
            HideCarInteractionPrompt();

            // Disable player character
            characterController.enabled = false;
            stateManager.PlayerCharacter.SetActive(false);

            Debug.Log("[OnFootState] Player entered car - switching to driving mode");
        }
    }
}