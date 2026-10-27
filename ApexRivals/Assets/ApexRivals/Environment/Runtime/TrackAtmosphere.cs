using ApexRivals.Audio.Runtime;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using UnityEngine;

namespace ApexRivals.Environment.Runtime
{
    /// <summary>One scene owner for crowd/flag motion and ambient audio. No per-spectator behaviours.</summary>
    [DisallowMultipleComponent]
    public sealed class TrackAtmosphere : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField] private RaceCoordinator coordinator;
        [SerializeField] private RaceSessionSceneInstaller sessionInstaller;
        [SerializeField] private Renderer[] animatedRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private AudioSource[] crowdSources = System.Array.Empty<AudioSource>();
        [SerializeField] private AudioSource weatherSource;
        [SerializeField] private int spectatorCount;
        [SerializeField, Range(0, 1)] private float crowdLevel = 0.14f;
        [SerializeField, Range(0, 1)] private float weatherLevel = 0.08f;
        private ApplicationContext _context;
        private RaceSessionCoordinator _session;
        private MaterialPropertyBlock _properties;
        private bool _suspended;
        private float _clock;
        private float _cheer;
        private bool _weatherDriven;
        private float _weatherWind = 1;
        private static readonly int ClockId = Shader.PropertyToID("_EnvironmentTime");
        private static readonly int CheerId = Shader.PropertyToID("_Cheer");

        public int SpectatorCount => spectatorCount;
        public AudioSource[] CrowdSources => crowdSources;
        public AudioSource WeatherSource => weatherSource;
        public float AnimationClock => _clock;
        public float Cheer => _cheer;
        public bool IsInstalled => _context != null;

        public void SetWeatherDriven(bool driven)
        {
            _weatherDriven = driven;
            if (driven && weatherSource != null) weatherSource.Stop();
        }

        public void SetWeatherWind(float strength) => _weatherWind = Mathf.Clamp01(strength);

        public void Install(ApplicationContext context)
        {
            Uninstall();
            if (context == null || coordinator == null || sessionInstaller == null || sessionInstaller.Session == null) return;
            _context = context;
            _session = sessionInstaller.Session;
            _properties = new MaterialPropertyBlock();
            _clock = _cheer = 0;
            coordinator.RaceStarted += OnStarted;
            coordinator.LapCompleted += OnLap;
            _session.ResultsReady += OnResults;
            for (var i = 0; i < crowdSources.Length; i++) StartLoop(crowdSources[i], i * 0.173f);
            StartLoop(weatherSource, 0);
            _suspended = false;
            Update(); // Loading/transition may still hold the clock at installation.
        }

        private static void StartLoop(AudioSource source, float phase)
        {
            if (source == null || source.clip == null) return;
            source.timeSamples = Mathf.FloorToInt((phase % 1f) * source.clip.samples);
            source.Play();
        }

        private void Update()
        {
            if (_context == null) return;
            var results = _session.State == RaceSessionLifecycleState.ShowingResults;
            var suspended = (Time.timeScale <= 0 && !results) || _context.Navigation.State != ApplicationState.Race ||
                (_context.LoadingPresentation?.IsVisible ?? false);
            if (suspended != _suspended)
            {
                _suspended = suspended;
                foreach (var source in crowdSources) Suspend(source, suspended);
                Suspend(weatherSource, suspended);
            }
            if (!suspended)
            {
                var delta = results ? Time.unscaledDeltaTime : Time.deltaTime;
                _clock += delta;
                _cheer = Mathf.MoveTowards(_cheer, 0, delta * 0.28f);
            }
            var master = _context.SettingsService != null ? _context.SettingsService.AudiblePreferences.MasterVolume
                : GameAudio.Current != null ? GameAudio.Current.Mixer.Preferences.MasterVolume : 1f;
            var gain = AudioBusMixer.ToGain(master);
            foreach (var source in crowdSources)
                if (source != null) source.volume = crowdLevel * gain * (1 + _cheer * 0.65f);
            if (weatherSource != null) weatherSource.volume = _weatherDriven ? 0 : weatherLevel * gain;
            ApplyMotion();
        }

        private static void Suspend(AudioSource source, bool suspended)
        {
            if (source == null) return;
            if (suspended) source.Pause(); else source.UnPause();
        }

        private void ApplyMotion()
        {
            if (_properties == null || animatedRenderers == null) return;
            _properties.SetFloat(ClockId, _clock);
            _properties.SetFloat(CheerId, _cheer);
            _properties.SetFloat("_WindStrength", _weatherWind);
            foreach (var renderer in animatedRenderers) if (renderer != null) renderer.SetPropertyBlock(_properties);
        }

        private void OnStarted(RaceStartedEvent change) => _cheer = 1f;
        private void OnLap(LapCompletedEvent change)
        {
            if (coordinator.PlayerParticipant != null && change.RacerId == coordinator.PlayerParticipant.RacerId) _cheer = 1f;
        }
        private void OnResults(RaceSessionResult result) => _cheer = result.FinalPosition == 1 ? 1f : 0.5f;

        public void Uninstall()
        {
            if (_session != null)
            {
                if (coordinator != null) { coordinator.RaceStarted -= OnStarted; coordinator.LapCompleted -= OnLap; }
                _session.ResultsReady -= OnResults;
            }
            _session = null;
            _context = null;
            _clock = _cheer = 0;
            if (crowdSources != null) foreach (var source in crowdSources) if (source != null) source.Stop();
            if (weatherSource != null) weatherSource.Stop();
            ApplyMotion();
        }
        private void OnDisable() => Uninstall();
        private void OnDestroy() => Uninstall();
    }
}
