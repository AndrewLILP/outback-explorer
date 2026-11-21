// BallooningState.cs
// Location: Assets/Scripts/Player/BallooningState.cs
//
// Player state while riding the hot air balloon. Sits alongside DrivingState,
// OnFootState and FloatingState in the State pattern.
//
// Enter: pause walking, hand the character to the balloon (it places the rider
//        every frame), reuse the walking camera with free look, launch.
// Fly:   camera free look via RideCameraLook. Height is read by BalloonMover's
//        temporary debug input for now.
// End:   the balloon raises OnFlightFinished -> back to OnFootState at the step-off spot.

using UnityEngine;
using RelaxingDrive.World;
using StarterAssets;

namespace RelaxingDrive.Player
{
    public class BallooningState : PlayerState
    {
        // Looks a little further down than the SUP, and allows looking well down at animals.
        private readonly RideCameraLook cameraLook =
            new RideCameraLook(defaultPitch: 15f, minPitch: -30f, maxPitch: 75f);

        private BalloonMover balloon;
        private CharacterController characterController;
        private ThirdPersonController walkController;
        private Animator animator;

        public BallooningState(PlayerStateManager manager) : base(manager) { }

        /// <summary>PlayerStateManager calls this just before ChangeState.</summary>
        public void SetBalloon(BalloonMover newBalloon) => balloon = newBalloon;

        public override void Enter()
        {
            Debug.Log("[BallooningState] ========== ENTERING BALLOONING STATE ==========");

            Transform player = stateManager.PlayerCharacter.transform;

            // OnFootState.Exit hid the character - bring it back.
            stateManager.PlayerCharacter.SetActive(true);

            // 1. Pause walking. CharacterController must be OFF while the balloon moves the character.
            characterController = player.GetComponent<CharacterController>();
            if (characterController != null) characterController.enabled = false;

            walkController = player.GetComponent<ThirdPersonController>();
            if (walkController != null) walkController.enabled = false;

            // 2. Standing idle.
            animator = player.GetComponent<Animator>();
            SetAnimatorFloat("Speed", 0f);
            SetAnimatorBool("Grounded", true);
            SetAnimatorBool("Jump", false);
            SetAnimatorBool("FreeFall", false);

            // 3. Into the basket.
            balloon.SetRider(player);

            // 4. Camera: walking (Cinemachine) camera with free look.
            Transform cameraTarget = walkController != null && walkController.CinemachineCameraTarget != null
                ? walkController.CinemachineCameraTarget.transform
                : null;
            cameraLook.Begin(cameraTarget, player.GetComponent<StarterAssetsInputs>());

            if (stateManager.WalkingCameraObject != null)
                stateManager.WalkingCameraObject.SetActive(true);

            // 5. Up, up and away.
            balloon.OnFlightFinished += HandleFlightFinished;
            balloon.Launch();

            Debug.Log($"[BallooningState] Flying route '{balloon.Route.name}'");
        }

        public override void Update()
        {
            cameraLook.Tick(Time.deltaTime);
        }

        private void HandleFlightFinished()
        {
            balloon.GetStepOffPose(out Vector3 position, out Quaternion rotation);
            stateManager.SwitchToOnFootAt(position, rotation);
        }

        public override void Exit()
        {
            Debug.Log("[BallooningState] ========== EXITING BALLOONING STATE ==========");

            if (balloon != null)
            {
                balloon.OnFlightFinished -= HandleFlightFinished;
                balloon.ClearRider();
            }

            // OnFootState.Enter places the player and re-enables the CharacterController.
            if (walkController != null) walkController.enabled = true;

            balloon = null;
        }

        // --- Animator helpers: only set parameters that exist. ---

        private void SetAnimatorFloat(string parameter, float value)
        {
            if (HasParameter(parameter, AnimatorControllerParameterType.Float))
                animator.SetFloat(parameter, value);
        }

        private void SetAnimatorBool(string parameter, bool value)
        {
            if (HasParameter(parameter, AnimatorControllerParameterType.Bool))
                animator.SetBool(parameter, value);
        }

        private bool HasParameter(string parameter, AnimatorControllerParameterType type)
        {
            if (animator == null) return false;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.name == parameter && p.type == type) return true;
            return false;
        }
    }
}