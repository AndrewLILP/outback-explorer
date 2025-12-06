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
    /// FIXED VERSION - Car stays visible and frozen, not disabled
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
            Debug.Log("[OnFootState] ========== ENTERING ON FOOT STATE ==========");

            // Get or add CharacterController
            characterController = stateManager.PlayerCharacter.GetComponent<CharacterController>();
            if (characterController == null)
            {
                Debug.Log("[OnFootState] CharacterController not found - adding one");
                characterController = stateManager.PlayerCharacter.AddComponent<CharacterController>();
                characterController.height = 2f;
                characterController.radius = 0.5f;
                characterController.center = new Vector3(0f, 1f, 0f);
            }
            else
            {
                Debug.Log($"[OnFootState] CharacterController found - Height: {characterController.height}, Radius: {characterController.radius}");
            }

            // Get interaction detector
            interactionDetector = stateManager.PlayerCharacter.GetComponent<PlayerInteractionDetector>();

            // Get interaction prompt UI
            interactionPrompt = Object.FindFirstObjectByType<UI.InteractionPromptUI>();
            if (interactionPrompt == null)
            {
                Debug.LogWarning("[OnFootState] InteractionPromptUI not found in scene!");
            }

            // Position player next to car (right side for Australian driving)
            Vector3 exitPosition = stateManager.CarGameObject.transform.position +
                                  stateManager.CarGameObject.transform.right * stateManager.ExitCarOffset.x;
            stateManager.PlayerCharacter.transform.position = exitPosition;
            Debug.Log($"[OnFootState] Player positioned at: {exitPosition}");

            // Enable player character
            stateManager.PlayerCharacter.SetActive(true);
            characterController.enabled = true;
            Debug.Log($"[OnFootState] Player active: {stateManager.PlayerCharacter.activeSelf}, CharController enabled: {characterController.enabled}");

            // KEEP CAR VISIBLE - Just freeze it in place
            // 1. Freeze car physics (make Rigidbody kinematic)
            Rigidbody carRigidbody = stateManager.CarGameObject.GetComponent<Rigidbody>();
            if (carRigidbody != null)
            {
                carRigidbody.isKinematic = true;
                carRigidbody.linearVelocity = Vector3.zero;
                carRigidbody.angularVelocity = Vector3.zero;
                Debug.Log("[OnFootState] Car Rigidbody set to kinematic - physics frozen");
            }

            // 2. Disable car cameras (children of car GameObject)
            Camera[] carCameras = stateManager.CarGameObject.GetComponentsInChildren<Camera>();
            foreach (Camera cam in carCameras)
            {
                cam.enabled = false;
                Debug.Log($"[OnFootState] Disabled car camera: {cam.gameObject.name}");
            }

            // Car GameObject stays ACTIVE and VISIBLE
            Debug.Log($"[OnFootState] Car remains visible at position: {stateManager.CarGameObject.transform.position}");

            // Update camera to follow player
            if (stateManager.FollowCamera != null)
            {
                stateManager.FollowCamera.SetTarget(stateManager.PlayerCharacter.transform);
                stateManager.FollowCamera.SetOffset(stateManager.WalkingCameraOffset);
                Debug.Log($"[OnFootState] Camera target set to PlayerWalking, offset: {stateManager.WalkingCameraOffset}");
            }
            else
            {
                Debug.LogWarning("[OnFootState] FollowCamera is NULL!");
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

            // Get camera reference
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[OnFootState] No main camera found! Make sure camera has 'MainCamera' tag!");
                return;
            }

            // Calculate movement direction RELATIVE TO CAMERA
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;

            // Flatten camera vectors to horizontal plane (ignore Y)
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            // Build movement direction
            Vector3 moveDirection = (cameraForward * vertical + cameraRight * horizontal).normalized;

            if (moveDirection.magnitude >= 0.1f)
            {
                // Rotate player to face movement direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                stateManager.PlayerCharacter.transform.rotation =
                    Quaternion.Slerp(stateManager.PlayerCharacter.transform.rotation,
                                    targetRotation,
                                    turnSpeed * Time.deltaTime);

                // Move player
                Vector3 movement = moveDirection * moveSpeed * Time.deltaTime;
                characterController.Move(movement);
            }

            // Apply gravity
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);

            // Reset vertical velocity if grounded
            if (characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
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
                Debug.Log($"[OnFootState] Near car - distance: {distanceToCar}");
            }
            else if (!isNearCar && wasNearCar)
            {
                HideCarInteractionPrompt();
                Debug.Log("[OnFootState] Left car area");
            }
        }

        private void HandleCarInteraction()
        {
            // Press E to enter car
            if (isNearCar && Input.GetKeyDown(KeyCode.E))
            {
                Debug.Log("[OnFootState] E pressed near car - entering car");
                HideCarInteractionPrompt();
                stateManager.SwitchToDriving();
            }
        }

        private void ShowCarInteractionPrompt()
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.ShowPrompt("Press E to Enter Car");
                Debug.Log("[OnFootState] Showing car interaction prompt");
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
            Debug.Log("[OnFootState] ========== EXITING ON FOOT STATE ==========");
            HideCarInteractionPrompt();
            characterController.enabled = false;
            stateManager.PlayerCharacter.SetActive(false);
            Debug.Log("[OnFootState] Player entered car - switching to driving mode");
        }
    }
}