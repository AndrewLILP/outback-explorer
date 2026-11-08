// AudioStateMixer.cs
// Location: Assets/Scripts/Audio/AudioStateMixer.cs
//
// Blends MainMixer between SNAPSHOTS to match what the player is doing.
//
// What's a snapshot? A saved set of mixer fader positions (like a preset on a
// real mixing desk). TransitionTo(seconds) glides every fader from where it is
// now to the snapshot's values - so "car off when you get out" is a smooth fade,
// not a hard cut.
//
// Observer pattern: this listens to PlayerStateManager.OnStateChanged.
// PlayerStateManager knows nothing about audio, and the audio knows nothing
// about how entering/exiting the car works.
//
// Use in TWO scenes:
//   - Gameplay scene: Start Snapshot = Driving, assign Driving + OnFoot.
//   - PauseMenuScene: Start Snapshot = Paused, leave the others empty
//     (no PlayerStateManager there - that's fine).
//
// Later: a Caring snapshot (Care System) can be added the same way.

using UnityEngine;
using UnityEngine.Audio;
using RelaxingDrive.Player;

namespace RelaxingDrive.Audio
{
    public class AudioStateMixer : MonoBehaviour
    {
        [Header("Snapshots (from MainMixer)")]
        [Tooltip("Faded to when this scene starts. Gameplay: Driving. Pause scene: Paused.")]
        [SerializeField] private AudioMixerSnapshot startSnapshot;
        [SerializeField] private AudioMixerSnapshot drivingSnapshot;
        [SerializeField] private AudioMixerSnapshot onFootSnapshot;

        [Header("Blend times (seconds)")]
        [Tooltip("Fade-in when the scene loads - softens the RCC engine start.")]
        [SerializeField] private float sceneStartFade = 2f;
        [Tooltip("Blend when getting in or out of the car.")]
        [SerializeField] private float stateChangeFade = 1f;

        [Header("References")]
        [Tooltip("Optional - found automatically if left empty.")]
        [SerializeField] private PlayerStateManager playerStateManager;
        [SerializeField] private bool showDebugLogs = false;

        private void Start()
        {
            if (startSnapshot != null)
            {
                startSnapshot.TransitionTo(sceneStartFade);
                Log($"Scene start -> {startSnapshot.name} over {sceneStartFade}s");
            }

            if (playerStateManager == null)
                playerStateManager = FindFirstObjectByType<PlayerStateManager>();

            // No state manager (e.g. PauseMenuScene) = just the start snapshot.
            if (playerStateManager != null)
                playerStateManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            // Always unsubscribe - the scene unloads on every pause.
            if (playerStateManager != null)
                playerStateManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(PlayerState newState)
        {
            AudioMixerSnapshot target =
                playerStateManager.IsDriving ? drivingSnapshot :
                playerStateManager.IsOnFoot ? onFootSnapshot :
                null;

            if (target == null) return;

            target.TransitionTo(stateChangeFade);
            Log($"State changed -> {target.name} over {stateChangeFade}s");
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[AudioStateMixer] {message}");
        }
    }
}