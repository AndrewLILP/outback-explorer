// FloatingState.cs
// Location: Assets/Scripts/Player/FloatingState.cs
//
// Player state while riding a river craft (SUP board now, tube/canoe later).
// Sits alongside DrivingState and OnFootState in the State pattern.
//
// Enter: the walking controller is paused, the craft is told to carry the
//        character (RiverCraftMover.SetRider), the walking camera follows,
//        and the board is launched.
// Float: free look - right stick / mouse turns the camera around the player.
//        After a few seconds without looking, it drifts back to facing downstream.
// End:   the board raises OnReachedEnd -> we hand the player back to
//        OnFootState at the river's landing spot.
//
// UPDATED (Nov 2026): no longer parents the character to the board (that
// passed the board's scale/tilt on to the model and stretched it); the craft
// now places the rider each frame instead. Added free look.

using UnityEngine;
using RelaxingDrive.World;
using StarterAssets;

namespace RelaxingDrive.Player
{
    public class FloatingState : PlayerState
    {
        // --- Free look tuning ---
        private const float DefaultPitch = 12f;    // slight downward tilt, degrees
        private const float MinPitch = -30f;       // looking up
        private const float MaxPitch = 60f;        // looking down
        private const float RecentreDelay = 4f;    // seconds of no look input before easing back
        private const float RecentreSpeed = 0.6f;  // how lazily it eases back (higher = faster)
        private const float LookDeadzone = 0.01f;

        private RiverCraftMover craft;
        private Transform player;
        private CharacterController characterController;
        private ThirdPersonController walkController;
        private StarterAssetsInputs starterInputs;
        private Transform cameraTarget;
        private Animator animator;

        // Camera angles RELATIVE to the board's heading, so "straight ahead"
        // follows the river round bends without the player doing anything.
        private float lookYaw;
        private float lookPitch;
        private float timeSinceLook;

        public FloatingState(PlayerStateManager manager) : base(manager) { }

        /// <summary>PlayerStateManager calls this just before ChangeState, to say which craft to ride.</summary>
        public void SetCraft(RiverCraftMover newCraft) => craft = newCraft;

        public override void Enter()
        {
            Debug.Log("[FloatingState] ========== ENTERING FLOATING STATE ==========");

            player = stateManager.PlayerCharacter.transform;

            // OnFootState.Exit hid the character - bring it back.
            stateManager.PlayerCharacter.SetActive(true);

            // 1. Pause walking. The CharacterController must be OFF while the
            //    craft moves the character by hand, or it fights back.
            characterController = player.GetComponent<CharacterController>();
            if (characterController != null) characterController.enabled = false;

            walkController = player.GetComponent<ThirdPersonController>();
            if (walkController != null) walkController.enabled = false;

            // StarterAssetsInputs keeps receiving look input even with the walk
            // controller off, so we can read it for our own free look.
            starterInputs = player.GetComponent<StarterAssetsInputs>();
            cameraTarget = walkController != null && walkController.CinemachineCameraTarget != null
                ? walkController.CinemachineCameraTarget.transform
                : null;

            // 2. Freeze the animation in a standing idle.
            animator = player.GetComponent<Animator>();
            SetAnimatorFloat("Speed", 0f);
            SetAnimatorBool("Grounded", true);
            SetAnimatorBool("Jump", false);
            SetAnimatorBool("FreeFall", false);

            // 3. Hand the character to the craft - it places the rider every frame.
            craft.SetRider(player);

            // 4. Camera: reuse the walking (Cinemachine) camera, facing downstream.
            lookYaw = 0f;
            lookPitch = DefaultPitch;
            timeSinceLook = 0f;
            ApplyLook();

            if (stateManager.WalkingCameraObject != null)
                stateManager.WalkingCameraObject.SetActive(true);

            // 5. Off we go.
            craft.OnReachedEnd += HandleReachedEnd;
            craft.Launch();

            Debug.Log($"[FloatingState] Floating down '{craft.River.name}'");
        }

        public override void Update()
        {
            UpdateFreeLook();
            // Steering: still read by the craft's temporary debug input for now.
        }

        /// <summary>
        /// Same maths as Starter Assets' own camera, so it feels identical to walking:
        /// mouse movement is already "per frame", stick input is "per second"
        /// and needs multiplying by deltaTime.
        /// </summary>
        private void UpdateFreeLook()
        {
            if (cameraTarget == null) return;

            Vector2 look = starterInputs != null ? starterInputs.look : Vector2.zero;

            if (look.sqrMagnitude > LookDeadzone)
            {
                bool usingGamepad = PlayerInputReader.Instance != null &&
                                    PlayerInputReader.Instance.CurrentDevice == InputDeviceType.Gamepad;
                float multiplier = usingGamepad ? Time.deltaTime : 1f;

                lookYaw += look.x * multiplier;
                lookPitch += look.y * multiplier;
                timeSinceLook = 0f;
            }
            else
            {
                // Hands off for a while -> ease back to the default river view.
                timeSinceLook += Time.deltaTime;
                if (timeSinceLook >= RecentreDelay)
                {
                    float t = 1f - Mathf.Exp(-RecentreSpeed * Time.deltaTime);
                    lookYaw = Mathf.LerpAngle(lookYaw, 0f, t);
                    lookPitch = Mathf.Lerp(lookPitch, DefaultPitch, t);
                }
            }

            lookYaw = Mathf.Repeat(lookYaw + 180f, 360f) - 180f; // keep in -180..180
            lookPitch = Mathf.Clamp(lookPitch, MinPitch, MaxPitch);
            ApplyLook();
        }

        /// <summary>
        /// The camera target is a child of the character, and the character faces
        /// the board's nose - so a LOCAL rotation is automatically "relative to the board".
        /// </summary>
        private void ApplyLook()
        {
            if (cameraTarget != null)
                cameraTarget.localRotation = Quaternion.Euler(lookPitch, lookYaw, 0f);
        }

        private void HandleReachedEnd()
        {
            craft.River.GetLandingPose(out Vector3 position, out Quaternion rotation);
            stateManager.SwitchToOnFootAt(position, rotation);
        }

        public override void Exit()
        {
            Debug.Log("[FloatingState] ========== EXITING FLOATING STATE ==========");

            if (craft != null)
            {
                craft.OnReachedEnd -= HandleReachedEnd;
                craft.ClearRider();
            }

            // OnFootState.Enter moves the player to the landing and turns the
            // CharacterController back on; we only undo what we switched off.
            if (walkController != null) walkController.enabled = true;

            craft = null;
        }

        // --- Animator helpers: only set parameters that exist, so a different
        //     character rig logs no warnings. ---

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