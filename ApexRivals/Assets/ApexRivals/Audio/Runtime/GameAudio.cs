using ApexRivals.Settings.Runtime;
using UnityEngine;
using UnityEngine.Audio;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Lives in the Bootstrap scene for the whole session: owns the <see cref="AudioBusMixer"/> that follows the audio
    /// settings, drives the AudioMixer's group volumes from it, starts the music once settings are bound (cycling through the
    /// soundtrack playlist when one is assigned) and plays the
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
        [SerializeField, Tooltip("Soundtrack played one after another, forever. Empty keeps the music source's own looping clip.")] private AudioClip[] playlist = new AudioClip[0];
        [SerializeField, Tooltip("Play the soundtrack in a new random order each time round, never repeating the track that just ended.")] private bool shufflePlaylist = true;
        [SerializeField, Tooltip("Plays the UI cues, routed through the UI bus.")] private AudioBusSource uiCues;
        [SerializeField, Tooltip("One clip per UiSound: Move, Confirm, Back.")] private AudioClip[] uiClips = new AudioClip[3];
        [SerializeField, Min(0f), Tooltip("Minimum seconds between two focus-move cues, so fast scrolling does not buzz.")] private float moveCueInterval = 0.05f;

        private float _lastUiCueTime = float.NegativeInfinity;
        private readonly System.Collections.Generic.List<int> _playOrder = new System.Collections.Generic.List<int>();
        private int _playOrderIndex = -1;
        private int _lastTrack = -1;
        private bool _playlistRunning;
        private bool _musicBound;
        private bool _trackAudible;
        private float _trackRequestedAt;
        private const float TrackStartTimeout = 5f;
        private readonly System.Collections.Generic.HashSet<object> _musicSilencers = new System.Collections.Generic.HashSet<object>();

        /// <summary>True while any owner (for example the loading screen) keeps the music silent.</summary>
        public bool MusicSilenced => _musicSilencers.Count > 0;

        public static GameAudio Current { get; private set; }

        public AudioBusMixer Mixer { get; private set; }

        /// <summary>Raised with the track's name each time a soundtrack track starts.</summary>
        public event System.Action<string> TrackStarted;

        /// <summary>Name of the soundtrack track playing now; empty when no playlist is running.</summary>
        public string CurrentTrackName { get; private set; } = string.Empty;

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
            _musicBound = true;
            if (music == null || music.Source.isPlaying)
            {
                return;
            }

            if (HasPlaylist())
            {
                _playlistRunning = true;
                if (!MusicSilenced) PlayNextTrack();
            }
            else if (!MusicSilenced)
            {
                music.Source.Play();
            }
        }

        /// <summary>
        /// Stops the music while <paramref name="owner"/> asks for silence (loading screens). Once every owner has
        /// released it the soundtrack resumes with a new track, announced like any other.
        /// </summary>
        public void SetMusicSilenced(object owner, bool silenced)
        {
            if (owner == null) return;
            var wasSilenced = MusicSilenced;
            if (silenced) _musicSilencers.Add(owner);
            else _musicSilencers.Remove(owner);
            if (music == null || wasSilenced == MusicSilenced) return;

            if (MusicSilenced)
            {
                music.Source.Stop();
                CurrentTrackName = string.Empty;
            }
            else if (_playlistRunning)
            {
                PlayNextTrack();
            }
            else if (_musicBound && music.Source.clip != null)
            {
                music.Source.Play();
            }
        }

        private void Update()
        {
            // Silence is not the end of a track: never advance the playlist while the music is held.
            if (!_playlistRunning || music == null || MusicSilenced) return;
            var source = music.Source;
            if (source.isPlaying)
            {
                _trackAudible = true;
                return;
            }

            // A streamed track needs a few frames before it reports playing; do not take that for its end.
            if (!_trackAudible && Time.unscaledTime - _trackRequestedAt < TrackStartTimeout) return;

            // A track that ran out stops at its end (or rewinds); a paused one stops mid-track and is left alone.
            if (TrackEnded(source)) PlayNextTrack();
        }

        private static bool TrackEnded(AudioSource source)
        {
            return source.clip == null || source.timeSamples == 0 || source.time >= source.clip.length - 0.25f;
        }

        private bool HasPlaylist()
        {
            if (playlist == null) return false;
            foreach (var clip in playlist)
            {
                if (clip != null) return true;
            }

            return false;
        }

        private void PlayNextTrack()
        {
            if (_playOrderIndex + 1 >= _playOrder.Count) BuildPlayOrder();
            if (_playOrder.Count == 0) return;
            _playOrderIndex++;
            _lastTrack = _playOrder[_playOrderIndex];
            var clip = playlist[_lastTrack];
            var source = music.Source;
            source.loop = false;
            source.clip = clip;
            source.Play();
            _trackAudible = false;
            _trackRequestedAt = Time.unscaledTime;
            CurrentTrackName = clip.name;
            TrackStarted?.Invoke(CurrentTrackName);
        }

        private void BuildPlayOrder()
        {
            _playOrder.Clear();
            _playOrderIndex = -1;
            for (var index = 0; index < playlist.Length; index++)
            {
                if (playlist[index] != null) _playOrder.Add(index);
            }

            if (!shufflePlaylist || _playOrder.Count < 2) return;
            for (var index = _playOrder.Count - 1; index > 0; index--)
            {
                var swap = Random.Range(0, index + 1);
                (_playOrder[index], _playOrder[swap]) = (_playOrder[swap], _playOrder[index]);
            }

            // A new round never opens with the track that just ended.
            if (_playOrder[0] == _lastTrack)
            {
                (_playOrder[0], _playOrder[_playOrder.Count - 1]) = (_playOrder[_playOrder.Count - 1], _playOrder[0]);
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
