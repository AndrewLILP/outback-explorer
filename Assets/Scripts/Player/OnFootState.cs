// OnFootState.cs
// Location: Assets/Scripts/Player/OnFootState.cs
//
// UPDATED (Nov 2026, river floats): changes marked "NEW (river)".
//   - Can arrive somewhere other than next to the car (SetArrivalPoint), used
//     when stepping off a river craft at the landing.
//   - Owns BOTH on-foot interactions: entering the car and hopping on a river
//     craft. One owner for the Interact button and the prompt means they can
//     never fight. If both are in reach, the board wins (it has the smaller
//     range, so being that close is a clear signal of intent).
using UnityEngine;
using RelaxingDrive.World;
using StarterAssets;

namespace RelaxingDrive.Player
{
    /// <summary>
    /// State when player is walking around (not in car).
    /// Handles character controller movement and interaction detection.
    /// Shows an input-aware "[E] / (A) Enter Car" or "Hop on board" prompt.
    ///
    /// Supports two walking-controller paths:
    /// - Starter Assets ThirdPersonController (RCC/Steam scene) - that component
    ///   owns its own movement, input, animation, and Cinemachine camera target.
    ///   This state only handles interactions and the camera handoff.
    /// - Legacy hand-rolled CharacterController movement (PolyStang/Itch scene) -
    ///   unchanged, drives movement itself and uses FollowCamera.
    ///
    /// Interactions use the Interact action (E / gamepad A) via PlayerInputReader.
    /// </summary>
    public class OnFootState : PlayerState
    {
        // NEW (river): what the Interact button would do right now.
        private enum PromptTarget { None, Car, Board }

        // NEW (river): camera tilt (degrees down) when arriving somewhere, e.g. off the SUP.
        // Matches FloatingState's default view so the hand-off feels continuous.
        private const float ArrivalCameraPitch = 12f;

        private CharacterController characterController;
        private PlayerInteractionDetector interactionDetector;
        private UI.InteractionPromptUI interactionPrompt;
        private bool usesExternalController;

        // Movement settings (legacy path only)
        private float moveSpeed = 5f;
        private float turnSpeed = 10f;
        private float gravity = -9.81f;
        private Vector3 velocity;

        // Car interaction
        private float carInteractionRange = 5f; // was 3f - retuned for largest RCC vehicle (Truck/Pickup)
                                                  // footprint; slightly generous for smaller cars (Coupe, F1)
                                                  // but avoids the prompt feeling unresponsive near big vehicles

        // NEW (river): current interaction target and the board in reach (if any)
        private PromptTarget currentTarget = PromptTarget.None;
        private RiverLaunchPoint nearbyLaunch;

        // NEW (river): one-off arrival point instead of "next to the car"
        private bool hasArrivalPoint;
        private Vector3 arrivalPosition;
        private Quaternion arrivalRotation;

        public OnFootState(PlayerStateManager manager) : base(manager) { }

        /// <summary>
        /// NEW (river): the next Enter() places the player here instead of beside the car.
        /// Used once, then forgotten.
        /// </summary>
        public void SetArrivalPoint(Vector3 position, Quaternion rotation)
        {
            hasArrivalPoint = true;
            arrivalPosition = position;
            arrivalRotation = rotation;
        }

        public override void Enter()
        {
            Debug.Log("[OnFootState] ========== ENTERING ON FOOT STATE ==========");

            // This state object is REUSED every time, so reset per-visit fields.
            // Otherwise the old target is still set, nothing looks "changed",
            // and the prompt never shows.
            currentTarget = PromptTarget.None;
            nearbyLaunch = null;

            // Detect which walking controller this scene's playerCharacter uses.
            // Starter Assets' ThirdPersonController owns its own CharacterController,
            // input, and animation - we just activate it. The legacy path (Itch/PolyStang)
            // has none of that and needs the manual setup below.
            usesExternalController = stateManager.PlayerCharacter.GetComponent<ThirdPersonController>() != null;

            characterController = stateManager.PlayerCharacter.GetComponent<CharacterController>();
            if (characterController == null)
            {
                if (usesExternalController)
                {
                    Debug.LogError("[OnFootState] ThirdPersonController found but no CharacterController on the same GameObject - check the PlayerArmature prefab setup!");
                }
                else
                {
                    Debug.Log("[OnFootState] CharacterController not found - adding one");
                    characterController = stateManager.PlayerCharacter.AddComponent<CharacterController>();
                    characterController.height = 2f;
                    characterController.radius = 0.5f;
                    characterController.center = new Vector3(0f, 1f, 0f);
                }
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

            // NEW (river): arriving from a river craft -> the landing spot.
            // Otherwise (getting out of the car) -> next to the car, right side
            // for Australian driving. The CharacterController is still OFF here,
            // which is what makes setting the position directly safe.
            if (hasArrivalPoint)
            {
                stateManager.PlayerCharacter.transform.SetPositionAndRotation(arrivalPosition, arrivalRotation);
                hasArrivalPoint = false;

                // NEW (river): the walking controller remembers the camera angle from
                // BEFORE the float, so it would swing round to that old angle (often in
                // front of the player). Point it the way the player now faces instead.
                if (usesExternalController)
                {
                    stateManager.PlayerCharacter.GetComponent<ThirdPersonController>()
                        .SetCameraAngles(arrivalRotation.eulerAngles.y, ArrivalCameraPitch);
                }
                Debug.Log($"[OnFootState] Player arrived at: {arrivalPosition}");
            }
            else
            {
                Vector3 exitPosition = stateManager.CarGameObject.transform.position +
                                      stateManager.CarGameObject.transform.right * stateManager.ExitCarOffset.x;
                stateManager.PlayerCharacter.transform.position = exitPosition;
                Debug.Log($"[OnFootState] Player positioned at: {exitPosition}");
            }

            // Enable player character
            stateManager.PlayerCharacter.SetActive(true);
            characterController.enabled = true;
            Debug.Log($"[OnFootState] Player active: {stateManager.PlayerCharacter.activeSelf}, CharController enabled: {characterController.enabled}");

            // KEEP CAR VISIBLE - Just freeze it in place (harmless if already frozen)
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

            // Camera handoff: each side now owns its own camera in the RCC scene
            // (RCC's rig while driving, Starter Assets' Cinemachine rig while on
            // foot) - turn the vehicle's camera off here (no-op for PolyStang,
            // which has none) and turn the walking camera on if this controller
            // has one. Falls back to FollowCamera for the legacy path.
            stateManager.VehicleController?.SetOwnCameraActive(false);

            if (usesExternalController && stateManager.WalkingCameraObject != null)
            {
                stateManager.WalkingCameraObject.SetActive(true);
                Debug.Log("[OnFootState] Activated walking controller's own camera (Cinemachine)");
            }
            else if (stateManager.FollowCamera != null)
            {
                stateManager.FollowCamera.SetTarget(stateManager.PlayerCharacter.transform);
                stateManager.FollowCamera.SetOffset(stateManager.WalkingCameraOffset);
                Debug.Log($"[OnFootState] Camera target set to PlayerWalking, offset: {stateManager.WalkingCameraOffset}");
            }
            else
            {
                Debug.LogWarning("[OnFootState] No walking camera available - neither WalkingCameraObject nor FollowCamera is assigned!");
            }

            Debug.Log("[OnFootState] Walking mode active");
        }

        public override void Update()
        {
            // Starter Assets' ThirdPersonController drives its own movement, gravity,
            // jumping, and camera-target rotation in its own Update()/LateUpdate() -
            // nothing for this state to do there. The legacy path still needs it.
            if (!usesExternalController)
            {
                HandleMovement();
            }

            UpdateInteractionTarget();
            HandleInteraction();
        }

        /// <summary>
        /// LEGACY PATH ONLY (itch.io / PolyStang scene). Never runs in the Steam scene,
        /// where Starter Assets handles walking. Still uses legacy Input.GetAxis -
        /// to be removed with the legacy path in Story #6 clean-up.
        /// </summary>
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

        /// <summary>
        /// NEW (river): replaces CheckCarProximity. Works out what Interact would do
        /// right now (board, car or nothing) and only touches the prompt when that changes.
        /// </summary>
        private void UpdateInteractionTarget()
        {
            Vector3 playerPosition = stateManager.PlayerCharacter.transform.position;

            nearbyLaunch = RiverLaunchPoint.FindInRange(playerPosition);

            float distanceToCar = Vector3.Distance(playerPosition, stateManager.CarGameObject.transform.position);

            PromptTarget target =
                nearbyLaunch != null ? PromptTarget.Board :
                distanceToCar <= carInteractionRange ? PromptTarget.Car :
                PromptTarget.None;

            if (target == currentTarget) return;
            currentTarget = target;

            switch (target)
            {
                case PromptTarget.Board: ShowPrompt(nearbyLaunch.PromptText); break;
                case PromptTarget.Car:   ShowPrompt("Enter Car"); break;
                default:                 HidePrompt(); break;
            }

            Debug.Log($"[OnFootState] Interaction target → {target}");
        }

        /// <summary>
        /// Interact (E or gamepad A). PlayerInputReader merges keyboard and gamepad
        /// into one action, so this state doesn't care which device was used.
        /// </summary>
        private void HandleInteraction()
        {
            if (!PlayerInputReader.InteractPressedThisFrame) return;

            switch (currentTarget)
            {
                case PromptTarget.Board:
                    Debug.Log("[OnFootState] Interact pressed near board - hopping on");
                    HidePrompt();
                    stateManager.SwitchToFloating(nearbyLaunch.Craft);
                    break;

                case PromptTarget.Car:
                    Debug.Log("[OnFootState] Interact pressed near car - entering car");
                    HidePrompt();
                    stateManager.SwitchToDriving();
                    break;
            }
        }

        private void ShowPrompt(string actionText)
        {
            // Action text only - InteractionPromptUI adds the E / A keycap itself
            if (interactionPrompt != null) interactionPrompt.ShowPrompt(actionText);
        }

        private void HidePrompt()
        {
            if (interactionPrompt != null) interactionPrompt.HidePrompt();
        }

        public override void Exit()
        {
            Debug.Log("[OnFootState] ========== EXITING ON FOOT STATE ==========");
            HidePrompt();
            characterController.enabled = false;
            stateManager.PlayerCharacter.SetActive(false);

            if (usesExternalController && stateManager.WalkingCameraObject != null)
            {
                stateManager.WalkingCameraObject.SetActive(false);
            }

            // FloatingState turns the character and walking camera straight back on;
            // DrivingState leaves them off.
        }
    }
}