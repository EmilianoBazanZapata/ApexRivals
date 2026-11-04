using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    /// <summary>Player-only nitro. Composition owns wiring; no AI performance stats are modified.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(50)]
    public sealed class PlayerVehicleNitro : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float boostAcceleration = 6f;
        [SerializeField, Min(1f)] private float boostTopSpeedMultiplier = 1.2f;
        private WheelArcadeVehicleController _motor;
        private Rigidbody _body;
        private WheelCollider[] _wheels;
        private VehicleResetter _resetter;
        private IDrivingInputProvider _input;
        private IDrivingInputAvailability _availability;
        private NitroReservoir _reservoir = new NitroReservoir();
        private int _resetVersion;
        private float _impactCooldown;

        public float NormalizedCharge => _reservoir.NormalizedCharge;
        public bool IsBoosting => isActiveAndEnabled && (_availability?.DrivingAllowed ?? false) && _reservoir.IsBoosting;
        public bool IsCharging => isActiveAndEnabled && (_availability?.DrivingAllowed ?? false) && _reservoir.IsCharging;

        public void Configure(WheelArcadeVehicleController motor, IDrivingInputProvider input,
            IDrivingInputAvailability availability, VehicleResetter resetter)
        {
            _motor = motor;
            _input = input;
            _availability = availability;
            _resetter = resetter;
            _body = GetComponent<Rigidbody>();
            _wheels = GetComponentsInChildren<WheelCollider>();
            _resetVersion = resetter != null ? resetter.ResetVersion : 0;
            _reservoir = new NitroReservoir();
            _impactCooldown = 0f;
        }

        private void FixedUpdate()
        {
            if (_motor == null || _body == null || _body.isKinematic || _input == null || _availability == null)
            {
                _reservoir.Interrupt();
                return;
            }
            if (_resetter != null && _resetter.ResetVersion != _resetVersion)
            {
                _resetVersion = _resetter.ResetVersion;
                _reservoir.Interrupt();
                _impactCooldown = 0.75f;
            }
            _impactCooldown = Mathf.Max(0f, _impactCooldown - Time.fixedDeltaTime);
            var grounded = 0;
            if (_wheels != null) foreach (var wheel in _wheels)
                if (wheel != null && wheel.isGrounded) grounded++;
            var input = _input.CurrentInput;
            var supported = grounded >= 2 && Vector3.Dot(transform.up, Vector3.up) > 0.5f
                && _impactCooldown <= 0f && Mathf.Abs(_body.angularVelocity.y) < 2.5f;
            var speedCap = _motor.CurrentPerformanceStats.TopSpeed * boostTopSpeedMultiplier;
            // At/above the boost cap, hold neither burns fuel nor adds propulsion.
            var allowed = _availability.DrivingAllowed && !input.ResetRequested && !input.RecoverVehicleRequested;
            var fundedStep = _reservoir.Step(Time.fixedDeltaTime, allowed, supported,
                _motor.CurrentForwardSpeed, _motor.CurrentVehicleSlipAngle,
                speedCap > _motor.CurrentForwardSpeed ? input.Throttle : 0f, input.Brake,
                input.Nitro);
            if (fundedStep <= 0f) return;
            var taper = speedCap > 0f
                ? VehiclePerformanceTuningRules.ForwardMotorFactor(_motor.CurrentForwardSpeed, speedCap) : 0f;
            _body.AddForce(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized
                * (boostAcceleration * fundedStep * taper), ForceMode.Acceleration);
        }

        private void OnDisable() => _reservoir.Interrupt();

        private void OnCollisionEnter(Collision collision) => CheckImpact(collision);
        private void OnCollisionStay(Collision collision) => CheckImpact(collision);

        private void CheckImpact(Collision collision)
        {
            for (var i = 0; i < collision.contactCount; i++)
            {
                if (Mathf.Abs(Vector3.Dot(collision.GetContact(i).normal, Vector3.up)) >= 0.5f) continue;
                _impactCooldown = 0.75f;
                _reservoir.Interrupt();
                break;
            }
        }
    }
}
