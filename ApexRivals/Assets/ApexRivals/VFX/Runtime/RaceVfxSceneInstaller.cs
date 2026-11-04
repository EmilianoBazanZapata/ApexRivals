using System.Collections.Generic;
using ApexRivals.Composition.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using UnityEngine;

namespace ApexRivals.VFX.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RaceVfxSceneInstaller : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField] private RaceVfxDefinition definition;
        [SerializeField] private RaceSessionSceneInstaller raceSessionInstaller;
        [SerializeField] private UnityEngine.Camera outputCamera;
        [SerializeField] private bool enableRain;
        [SerializeField, Tooltip("World-space tire marks surface (renderer and material set up in the scene; inactive until a race installs).")]
        private SkidmarkSurface tireMarks;
        [SerializeField, Tooltip("Add weather wind to the prefab's velocity curves, in the prefab's simulation space. Disable to leave its velocity module untouched.")]
        private bool applyWeatherWind = true;
        private readonly List<VehicleRaceVfx> _vehicles = new List<VehicleRaceVfx>();
        private readonly List<VehicleSkidmarks> _tires = new List<VehicleSkidmarks>();
        private SkidmarkSurface _skidmarks;
        private RaceSessionCoordinator _session;
        private ParticleSystem _rain;
        private GameObject _celebrationCanvas;
        private VictoryConfettiGraphic _confetti;
        private bool _celebrated;
        private Vector3 _rainAnchor;
        private float _rainIntensity;
        private float _rainEmission;
        private bool _rainVelocityEnabled;
        private ParticleSystem.MinMaxCurve _rainX, _rainY, _rainZ;
        private Vector3 _lastRainWind;
        private bool _windApplied;
        public bool RainEnabled => _rainIntensity > 0.001f;
        public ParticleSystem Rain => _rain;
        public bool HasCelebrated => _celebrated;
        public VictoryConfettiGraphic Confetti => _confetti;
        public SkidmarkSurface Skidmarks => _skidmarks;

        public void Install(ApplicationContext context)
        {
            Uninstall();
            if (definition == null || raceSessionInstaller == null || raceSessionInstaller.Session == null) return;
            _session = raceSessionInstaller.Session;
            if (tireMarks != null)
            {
                // The marks object (renderer and material) is set up in the scene; only its mesh is built per race.
                tireMarks.gameObject.SetActive(true);
                tireMarks.Initialize(definition);
                _skidmarks = tireMarks;
            }
            // Resolve the freshly spawned cars once; their VFX components come with the car prefab.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var vehicle in root.GetComponentsInChildren<RaceVehicleComposition>())
                {
                    var effect = vehicle.GetComponent<VehicleRaceVfx>();
                    if (effect != null)
                    {
                        effect.Configure(definition, vehicle);
                        _vehicles.Add(effect);
                    }
                    var tires = vehicle.GetComponent<VehicleSkidmarks>();
                    if (tires != null && _skidmarks != null)
                    {
                        tires.Configure(definition, vehicle, _skidmarks);
                        _tires.Add(tires);
                    }
                    if (effect == null || tires == null)
                        Debug.LogWarning(vehicle.name + " is missing VehicleRaceVfx or VehicleSkidmarks; add them to the car prefab (ApexRivals/Vehicle/Install Race Components On Cars).", vehicle);
                }
            _session.ResultsReady += OnResultsReady;
            SetWeather(enableRain ? 1 : 0, Vector3.zero);
        }

        public void SetWeather(float rainIntensity, Vector3 windVelocity)
        {
            _rainIntensity = Mathf.Clamp01(rainIntensity);
            if (_session == null || definition == null || outputCamera == null) return;
            if (_rain == null && RainEnabled && definition.rainPrefab != null)
            {
                _rain = Instantiate(definition.rainPrefab, transform);
                _rainEmission = _rain.emission.rateOverTimeMultiplier;
                var velocity = _rain.velocityOverLifetime;
                _rainVelocityEnabled = velocity.enabled;
                _rainX = velocity.x; _rainY = velocity.y; _rainZ = velocity.z;
                _windApplied = false;
                PositionRain();
            }
            if (_rain == null) return;
            var emission = _rain.emission;
            emission.rateOverTimeMultiplier = _rainEmission * _rainIntensity;
            ApplyRainWind(windVelocity);
            if (RainEnabled && !_rain.isPlaying) _rain.Play();
            else if (!RainEnabled && !_rain.isStopped) _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void ApplyRainWind(Vector3 worldWind)
        {
            var velocity = _rain.velocityOverLifetime;
            var wind = applyWeatherWind ? worldWind : Vector3.zero;
            if (velocity.space == ParticleSystemSimulationSpace.Local)
                wind = _rain.transform.InverseTransformDirection(wind);
            if (_windApplied && wind == _lastRainWind) return;
            // Build from the captured prefab curves, never from last frame's result.
            var addWind = applyWeatherWind && wind.sqrMagnitude > 0f;
            velocity.x = addWind ? OffsetCurve(_rainVelocityEnabled ? _rainX : new ParticleSystem.MinMaxCurve(0f), wind.x) : _rainX;
            velocity.y = addWind ? OffsetCurve(_rainVelocityEnabled ? _rainY : new ParticleSystem.MinMaxCurve(0f), wind.y) : _rainY;
            velocity.z = addWind ? OffsetCurve(_rainVelocityEnabled ? _rainZ : new ParticleSystem.MinMaxCurve(0f), wind.z) : _rainZ;
            velocity.enabled = _rainVelocityEnabled || addWind;
            _lastRainWind = wind;
            _windApplied = true;
        }

        private static ParticleSystem.MinMaxCurve OffsetCurve(ParticleSystem.MinMaxCurve source, float offset)
        {
            switch (source.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(source.constant + offset);
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(source.constantMin + offset, source.constantMax + offset);
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(1f, OffsetAnimationCurve(source.curve, source.curveMultiplier, offset));
                default:
                    return new ParticleSystem.MinMaxCurve(1f,
                        OffsetAnimationCurve(source.curveMin, source.curveMultiplier, offset),
                        OffsetAnimationCurve(source.curveMax, source.curveMultiplier, offset));
            }
        }

        private static AnimationCurve OffsetAnimationCurve(AnimationCurve source, float multiplier, float offset)
        {
            if (source == null) return AnimationCurve.Constant(0f, 1f, offset);
            var keys = source.keys;
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i].value = keys[i].value * multiplier + offset;
                keys[i].inTangent *= multiplier;
                keys[i].outTangent *= multiplier;
            }
            return new AnimationCurve(keys) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
        }

        private void LateUpdate()
        {
            if (_rain == null || outputCamera == null) return;
            if (Vector3.Distance(_rainAnchor, outputCamera.transform.position) > 45f) _rain.Clear();
            PositionRain();
        }

        private void PositionRain()
        {
            _rainAnchor = outputCamera.transform.position;
            var ahead = Vector3.ProjectOnPlane(outputCamera.transform.forward, Vector3.up).normalized;
            _rain.transform.SetPositionAndRotation(_rainAnchor + ahead * 8f + Vector3.up * 9f, Quaternion.Euler(90f, 0f, 0f));
        }

        private void OnResultsReady(RaceSessionResult result) => TryCelebrate(result.FinalPosition);

        public bool TryCelebrate(int playerPosition)
        {
            if (_session == null || definition == null || definition.confettiPrefab == null || _celebrated || !RaceVfxRules.ShouldCelebrate(true, playerPosition)) return false;
            _celebrated = true;
            // The overlay (canvas order above the results, material, layout) is set up in the prefab.
            _celebrationCanvas = Instantiate(definition.confettiPrefab, transform);
            _confetti = _celebrationCanvas.GetComponentInChildren<VictoryConfettiGraphic>(true);
            if (_confetti != null) _confetti.Burst();
            return true;
        }

        public void Uninstall()
        {
            if (_session != null) _session.ResultsReady -= OnResultsReady;
            _session = null;
            foreach (var vehicle in _vehicles) if (vehicle != null) vehicle.Shutdown();
            _vehicles.Clear();
            foreach (var tires in _tires) if (tires != null) tires.Shutdown();
            _tires.Clear();
            if (_skidmarks != null) _skidmarks.gameObject.SetActive(false);
            _skidmarks = null;
            if (_rain != null) { _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); Destroy(_rain.gameObject); }
            _rain = null;
            _rainIntensity = _rainEmission = 0;
            if (_celebrationCanvas != null) { _celebrationCanvas.SetActive(false); Destroy(_celebrationCanvas); }
            _celebrationCanvas = null;
            _confetti = null;
            _celebrated = false;
        }

        private void OnDestroy() => Uninstall();
    }
}
