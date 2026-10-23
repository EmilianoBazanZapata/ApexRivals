using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.Audio.Runtime
{
    /// <summary>
    /// Engine sound for one car: the shared engine loop pitched by the car's RPM, a little louder as the revs rise,
    /// in 3D so rivals are heard where they are. Routed through the Vehicles bus; silent while the game is paused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleEngineAudio : MonoBehaviour
    {
        private const float MinimumPitch = 0.3f;
        private const float MaximumPitch = 3f;
        private const float PitchResponse = 12f;
        private const float IdleLevel = 0.55f;
        private const float RedlineRpm = 6500f;

        [SerializeField, Range(0f, 1f)] private float level = 0.8f;

        private IVehicleTelemetry _telemetry;
        private AudioSource _source;
        private GameAudio _audio;
        private float _pitch = 1f;

        private void Awake()
        {
            _telemetry = GetComponentInChildren<IVehicleTelemetry>();
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Logarithmic;
            _source.minDistance = 8f;
            _source.maxDistance = 250f;
            _source.dopplerLevel = 0.4f;
            _source.volume = 0f;
        }

        private void Start()
        {
            _audio = GameAudio.Current;
            if (_audio == null || _audio.EngineClip == null || _telemetry == null)
            {
                enabled = false;
                return;
            }

            _source.clip = _audio.EngineClip;
            // Start each car at a different point of the loop so a full grid does not phase together.
            _source.timeSamples = Random.Range(0, _audio.EngineClip.samples);
            _source.Play();
        }

        private void Update()
        {
            var paused = Time.timeScale <= 0f;
            if (paused != !_source.isPlaying)
            {
                if (paused) _source.Pause();
                else _source.UnPause();
            }

            if (paused)
            {
                return;
            }

            var rpm = Mathf.Max(0f, _telemetry.EngineRpm);
            var targetPitch = EnginePitch(rpm);
            _pitch = Mathf.Lerp(_pitch, targetPitch, 1f - Mathf.Exp(-PitchResponse * Time.deltaTime));
            _source.pitch = _pitch;
            _source.volume = level * EngineLoudness(rpm) * _audio.Mixer.GetVolume(AudioBus.Vehicles);
        }

        /// <summary>Playback pitch that makes the engine loop sound at <paramref name="rpm"/>.</summary>
        public static float EnginePitch(float rpm)
        {
            return Mathf.Clamp(rpm / ProceduralAudio.EngineLoopRpm, MinimumPitch, MaximumPitch);
        }

        /// <summary>Idle sits at <c>0.55</c> and the sound swells to full level at the redline.</summary>
        public static float EngineLoudness(float rpm)
        {
            return Mathf.Lerp(IdleLevel, 1f, Mathf.Clamp01(rpm / RedlineRpm));
        }

        private void OnDestroy()
        {
            if (_source != null)
            {
                _source.Stop();
            }
        }
    }
}
