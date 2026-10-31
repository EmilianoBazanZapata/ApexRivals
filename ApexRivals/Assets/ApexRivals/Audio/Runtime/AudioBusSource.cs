using UnityEngine;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Routes the AudioSource next to it through an <see cref="AudioBus"/>: its volume is always
    /// <c>level × Intensity × bus volume</c>, so the Settings sliders reach every sound. With the source's output set
    /// to the bus's AudioMixer group the mixer applies the bus volume instead. Everything else about the
    /// source (clip, loop, 3D settings, priority) is set up on the AudioSource itself in the prefab or scene. Gameplay
    /// only drives <see cref="Intensity"/> (how loud the sound is right now, e.g. how hard the tyres slide).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class AudioBusSource : MonoBehaviour
    {
        [SerializeField] private AudioBus bus = AudioBus.Vehicles;
        [SerializeField, Range(0f, 1f), Tooltip("Mix level of this sound before the bus and master sliders.")] private float level = 1f;
        [SerializeField, Tooltip("Pause while the game is paused (time scale 0). Leave off for menu sounds and for sources whose owner already pauses them.")]
        private bool pauseWithGame;

        private AudioSource _source;
        private float _intensity = 1f;
        private bool _paused;

        public AudioSource Source
        {
            get
            {
                if (_source == null) _source = GetComponent<AudioSource>();
                return _source;
            }
        }

        public AudioBus Bus => bus;
        public float Level => level;

        /// <summary>How loud the sound is right now (1 = its mix level). Applied at once.</summary>
        public float Intensity
        {
            get => _intensity;
            set
            {
                _intensity = Mathf.Max(0f, value);
                Apply();
            }
        }

        /// <summary>
        /// The volume the source plays at: level × intensity, times the bus volume unless the source plays through an
        /// AudioMixer group (the mixer applies the bus volume then).
        /// </summary>
        public float Volume => level * _intensity * (Source != null && Source.outputAudioMixerGroup != null ? 1f : BusGain(bus));

        /// <summary>Linear gain of <paramref name="bus"/> under the current settings (1 before the game audio exists).</summary>
        public static float BusGain(AudioBus bus)
        {
            var audio = GameAudio.Current;
            return audio != null && audio.Mixer != null ? audio.Mixer.GetVolume(bus) : 1f;
        }

        /// <summary>Sets the intensity of a source that may or may not be routed (an unrouted one gets it as its volume).</summary>
        public static void SetIntensity(AudioSource source, float intensity)
        {
            if (source == null) return;
            var routed = source.GetComponent<AudioBusSource>();
            if (routed != null) routed.Intensity = intensity;
            else source.volume = intensity;
        }

        public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            Apply();
            Source.PlayOneShot(clip, volumeScale);
        }

        private void Awake()
        {
            Apply();
        }

        private void LateUpdate()
        {
            if (pauseWithGame)
            {
                var paused = Time.timeScale <= 0f;
                if (paused != _paused)
                {
                    _paused = paused;
                    if (paused) Source.Pause();
                    else Source.UnPause();
                }
            }

            Apply();
        }

        private void Apply()
        {
            if (Source != null) Source.volume = Volume;
        }
    }
}
