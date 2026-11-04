using UnityEngine;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Plays an <see cref="EngineSoundProfile"/> live through this object's AudioSource, generated on the audio thread
    /// by <see cref="EngineSynthCore"/>. Set <see cref="Rpm"/>, <see cref="Throttle"/> and <see cref="Running"/> from
    /// gameplay every frame; the core smooths them. Keeps the last output samples for a scope display.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProceduralEngineSynth : MonoBehaviour
    {
        public const int ScopeLength = 2048;

        [SerializeField] private EngineSoundProfile profile;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        private readonly float[] _scope = new float[ScopeLength];
        private EngineSynthCore _core;
        private float[] _mono = new float[4096];
        private AudioSource _source;
        private volatile float _rpm;
        private volatile float _throttle;
        private volatile float _strain;
        private volatile bool _running = true;
        private volatile float _volume;
        private volatile float _heardGain;
        private volatile EngineSynthCore _pendingCore;
        private int _scopeWrite;

        public EngineSoundProfile Profile => profile;
        public float Rpm { get => _rpm; set => _rpm = value; }
        public float Throttle { get => _throttle; set => _throttle = value; }
        /// <summary>How hard the engine is pulling for revs (0-1); see <see cref="EngineSynthCore.Strain"/>.</summary>
        public float Strain { get => _strain; set => _strain = value; }
        public bool Running { get => _running; set => _running = value; }
        public float Volume { get => volume; set => volume = Mathf.Clamp01(value); }
        public int LastFiringIndex => _core != null ? _core.LastFiringIndex : 0;
        public int FiringCount => _core != null ? _core.FiringCount : 0;

        /// <summary>Average gain Unity applied to this source last buffer (volume × distance × pan); for diagnostics.</summary>
        public float HeardGain => _heardGain;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            // Unity applies the source's volume, 3D distance rolloff and panning before this filter runs. The source
            // therefore plays a constant 1.0 "carrier" and the synth multiplies its output by what arrives: the result
            // carries all of those gains. (Overwriting the samples would throw them away and every engine would sound
            // as loud far away as up close.)
            var rate = AudioSettings.outputSampleRate;
            var carrier = new float[rate];
            for (var i = 0; i < carrier.Length; i++) carrier[i] = 1f;
            var clip = AudioClip.Create("EngineSynthCarrier", rate, 1, rate, false);
            clip.SetData(carrier, 0);
            _source.clip = clip;

            SetProfile(profile);
        }

        private void OnEnable()
        {
            if (_source != null && !_source.isPlaying) _source.Play();
        }

        private void Update()
        {
            _volume = volume;
        }

        /// <summary>Switches engine; the audio thread picks the new one up on its next buffer.</summary>
        public void SetProfile(EngineSoundProfile newProfile)
        {
            profile = newProfile;
            if (profile == null) return;
            var core = new EngineSynthCore(AudioSettings.outputSampleRate, (uint)GetInstanceID() * 2654435761u);
            core.Configure(profile);
            _pendingCore = core;
            if (_core == null) _core = core;
        }

        /// <summary>Copies the latest output (oldest first) into <paramref name="destination"/>.</summary>
        public void CopyScope(float[] destination)
        {
            var start = _scopeWrite;
            var length = Mathf.Min(destination.Length, ScopeLength);
            for (var i = 0; i < length; i++)
            {
                destination[i] = _scope[(start + ScopeLength - length + i) % ScopeLength];
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            var pending = _pendingCore;
            if (pending != null)
            {
                _core = pending;
                _pendingCore = null;
            }

            var core = _core;
            if (core == null || channels <= 0) return;

            var frames = data.Length / channels;
            if (_mono.Length < frames) _mono = new float[frames];
            core.Strain = _strain;
            core.Render(_mono, 0, frames, _rpm, _throttle, _running);
            var gain = _volume;
            var carrierSum = 0f;
            for (var i = 0; i < frames; i++)
            {
                var sample = _mono[i] * gain;
                // Each channel keeps the gain Unity gave the carrier there (volume x distance x pan).
                for (var c = 0; c < channels; c++)
                {
                    var index = i * channels + c;
                    carrierSum += data[index];
                    data[index] *= sample;
                }

                _scope[_scopeWrite] = sample;
                _scopeWrite = (_scopeWrite + 1) % ScopeLength;
            }

            _heardGain = carrierSum / Mathf.Max(1, data.Length);
        }
    }
}
