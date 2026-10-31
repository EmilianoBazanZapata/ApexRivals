using ApexRivals.Settings.Runtime;
using UnityEngine;
using UnityEngine.Audio;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Lives in the Bootstrap scene for the whole session: owns the <see cref="AudioBusMixer"/> that follows the audio
    /// settings, drives the AudioMixer's group volumes from it, starts the music once settings are bound and plays the
    /// UI cues. The music and UI sources are child AudioSources set up in the scene (see ApexRivals/Audio/Set Up Audio
    /// Sources); every sound plays through the mixer group of its bus.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        public const string MasterVolumeParameter = "MasterVolume";
        public const string MusicVolumeParameter = "MusicVolume";
        public const string UiVolumeParameter = "UiVolume";
        public const string VehiclesVolumeParameter = "VehiclesVolume";
        public const string AmbienceVolumeParameter = "AmbienceVolume";
        [SerializeField, Tooltip("Exposed AudioMixer parameter for the Ambience group volume.")]
        private string ambienceVolumeParameter = AmbienceVolumeParameter;

        [SerializeField, Tooltip("Master > Music, UI, Vehicles, Ambience, with the group volumes exposed.")] private AudioMixer mixer;
        [SerializeField, Tooltip("Looping music, routed through the Music bus.")] private AudioBusSource music;
        [SerializeField, Tooltip("Plays the UI cues, routed through the UI bus.")] private AudioBusSource uiCues;
        [SerializeField, Tooltip("One clip per UiSound: Move, Confirm, Back.")] private AudioClip[] uiClips = new AudioClip[3];
        [SerializeField, Min(0f), Tooltip("Minimum seconds between two focus-move cues, so fast scrolling does not buzz.")] private float moveCueInterval = 0.05f;

        private float _lastUiCueTime = float.NegativeInfinity;

        public static GameAudio Current { get; private set; }

        public AudioBusMixer Mixer { get; private set; }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }

            Current = this;
            // Until settings are bound the bus plays everything at full slider positions.
            UseMixer(new AudioBusMixer(null));
        }

        private void Start()
        {
            // AudioMixer parameters set during Awake do not stick; apply them again once the scene is running.
            ApplyMixerVolumes();
        }

        private void OnEnable()
        {
            UiAudioEvents.Played += PlayUi;
        }

        private void OnDisable()
        {
            UiAudioEvents.Played -= PlayUi;
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            ReleaseMixer();
        }

        /// <summary>Follows the player's audio settings (including the live slider preview) and starts the music.</summary>
        public void Bind(IAudioPreferenceSource preferences)
        {
            UseMixer(new AudioBusMixer(preferences));
            if (music != null && !music.Source.isPlaying)
            {
                music.Source.Play();
            }
        }

        public void PlayUi(UiSound sound)
        {
            var index = (int)sound;
            if (uiCues == null || uiClips == null || index >= uiClips.Length)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (sound == UiSound.Move && now - _lastUiCueTime < moveCueInterval)
            {
                return;
            }

            _lastUiCueTime = now;
            uiCues.PlayOneShot(uiClips[index]);
        }

        /// <summary>Linear gain to mixer decibels; silence is the mixer's floor of -80 dB.</summary>
        public static float ToDecibels(float gain)
        {
            return gain <= 0.0001f ? -80f : 20f * Mathf.Log10(gain);
        }

        private void UseMixer(AudioBusMixer busMixer)
        {
            ReleaseMixer();
            Mixer = busMixer;
            Mixer.Changed += ApplyMixerVolumes;
            ApplyMixerVolumes();
        }

        private void ReleaseMixer()
        {
            if (Mixer == null) return;
            Mixer.Changed -= ApplyMixerVolumes;
            Mixer.Dispose();
            Mixer = null;
        }

        private void ApplyMixerVolumes()
        {
            if (mixer == null || Mixer == null) return;
            var preferences = Mixer.Preferences;
            mixer.SetFloat(MasterVolumeParameter, ToDecibels(AudioBusMixer.ToGain(preferences.MasterVolume)));
            mixer.SetFloat(MusicVolumeParameter, ToDecibels(AudioBusMixer.ToGain(preferences.MusicVolume)));
            mixer.SetFloat(UiVolumeParameter, ToDecibels(AudioBusMixer.ToGain(preferences.UiVolume)));
            mixer.SetFloat(VehiclesVolumeParameter, ToDecibels(AudioBusMixer.ToGain(preferences.VehicleVolume)));
            mixer.SetFloat(ambienceVolumeParameter, ToDecibels(AudioBusMixer.ToGain(preferences.AmbienceVolume)));
        }
    }
}
