using ApexRivals.AI.Runtime;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.DrivingAssistance.Runtime
{
    /// <summary>Read-only player guidance. One shared mesh; geometry and road contacts are built once per attempt.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DynamicDrivingLine : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField] private RaceCoordinator coordinator;
        [SerializeField] private RaceSessionSceneInstaller sessionInstaller;
        [SerializeField] private RacingLine racingLine;
        [SerializeField] private MeshCollider road;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private bool showDrivingLine = true;
        [SerializeField, Range(60, 250)] private float visibleDistance = 180;
        [SerializeField, Min(1)] private float brakingDeceleration = 5;
        [SerializeField, Min(1)] private float corneringGrip = 6;
        [Header("Zone colours (alpha is driven by distance)")]
        [SerializeField, Tooltip("Comfortably below the safe speed.")] private Color safeColor = new Color32(45, 245, 90, 255);
        [SerializeField, Tooltip("Close to the safe speed: brake soon.")] private Color cautionColor = new Color32(255, 205, 30, 255);
        [SerializeField, Tooltip("Over the safe speed: braking is overdue.")] private Color overColor = new Color32(255, 45, 30, 255);
        private ApplicationContext _context;
        private RaceSessionCoordinator _session;
        private Rigidbody _body;
        private VehicleResetter _resetter;
        private IdealRacingPath _path;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Color32[] _colors;
        private float[] _safeSpeeds;
        private bool[] _onRoad;
        private int _anchor = -1;
        private int _resetVersion;
        private Vector3 _lastPosition;
        private float _refresh;
        public int MarkerCount => _path?.SampleCount ?? 0;
        public int VisibleMarkerCount { get; private set; }
        public float CurrentRecommendedSpeed { get; private set; }
        public DrivingLineZone CurrentZone { get; private set; }
        public float DistanceAlongLap { get; private set; }
        public Mesh Mesh => _mesh;
        public IdealRacingPath Path => _path;
        public bool IsVisible => _renderer != null && _renderer.enabled;
        public bool AssistanceEnabled
        {
            get => showDrivingLine;
            set { showDrivingLine = value; _refresh = 0; if (!value && _renderer != null) _renderer.enabled = false; }
        }

        public void Install(ApplicationContext context)
        {
            Uninstall();
            var player = coordinator != null && coordinator.PlayerParticipant != null
                ? coordinator.PlayerParticipant.GetComponentInParent<RaceVehicleComposition>() : null;
            if (context == null || player == null || player.VehicleRuntime == null || road == null || lineMaterial == null ||
                sessionInstaller == null || sessionInstaller.Session == null) return;
            var stats = player.VehicleRuntime.CurrentPerformanceStats;
            var lookup = context.VehicleCatalog?.FindById(context.SelectedVehicleState.SelectedVehicleId);
            var baselineHandling = lookup.HasValue && lookup.Value.Succeeded ? lookup.Value.Vehicle.BasePerformanceStats.Handling : stats.Handling;
            var handling = VehiclePerformanceTuningRules.RelativeMultiplier(baselineHandling, stats.Handling);
            // Straight-line headroom keeps ordinary top-speed driving green; corner grip and braking
            // still determine the warning limits before a bend.
            if (!IdealRacingPath.TryBuild(racingLine, 5, Mathf.Max(10, stats.TopSpeed) * 1.2f, 5, 4, corneringGrip * handling, out _path)) return;
            if (_path.SampleCount > 4096) { _path = null; Debug.LogWarning("Driving line exceeds the supported sample budget.", this); return; }
            _context = context;
            _session = sessionInstaller.Session;
            _body = player.VehicleRigidbody;
            _resetter = player.VehicleResetter;
            _resetVersion = _resetter != null ? _resetter.ResetVersion : 0;
            _lastPosition = _body.position;
            BuildGeometry();
            _renderer = GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = lineMaterial;
            _renderer.enabled = false;
            LateUpdate();
        }

        private void BuildGeometry()
        {
            var count = _path.SampleCount;
            var vertices = new Vector3[count * 6];
            var indices = new int[vertices.Length];
            _colors = new Color32[vertices.Length];
            _safeSpeeds = new float[count];
            _onRoad = new bool[count];
            var shape = new[] { new Vector2(0, 1.4f), new Vector2(-0.7f, -0.9f), new Vector2(0, -0.2f),
                new Vector2(0, 1.4f), new Vector2(0, -0.2f), new Vector2(0.7f, -0.9f) };
            for (var i = 0; i < count; i++)
            {
                var sample = _path.GetSample(i);
                var projection = new IdealRacingPath.Projection(i, 0, sample.Position, sample.Forward, sample.DistanceAlongLap, 0, 0);
                _safeSpeeds[i] = _path.GetBrakingLimitedSpeed(projection, 300, brakingDeceleration, 1);
                var right = Vector3.Cross(Vector3.up, sample.Forward).normalized;
                var valid = true;
                for (var corner = 0; corner < 6; corner++)
                {
                    var point = sample.Position + right * shape[corner].x + sample.Forward * shape[corner].y;
                    // Cast only against the road, from the authored route's own height. Cars and the upper
                    // deck cannot steal contacts from the road below a bridge.
                    if (!road.Raycast(new Ray(point + Vector3.up * 1.5f, Vector3.down), out var hit, 4f) || hit.normal.y < 0.5f) valid = false;
                    else point = hit.point + hit.normal * 0.04f;
                    var vertex = i * 6 + corner;
                    vertices[vertex] = transform.InverseTransformPoint(point);
                    indices[vertex] = vertex;
                }
                _onRoad[i] = valid;
            }
            _mesh = new Mesh { name = "DynamicDrivingLine" };
            _mesh.MarkDynamic();
            _mesh.vertices = vertices;
            _mesh.colors32 = _colors;
            _mesh.triangles = indices;
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private void LateUpdate()
        {
            if (_context == null || _renderer == null || _body == null) return;
            var active = showDrivingLine && Time.timeScale > 0 && _context.Navigation.State == ApplicationState.Race &&
                !(_context.LoadingPresentation?.IsVisible ?? false) &&
                (_session.State == RaceSessionLifecycleState.Starting || _session.State == RaceSessionLifecycleState.Racing);
            if (!active || Vector3.Dot(_body.linearVelocity, _body.transform.forward) < -1f)
            {
                _renderer.enabled = false;
                _refresh = 0;
                return;
            }
            _refresh -= Time.deltaTime;
            if (_refresh > 0) return;
            _refresh = 0.1f;
            if ((_resetter != null && _resetter.ResetVersion != _resetVersion) || Vector3.Distance(_lastPosition, _body.position) > 15)
                _anchor = -1;
            _lastPosition = _body.position;
            _resetVersion = _resetter != null ? _resetter.ResetVersion : 0;
            if (!_path.TryProjectTracked(_body.position, _anchor, 12, 15, out var projection) || projection.Distance > 22)
            {
                _renderer.enabled = false;
                return;
            }
            _anchor = projection.SegmentIndex;
            DistanceAlongLap = projection.DistanceAlongLap;
            var speed = Vector3.ProjectOnPlane(_body.linearVelocity, Vector3.up).magnitude;
            CurrentRecommendedSpeed = _path.GetBrakingLimitedSpeed(projection, 300, brakingDeceleration, 1);
            CurrentZone = DrivingLineRules.Evaluate(speed, CurrentRecommendedSpeed);
            VisibleMarkerCount = 0;
            for (var i = 0; i < _path.SampleCount; i++)
            {
                var distance = Mathf.Repeat(_path.GetSample(i).DistanceAlongLap - DistanceAlongLap, _path.LapLength);
                var alpha = _onRoad[i] && distance >= 3 && distance <= visibleDistance
                    ? Mathf.InverseLerp(3, 9, distance) * Mathf.InverseLerp(visibleDistance, visibleDistance - 25, distance) * 0.9f : 0;
                var color = ZoneColor(DrivingLineRules.Evaluate(speed, _safeSpeeds[i]), alpha);
                if (color.a > 0) VisibleMarkerCount++;
                for (var vertex = 0; vertex < 6; vertex++) _colors[i * 6 + vertex] = color;
            }
            _mesh.SetColors(_colors);
            _renderer.enabled = VisibleMarkerCount > 0;
        }

        private Color32 ZoneColor(DrivingLineZone zone, float alpha)
        {
            var color = zone == DrivingLineZone.Green ? safeColor : zone == DrivingLineZone.Yellow ? cautionColor : overColor;
            color.a *= alpha;
            return color;
        }

        public void Uninstall()
        {
            if (_renderer != null) _renderer.enabled = false;
            var filter = GetComponent<MeshFilter>();
            if (filter != null) filter.sharedMesh = null;
            if (_mesh != null) Destroy(_mesh);
            _mesh = null; _path = null; _context = null; _session = null; _body = null; _resetter = null;
            _anchor = -1; _refresh = 0; VisibleMarkerCount = 0; CurrentRecommendedSpeed = 0;
        }
        private void OnDisable() => Uninstall();
        private void OnDestroy() => Uninstall();
    }
}
