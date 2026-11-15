// RiverCraftMover.cs
// Location: Assets/Scripts/World/River/RiverCraftMover.cs
//
// Carries a river craft down a RiverPath. Craft-agnostic: a stand-up paddle
// board now, a tube or canoe later, each just a prefab with different settings.
// No physics, so the craft can't tip, sink or snag (no-failure pillar, cheap
// on Steam Deck).
//
// The craft has its own small life cycle (a simple state machine via an enum):
//   Docked    - waiting at the launch, gently bobbing. RiverLaunchPoint offers "Hop on".
//   Floating  - the current carries it; the player steers.
//   Landed    - player has stepped off; it waits at the landing for returnDelay seconds.
//   Returning - glides back upstream on its own, then docks again.
//
// SETUP (stand-up paddle board):
//   SUP_Board            <- empty; add RiverCraftMover (River = River_01) and RiverLaunchPoint
//   (SUP_Board must stay at scale 1,1,1 - only the Deck cube is scaled)
//     Sway               <- empty, scale (1,1,1). Drag into 'Sway Visual'
//       Deck             <- cube, scale (0.8, 0.15, 3), no collider
//       StandPoint       <- empty at local (0, 0.075, 0), the top of the deck. Drag into 'Stand Point'
//   Keep StandPoint OUT of the scaled cube, or anything on it gets squashed too.
//   The board snaps to the river's launch point when Play starts.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RelaxingDrive.World
{
    public class RiverCraftMover : MonoBehaviour
    {
        public enum CraftMode { Docked, Floating, Landed, Returning }

        [Header("River")]
        [SerializeField] private RiverPath river;

        [Header("Craft")]
        [Tooltip("Where the player's feet go. Put it under the Sway Visual so the player sways with the board.")]
        [SerializeField] private Transform standPoint;
        [Tooltip("Half the craft's width, so it stops before its edge reaches the bank. SUP ~0.5, tube ~0.8.")]
        [SerializeField, Min(0f)] private float craftHalfWidth = 0.5f;

        [Header("Steering")]
        [Tooltip("Sideways speed at full steer, in metres per second.")]
        [SerializeField, Min(0f)] private float steerSpeed = 1.5f;
        [Tooltip("How strongly the current eases the craft back to the middle when not steering. 0 = none.")]
        [SerializeField, Min(0f)] private float centringPull = 0.3f;

        [Header("Return to launch")]
        [Tooltip("Seconds the craft waits at the landing before heading back.")]
        [SerializeField, Min(0f)] private float returnDelay = 60f;
        [Tooltip("Speed of the empty craft gliding back upstream, in metres per second.")]
        [SerializeField, Min(0.1f)] private float returnSpeed = 3f;

        [Header("Feel")]
        [Tooltip("How quickly the craft turns to follow bends. Lower = lazier.")]
        [SerializeField, Min(0.1f)] private float turnSmoothing = 2f;
        [SerializeField, Min(0f)] private float bobHeight = 0.04f;
        [SerializeField, Min(0f)] private float bobCyclesPerSecond = 0.4f;

        [Header("Sway (visual only)")]
        [Tooltip("Child holding the mesh. It rolls and pitches gently; the root stays level so the path maths stays simple.")]
        [SerializeField] private Transform swayVisual;
        [SerializeField, Min(0f)] private float rollDegrees = 1.5f;
        [SerializeField, Min(0f)] private float pitchDegrees = 0.8f;
        [SerializeField, Min(0f)] private float swayCyclesPerSecond = 0.25f;
        [Tooltip("Extra roll into the direction you're steering, like shifting your weight on a board.")]
        [SerializeField, Min(0f)] private float steerLeanDegrees = 3f;

        [Header("Prototype input")]
        [Tooltip("TEMPORARY: read A/D and the left stick directly while floating. Later replaced by PlayerInputReader calling SetSteerInput().")]
        [SerializeField] private bool debugReadInput = true;

        /// <summary>Raised once when the craft reaches the end of the river with the player on it.</summary>
        public event Action OnReachedEnd;

        private CraftMode mode = CraftMode.Docked;
        private float distance;     // metres travelled along the river
        private float lateral;      // metres right (+) or left (-) of the centre line
        private float steerInput;   // -1 (left) to 1 (right)
        private float lean;         // smoothed steer, for the weight-shift roll
        private float landedTimer;
        private Transform rider;    // the character standing on the deck, if any

        public CraftMode Mode => mode;
        public bool IsDocked => mode == CraftMode.Docked;
        public RiverPath River => river;
        public Transform StandPoint => standPoint != null ? standPoint : transform;

        private void Start()
        {
            if (river == null || !river.IsValid)
            {
                Debug.LogWarning($"[RiverCraftMover] '{name}' has no valid RiverPath (needs at least 2 child points).");
                enabled = false;
                return;
            }

            // Start docked at the launch, facing downstream.
            distance = 0f;
            lateral = 0f;
            river.Evaluate(0f, out Vector3 position, out Vector3 forward);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            mode = CraftMode.Docked;
        }

        /// <summary>Called by FloatingState once the player is standing on the board.</summary>
        public void Launch()
        {
            if (mode != CraftMode.Docked) return;
            steerInput = 0f;
            mode = CraftMode.Floating;
        }

        /// <summary>-1 = steer left, 1 = steer right. Whoever owns input calls this each frame.</summary>
        public void SetSteerInput(float value) => steerInput = Mathf.Clamp(value, -1f, 1f);

        /// <summary>
        /// The character to carry. Instead of parenting it (which passes on any
        /// scale and tilt from the board and can stretch the model), the craft
        /// places the rider on the StandPoint itself, at the end of every Update -
        /// same frame, so there's no lag.
        /// </summary>
        public void SetRider(Transform newRider) => rider = newRider;
        public void ClearRider() => rider = null;

        private void Update()
        {
            float dt = Time.deltaTime;

            switch (mode)
            {
                case CraftMode.Floating:  UpdateFloating(dt);  break;
                case CraftMode.Landed:    UpdateLanded(dt);    break;
                case CraftMode.Returning: UpdateReturning(dt); break;
                // Docked: stays put, just bobs and sways below.
            }

            PlaceOnRiver(dt);
            ApplySway(dt);
            CarryRider();
        }

        /// <summary>
        /// Feet on the StandPoint (so they rise and fall with the bob and sway),
        /// body upright facing the board's nose. Only the board's YAW is copied:
        /// a rider that rolled with the deck would roll the camera too.
        /// </summary>
        private void CarryRider()
        {
            if (rider == null) return;
            rider.SetPositionAndRotation(StandPoint.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        }

        private void UpdateFloating(float dt)
        {
            if (debugReadInput) SetSteerInput(ReadDebugSteer());

            // The current carries us forward; steering moves us sideways.
            distance += river.FlowSpeed * dt;
            lateral += steerInput * steerSpeed * dt;
            if (Mathf.Approximately(steerInput, 0f))
                lateral = Mathf.Lerp(lateral, 0f, centringPull * dt);

            float maxOffset = Mathf.Max(0f, river.HalfWidth - craftHalfWidth);
            lateral = Mathf.Clamp(lateral, -maxOffset, maxOffset);

            if (distance >= river.TotalLength)
            {
                distance = river.TotalLength;
                steerInput = 0f;
                landedTimer = returnDelay;
                mode = CraftMode.Landed;
                OnReachedEnd?.Invoke(); // FloatingState steps the player off
            }
        }

        private void UpdateLanded(float dt)
        {
            landedTimer -= dt;
            if (landedTimer <= 0f) mode = CraftMode.Returning;
        }

        private void UpdateReturning(float dt)
        {
            // Glide back upstream along the same path, drifting to the middle.
            distance -= returnSpeed * dt;
            lateral = Mathf.Lerp(lateral, 0f, 1f - Mathf.Exp(-1f * dt));

            if (distance <= 0f)
            {
                distance = 0f;
                mode = CraftMode.Docked;
            }
        }

        /// <summary>Turns "distance along + sideways offset" into a world position and rotation.</summary>
        private void PlaceOnRiver(float dt)
        {
            river.Evaluate(distance, out Vector3 centre, out Vector3 forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float bob = Mathf.Sin(Time.time * bobCyclesPerSecond * Mathf.PI * 2f) * bobHeight;
            transform.position = centre + right * lateral + Vector3.up * bob;

            // Face downstream, except when heading home (face upstream).
            Vector3 facing = mode == CraftMode.Returning ? -forward : forward;
            Quaternion target = Quaternion.LookRotation(facing, Vector3.up);

            // 1 - e^(-k*dt): same smoothness at 30 FPS (Deck) and 60+ FPS (PC).
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSmoothing * dt));
        }

        /// <summary>
        /// Two sine waves at slightly different speeds for roll and pitch, so the
        /// motion never lines up into an obvious loop, plus a lean into the steer.
        /// </summary>
        private void ApplySway(float dt)
        {
            if (swayVisual == null) return;

            lean = Mathf.Lerp(lean, steerInput, 1f - Mathf.Exp(-3f * dt));

            float phase = Time.time * swayCyclesPerSecond * Mathf.PI * 2f;
            float roll = Mathf.Sin(phase) * rollDegrees - lean * steerLeanDegrees; // negative Z = lean right
            float pitch = Mathf.Sin(phase * 0.73f + 1.3f) * pitchDegrees;

            swayVisual.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        /// <summary>TEMPORARY prototype input (Input System, not legacy).</summary>
        private float ReadDebugSteer()
        {
            float value = 0f;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) value -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) value += 1f;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float stick = gamepad.leftStick.x.ReadValue();
                if (Mathf.Abs(stick) > Mathf.Abs(value)) value = stick;
            }

            return value;
        }
    }
}