// OnFootCameraPresets.cs
// Location: Assets/Scripts/Player/OnFootCameraPresets.cs
//
// Cycles the ON-FOOT camera between Close / Medium / Far presets
// (V on keyboard, R3 on gamepad) and blends smoothly between them.
// The chosen preset is remembered in PlayerPrefs.
//
// SETUP
// 1. Add this component to PlayerFollowCamera (the Cinemachine camera that
//    has the Third Person Follow on it).
// 2. Drag your "CycleCamera" Input Action into Cycle Preset Action.
// 3. Press Play and tweak the preset values live - Update() reads them every
//    frame, so you can experiment without restarting.
//
// Written for Cinemachine 2 (CinemachineVirtualCamera), which Starter Assets uses.
// In Cinemachine 2, the Third Person Follow is a "body" inside the virtual camera,
// not its own component, so we ask the virtual camera for it.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

/// <summary>
/// One camera framing. Pure data - add more presets in the Inspector
/// without touching code (Open/Closed principle).
/// </summary>
[Serializable]
public struct CameraPreset
{
    public string name;

    [Tooltip("How far behind the player the camera sits (metres).")]
    public float cameraDistance;

    [Tooltip("Offset from the player's shoulder. X = sideways, Y = up.")]
    public Vector3 shoulderOffset;

    [Tooltip("Raises the camera pivot. Higher = looking down on the player more.")]
    public float verticalArmLength;
}

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class OnFootCameraPresets : MonoBehaviour
{
    private const string PrefsKey = "OnFootCameraPreset";

    [Header("Input")]
    [Tooltip("Button action bound to V (keyboard) and Right Stick Press (gamepad).")]
    [SerializeField] private InputActionReference cyclePresetAction;

    [Header("Presets (starting values - tune these!)")]
    [SerializeField] private CameraPreset[] presets =
    {
        new CameraPreset { name = "Close",  cameraDistance = 2.5f, shoulderOffset = new Vector3(0.6f, 0.1f, 0f), verticalArmLength = 0.2f },
        new CameraPreset { name = "Medium", cameraDistance = 4.0f, shoulderOffset = new Vector3(0.8f, 0.0f, 0f), verticalArmLength = 0.4f },
        new CameraPreset { name = "Far",    cameraDistance = 6.5f, shoulderOffset = new Vector3(0.5f, 0.3f, 0f), verticalArmLength = 1.0f },
    };

    [Tooltip("Preset used the first time the game runs (0 = Close, 1 = Medium, 2 = Far).")]
    [SerializeField] private int defaultPresetIndex = 1;

    [Tooltip("How quickly the camera glides to a new preset. Higher = snappier. ~3-5 feels calm.")]
    [SerializeField] private float blendSpeed = 4f;

    /// <summary>Index of the active preset.</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>Fires with the preset name - ready for a small HUD toast later.</summary>
    public event Action<string> OnPresetChanged;

    private Cinemachine3rdPersonFollow follow;

    private void Awake()
    {
        // The 3rd Person Follow lives inside the virtual camera's Body settings.
        follow = GetComponent<CinemachineVirtualCamera>()
                    .GetCinemachineComponent<Cinemachine3rdPersonFollow>();

        if (follow == null)
        {
            Debug.LogError("OnFootCameraPresets: set the virtual camera's Body to '3rd Person Follow'.", this);
            enabled = false;
            return;
        }

        // Load the saved choice, clamped in case the preset list changed size.
        CurrentIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, defaultPresetIndex), 0, presets.Length - 1);

        // Start exactly on the preset (no blend on load).
        ApplyInstant(presets[CurrentIndex]);
    }

    private void OnEnable()
    {
        if (cyclePresetAction == null) return;
        cyclePresetAction.action.performed += OnCyclePerformed;
        cyclePresetAction.action.Enable(); // harmless if PlayerInput already enabled it
    }

    private void OnDisable()
    {
        if (cyclePresetAction == null) return;
        // Unsubscribe only. We don't Disable() the action because it lives in the
        // shared input asset that PlayerInput also uses.
        cyclePresetAction.action.performed -= OnCyclePerformed;
    }

    private void Update()
    {
        CameraPreset target = presets[CurrentIndex];

        // Exponential smoothing: covers the same fraction of the remaining gap
        // per second at 30 FPS (Deck) or 60 FPS (PC). A plain Lerp(a, b, speed * dt)
        // would feel slightly different at different framerates.
        float t = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);

        follow.CameraDistance    = Mathf.Lerp(follow.CameraDistance, target.cameraDistance, t);
        follow.ShoulderOffset    = Vector3.Lerp(follow.ShoulderOffset, target.shoulderOffset, t);
        follow.VerticalArmLength = Mathf.Lerp(follow.VerticalArmLength, target.verticalArmLength, t);
    }

    private void OnCyclePerformed(InputAction.CallbackContext context)
    {
        SetPreset((CurrentIndex + 1) % presets.Length); // Close -> Medium -> Far -> Close
    }

    /// <summary>
    /// Public so a pause-menu setting can choose a preset directly later.
    /// </summary>
    public void SetPreset(int index)
    {
        CurrentIndex = Mathf.Clamp(index, 0, presets.Length - 1);
        PlayerPrefs.SetInt(PrefsKey, CurrentIndex);
        OnPresetChanged?.Invoke(presets[CurrentIndex].name);
    }

    private void ApplyInstant(CameraPreset preset)
    {
        follow.CameraDistance    = preset.cameraDistance;
        follow.ShoulderOffset    = preset.shoulderOffset;
        follow.VerticalArmLength = preset.verticalArmLength;
    }
}