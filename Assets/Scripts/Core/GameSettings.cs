// GameSettings.cs
// Location: Assets/Scripts/Core/GameSettings.cs
//
// Player settings: master / music / ambience / car / SFX volume and fullscreen.
// Single Responsibility: hold the values, save/load them, and apply them.
// It knows nothing about UI - the settings panel just calls the setters.
//
// Storage: PlayerPrefs (Windows registry). Settings are per-machine on purpose -
// a Steam Deck and a desktop PC usually want different volume/screen settings,
// so they should NOT sync through Steam Cloud like the save file can.
//
// Volume model:
//   - Stored as STEPS 0-10 (10% each) - easy for gamepad/keyboard to adjust.
//   - Master -> AudioListener.volume: affects EVERY sound in the game, even
//     AudioSources that aren't routed through the mixer.
//   - Music / Ambience / Car / SFX -> exposed AudioMixer parameters (in decibels).
//
// IMPORTANT - exposed parameters vs snapshots:
//   Once code calls SetFloat on an exposed parameter, snapshots stop controlling it.
//   So the PLAYER's sliders drive the top-level groups (Music, Ambience, Car, SFX),
//   and the STATE snapshots (Driving / OnFoot / Paused) drive their child groups
//   (Radio, Birds, Engine). The two multiply together and never fight.

using UnityEngine;
using UnityEngine.Audio;

namespace RelaxingDrive.Core
{
    public static class GameSettings
    {
        #region Playtest override

        // PLAYTEST BUILD (Nov 2026): the car volume starts at PlaytestCarVolume on
        // every launch, ignoring the saved value. The slider still works during the
        // session so testers can adjust it and you can find a good default.
        // To revert: set this to false (players keep their own saved setting again).
        //
        // 'static readonly' rather than 'const' on purpose: a const bool makes the
        // compiler warn about unreachable code in the branch that's switched off.
        private static readonly bool PlaytestForceCarVolume = true;
        private const int PlaytestCarVolume = 1; // 1 step = 10%

        #endregion

        #region Constants

        public const int MaxVolumeStep = 10;

        // Exposed parameter names in MainMixer - must match the mixer EXACTLY.
        public const string MusicMixerParam = "MusicVolume";
        public const string AmbienceMixerParam = "AmbienceVolume";
        public const string CarMixerParam = "CarVolume";
        public const string SfxMixerParam = "SFXVolume";

        // PlayerPrefs keys (prefixed so they never clash with RCC's own PlayerPrefs).
        private const string MasterKey = "SundayDrive.Settings.MasterVolume";
        private const string MusicKey = "SundayDrive.Settings.MusicVolume";
        private const string AmbienceKey = "SundayDrive.Settings.AmbienceVolume";
        private const string CarKey = "SundayDrive.Settings.CarVolume";
        private const string SfxKey = "SundayDrive.Settings.SfxVolume";

        // First-launch defaults. Music sits under the world; the car sits under
        // the birds so the vibe stays calm. Tune these after playtesting.
        private const int DefaultMaster = 10;
        private const int DefaultMusic = 7;
        private const int DefaultAmbience = 8;
        private const int DefaultCar = 3;
        private const int DefaultSfx = 10;

        // The mixer's quietest setting. 0 linear volume has no dB value (log of 0),
        // so "silent" is mapped to -80 dB, which the mixer treats as off.
        private const float SilentDb = -80f;

        // Window size when fullscreen is turned off (Steam Deck's native size).
        private const int WindowedWidth = 1280;
        private const int WindowedHeight = 800;

        #endregion

        #region State

        private static AudioMixer mixer;
        private static bool isLoaded;

        private static int masterVolume;
        private static int musicVolume;
        private static int ambienceVolume;
        private static int carVolume;
        private static int sfxVolume;

        #endregion

        #region Public API - values

        public static int MasterVolume { get { EnsureLoaded(); return masterVolume; } }
        public static int MusicVolume { get { EnsureLoaded(); return musicVolume; } }
        public static int AmbienceVolume { get { EnsureLoaded(); return ambienceVolume; } }
        public static int CarVolume { get { EnsureLoaded(); return carVolume; } }
        public static int SfxVolume { get { EnsureLoaded(); return sfxVolume; } }

        /// <summary>
        /// True unless the game is in a window. Read live from Unity - Unity already
        /// remembers the screen mode between launches, so we don't store it ourselves.
        /// </summary>
        public static bool IsFullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;

        #endregion

        #region Public API - setup

        /// <summary>
        /// Gives GameSettings the mixer and applies all saved settings.
        /// Called by SettingsApplier when a scene starts.
        /// </summary>
        public static void Initialize(AudioMixer audioMixer)
        {
            mixer = audioMixer;
            EnsureLoaded();
            ApplyAll();
        }

        /// <summary>Reads PlayerPrefs once per session (safe to call any time).</summary>
        public static void EnsureLoaded()
        {
            if (isLoaded) return;

            masterVolume = ClampStep(PlayerPrefs.GetInt(MasterKey, DefaultMaster));
            musicVolume = ClampStep(PlayerPrefs.GetInt(MusicKey, DefaultMusic));
            ambienceVolume = ClampStep(PlayerPrefs.GetInt(AmbienceKey, DefaultAmbience));
            sfxVolume = ClampStep(PlayerPrefs.GetInt(SfxKey, DefaultSfx));

            // Playtest: ignore the saved car volume once per launch (see top of file).
            // Because this only runs once per session, slider changes still stick
            // until the game is closed.
            carVolume = PlaytestForceCarVolume
                ? PlaytestCarVolume
                : ClampStep(PlayerPrefs.GetInt(CarKey, DefaultCar));

            isLoaded = true;
        }

        /// <summary>Writes PlayerPrefs to disk now (Unity also does this on quit).</summary>
        public static void Save()
        {
            PlayerPrefs.Save();
        }

        #endregion

        #region Public API - setters (apply immediately)

        public static void SetMasterVolume(int step)
        {
            EnsureLoaded();
            masterVolume = ClampStep(step);
            PlayerPrefs.SetInt(MasterKey, masterVolume);
            ApplyMaster();
        }

        public static void SetMusicVolume(int step)
        {
            EnsureLoaded();
            musicVolume = ClampStep(step);
            PlayerPrefs.SetInt(MusicKey, musicVolume);
            ApplyMixerVolume(MusicMixerParam, musicVolume);
        }

        public static void SetAmbienceVolume(int step)
        {
            EnsureLoaded();
            ambienceVolume = ClampStep(step);
            PlayerPrefs.SetInt(AmbienceKey, ambienceVolume);
            ApplyMixerVolume(AmbienceMixerParam, ambienceVolume);
        }

        public static void SetCarVolume(int step)
        {
            EnsureLoaded();
            carVolume = ClampStep(step);
            PlayerPrefs.SetInt(CarKey, carVolume);
            ApplyMixerVolume(CarMixerParam, carVolume);
        }

        public static void SetSfxVolume(int step)
        {
            EnsureLoaded();
            sfxVolume = ClampStep(step);
            PlayerPrefs.SetInt(SfxKey, sfxVolume);
            ApplyMixerVolume(SfxMixerParam, sfxVolume);
        }

        /// <summary>
        /// Fullscreen = borderless window at the monitor's resolution.
        /// Windowed = 1280x800 window. (No visible effect in the Editor - test in a build.)
        /// On Steam Deck the game always fills the screen, whatever this says.
        /// </summary>
        public static void SetFullscreen(bool fullscreen)
        {
            if (fullscreen)
            {
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight,
                                     FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(WindowedWidth, WindowedHeight, FullScreenMode.Windowed);
            }
        }

        #endregion

        #region Applying

        private static void ApplyAll()
        {
            ApplyMaster();
            ApplyMixerVolume(MusicMixerParam, musicVolume);
            ApplyMixerVolume(AmbienceMixerParam, ambienceVolume);
            ApplyMixerVolume(CarMixerParam, carVolume);
            ApplyMixerVolume(SfxMixerParam, sfxVolume);
        }

        private static void ApplyMaster()
        {
            // AudioListener.volume is a simple 0-1 multiplier on everything you hear.
            AudioListener.volume = masterVolume / (float)MaxVolumeStep;
        }

        /// <summary>
        /// AudioMixer volumes are in DECIBELS, not 0-1. Converting:
        ///   dB = 20 x log10(linear)    e.g. 1.0 -> 0 dB, 0.5 -> -6 dB, 0.1 -> -20 dB
        /// Without this, a "half volume" slider would barely sound quieter.
        /// </summary>
        private static void ApplyMixerVolume(string parameter, int step)
        {
            if (mixer == null) return; // not initialised yet - applied later by Initialize

            float linear = step / (float)MaxVolumeStep;
            float decibels = linear > 0f ? Mathf.Log10(linear) * 20f : SilentDb;

            if (!mixer.SetFloat(parameter, decibels))
            {
                Debug.LogWarning($"[GameSettings] Mixer has no exposed parameter '{parameter}' - " +
                                 "check the names in the Audio Mixer's Exposed Parameters list.");
            }
        }

        private static int ClampStep(int step) => Mathf.Clamp(step, 0, MaxVolumeStep);

        #endregion
    }
}