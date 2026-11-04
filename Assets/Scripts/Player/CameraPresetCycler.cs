// CameraPresetCycler.cs
// Location: Assets/Scripts/Player/ (attach to the RCC_Camera GameObject)
//
// Cycles the RCC TPS camera between presets (Close -> Medium -> Far -> Close)
// when the "CycleCameraPreset" Input System action is pressed (V / gamepad View).
// RCC's own C key (change camera MODE) is left untouched.
//
// Responsibilities (SRP): pick a preset and blend the RCC camera towards it.
// It does NOT save settings or draw UI - it raises OnPresetChanged so a
// SettingsManager and a HUD label can listen (Observer pattern).

using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraPresetCycler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The RCC camera to drive. Auto-found on this GameObject if left empty.")]
    [SerializeField] private RCC_Camera rccCamera;

    [Tooltip("Button action from your Input Actions asset (bind V and <Gamepad>/select).")]
    [SerializeField] private InputActionReference cyclePresetAction;

    [Header("Presets (in cycle order)")]
    [SerializeField] private CameraPresetData[] presets;

    [Tooltip("Preset used at start (0 = first in the list). Default: Far.")]
    [SerializeField] private int startIndex = 2;

    [Header("Blending")]
    [Tooltip("How quickly the camera glides to a new preset. Lower = slower, calmer.")]
    [SerializeField] private float blendSpeed = 3f;

    /// <summary>Raised when the preset changes. Hook up the HUD label and settings save here.</summary>
    public event Action<CameraPresetData> OnPresetChanged;

    public int CurrentIndex => currentIndex;

    private int currentIndex;

    private void Awake()
    {
        if (!rccCamera)
            rccCamera = GetComponent<RCC_Camera>();

        // Turn off RCC Auto Focus as early as possible.
        // Otherwise SetTarget() starts a coroutine that overwrites distance/height
        // every time the player gets into the car.
        rccCamera.TPSAutoFocus = false;
    }

    private void OnEnable()
    {
        if (cyclePresetAction == null) return;

        cyclePresetAction.action.performed += HandleCyclePressed;
        cyclePresetAction.action.Enable();
    }

    private void OnDisable()
    {
        // Only unsubscribe - don't Disable() the action, other scripts may share the asset.
        if (cyclePresetAction == null) return;

        cyclePresetAction.action.performed -= HandleCyclePressed;
    }

    private void Start()
    {
        if (presets == null || presets.Length == 0)
        {
            Debug.LogWarning("CameraPresetCycler: no presets assigned.", this);
            enabled = false;
            return;
        }

        // Snap (no blend) to the starting preset.
        SetPreset(startIndex, instant: true);
    }

    private void Update()
    {
        if (presets == null || presets.Length == 0) return;

        CameraPresetData target = presets[currentIndex];

        // Frame-rate independent smoothing: same feel at 30 FPS (Deck) and 60 FPS (PC).
        // 1 - e^(-speed * dt) is the fraction of the remaining gap to close this frame.
        float t = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);

        // We write every frame on purpose: if anything else (e.g. an RCC
        // AutoFocus call) changes these values, we gently pull them back.
        // Update runs before RCC_Camera.LateUpdate, so RCC uses our values this frame.
        rccCamera.TPSDistance = Mathf.Lerp(rccCamera.TPSDistance, target.distance, t);
        rccCamera.TPSHeight   = Mathf.Lerp(rccCamera.TPSHeight,   target.height,   t);
        rccCamera.TPSPitch    = Mathf.Lerp(rccCamera.TPSPitch,    target.pitch,    t);
    }

    private void HandleCyclePressed(InputAction.CallbackContext context)
    {
        // Ignore while paused, when the RCC camera isn't in use (on foot),
        // or when RCC is in a non-TPS mode (hood, cinematic, etc.).
        if (Time.timeScale <= 0f) return;
        if (!rccCamera.isRendering) return;
        if (rccCamera.cameraMode != RCC_Camera.CameraMode.TPS) return;

        int next = (currentIndex + 1) % presets.Length;   // wrap Far -> Close
        SetPreset(next, instant: false);
    }

    /// <summary>
    /// Selects a preset. Public so the pause-menu setting can call it too.
    /// instant = true snaps immediately (used at start / on load).
    /// </summary>
    public void SetPreset(int index, bool instant)
    {
        currentIndex = Mathf.Clamp(index, 0, presets.Length - 1);
        CameraPresetData preset = presets[currentIndex];

        if (instant)
        {
            rccCamera.TPSDistance = preset.distance;
            rccCamera.TPSHeight   = preset.height;
            rccCamera.TPSPitch    = preset.pitch;
        }

        OnPresetChanged?.Invoke(preset);
    }
}