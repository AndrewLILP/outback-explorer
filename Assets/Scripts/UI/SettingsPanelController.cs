// SettingsPanelController.cs
// Location: Assets/Scripts/UI/SettingsPanelController.cs
//
// Connects the Settings panel's controls to GameSettings.
// Plain C# class (not a MonoBehaviour): it's created and owned by the menu
// that shows it (PauseMenuSceneController), so it can be reused later by a
// main menu without changes.
//
// Single Responsibility:
//   - this class: control values <-> GameSettings, and the "70%" / "On" labels
//   - the owning menu: showing/hiding the panel, focus, navigation, sounds
// It raises events so the menu can play feedback sounds without this class
// knowing anything about audio clips.

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using RelaxingDrive.Core;

namespace RelaxingDrive.UI
{
    public class SettingsPanelController
    {
        #region Events

        /// <summary>A setting changed (play a small tick).</summary>
        public event Action OnSettingChanged;

        /// <summary>SFX volume changed (play a sample so the player hears the new level).</summary>
        public event Action OnSfxVolumeChanged;

        #endregion

        #region Fields

        private readonly SliderInt masterSlider;
        private readonly SliderInt musicSlider;
        private readonly SliderInt sfxSlider;
        private readonly Toggle fullscreenToggle;

        private readonly Label masterValue;
        private readonly Label musicValue;
        private readonly Label sfxValue;
        private readonly Label fullscreenValue;

        #endregion

        #region Public API

        /// <summary>The control to focus when the panel opens.</summary>
        public VisualElement FirstFocusable => masterSlider;

        /// <summary>All interactive controls, so the menu can wire focus/hover/sounds.</summary>
        public IEnumerable<VisualElement> Controls
        {
            get
            {
                if (masterSlider != null) yield return masterSlider;
                if (musicSlider != null) yield return musicSlider;
                if (sfxSlider != null) yield return sfxSlider;
                if (fullscreenToggle != null) yield return fullscreenToggle;
            }
        }

        /// <summary>Finds the controls inside the panel and hooks up their change events.</summary>
        public SettingsPanelController(VisualElement panel)
        {
            masterSlider = panel.Q<SliderInt>("MasterSlider");
            musicSlider = panel.Q<SliderInt>("MusicSlider");
            sfxSlider = panel.Q<SliderInt>("SfxSlider");
            fullscreenToggle = panel.Q<Toggle>("FullscreenToggle");

            masterValue = panel.Q<Label>("MasterValue");
            musicValue = panel.Q<Label>("MusicValue");
            sfxValue = panel.Q<Label>("SfxValue");
            fullscreenValue = panel.Q<Label>("FullscreenValue");

            SetupSlider(masterSlider);
            SetupSlider(musicSlider);
            SetupSlider(sfxSlider);

            masterSlider?.RegisterValueChangedCallback(evt =>
            {
                GameSettings.SetMasterVolume(evt.newValue);
                SetPercent(masterValue, evt.newValue);
                OnSettingChanged?.Invoke();
            });

            musicSlider?.RegisterValueChangedCallback(evt =>
            {
                GameSettings.SetMusicVolume(evt.newValue);
                SetPercent(musicValue, evt.newValue);
                OnSettingChanged?.Invoke();
            });

            sfxSlider?.RegisterValueChangedCallback(evt =>
            {
                GameSettings.SetSfxVolume(evt.newValue);
                SetPercent(sfxValue, evt.newValue);
                OnSfxVolumeChanged?.Invoke();
            });

            fullscreenToggle?.RegisterValueChangedCallback(evt =>
            {
                GameSettings.SetFullscreen(evt.newValue);
                SetOnOff(fullscreenValue, evt.newValue);
                OnSettingChanged?.Invoke();
            });

            Refresh();
        }

        /// <summary>
        /// Shows the current saved values. Uses SetValueWithoutNotify so opening the
        /// panel doesn't fire change events (no sounds, no re-saving).
        /// </summary>
        public void Refresh()
        {
            GameSettings.EnsureLoaded();

            masterSlider?.SetValueWithoutNotify(GameSettings.MasterVolume);
            musicSlider?.SetValueWithoutNotify(GameSettings.MusicVolume);
            sfxSlider?.SetValueWithoutNotify(GameSettings.SfxVolume);
            fullscreenToggle?.SetValueWithoutNotify(GameSettings.IsFullscreen);

            SetPercent(masterValue, GameSettings.MasterVolume);
            SetPercent(musicValue, GameSettings.MusicVolume);
            SetPercent(sfxValue, GameSettings.SfxVolume);
            SetOnOff(fullscreenValue, GameSettings.IsFullscreen);
        }

        /// <summary>Writes settings to disk. Call when the panel closes.</summary>
        public void SaveSettings() => GameSettings.Save();

        #endregion

        #region Helpers

        private static void SetupSlider(SliderInt slider)
        {
            if (slider == null) return;
            slider.lowValue = 0;
            slider.highValue = GameSettings.MaxVolumeStep;
            slider.pageSize = 1; // one step per press / click on the track
        }

        private static void SetPercent(Label label, int step)
        {
            if (label != null) label.text = $"{step * 100 / GameSettings.MaxVolumeStep}%";
        }

        private static void SetOnOff(Label label, bool on)
        {
            if (label != null) label.text = on ? "On" : "Off";
        }

        #endregion
    }
}
