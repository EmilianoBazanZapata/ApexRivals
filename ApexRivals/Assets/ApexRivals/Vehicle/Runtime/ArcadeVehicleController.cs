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

        public float CurrentSpeed { get; private set; }
        public bool IsGrounded { get; private set; }

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
        }

        private void FixedUpdate()
        {
            if (configuration == null)
            {
                return;
            }

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
                configuration.groundCheckDistance,
                configuration.groundLayerMask,
                QueryTriggerInteraction.Ignore);

            if (!IsGrounded)
            {
                return;
            }

            ApplyDriveForces(input);
            ApplySteering(input);
            ApplyLateralGrip(input);
            ApplyDownforce();
            ClampForwardSpeed();
        }

        private void ApplyDriveForces(DrivingInput input)
        {
            var driveMode = VehicleDriveRules.ResolveDriveMode(
                CurrentSpeed,
                input.Throttle,
                input.Brake,
                configuration.reverseEntrySpeed);

            switch (driveMode)
            {
                case VehicleDriveMode.Forward:
                    _rigidbody.AddForce(transform.forward * (input.Throttle * configuration.forwardAcceleration), ForceMode.Acceleration);
                    break;
                case VehicleDriveMode.Brake:
                    var brakeDirection = CurrentSpeed >= 0f ? -1f : 1f;
                    var brakeInput = Mathf.Max(input.Brake, input.Throttle);
                    _rigidbody.AddForce(transform.forward * (brakeDirection * brakeInput * configuration.brakeForce), ForceMode.Acceleration);
                    break;
                case VehicleDriveMode.Reverse:
                    _rigidbody.AddForce(-transform.forward * (input.Brake * configuration.reverseAcceleration), ForceMode.Acceleration);
                    break;
            }
        }

        private void ApplySteering(DrivingInput input)
        {
            var speedMagnitude = Mathf.Abs(CurrentSpeed);
            var maximumSpeed = CurrentSpeed >= 0f ? configuration.maximumForwardSpeed : configuration.maximumReverseSpeed;
            var steeringResponse = VehicleDriveRules.CalculateSteeringResponse(speedMagnitude, maximumSpeed, configuration.minimumSteeringResponse);
            var driftMultiplier = input.Drift ? configuration.driftSteeringMultiplier : 1f;
            var steering = input.Steering * configuration.steeringStrength * steeringResponse * driftMultiplier;

            if (speedMagnitude < 0.05f || Mathf.Abs(steering) < 0.001f)
            {
                return;
            }

            var reverseSign = CurrentSpeed >= 0f ? 1f : -1f;
            _rigidbody.AddTorque(transform.up * (steering * reverseSign), ForceMode.Acceleration);
        }

        private void ApplyLateralGrip(DrivingInput input)
        {
            var velocity = _rigidbody.linearVelocity;
            var lateralVelocity = Vector3.Project(velocity, transform.right);
            var grip = VehicleDriveRules.SelectLateralGrip(configuration.normalLateralGrip, configuration.driftLateralGrip, input.Drift);
            var gripCorrection = -lateralVelocity * Mathf.Clamp01(grip);
            _rigidbody.AddForce(gripCorrection, ForceMode.VelocityChange);
        }

        private void ApplyDownforce()
        {
            _rigidbody.AddForce(-transform.up * configuration.downforce, ForceMode.Acceleration);
        }

        private void ClampForwardSpeed()
        {
            var velocity = _rigidbody.linearVelocity;
            var forwardSpeed = Vector3.Dot(velocity, transform.forward);
            var maximumSpeed = forwardSpeed >= 0f ? configuration.maximumForwardSpeed : configuration.maximumReverseSpeed;

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
    }
}
