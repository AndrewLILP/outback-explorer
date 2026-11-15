// TubeMover.cs
// Location: Assets/Scripts/World/River/TubeMover.cs
//
// Carries a tube down a RiverPath. The current does the moving; the player only
// nudges left and right between the banks. No physics: the tube can't flip,
// sink or get stuck, which keeps the no-failure pillar and is cheap on Steam Deck.
//
// SETUP (step 2 prototype):
//   1. Create a cube (scale ~1.5, 0.4, 1.5) called "Tube". Remove its collider
//      for now, or set any Rigidbody to Kinematic.
//   2. Add TubeMover, drag River_01 into 'River', leave 'Start On Play' ticked.
//   3. Press Play: the cube floats from the green point to the red one.
//      A/D, the arrow keys or the left stick steer.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RelaxingDrive.World
{
    public class TubeMover : MonoBehaviour
    {
        [Header("River")]
        [SerializeField] private RiverPath river;
        [Tooltip("PROTOTYPE: start floating as soon as Play begins. Untick once FloatingState starts the float.")]
        [SerializeField] private bool startOnPlay = true;

        [Header("Steering")]
        [Tooltip("Sideways speed at full steer, in metres per second.")]
        [SerializeField, Min(0f)] private float steerSpeed = 1.5f;
        [Tooltip("Half the tube's width, so it stops before its edge reaches the bank.")]
        [SerializeField, Min(0f)] private float tubeRadius = 0.8f;
        [Tooltip("How strongly the current eases the tube back to the middle when not steering. 0 = none.")]
        [SerializeField, Min(0f)] private float centringPull = 0.3f;

        [Header("Feel")]
        [Tooltip("How quickly the tube turns to follow bends. Lower = lazier.")]
        [SerializeField, Min(0.1f)] private float turnSmoothing = 2f;
        [SerializeField, Min(0f)] private float bobHeight = 0.05f;
        [SerializeField, Min(0f)] private float bobCyclesPerSecond = 0.4f;
        [Tooltip("Optional child (the tube mesh) that slowly spins. Spin only the visual so the player and camera stay steady.")]
        [SerializeField] private Transform spinVisual;
        [SerializeField] private float spinDegreesPerSecond = 6f;

        [Header("Prototype input")]
        [Tooltip("TEMPORARY: read A/D and the left stick directly. Step 4 replaces this with PlayerInputReader calling SetSteerInput().")]
        [SerializeField] private bool debugReadInput = true;

        /// <summary>Raised once when the tube reaches the end of the river. TubeLandingPoint will listen for this.</summary>
        public event Action OnReachedEnd;

        private float distance;    // metres travelled along the river
        private float lateral;     // metres right (+) or left (-) of the centre line
        private float steerInput;  // -1 (left) to 1 (right)
        private bool floating;

        public bool IsFloating => floating;
        public float Progress01 => river != null && river.TotalLength > 0f ? distance / river.TotalLength : 0f;

        private void Start()
        {
            if (startOnPlay && river != null) Begin(river);
        }

        /// <summary>Put the tube at the start of a river and let the current take it.</summary>
        public void Begin(RiverPath path, float startDistance = 0f)
        {
            river = path;
            if (river == null || !river.IsValid)
            {
                Debug.LogWarning($"[TubeMover] '{name}' has no valid RiverPath (needs at least 2 child points).");
                return;
            }

            distance = Mathf.Clamp(startDistance, 0f, river.TotalLength);
            lateral = 0f;
            steerInput = 0f;
            floating = true;

            river.Evaluate(distance, out Vector3 position, out Vector3 forward);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        }

        public void Stop()
        {
            floating = false;
            steerInput = 0f;
        }

        /// <summary>-1 = steer left, 1 = steer right. Whoever owns input calls this each frame.</summary>
        public void SetSteerInput(float value) => steerInput = Mathf.Clamp(value, -1f, 1f);

        private void Update()
        {
            if (!floating) return;
            if (debugReadInput) SetSteerInput(ReadDebugSteer());

            float dt = Time.deltaTime;

            // 1. The current carries us forward.
            distance += river.FlowSpeed * dt;

            // 2. Steering moves us sideways; when hands are off, drift back to the middle.
            lateral += steerInput * steerSpeed * dt;
            if (Mathf.Approximately(steerInput, 0f))
                lateral = Mathf.Lerp(lateral, 0f, centringPull * dt);

            float maxOffset = Mathf.Max(0f, river.HalfWidth - tubeRadius);
            lateral = Mathf.Clamp(lateral, -maxOffset, maxOffset);

            // 3. Turn "distance + sideways offset" into a world position.
            river.Evaluate(distance, out Vector3 centre, out Vector3 forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float bob = Mathf.Sin(Time.time * bobCyclesPerSecond * Mathf.PI * 2f) * bobHeight;
            transform.position = centre + right * lateral + Vector3.up * bob;

            // 4. Lazily turn to face downstream. 1 - e^(-k*dt) gives the same
            //    smoothness at 30 FPS (Deck) and 60+ FPS (PC).
            Quaternion target = Quaternion.LookRotation(forward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSmoothing * dt));

            if (spinVisual != null)
                spinVisual.Rotate(Vector3.up, spinDegreesPerSecond * dt, Space.Self);

            // 5. Reached the landing?
            if (distance >= river.TotalLength)
            {
                floating = false;
                OnReachedEnd?.Invoke();
            }
        }

        /// <summary>TEMPORARY prototype input (Input System, not legacy). Removed in step 4.</summary>
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