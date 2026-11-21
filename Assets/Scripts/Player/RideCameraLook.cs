// RideCameraLook.cs
// Location: Assets/Scripts/Player/RideCameraLook.cs
//
// Free-look camera for riding states (balloon now; the SUP could switch to it later).
// Mouse / right stick turns the walking camera around the rider - no button needed.
// After a few seconds hands-off it eases back to looking forward.
//
// A plain C# class, not a MonoBehaviour: a state CREATES one and calls Tick()
// each frame. Composition - each ride state HAS a camera look - instead of
// copying the same code into every state.

using UnityEngine;
using StarterAssets;

namespace RelaxingDrive.Player
{
    public class RideCameraLook
    {
        private const float LookDeadzone = 0.01f;

        private readonly float defaultPitch;
        private readonly float minPitch;
        private readonly float maxPitch;
        private readonly float recentreDelay;
        private readonly float recentreSpeed;

        private Transform cameraTarget;
        private StarterAssetsInputs inputs;
        private float yaw;
        private float pitch;
        private float timeSinceLook;

        public RideCameraLook(float defaultPitch, float minPitch, float maxPitch,
                              float recentreDelay = 4f, float recentreSpeed = 0.6f)
        {
            this.defaultPitch = defaultPitch;
            this.minPitch = minPitch;
            this.maxPitch = maxPitch;
            this.recentreDelay = recentreDelay;
            this.recentreSpeed = recentreSpeed;
        }

        /// <summary>Start looking forward from this camera target (Starter Assets' CinemachineCameraTarget).</summary>
        public void Begin(Transform target, StarterAssetsInputs lookInputs)
        {
            cameraTarget = target;
            inputs = lookInputs;
            yaw = 0f;
            pitch = defaultPitch;
            timeSinceLook = 0f;
            Apply();
        }

        public void Tick(float dt)
        {
            if (cameraTarget == null) return;

            Vector2 look = inputs != null ? inputs.look : Vector2.zero;

            if (look.sqrMagnitude > LookDeadzone)
            {
                // Mouse is already "per frame"; stick is "per second" and needs deltaTime.
                bool usingGamepad = PlayerInputReader.Instance != null &&
                                    PlayerInputReader.Instance.CurrentDevice == InputDeviceType.Gamepad;
                float multiplier = usingGamepad ? dt : 1f;

                yaw += look.x * multiplier;
                pitch += look.y * multiplier;
                timeSinceLook = 0f;
            }
            else
            {
                // Hands off for a while -> ease back to looking forward.
                timeSinceLook += dt;
                if (timeSinceLook >= recentreDelay)
                {
                    float t = 1f - Mathf.Exp(-recentreSpeed * dt);
                    yaw = Mathf.LerpAngle(yaw, 0f, t);
                    pitch = Mathf.Lerp(pitch, defaultPitch, t);
                }
            }

            yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f; // keep in -180..180
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            Apply();
        }

        /// <summary>LOCAL rotation: the target is a child of the rider, who faces the craft's nose.</summary>
        private void Apply()
        {
            if (cameraTarget != null)
                cameraTarget.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}