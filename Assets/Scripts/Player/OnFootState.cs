// OnFootState.cs - UPDATED VERSION
using UnityEngine;
using RelaxingDrive.World;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// Player state when walking on foot.
    /// Handles WASD movement using CharacterController.
    /// Handles interaction with objects (E key).
    /// Uses PlayerInteractionDetector to find nearby interactables.
    /// </summary>
    public class OnFootState : PlayerState
    {
        private GameObject playerCharacter;
        private CharacterController characterController;
        private FollowCamera camera;
        private GameObject carGameObject;

        // NEW: Reference to interaction detector
        private PlayerInteractionDetector interactionDetector;

        // Movement settings
        private float moveSpeed = 5f;
        private float turnSpeed = 10f;
        private float gravity = -9.81f;
        private Vector3 verticalVelocity;

        // Interaction
        private float interactionRange = 3f;

        public OnFootState(PlayerStateManager manager) : base(manager)
        {
            playerCharacter = manager.PlayerCharacter;
            camera = manager.FollowCamera;
            carGameObject = manager.CarGameObject;

            // Get or add CharacterController
            if (playerCharacter != null)
            {
                characterController = playerCharacter.GetComponent<CharacterController>();
                if (characterController == null)
                {
                    characterController = playerCharacter.AddComponent<CharacterController>();
                    // Setup default character controller values
                    characterController.height = 2f;
                    characterController.radius = 0.5f;
                    characterController.center = new Vector3(0, 1f, 0);
                }

                // Get PlayerInteractionDetector component
                interactionDetector = playerCharacter.GetComponent<PlayerInteractionDetector>();
                if (interactionDetector == null)
                {
                    Debug.LogWarning("OnFootState: PlayerInteractionDetector not found on player! Adding one...");
                    interactionDetector = playerCharacter.AddComponent<PlayerInteractionDetector>();
                }
            }
        }

        public override void Enter()
        {
            Debug.Log("Entered OnFoot State");

            // Enable player character
            if (playerCharacter != null)
            {
                playerCharacter.SetActive(true);
            }

            // Setup camera for walking
            if (camera != null)
            {
                camera.SetTarget(playerCharacter.transform);
                camera.SetOffset(stateManager.WalkingCameraOffset);
            }

            // Reset vertical velocity
            verticalVelocity = Vector3.zero;
        }

        public override void Update()
        {
            HandleMovement();
            HandleInteractionInput();
        }

        public override void Exit()
        {
            Debug.Log("Exited OnFoot State");
        }

        /// <summary>
        /// Handles WASD movement
        /// </summary>
        private void HandleMovement()
        {
            if (characterController == null)
                return;

            // Get input
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            // Create movement direction
            Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

            if (moveDirection.magnitude > 0.1f)
            {
                // Calculate movement relative to camera (optional - currently world-relative)
                Vector3 move = moveDirection * moveSpeed * Time.deltaTime;

                // Rotate player to face movement direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                playerCharacter.transform.rotation = Quaternion.Slerp(
                    playerCharacter.transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime
                );

                // Apply horizontal movement
                characterController.Move(move);
            }

            // Apply gravity
            if (characterController.isGrounded)
            {
                verticalVelocity.y = -2f; // Small downward force to stay grounded
            }
            else
            {
                verticalVelocity.y += gravity * Time.deltaTime;
            }

            characterController.Move(verticalVelocity * Time.deltaTime);
        }

        /// <summary>
        /// Handles E key for interaction
        /// </summary>
        private void HandleInteractionInput()
        {
            if (!Input.GetKeyDown(KeyCode.E))
                return;

            // Priority 1: Check if near car
            float distanceToCar = Vector3.Distance(
                playerCharacter.transform.position,
                carGameObject.transform.position
            );

            if (distanceToCar <= interactionRange)
            {
                EnterCar();
                return;
            }

            // Priority 2: Interact with closest interactable object
            if (interactionDetector != null && interactionDetector.HasInteractable)
            {
                interactionDetector.InteractWithClosest();
                return;
            }

            // No interactable nearby
            Debug.Log("Nothing to interact with nearby");
        }

        /// <summary>
        /// Handles entering the car
        /// </summary>
        private void EnterCar()
        {
            Debug.Log("Entering car...");
            stateManager.TransitionToDriving();
        }
    }
}