// CameraLookInput.cs
// Location: Assets/Scripts/Player/ (attach to the RCC_Camera prefab, next to CameraPresetCycler)
//
// Lets the player look around the car while driving:
//   - Gamepad / Steam Deck: right stick
//   - Mouse: hold right mouse button and move
// After the player lets go, the camera waits a moment and then glides back
// behind the car (no snap).
//
// WHY THIS SCRIPT EXISTS
// RCC's input manager reads the right stick into RCC_Inputs.orbitX/Y, but in
// this RCC version RCC_Camera never uses those values. Its orbit only changes
// through OnDrag(), which is called by RCC's mobile touch panel. So on PC
// and Deck the right stick did nothing. This script reads our own Input System
// action and feeds the result into RCC_Camera's orbit.
//
// WHY REFLECTION
// RCC keeps orbitX / orbitY private, and we need to read them back to recentre
// smoothly. Reflection lets us reach those two fields without editing RCC's
// source, so an RCC update won't wipe our change. The trade-off: if a future
// RCC renames the fields, this script logs a warning and switches itself off
// (it won't crash the game).
//
// RULES (SRP): this class only turns look input into camera orbit.
// It doesn't change presets (CameraPresetCycler does) or camera modes (RCC does).

using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraLookInput : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The RCC camera to drive. Auto-found on this GameObject if left empty.")]
    [SerializeField] private RCC_Camera rccCamera;

    [Tooltip("Value / Vector2 action. Bind <Gamepad>/rightStick, plus a " +
             "'Binding With One Modifier': Modifier = <Mouse>/rightButton, Binding = <Mouse>/delta.")]
    [SerializeField] private InputActionReference lookAction;

    [Header("Gamepad (degrees per second at full stick)")]
    [SerializeField] private float stickYawSpeed = 90f;
    [SerializeField] private float stickPitchSpeed = 60f;

    [Header("Mouse (degrees per pixel moved)")]
    [SerializeField] private float mouseSensitivity = 0.15f;

    [Tooltip("Tick to make pushing up look down (flight-style).")]
    [SerializeField] private bool invertY = false;

    [Header("Recentre")]
    [Tooltip("Seconds of no look input before the camera drifts back behind the car.")]
    [SerializeField] private float recentreDelay = 1.5f;

    [Tooltip("How quickly it drifts back. Lower = slower, calmer.")]
    [SerializeField] private float recentreSpeed = 2f;

    [Tooltip("Within this many degrees, control is handed back to RCC's normal follow.")]
    [SerializeField] private float handBackThreshold = 0.5f;

    [SerializeField] private bool showDebugLogs = false;

    // RCC's private orbit fields, found once via reflection.
    private FieldInfo orbitXField;
    private FieldInfo orbitYField;

    // Our working copy of the orbit angles while the player is looking around.
    private float yaw;
    private float pitch;

    private bool isLooking;      // true = we own the orbit values right now
    private float idleTimer;     // time since the last look input
    private RCC_Camera.CameraMode lastMode;

    private void Awake()
    {
        if (rccCamera == null) rccCamera = GetComponent<RCC_Camera>();

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        orbitXField = typeof(RCC_Camera).GetField("orbitX", flags);
        orbitYField = typeof(RCC_Camera).GetField("orbitY", flags);

        if (rccCamera == null || orbitXField == null || orbitYField == null || lookAction == null)
        {
            Debug.LogWarning("[CameraLookInput] Missing RCC_Camera, look action, or RCC orbit fields. Look-around disabled.");
            enabled = false;
            return;
        }

        lastMode = rccCamera.cameraMode;
    }

    private void OnEnable()
    {
        // Project-wide actions are already enabled, but enabling twice is harmless
        // and keeps this working if the action ever lives in a different asset.
        lookAction?.action?.Enable();
    }

    private void Update()
    {
        // RCC resets orbit to zero whenever the camera mode changes (C / B).
        // Drop our copy so we re-seed from the new view next time.
        if (rccCamera.cameraMode != lastMode)
        {
            lastMode = rccCamera.cameraMode;
            isLooking = false;
        }

        if (!CanLook())
        {
            isLooking = false;
            return;
        }

        Vector2 input = lookAction.action.ReadValue<Vector2>();

        if (input.sqrMagnitude > 0.0001f)
        {
            ApplyLook(input);
            idleTimer = 0f;
        }
        else if (isLooking)
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= recentreDelay)
                Recentre();
        }
    }

    /// <summary>Only look around while driving, in chase (TPS) or hood (FPS) view.</summary>
    private bool CanLook()
    {
        if (Time.timeScale <= 0f) return false;
        if (!rccCamera.isRendering) return false;
        if (rccCamera.cameraTarget == null || rccCamera.cameraTarget.playerVehicle == null) return false;

        return rccCamera.cameraMode == RCC_Camera.CameraMode.TPS
            || rccCamera.cameraMode == RCC_Camera.CameraMode.FPS;
    }

    private void ApplyLook(Vector2 input)
    {
        if (!isLooking) Seed();

        // Mouse delta is already "how far this frame", so it doesn't use deltaTime.
        // A stick is "how far pushed", so it's turned into degrees per second.
        bool fromMouse = lookAction.action.activeControl?.device is Mouse;

        float yawDelta = fromMouse
            ? input.x * mouseSensitivity
            : input.x * stickYawSpeed * Time.deltaTime;

        float pitchDelta = fromMouse
            ? input.y * mouseSensitivity
            : input.y * stickPitchSpeed * Time.deltaTime;

        // In Unity a larger X angle tilts the camera DOWN, so pushing up subtracts.
        if (!invertY) pitchDelta = -pitchDelta;

        yaw += yawDelta;
        pitch = Mathf.Clamp(pitch + pitchDelta, rccCamera.minOrbitY, rccCamera.maxOrbitY);

        WriteOrbit(yaw, pitch);
    }

    /// <summary>Start from wherever the camera is currently pointing, so there's no jump.</summary>
    private void Seed()
    {
        isLooking = true;

        if (rccCamera.cameraMode == RCC_Camera.CameraMode.TPS)
        {
            // TPS orbit is in WORLD angles.
            // RCC adds TPSPitch on top of orbitY, so remove it here to avoid a jump.
            Vector3 euler = rccCamera.transform.eulerAngles;
            yaw = euler.y;
            pitch = Mathf.DeltaAngle(0f, euler.x - rccCamera.TPSPitch);
        }
        else
        {
            // Hood (FPS) orbit is RELATIVE to the car, so straight ahead is 0, 0.
            yaw = 0f;
            pitch = 0f;
        }
    }

    /// <summary>
    /// Glide back behind the car using frame-rate-independent smoothing:
    /// 1 - e^(-speed * dt) closes the same share of the gap per second at 30 or 60 FPS.
    /// </summary>
    private void Recentre()
    {
        float targetYaw = 0f;
        float targetPitch = 0f;

        if (rccCamera.cameraMode == RCC_Camera.CameraMode.TPS)
        {
            Transform car = rccCamera.cameraTarget.playerVehicle.transform;
            targetYaw = car.eulerAngles.y;
            targetPitch = Mathf.DeltaAngle(0f, car.eulerAngles.x);
        }

        float t = 1f - Mathf.Exp(-recentreSpeed * Time.deltaTime);

        // DeltaAngle takes the short way round (e.g. 350 -> 10 is +20, not -340).
        yaw += Mathf.DeltaAngle(yaw, targetYaw) * t;
        pitch = Mathf.Lerp(pitch, targetPitch, t);

        bool closeEnough =
            Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw)) < handBackThreshold &&
            Mathf.Abs(pitch - targetPitch) < handBackThreshold;

        if (closeEnough)
        {
            // Zero means "no orbit" to RCC, so its normal chase follow takes over again.
            orbitXField.SetValue(rccCamera, 0f);
            orbitYField.SetValue(rccCamera, 0f);
            isLooking = false;
            Log("Recentred - handed back to RCC");
            return;
        }

        WriteOrbit(yaw, pitch);
    }

    private void WriteOrbit(float x, float y)
    {
        // RCC ignores an axis whose value is exactly 0, so nudge off zero while we own it.
        if (x == 0f) x = 0.001f;
        if (y == 0f) y = 0.001f;

        orbitXField.SetValue(rccCamera, x);
        orbitYField.SetValue(rccCamera, y);
    }

    private void Log(string message)
    {
        if (showDebugLogs) Debug.Log($"[CameraLookInput] {message}");
    }
}