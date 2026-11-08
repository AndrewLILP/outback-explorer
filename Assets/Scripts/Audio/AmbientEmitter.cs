// AmbientEmitter.cs
// Location: Assets/Scripts/Audio/AmbientEmitter.cs
//
// A 3D ambience loop placed in the world (birds in a stand of trees, frogs by
// the lake). It gets louder as the camera approaches and fades out past its
// Max Distance - so driving past a patch of bush sounds like driving past it.
//
// The AudioSource does the real work; this script only fixes the things that
// make several copies of the same long loop sound fake:
//   - Random start point: two emitters using the same clip don't play in sync.
//   - Tiny pitch variation: copies don't "phase" (that hollow, swirly sound).
//   - Doppler OFF: Unity bends pitch for moving listeners - at car speed the
//     birds would warp like a passing siren.
//
// Set on the AudioSource in the Inspector:
//   Output -> MainMixer/Ambience/Birds, Loop on, Play On Awake OFF (we start it),
//   Volume Rolloff: Linear, Min Distance ~20, Max Distance ~80 (tune by ear).

using UnityEngine;

namespace RelaxingDrive.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class AmbientEmitter : MonoBehaviour
    {
        [Tooltip("Random pitch offset (+/-). 0.03 = 3%, too small to hear as 'different'.")]
        [SerializeField, Range(0f, 0.1f)] private float pitchVariation = 0.03f;

        [Tooltip("How wide the sound feels up close (0 = a point, 180 = all around you).")]
        [SerializeField, Range(0f, 360f)] private float spread = 120f;

        private void Start()
        {
            AudioSource source = GetComponent<AudioSource>();

            if (source.clip == null)
            {
                Debug.LogWarning($"[AmbientEmitter] '{name}' has no clip assigned.", this);
                return;
            }

            source.loop = true;
            source.spatialBlend = 1f;   // fully 3D
            source.dopplerLevel = 0f;   // no pitch warping at speed
            source.spread = spread;
            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);

            source.Play();
            // Jump to a random point AFTER Play() - the most reliable order for
            // streamed clips.
            source.timeSamples = Random.Range(0, source.clip.samples);
        }
    }
}