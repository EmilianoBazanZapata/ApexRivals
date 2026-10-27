using System.Collections.Generic;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.VFX.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RaceVfxSceneInstaller : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField] private RaceVfxDefinition definition;
        [SerializeField] private RaceSessionSceneInstaller raceSessionInstaller;
        [SerializeField] private UnityEngine.Camera outputCamera;
        [SerializeField] private bool enableRain;
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
            if (definition.skidmarkMaterial != null)
            {
                var marks = new GameObject("RaceTireMarks");
                // Preserve an identity world transform: marks remain on the road, never parented to a car.
                marks.transform.SetParent(transform, true);
                _skidmarks = marks.AddComponent<SkidmarkSurface>();
                _skidmarks.Initialize(definition);
            }
            // Resolve the freshly spawned cars once. The installer owns only these presentation components.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var vehicle in root.GetComponentsInChildren<RaceVehicleComposition>())
                {
                    var effect = vehicle.GetComponent<VehicleRaceVfx>();
                    if (effect == null) effect = vehicle.gameObject.AddComponent<VehicleRaceVfx>();
                    effect.Configure(definition, vehicle);
                    _vehicles.Add(effect);
                    if (_skidmarks != null)
                    {
                        var tires = vehicle.GetComponent<VehicleSkidmarks>();
                        if (tires == null) tires = vehicle.gameObject.AddComponent<VehicleSkidmarks>();
                        tires.Configure(definition, vehicle, _skidmarks);
                        _tires.Add(tires);
                    }
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
                _rainEmission = _rain.emission.rateOverTime.constant;
                PositionRain();
            }
            if (_rain == null) return;
            var emission = _rain.emission;
            emission.rateOverTime = _rainEmission * _rainIntensity;
            var velocity = _rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = windVelocity.x;
            velocity.y = 0;
            velocity.z = windVelocity.z;
            if (RainEnabled && !_rain.isPlaying) _rain.Play();
            else if (!RainEnabled && !_rain.isStopped) _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
            if (_session == null || definition == null || _celebrated || !RaceVfxRules.ShouldCelebrate(true, playerPosition)) return false;
            _celebrated = true;
            _celebrationCanvas = new GameObject("VictoryConfetti", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _celebrationCanvas.transform.SetParent(transform, false);
            var canvas = _celebrationCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // Above results; below the persistent loading overlay (32767).
            var scaler = _celebrationCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var papers = new GameObject("LowPolyPapers", typeof(RectTransform));
            papers.transform.SetParent(_celebrationCanvas.transform, false);
            var rect = papers.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _confetti = papers.AddComponent<VictoryConfettiGraphic>();
            _confetti.material = definition.confettiMaterial;
            _confetti.raycastTarget = false;
            _confetti.Burst();
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
            if (_skidmarks != null) { _skidmarks.gameObject.SetActive(false); Destroy(_skidmarks.gameObject); }
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
