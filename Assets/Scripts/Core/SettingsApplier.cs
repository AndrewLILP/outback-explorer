// SettingsApplier.cs
// Location: Assets/Scripts/Core/SettingsApplier.cs
//
// Put ONE of these in every scene the player can start or land in
// (gameplay scene and PauseMenuScene). On Start it hands the mixer to
// GameSettings and applies the saved volumes.
//
// Why Start() and not Awake()?
// AudioMixer.SetFloat called in Awake is a known case where the value can be
// ignored, because the mixer isn't fully running yet. Start is reliably later.

using UnityEngine;
using UnityEngine.Audio;

namespace RelaxingDrive.Core
{
    public class SettingsApplier : MonoBehaviour
    {
        [Tooltip("Assets/Audio/MainMixer")]
        [SerializeField] private AudioMixer audioMixer;

        private void Start()
        {
            if (audioMixer == null)
            {
                Debug.LogWarning("[SettingsApplier] No AudioMixer assigned - music/SFX volume won't apply.");
            }

            GameSettings.Initialize(audioMixer);
        }
    }
}
