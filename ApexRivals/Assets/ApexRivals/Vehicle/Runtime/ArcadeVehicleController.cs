using ApexRivals.Vehicle.Configuration;
using UnityEngine;

namespace ApexRivals.Vehicle.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArcadeVehicleController : MonoBehaviour
    {
        [SerializeField]
        private ArcadeVehicleConfiguration configuration;

        [SerializeField]
        private MonoBehaviour inputProviderComponent;

        [SerializeField]
        private VehicleResetter resetter;

        [SerializeField]
        private Transform groundCheckOrigin;

        private Rigidbody _rigidbody;
        private IDrivingInputProvider _inputProvider;
        private ArcadeVehicleStats _effectiveStats;
        private bool _hasEffectiveStats;

        public float CurrentSpeed { get; private set; }
        public bool IsGrounded { get; private set; }
        public ArcadeVehicleStats EffectiveStats => GetEffectiveStats();

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            if (inputProviderComponent != null)
            {
                _inputProvider = inputProviderComponent as IDrivingInputProvider;
            }

            if (resetter == null)
            {
                resetter = GetComponent<VehicleResetter>();
            }

            if (groundCheckOrigin == null)
            {
                groundCheckOrigin = transform;
            }

            RefreshBaseStats();
        }

        public void RefreshBaseStats()
        {
            if (configuration == null)
            {
                _hasEffectiveStats = false;
                return;
            }

            _effectiveStats = configuration.CreateStats();
            _hasEffectiveStats = true;
        }

        public void ApplyEffectiveStats(ArcadeVehicleStats stats)
        {
            _effectiveStats = stats;
            _hasEffectiveStats = true;
        }

        public void Configure(
            ArcadeVehicleConfiguration vehicleConfiguration,
            IDrivingInputProvider inputProvider,
            VehicleResetter vehicleResetter,
            Transform groundOrigin)
        {
            configuration = vehicleConfiguration;
            inputProviderComponent = inputProvider as MonoBehaviour;
            _inputProvider = inputProvider;
            resetter = vehicleResetter;
            groundCheckOrigin = groundOrigin != null ? groundOrigin : transform;
            RefreshBaseStats();
        }

        private void FixedUpdate()
        {
            if (configuration == null)
            {
                return;
            }

            var stats = GetEffectiveStats();
            var input = _inputProvider != null ? _inputProvider.CurrentInput : DrivingInput.Neutral;
            if (input.ResetRequested)
            {
                resetter?.ResetVehicle();
                _inputProvider?.ConsumeResetRequest();
                return;
            }

            var velocity = _rigidbody.linearVelocity;
            CurrentSpeed = Vector3.Dot(velocity, transform.forward);
            IsGrounded = Physics.Raycast(
                groundCheckOrigin.position,
                -transform.up,
                stats.GroundCheckDistance,
                stats.GroundLayerMask,
                QueryTriggerInteraction.Ignore);

            if (!IsGrounded)
            {
                return;
            }

            ApplyDriveForces(input, stats);
            ApplySteering(input, stats);
            ApplyLateralGrip(input, stats);
            ApplyDownforce(stats);
            ClampForwardSpeed(stats);
        }

        private void ApplyDriveForces(DrivingInput input, ArcadeVehicleStats stats)
        {
            var driveMode = VehicleDriveRules.ResolveDriveMode(
                CurrentSpeed,
                input.Throttle,
                input.Brake,
                stats.ReverseEntrySpeed);

            switch (driveMode)
            {
                case VehicleDriveMode.Forward:
                    _rigidbody.AddForce(transform.forward * (input.Throttle * stats.ForwardAcceleration), ForceMode.Acceleration);
                    break;
                case VehicleDriveMode.Brake:
                    var brakeDirection = CurrentSpeed >= 0f ? -1f : 1f;
                    var brakeInput = Mathf.Max(input.Brake, input.Throttle);
                    _rigidbody.AddForce(transform.forward * (brakeDirection * brakeInput * stats.BrakeForce), ForceMode.Acceleration);
                    break;
                case VehicleDriveMode.Reverse:
                    _rigidbody.AddForce(-transform.forward * (input.Brake * stats.ReverseAcceleration), ForceMode.Acceleration);
                    break;
            }
        }

        private void ApplySteering(DrivingInput input, ArcadeVehicleStats stats)
        {
            var speedMagnitude = Mathf.Abs(CurrentSpeed);
            var maximumSpeed = CurrentSpeed >= 0f ? stats.MaximumForwardSpeed : stats.MaximumReverseSpeed;
            var steeringResponse = VehicleDriveRules.CalculateSteeringResponse(speedMagnitude, maximumSpeed, stats.MinimumSteeringResponse);
            var driftMultiplier = input.Drift ? stats.DriftSteeringMultiplier : 1f;
            var steering = input.Steering * stats.SteeringStrength * steeringResponse * driftMultiplier;

            if (speedMagnitude < 0.05f || Mathf.Abs(steering) < 0.001f)
            {
                return;
            }

            var reverseSign = CurrentSpeed >= 0f ? 1f : -1f;
            _rigidbody.AddTorque(transform.up * (steering * reverseSign), ForceMode.Acceleration);
        }

        private void ApplyLateralGrip(DrivingInput input, ArcadeVehicleStats stats)
        {
            var velocity = _rigidbody.linearVelocity;
            var lateralVelocity = Vector3.Project(velocity, transform.right);
            var grip = VehicleDriveRules.SelectLateralGrip(stats.NormalLateralGrip, stats.DriftLateralGrip, input.Drift);
            var gripCorrection = -lateralVelocity * Mathf.Clamp01(grip);
            _rigidbody.AddForce(gripCorrection, ForceMode.VelocityChange);
        }

        private void ApplyDownforce(ArcadeVehicleStats stats)
        {
            _rigidbody.AddForce(-transform.up * stats.Downforce, ForceMode.Acceleration);
        }

        private void ClampForwardSpeed(ArcadeVehicleStats stats)
        {
            var velocity = _rigidbody.linearVelocity;
            var forwardSpeed = Vector3.Dot(velocity, transform.forward);
            var maximumSpeed = forwardSpeed >= 0f ? stats.MaximumForwardSpeed : stats.MaximumReverseSpeed;

            if (maximumSpeed <= 0f || Mathf.Abs(forwardSpeed) <= maximumSpeed)
            {
                return;
            }

            var forwardVelocity = transform.forward * (Mathf.Sign(forwardSpeed) * maximumSpeed);
            var lateralVelocity = Vector3.Project(velocity, transform.right);
            var verticalVelocity = Vector3.Project(velocity, transform.up);
            _rigidbody.linearVelocity = forwardVelocity + lateralVelocity + verticalVelocity;
            CurrentSpeed = Mathf.Sign(forwardSpeed) * maximumSpeed;
        }

        private ArcadeVehicleStats GetEffectiveStats()
        {
            if (!_hasEffectiveStats)
            {
                RefreshBaseStats();
            }

            return _effectiveStats;
        }
    }
}
