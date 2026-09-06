using ApexRivals.AI.Configuration;
using ApexRivals.Race.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AiDrivingInputProvider : MonoBehaviour, IDrivingInputProvider
    {
        [SerializeField]
        private AiDriverConfiguration configuration;

        [SerializeField]
        private RacingLine racingLine;

        [SerializeField]
        private ArcadeVehicleController vehicleController;

        [SerializeField]
        private VehicleResetter vehicleResetter;

        [SerializeField]
        private RaceCoordinator raceCoordinator;

        [SerializeField]
        private RaceParticipant raceParticipant;

        [SerializeField]
        private Transform vehicleTransform;

        [SerializeField]
        private Transform recoveryPose;

        [SerializeField]
        private bool enableStuckRecovery = true;

        private DrivingInput _currentInput = DrivingInput.Neutral;
        private int _currentWaypointIndex;
        private float _smoothedSteering;
        private float _smoothedThrottle;
        private float _smoothedBrake;
        private float _stuckTimer;
        private float _lastRecoveryTime = float.NegativeInfinity;
        private bool _racingLineValid;

        public DrivingInput CurrentInput => _currentInput;
        public AiDriverConfiguration Configuration => configuration;

        public void Configure(
            AiDriverConfiguration driverConfiguration,
            RacingLine route,
            ArcadeVehicleController controller,
            VehicleResetter resetBoundary,
            RaceCoordinator coordinator,
            RaceParticipant participant,
            Transform controlledTransform,
            Transform recoveryTransform)
        {
            configuration = driverConfiguration;
            racingLine = route;
            vehicleController = controller;
            vehicleResetter = resetBoundary;
            raceCoordinator = coordinator;
            raceParticipant = participant;
            vehicleTransform = controlledTransform != null ? controlledTransform : transform;
            recoveryPose = recoveryTransform;
            _racingLineValid = racingLine != null && racingLine.IsValid;
            ResetDecisionState();
        }

        private void Awake()
        {
            if (vehicleTransform == null)
            {
                vehicleTransform = transform;
            }

            if (vehicleController == null)
            {
                vehicleController = GetComponent<ArcadeVehicleController>();
            }

            if (vehicleResetter == null)
            {
                vehicleResetter = GetComponent<VehicleResetter>();
            }

            if (raceParticipant == null)
            {
                raceParticipant = GetComponent<RaceParticipant>();
            }
        }

        private void OnEnable()
        {
            _racingLineValid = racingLine != null && racingLine.IsValid;
            ResetDecisionState();
        }

        private void Update()
        {
            if (!CanDrive())
            {
                ResetDecisionState();
                return;
            }

            AdvanceWaypointIfReached();

            var tuning = configuration.CreateTuning();
            var position = vehicleTransform.position;
            var targetPosition = racingLine.GetWaypointPosition(_currentWaypointIndex);
            var lookAheadIndex = racingLine.WrapIndex(_currentWaypointIndex + configuration.LookAheadWaypointCount);
            var lookAheadPosition = racingLine.GetWaypointPosition(lookAheadIndex);
            var localTarget = vehicleTransform.InverseTransformPoint(targetPosition);
            var currentDirection = targetPosition - position;
            var nextDirection = lookAheadPosition - targetPosition;
            var steering = AiDriverRules.CalculateSteering(localTarget, tuning.SteeringSensitivity);
            var cornerSeverity = AiDriverRules.CalculateCornerSeverity(currentDirection, nextDirection);
            var speed = vehicleController != null ? Mathf.Abs(vehicleController.CurrentSpeed) : 0f;
            var decision = AiDriverRules.CreateDecision(speed, steering, cornerSeverity, tuning);
            var deltaTime = Time.deltaTime;

            _smoothedSteering = SmoothValue(_smoothedSteering, decision.Steering, tuning.SteeringSmoothing, deltaTime);
            _smoothedThrottle = SmoothValue(_smoothedThrottle, decision.Throttle, tuning.ThrottleSmoothing, deltaTime);
            _smoothedBrake = SmoothValue(_smoothedBrake, decision.Brake, tuning.BrakeSmoothing, deltaTime);
            _currentInput = new DrivingInput(_smoothedThrottle, _smoothedBrake, _smoothedSteering, decision.Drift, false);

            UpdateStuckRecovery(speed, decision.Throttle > 0.2f, tuning, deltaTime);
        }

        public void ConsumeResetRequest()
        {
            _currentInput = _currentInput.WithoutResetRequest();
        }

        private bool CanDrive()
        {
            if (configuration == null || racingLine == null || !_racingLineValid || vehicleTransform == null)
            {
                return false;
            }

            if (raceCoordinator == null)
            {
                return true;
            }

            if (raceCoordinator.State != RaceState.Racing)
            {
                return false;
            }

            return raceParticipant == null
                || !raceCoordinator.TryGetProgress(raceParticipant, out var progress)
                || !progress.HasFinished;
        }

        private void AdvanceWaypointIfReached()
        {
            var reachDistance = Mathf.Max(configuration.WaypointReachDistance, racingLine.WaypointReachDistance);
            var position = vehicleTransform.position;

            for (var step = 0; step < racingLine.WaypointCount; step++)
            {
                var targetPosition = racingLine.GetWaypointPosition(_currentWaypointIndex);
                var distance = Vector3.Distance(position, targetPosition);
                if (distance > reachDistance)
                {
                    return;
                }

                _currentWaypointIndex = racingLine.WrapIndex(_currentWaypointIndex + 1);
            }
        }

        private void UpdateStuckRecovery(float speed, bool expectedToMove, AiDriverTuning tuning, float deltaTime)
        {
            if (!enableStuckRecovery || vehicleResetter == null)
            {
                _stuckTimer = 0f;
                return;
            }

            _stuckTimer = AiDriverRules.UpdateStuckTimer(
                raceCoordinator == null || raceCoordinator.State == RaceState.Racing,
                expectedToMove,
                speed,
                tuning.StuckSpeedThreshold,
                deltaTime,
                _stuckTimer);

            var timeSinceRecovery = Time.time - _lastRecoveryTime;
            if (!AiDriverRules.ShouldRequestRecovery(_stuckTimer, tuning.StuckDetectionDuration, timeSinceRecovery, tuning.RecoveryCooldown))
            {
                return;
            }

            if (recoveryPose != null)
            {
                vehicleResetter.CaptureResetPose(recoveryPose);
            }

            vehicleResetter.ResetVehicle();
            _lastRecoveryTime = Time.time;
            ResetDecisionState();
        }

        private void ResetDecisionState()
        {
            _currentInput = DrivingInput.Neutral;
            _smoothedSteering = 0f;
            _smoothedThrottle = 0f;
            _smoothedBrake = 0f;
            _stuckTimer = 0f;
        }

        private static float SmoothValue(float current, float target, float smoothing, float deltaTime)
        {
            if (smoothing <= 0f)
            {
                return target;
            }

            var t = 1f - Mathf.Exp(-smoothing * deltaTime);
            return Mathf.Lerp(current, target, t);
        }
    }
}
