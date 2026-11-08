// CarRadio.cs
// Location: Assets/Scripts/Audio/CarRadio.cs
//
// Plays music tracks with gaps of silence between them (birds only), so a small
// playlist never feels repetitive.
//
// PERSISTENT: survives scene loads (DontDestroyOnLoad). The pause menu REPLACES
// the gameplay scene, so a normal music player would stop on every pause and
// restart the track on every resume. This one keeps playing through the pause
// menu (ducked by the Paused snapshot) and carries on when you resume.
//
// Singleton guard: the gameplay scene reloads after each pause, which would
// create a second radio - the new one sees the old one and destroys itself.
//
// Volume is NOT controlled here (except the soft fade-in at the start of each
// track). Loudness comes from the mixer: AudioSource Output -> MainMixer/Music/Radio.
//   - Player's Music slider  -> Music group
//   - Driving / OnFoot / Paused snapshots -> Radio group
//
// Future: radio stations = several playlists (e.g. ScriptableObjects) to swap.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RelaxingDrive.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class CarRadio : MonoBehaviour
    {
        [Header("Playlist")]
        [SerializeField] private List<AudioClip> tracks = new List<AudioClip>();
        [Tooltip("Random order, never the same track twice in a row (when there's more than one).")]
        [SerializeField] private bool shuffle = true;

        [Header("Timing (real seconds)")]
        [Tooltip("Silence after the game loads before the first track - lets the birds set the mood.")]
        [SerializeField] private float firstTrackDelay = 4f;
        [Tooltip("Random silence between tracks (min, max).")]
        [SerializeField] private Vector2 silenceBetweenTracks = new Vector2(60f, 120f);
        [Tooltip("Each track fades in over this many seconds.")]
        [SerializeField] private float fadeInSeconds = 3f;

        [SerializeField] private bool showDebugLogs = false;

        private static CarRadio instance;

        private AudioSource source;
        private int lastTrackIndex = -1;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject); // a radio from an earlier scene is already playing
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject); // must be a ROOT object for this to work

            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;        // we handle repeats ourselves, with gaps
            source.spatialBlend = 0f;   // 2D: same volume wherever the camera is
        }

        private void Start()
        {
            if (instance != this) return;

            if (tracks.Count == 0)
            {
                Debug.LogWarning("[CarRadio] No tracks assigned - radio is silent.");
                return;
            }

            StartCoroutine(PlayLoop());
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Play a track -> wait for it to end -> silence -> repeat, forever.</summary>
        private IEnumerator PlayLoop()
        {
            // Realtime waits: unaffected by Time.timeScale, so pausing can't freeze the radio.
            yield return new WaitForSecondsRealtime(firstTrackDelay);

            while (true)
            {
                AudioClip clip = PickNextTrack();
                source.clip = clip;
                source.volume = 0f;
                source.Play();
                Log($"Playing '{clip.name}'");

                yield return FadeIn();

                // Wait for the track to finish. The isFocused check stops an
                // alt-tab (where Unity pauses audio) being mistaken for "track ended".
                yield return new WaitUntil(() => !source.isPlaying && Application.isFocused);

                float silence = Random.Range(silenceBetweenTracks.x, silenceBetweenTracks.y);
                Log($"Track ended - {silence:F0}s of silence");
                yield return new WaitForSecondsRealtime(silence);
            }
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < fadeInSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Clamp01(elapsed / fadeInSeconds);
                yield return null;
            }
            source.volume = 1f;
        }

        private AudioClip PickNextTrack()
        {
            if (tracks.Count == 1) return tracks[0];

            int index;
            if (shuffle)
            {
                do { index = Random.Range(0, tracks.Count); }
                while (index == lastTrackIndex);
            }
            else
            {
                index = (lastTrackIndex + 1) % tracks.Count;
            }

            lastTrackIndex = index;
            return tracks[index];
        }

        private void Log(string message)
        {
            if (showDebugLogs) Debug.Log($"[CarRadio] {message}");
        }
    }
}