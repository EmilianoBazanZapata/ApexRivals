using ApexRivals.Audio.Runtime;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.VFX.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ApexRivals.Environment.Runtime
{
    /// <summary>Scene-owned weather presentation. Does not change tyre grip or vehicle forces.</summary>
    [DisallowMultipleComponent]
    public sealed class RaceWeatherController : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField] private RaceSessionSceneInstaller sessionInstaller;
        [SerializeField] private RaceVfxSceneInstaller vfx;
        [SerializeField] private TrackAtmosphere atmosphere;
        [SerializeField] private Material sunnySky;
        [SerializeField] private Material stormSky;
        [SerializeField] private AudioSource rainAudio;
        [SerializeField] private AudioSource windAudio;
        private ApplicationContext _context;
        private RaceSessionCoordinator _session;
        private Material _runtimeSky;
        private Material _originalSky;
        private Light _sun;
        private float _sunIntensity, _ambientIntensity;
        private Color _sunColor, _ambientSky, _ambientEquator, _ambientGround, _fogColor;
        private AmbientMode _ambientMode;
        private bool _fogEnabled;
        private FogMode _fogMode;
        private float _fogStart, _fogEnd, _fogDensity;
        private Vector3 _windDirection;
        private static readonly string[] SkyColors = { "_HorizonColor", "_ZenithColor", "_CloudColor", "_CloudShadow", "_SunColor" };
        private static readonly string[] SkyFloats = { "_Coverage", "_CloudScale", "_SunSize" };
        public bool IsInstalled => _context != null;
        public RaceWeatherForecast Forecast { get; private set; }
        public RaceWeatherState Current { get; private set; }
        public float Elapsed { get; private set; }
        public AudioSource RainAudio => rainAudio;
        public AudioSource WindAudio => windAudio;

        public void Install(ApplicationContext context)
        {
            Uninstall();
            if (!isActiveAndEnabled || context == null || sessionInstaller == null || sessionInstaller.Session == null ||
                vfx == null || atmosphere == null || sunnySky == null || stormSky == null || rainAudio == null || windAudio == null) return;
            if (SceneManager.GetActiveScene() != gameObject.scene) return;
            _context = context;
            _session = sessionInstaller.Session;
            _originalSky = RenderSettings.skybox;
            _fogEnabled = RenderSettings.fog; _fogMode = RenderSettings.fogMode;
            _fogColor = RenderSettings.fogColor; _fogStart = RenderSettings.fogStartDistance;
            _fogEnd = RenderSettings.fogEndDistance; _fogDensity = RenderSettings.fogDensity;
            _ambientMode = RenderSettings.ambientMode; _ambientIntensity = RenderSettings.ambientIntensity;
            _ambientSky = RenderSettings.ambientSkyColor; _ambientEquator = RenderSettings.ambientEquatorColor;
            _ambientGround = RenderSettings.ambientGroundColor;
            _sun = RenderSettings.sun;
            if (_sun != null) { _sunIntensity = _sun.intensity; _sunColor = _sun.color; }
            _runtimeSky = new Material(sunnySky) { name = "RaceWeatherSky (Instance)" };
            RenderSettings.skybox = _runtimeSky;
            atmosphere.SetWeatherDriven(true);
            rainAudio.volume = windAudio.volume = 0;
            rainAudio.Play(); windAudio.Play();
            SetForecast(RaceWeatherForecast.Random(System.Guid.NewGuid().GetHashCode()));
        }

        /// <summary>Also permits a deterministic forecast for previews and reproducible scenarios.</summary>
        public void SetForecast(RaceWeatherForecast forecast)
        {
            Forecast = forecast;
            Elapsed = 0;
            var angle = (uint)forecast.Seed % 360 * Mathf.Deg2Rad;
            _windDirection = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            if (IsInstalled) Apply();
        }

        private void Update()
        {
            if (!IsInstalled) return;
            if (SceneManager.GetActiveScene() != gameObject.scene) return;
            var suspended = Time.timeScale <= 0 || _context.Navigation.State != ApplicationState.Race ||
                (_context.LoadingPresentation?.IsVisible ?? false);
            if (!suspended && _session.State == RaceSessionLifecycleState.Racing) Elapsed += Time.deltaTime;
            if (suspended) { rainAudio.Pause(); windAudio.Pause(); }
            else { rainAudio.UnPause(); windAudio.UnPause(); }
            Apply();
        }

        private void Apply()
        {
            Current = Forecast.Evaluate(Elapsed);
            var clouds = Current.Cloudiness;
            foreach (var property in SkyColors)
                _runtimeSky.SetColor(property, Color.Lerp(sunnySky.GetColor(property), stormSky.GetColor(property), clouds));
            foreach (var property in SkyFloats)
                _runtimeSky.SetFloat(property, Mathf.Lerp(sunnySky.GetFloat(property), stormSky.GetFloat(property), clouds));
            // Keep the cloud layout fixed so clouds grow and darken without popping between patterns.
            _runtimeSky.SetFloat("_Seed", sunnySky.GetFloat("_Seed"));
            RenderSettings.fog = clouds > 0.001f;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = _runtimeSky.GetColor("_HorizonColor");
            RenderSettings.fogStartDistance = Mathf.Lerp(1400, 45, clouds);
            RenderSettings.fogEndDistance = Mathf.Lerp(2400, 300, clouds);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.65f,.75f,.9f), new Color(.38f,.43f,.5f), clouds);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.55f,.62f,.7f), new Color(.32f,.37f,.42f), clouds);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.32f,.34f,.31f), new Color(.23f,.26f,.28f), clouds);
            if (_sun != null)
            {
                _sun.intensity = Mathf.Lerp(_sunIntensity, _sunIntensity * .55f, clouds);
                _sun.color = Color.Lerp(_sunColor, new Color(.7f,.78f,.88f), clouds);
            }
            vfx.SetWeather(Current.Rain, _windDirection * (Current.Wind * 7));
            atmosphere.SetWeatherWind(Current.Wind);
            var master = _context.SettingsService != null ? _context.SettingsService.AudiblePreferences.MasterVolume
                : GameAudio.Current != null ? GameAudio.Current.Mixer.Preferences.MasterVolume : 1;
            var gain = AudioBusMixer.ToGain(master);
            rainAudio.volume = .2f * Current.Rain * gain;
            windAudio.volume = .16f * Current.Wind * gain;
        }

        public void Uninstall()
        {
            if (!IsInstalled) return;
            if (rainAudio != null) rainAudio.Stop();
            if (windAudio != null) windAudio.Stop();
            if (vfx != null) vfx.SetWeather(0, Vector3.zero);
            if (atmosphere != null) { atmosphere.SetWeatherDriven(false); atmosphere.SetWeatherWind(1); }
            if (_sun != null) { _sun.intensity = _sunIntensity; _sun.color = _sunColor; }
            // Never restore global settings over a newly activated content scene.
            if (SceneManager.GetActiveScene() == gameObject.scene && RenderSettings.skybox == _runtimeSky)
            {
                RenderSettings.skybox = _originalSky;
                RenderSettings.fog = _fogEnabled; RenderSettings.fogMode = _fogMode; RenderSettings.fogColor = _fogColor;
                RenderSettings.fogStartDistance = _fogStart; RenderSettings.fogEndDistance = _fogEnd; RenderSettings.fogDensity = _fogDensity;
                RenderSettings.ambientMode = _ambientMode; RenderSettings.ambientIntensity = _ambientIntensity;
                RenderSettings.ambientSkyColor = _ambientSky; RenderSettings.ambientEquatorColor = _ambientEquator;
                RenderSettings.ambientGroundColor = _ambientGround;
            }
            if (_runtimeSky != null) Destroy(_runtimeSky);
            _runtimeSky = null; _originalSky = null; _sun = null; _context = null; _session = null;
        }
        private void OnDisable() => Uninstall();
        private void OnDestroy() => Uninstall();
    }
}
