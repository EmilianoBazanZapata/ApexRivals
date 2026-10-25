using ApexRivals.Input.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.VFX.Runtime
{
    /// <summary>Presentation-only contact/slip effects. Never writes vehicle forces or input.</summary>
    [DisallowMultipleComponent]
    public sealed class VehicleRaceVfx : MonoBehaviour
    {
        private RaceVfxDefinition _definition;
        private WheelArcadeVehicleController _controller;
        private DrivingInputGate _gate;
        private WheelCollider[] _rearWheels;
        private ParticleSystem[] _smoke;
        private ParticleSystem _sparks;
        private float[] _emissionRemainder;
        private float _lastImpactTime = float.NegativeInfinity;
        private Vector3 _lastPosition;
        public int SmokeParticlesEmitted { get; private set; }
        public int ImpactBurstsEmitted { get; private set; }
        public bool LastImpactWasVehicle { get; private set; }

        public void Configure(RaceVfxDefinition definition, RaceVehicleComposition vehicle)
        {
            Shutdown();
            _definition = definition;
            _controller = vehicle != null ? vehicle.GetComponentInChildren<WheelArcadeVehicleController>() : null;
            _gate = vehicle != null ? vehicle.DrivingInputGate : null;
            if (_definition == null) return;
            var wheels = GetComponentsInChildren<WheelCollider>();
            System.Array.Sort(wheels, (a, b) => transform.InverseTransformPoint(a.transform.position).z
                .CompareTo(transform.InverseTransformPoint(b.transform.position).z));
            var count = Mathf.Min(2, wheels.Length);
            _rearWheels = new WheelCollider[count];
            _smoke = new ParticleSystem[count];
            _emissionRemainder = new float[count];
            for (var i = 0; i < count; i++)
            {
                _rearWheels[i] = wheels[i];
                if (_definition.driftSmokePrefab != null)
                {
                    _smoke[i] = Instantiate(_definition.driftSmokePrefab, transform);
                    _smoke[i].Play();
                }
            }
            if (_definition.impactSparksPrefab != null)
            {
                _sparks = Instantiate(_definition.impactSparksPrefab, transform);
                _sparks.Play();
            }
            _lastPosition = transform.position;
            _lastImpactTime = float.NegativeInfinity;
        }

        private void Update()
        {
            if (_definition == null || _controller == null || _smoke == null) return;
            if (Vector3.Distance(_lastPosition, transform.position) > 20f)
            {
                foreach (var smoke in _smoke) if (smoke != null) smoke.Clear();
                if (_sparks != null) _sparks.Clear();
            }
            _lastPosition = transform.position;
            for (var i = 0; i < _rearWheels.Length; i++)
            {
                if (_smoke[i] == null || _rearWheels[i] == null) continue;
                var grounded = _rearWheels[i].GetGroundHit(out var hit);
                var active = RaceVfxRules.ShouldSmoke(_controller.CurrentForwardSpeed,
                    _controller.CurrentVehicleSlipAngle, hit.sidewaysSlip, grounded,
                    _gate != null && _gate.DrivingAllowed && Time.timeScale > 0f,
                    _definition.minimumSmokeSpeed, _definition.minimumSlipAngle, _definition.minimumWheelSlip);
                if (!active) { _emissionRemainder[i] = 0f; continue; }
                _emissionRemainder[i] += _definition.smokeRatePerWheel * Time.deltaTime;
                var particles = Mathf.FloorToInt(_emissionRemainder[i]);
                _emissionRemainder[i] -= particles;
                if (particles == 0) continue;
                _smoke[i].transform.position = hit.point + hit.normal * 0.12f;
                _smoke[i].Emit(particles);
                SmokeParticlesEmitted += particles;
            }
        }

        private void OnCollisionEnter(Collision collision) => HandleCollision(collision, false);
        private void OnCollisionStay(Collision collision) => HandleCollision(collision, true);

        private void HandleCollision(Collision collision, bool scraping)
        {
            if (_gate == null || !_gate.DrivingAllowed || Time.timeScale <= 0f) return;
            var otherVehicle = collision.collider.GetComponentInParent<RaceVehicleComposition>();
            // Unity reports the same car/car contact to both bodies: emit only once per pair.
            if (otherVehicle != null && gameObject.GetInstanceID() > otherVehicle.gameObject.GetInstanceID()) return;
            var strongest = 0f;
            var contact = default(ContactPoint);
            for (var i = 0; i < collision.contactCount; i++)
            {
                var candidate = collision.GetContact(i);
                var strength = RaceVfxRules.ImpactStrength(collision.relativeVelocity, candidate.normal, scraping);
                if (strength > strongest) { strongest = strength; contact = candidate; }
            }
            EmitImpact(contact.point, contact.normal, strongest, otherVehicle != null);
        }

        public bool EmitImpact(Vector3 position, Vector3 normal, float strength, bool hitVehicle)
        {
            if (_definition == null || _sparks == null || strength < _definition.minimumImpactSpeed
                || Time.time - _lastImpactTime < _definition.impactCooldown) return false;
            _lastImpactTime = Time.time;
            _sparks.transform.SetPositionAndRotation(position + normal * 0.04f,
                Quaternion.LookRotation((normal + Vector3.up * 0.65f).normalized, Vector3.up));
            var count = Mathf.Clamp(Mathf.RoundToInt(strength * 3f), 6, _definition.maximumSparkCount);
            var parameters = new ParticleSystem.EmitParams
            {
                startColor = hitVehicle ? new Color(1f, 0.48f, 0.08f) : new Color(1f, 0.78f, 0.3f),
                applyShapeToPosition = true
            };
            _sparks.Emit(parameters, count);
            ImpactBurstsEmitted++;
            LastImpactWasVehicle = hitVehicle;
            return true;
        }

        public void Shutdown()
        {
            if (_smoke != null) foreach (var smoke in _smoke) if (smoke != null) Destroy(smoke.gameObject);
            if (_sparks != null) Destroy(_sparks.gameObject);
            _smoke = null;
            _sparks = null;
            _definition = null;
        }

        private void OnDestroy() => Shutdown();
    }
}
