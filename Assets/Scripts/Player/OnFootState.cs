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
    /// ULTRA-DEBUG VERSION - Logs every single frame to diagnose input issue
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

        // Debug
        private int frameCount = 0;
        private float logInterval = 0.5f; // Log every 0.5 seconds
        private float lastLogTime = 0f;

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

            // Position player next to car
            Vector3 exitPosition = stateManager.CarGameObject.transform.position +
                                  stateManager.CarGameObject.transform.right * stateManager.ExitCarOffset.x;
            stateManager.PlayerCharacter.transform.position = exitPosition;
            Debug.Log($"[OnFootState] Player positioned at: {exitPosition}");

            // Enable player character
            stateManager.PlayerCharacter.SetActive(true);
            characterController.enabled = true;
            Debug.Log($"[OnFootState] Player active: {stateManager.PlayerCharacter.activeSelf}, CharController enabled: {characterController.enabled}");

            // Disable car
            stateManager.CarGameObject.SetActive(false);
            Debug.Log($"[OnFootState] Car disabled: {!stateManager.CarGameObject.activeSelf}");

            // Update camera
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
            Debug.Log("[OnFootState] ⚡ UPDATE LOOP STARTING - Watch for input logs!");
        }

        public override void Update()
        {
            frameCount++;
            
            // Log periodically to show Update is being called
            if (Time.time - lastLogTime > logInterval)
            {
                Debug.Log($"[OnFootState] Update() called {frameCount} times. Still in OnFoot state.");
                lastLogTime = Time.time;
                frameCount = 0;
            }

            HandleMovement();
            CheckCarProximity();
            HandleCarInteraction();
        }

        private void HandleMovement()
        {
            // Get input - LOG ALWAYS, even if zero
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            // ALWAYS log input values (even zeros) for first 5 seconds
            if (Time.time < 5f || horizontal != 0 || vertical != 0)
            {
                Debug.Log($"[OnFootState] RAW INPUT → H: {horizontal:F3}, V: {vertical:F3}");
            }

            // Also check raw key states
            bool wPressed = Input.GetKey(KeyCode.W);
            bool aPressed = Input.GetKey(KeyCode.A);
            bool sPressed = Input.GetKey(KeyCode.S);
            bool dPressed = Input.GetKey(KeyCode.D);

            if (wPressed || aPressed || sPressed || dPressed)
            {
                Debug.Log($"[OnFootState] KEYS → W:{wPressed} A:{aPressed} S:{sPressed} D:{dPressed}");
            }

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
                Debug.Log($"[OnFootState] ✅ MOVING! Direction: {moveDirection}, Magnitude: {moveDirection.magnitude:F3}");

                // Rotate player to face movement direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                stateManager.PlayerCharacter.transform.rotation =
                    Quaternion.Slerp(stateManager.PlayerCharacter.transform.rotation,
                                    targetRotation,
                                    turnSpeed * Time.deltaTime);

                // Move player
                Vector3 movement = moveDirection * moveSpeed * Time.deltaTime;
                characterController.Move(movement);

                Debug.Log($"[OnFootState] CharacterController.Move({movement}) called");
                Debug.Log($"[OnFootState] Player position: {stateManager.PlayerCharacter.transform.position}");
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