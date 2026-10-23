using ApexRivals.Settings.Runtime;
using UnityEngine;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Lives in the Bootstrap scene for the whole session: owns the <see cref="AudioBusMixer"/>, plays the music loop
    /// and the UI cues, and shares the engine loop with every <see cref="VehicleEngineAudio"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        private const int EffectsSampleRate = 44100;
        private const int MusicSampleRate = 22050;

        [SerializeField, Range(0f, 1f), Tooltip("Music level before the Music and Master sliders.")] private float musicLevel = 0.45f;
        [SerializeField, Range(0f, 1f), Tooltip("UI cue level before the UI and Master sliders.")] private float uiLevel = 0.7f;
        [SerializeField, Min(0f), Tooltip("Minimum seconds between two focus-move cues, so fast scrolling does not buzz.")] private float moveCueInterval = 0.05f;

        private AudioSource _musicSource;
        private AudioSource _uiSource;
        private AudioClip[] _uiClips;
        private float _lastUiCueTime = float.NegativeInfinity;

        public static GameAudio Current { get; private set; }

        public AudioBusMixer Mixer { get; private set; }
        public AudioClip EngineClip { get; private set; }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }

            Current = this;
            EngineClip = CreateClip("EngineLoop", ProceduralAudio.CreateEngineLoop(EffectsSampleRate), EffectsSampleRate);
            _uiClips = new[]
            {
                CreateClip("UiMove", ProceduralAudio.CreateUiSound(UiSound.Move, EffectsSampleRate), EffectsSampleRate),
                CreateClip("UiConfirm", ProceduralAudio.CreateUiSound(UiSound.Confirm, EffectsSampleRate), EffectsSampleRate),
                CreateClip("UiBack", ProceduralAudio.CreateUiSound(UiSound.Back, EffectsSampleRate), EffectsSampleRate)
            };

            _musicSource = CreateSource("Music");
            _musicSource.clip = CreateClip("MusicLoop", ProceduralAudio.CreateMusicLoop(MusicSampleRate), MusicSampleRate);
            _musicSource.loop = true;
            _uiSource = CreateSource("Ui");

            // Until settings are bound the bus plays everything at full slider positions.
            Mixer = new AudioBusMixer(null);
            Mixer.Changed += ApplyVolumes;
            ApplyVolumes();
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

            if (Mixer != null)
            {
                Mixer.Changed -= ApplyVolumes;
                Mixer.Dispose();
            }
        }

        /// <summary>Follows the player's audio settings (including the live slider preview) and starts the music.</summary>
        public void Bind(IAudioPreferenceSource preferences)
        {
            if (Mixer != null)
            {
                Mixer.Changed -= ApplyVolumes;
                Mixer.Dispose();
            }

            Mixer = new AudioBusMixer(preferences);
            Mixer.Changed += ApplyVolumes;
            ApplyVolumes();
            if (_musicSource != null && !_musicSource.isPlaying)
            {
                _musicSource.Play();
            }
        }

        public void PlayUi(UiSound sound)
        {
            if (_uiSource == null || _uiClips == null)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (sound == UiSound.Move && now - _lastUiCueTime < moveCueInterval)
            {
                return;
            }

            _lastUiCueTime = now;
            _uiSource.PlayOneShot(_uiClips[(int)sound]);
        }

        private void ApplyVolumes()
        {
            if (_musicSource != null) _musicSource.volume = musicLevel * Mixer.GetVolume(AudioBus.Music);
            if (_uiSource != null) _uiSource.volume = uiLevel * Mixer.GetVolume(AudioBus.Ui);
        }

        private AudioSource CreateSource(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            // Menus keep their sounds while a race is paused.
            source.ignoreListenerPause = true;
            return source;
        }

        private static AudioClip CreateClip(string name, float[] samples, int sampleRate)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
