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

        [Header("Lighting ownership")]
        [SerializeField, Tooltip("Disable to leave the scene's fog settings untouched.")] private bool controlFog = true;
        [SerializeField] private FogMode weatherFogMode = FogMode.Linear;
        [SerializeField, Min(0f)] private float clearFogDensity;
        [SerializeField, Min(0f)] private float stormFogDensity = 0.01f;
        [SerializeField, Tooltip("Disable to leave the scene's ambient lighting untouched.")] private bool controlAmbient = true;
        [SerializeField] private AmbientMode weatherAmbientMode = AmbientMode.Trilight;
        private bool _fogWasControlled, _ambientWasControlled;

        [Header("Clear → storm (blended by cloudiness)")]
        [SerializeField] private Vector2 clearFogRange = new Vector2(1400f, 2400f);
        [SerializeField] private Vector2 stormFogRange = new Vector2(45f, 300f);
        [SerializeField] private Color clearAmbientSky = new Color(.65f, .75f, .9f);
        [SerializeField] private Color stormAmbientSky = new Color(.38f, .43f, .5f);
        [SerializeField] private Color clearAmbientEquator = new Color(.55f, .62f, .7f);
        [SerializeField] private Color stormAmbientEquator = new Color(.32f, .37f, .42f);
        [SerializeField] private Color clearAmbientGround = new Color(.32f, .34f, .31f);
        [SerializeField] private Color stormAmbientGround = new Color(.23f, .26f, .28f);
        [SerializeField, Range(0f, 1f), Tooltip("Sun intensity in a full storm, relative to the scene's.")] private float stormSunIntensity = .55f;
        [SerializeField, Tooltip("Sun colour in a full storm.")] private Color stormSunColor = new Color(.7f, .78f, .88f);

        [Header("Wind and sound")]
        [SerializeField, Min(0f), Tooltip("Wind speed (m/s) pushing the rain at full wind.")] private float maximumWindSpeed = 7f;
        [SerializeField, Range(0f, 1f), Tooltip("Rain loop level at full rain.")] private float rainLevel = .2f;
        [SerializeField, Range(0f, 1f), Tooltip("Wind loop level at full wind.")] private float windLevel = .16f;
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
            AudioBusSource.SetIntensity(rainAudio, 0); AudioBusSource.SetIntensity(windAudio, 0);
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
            if (controlFog)
            {
                _fogWasControlled = true;
                RenderSettings.fog = clouds > 0.001f;
                RenderSettings.fogMode = weatherFogMode;
                RenderSettings.fogColor = _runtimeSky.GetColor("_HorizonColor");
                RenderSettings.fogStartDistance = Mathf.Lerp(clearFogRange.x, stormFogRange.x, clouds);
                RenderSettings.fogEndDistance = Mathf.Lerp(clearFogRange.y, stormFogRange.y, clouds);
                if (weatherFogMode != FogMode.Linear)
                    RenderSettings.fogDensity = Mathf.Lerp(clearFogDensity, stormFogDensity, clouds);
            }
            else RestoreFog();
            if (controlAmbient)
            {
                _ambientWasControlled = true;
                RenderSettings.ambientMode = weatherAmbientMode;
                RenderSettings.ambientSkyColor = Color.Lerp(clearAmbientSky, stormAmbientSky, clouds);
                RenderSettings.ambientEquatorColor = Color.Lerp(clearAmbientEquator, stormAmbientEquator, clouds);
                RenderSettings.ambientGroundColor = Color.Lerp(clearAmbientGround, stormAmbientGround, clouds);
            }
            else RestoreAmbient();
            if (_sun != null)
            {
                _sun.intensity = Mathf.Lerp(_sunIntensity, _sunIntensity * stormSunIntensity, clouds);
                _sun.color = Color.Lerp(_sunColor, stormSunColor, clouds);
            }
            vfx.SetWeather(Current.Rain, _windDirection * (Current.Wind * maximumWindSpeed));
            atmosphere.SetWeatherWind(Current.Wind);
            // Routed through the Ambience bus, which applies the master volume.
            AudioBusSource.SetIntensity(rainAudio, rainLevel * Current.Rain);
            AudioBusSource.SetIntensity(windAudio, windLevel * Current.Wind);
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
                RestoreFog();
                RestoreAmbient();
            }
            if (_runtimeSky != null) Destroy(_runtimeSky);
            _runtimeSky = null; _originalSky = null; _sun = null; _context = null; _session = null;
        }
        private void RestoreFog()
        {
            if (!_fogWasControlled) return;
            RenderSettings.fog = _fogEnabled; RenderSettings.fogMode = _fogMode; RenderSettings.fogColor = _fogColor;
            RenderSettings.fogStartDistance = _fogStart; RenderSettings.fogEndDistance = _fogEnd; RenderSettings.fogDensity = _fogDensity;
            _fogWasControlled = false;
        }

        private void RestoreAmbient()
        {
            if (!_ambientWasControlled) return;
            RenderSettings.ambientMode = _ambientMode; RenderSettings.ambientIntensity = _ambientIntensity;
            RenderSettings.ambientSkyColor = _ambientSky; RenderSettings.ambientEquatorColor = _ambientEquator;
            RenderSettings.ambientGroundColor = _ambientGround;
            _ambientWasControlled = false;
        }

        private void OnDisable() => Uninstall();
        private void OnDestroy() => Uninstall();
    }
}
