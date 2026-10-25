using ApexRivals.Input.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.VFX.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VehicleSkidmarks : MonoBehaviour
    {
        private struct WheelContact
        {
            public bool Connected;
            public Vector3 Point, Normal;
            public Collider Surface;
        }
        private SkidmarkSurface _surface;
        private RaceVfxDefinition _definition;
        private WheelArcadeVehicleController _controller;
        private DrivingInputGate _gate;
        private Rigidbody _body;
        private VehicleResetter _resetter;
        private int _resetVersion;
        private WheelCollider[] _wheels;
        private WheelContact[] _contacts;
        private bool[] _rear;
        private Vector3 _lastPosition;
        public int SegmentsWritten { get; private set; }

        public void Configure(RaceVfxDefinition definition, RaceVehicleComposition vehicle, SkidmarkSurface surface)
        {
            _definition = definition;
            _surface = surface;
            _controller = vehicle.GetComponentInChildren<WheelArcadeVehicleController>();
            _gate = vehicle.DrivingInputGate;
            _body = vehicle.VehicleRigidbody;
            _resetter = vehicle.VehicleResetter;
            _resetVersion = _resetter != null ? _resetter.ResetVersion : 0;
            _wheels = vehicle.GetComponentsInChildren<WheelCollider>();
            _contacts = new WheelContact[_wheels.Length];
            _rear = new bool[_wheels.Length];
            var center = 0f;
            foreach (var wheel in _wheels) center += transform.InverseTransformPoint(wheel.transform.position).z;
            center /= Mathf.Max(1, _wheels.Length);
            for (var i = 0; i < _wheels.Length; i++)
                _rear[i] = transform.InverseTransformPoint(_wheels[i].transform.position).z < center;
            _lastPosition = transform.position;
        }

        private void FixedUpdate()
        {
            if (_surface == null || _controller == null || _gate == null || _body == null) return;
            if (!_gate.DrivingAllowed) { BreakAll(); return; }
            if (_resetter != null && _resetter.ResetVersion != _resetVersion)
            {
                BreakAll();
                _resetVersion = _resetter.ResetVersion;
                _lastPosition = _body.position;
                return; // Wheel contacts from before the teleport must not seed the next stroke.
            }
            if (Vector3.Distance(_lastPosition, _body.position) > _definition.skidMaximumConnection * 2f) BreakAll();
            _lastPosition = _body.position;
            for (var i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] == null || !_wheels[i].GetGroundHit(out var hit) || hit.collider == null
                    || hit.collider.attachedRigidbody != null || hit.normal.y < 0.45f)
                { _contacts[i].Connected = false; continue; }
                // The Brake binding also drives reverse. Only count it when the controller actually applies normal brakes.
                var input = _gate.CurrentInput;
                var brake = _controller.CurrentNormalBrakeTorque > 0f
                    ? (_controller.CurrentForwardSpeed < 0f ? input.Throttle : input.Brake) : 0f;
                var intensity = RaceVfxRules.SkidIntensity(_controller.CurrentPlanarSpeed, _controller.CurrentVehicleSlipAngle,
                    hit.sidewaysSlip, hit.forwardSlip, brake, _rear[i] && _controller.HandbrakeActive, _definition);
                if (intensity <= 0f) { _contacts[i].Connected = false; continue; }
                var previous = _contacts[i];
                if (!previous.Connected || previous.Surface != hit.collider
                    || Vector3.Distance(previous.Point, hit.point) > _definition.skidMaximumConnection
                    || Vector3.Dot(previous.Normal, hit.normal) < 0.5f)
                {
                    _contacts[i] = new WheelContact { Connected = true, Point = hit.point, Normal = hit.normal, Surface = hit.collider };
                    continue;
                }
                if (Vector3.Distance(previous.Point, hit.point) < _definition.skidMinimumSpacing) continue;
                if (_surface.TryAddSegment(previous.Point, previous.Normal, hit.point, hit.normal, intensity)) SegmentsWritten++;
                _contacts[i] = new WheelContact { Connected = true, Point = hit.point, Normal = hit.normal, Surface = hit.collider };
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || _gate != null && !_gate.DrivingAllowed) BreakAll();
        }

        public void Shutdown() { BreakAll(); _surface = null; }
        private void OnDisable() => BreakAll();
        private void BreakAll()
        {
            if (_contacts == null) return;
            for (var i = 0; i < _contacts.Length; i++) _contacts[i].Connected = false;
        }
    }
}
