// CameraPresetData.cs
// Location: Assets/Scripts/Data/ (assets go in Assets/Data/CameraPresets/)
//
// A single camera preset (e.g. Close / Medium / Far) as data.
// Create via: Right-click > Create > Sunday Drive > Camera Preset
//
// Why a ScriptableObject?
// - Open/Closed: add or tweak presets by editing assets, not code.
// - Designers (you!) can tune values in the Inspector without touching scripts.

using UnityEngine;

[CreateAssetMenu(fileName = "CameraPreset_", menuName = "Sunday Drive/Camera Preset")]
public class CameraPresetData : ScriptableObject
{
    [Tooltip("Name shown to the player, e.g. 'Close', 'Medium', 'Far'.")]
    public string displayName = "Far";

    [Tooltip("How far behind the car the camera sits (RCC TPSDistance).")]
    [Range(0f, 20f)] public float distance = 14f;

    [Tooltip("How high above the car the camera sits (RCC TPSHeight).")]
    [Range(0f, 10f)] public float height = 7f;

    [Tooltip("Downward tilt of the camera in degrees (RCC TPSPitch). " +
             "Higher = car sits further up the screen.")]
    [Range(-45f, 45f)] public float pitch = 10f;
}