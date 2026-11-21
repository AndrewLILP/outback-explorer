// ResumeSpawnHandoff.cs
// Location: Assets/Scripts/Core/ResumeSpawnHandoff.cs
//
// Carries the car's position from the gameplay scene, through PauseMenuScene,
// and back into the gameplay scene when the player resumes.
//
// WHY A STATIC CLASS?
// Pausing REPLACES the gameplay scene (see PauseFlowManager), so every
// MonoBehaviour in it is destroyed. A static class isn't attached to any scene,
// so it lives for the whole app session - exactly the lifetime this needs.
// It is NOT written to disk; it's a short-lived "note passed between scenes".
// (Saving position to the save file is a separate job for GameSaveManager.)
//
// LATER: when the time-of-day system exists, add its current time here too
// (capture on pause, restore on resume) so the time doesn't change.
//
// This class only HOLDS data. ResumeSpawnController (gameplay scene) captures
// and applies it; PauseMenuSceneController sets the player's choice.

using UnityEngine;

namespace RelaxingDrive.Core
{
    /// <summary>Where the player wants to continue from after the pause menu.</summary>
    public enum ResumeSpawnMode
    {
        CurrentPosition,   // put the car back where it was when they paused
        StartPosition      // leave the scene's normal start spawn alone
    }

    public static class ResumeSpawnHandoff
    {
        public static bool HasSnapshot { get; private set; }
        public static Vector3 Position { get; private set; }
        public static Quaternion Rotation { get; private set; }

        // Speed limit state at the moment of pausing. Gates toggle the limit as
        // the player drives past them; a fresh scene load would reset it to the
        // spawn default, which is wrong if they paused out on the highway.
        public static bool SpeedLimitEnabled { get; private set; }
        public static float SpeedLimitKmh { get; private set; }

        public static ResumeSpawnMode Mode { get; private set; } = ResumeSpawnMode.StartPosition;

        /// <summary>True when the next gameplay load should move the car back.</summary>
        public static bool ShouldRestore => HasSnapshot && Mode == ResumeSpawnMode.CurrentPosition;

        /// <summary>Called on pause. Defaults to "where I left off" - the cosy choice.</summary>
        public static void Capture(Vector3 position, Quaternion rotation, bool limitEnabled, float limitKmh)
        {
            Position = position;
            Rotation = rotation;
            SpeedLimitEnabled = limitEnabled;
            SpeedLimitKmh = limitKmh;
            HasSnapshot = true;
            Mode = ResumeSpawnMode.CurrentPosition;
        }

        /// <summary>Called by the pause menu when the player picks an option.</summary>
        public static void SetMode(ResumeSpawnMode mode) => Mode = mode;

        /// <summary>Forget the snapshot (after it's used, or on New Game).</summary>
        public static void Clear()
        {
            HasSnapshot = false;
            Mode = ResumeSpawnMode.StartPosition;
        }

        // Statics survive between Play sessions in the Editor if "Enter Play Mode
        // Options" has domain reload turned off. This resets them on every Play,
        // so an old snapshot can never leak into a new test run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();
    }
}