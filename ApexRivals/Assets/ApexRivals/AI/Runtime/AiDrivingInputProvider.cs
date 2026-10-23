using System.Collections.Generic;
using ApexRivals.AI.Configuration;
using ApexRivals.Race.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    /// <summary>
    /// AI rival driver. Coordinates the driving pipeline each frame and owns the driving state:
    /// <list type="number">
    /// <item><see cref="AiPathTracker"/> locates the car on its ideal path.</item>
    /// <item><see cref="AiRecoverySupervisor"/> escalates when the car cannot progress: rejoin, reverse, reset.</item>
    /// <item><see cref="AiTrafficPerception"/>, <see cref="AiObstacleSensor"/> and <see cref="AiTacticalPlanner"/> decide the line and traffic speed cap.</item>
    /// <item><see cref="AiDriverPersonality"/> adds line wander and mistakes.</item>
    /// <item><see cref="AiVehicleControl"/> turns it all into <see cref="DrivingInput"/>.</item>
    /// </list>
    /// It never touches physics directly; resets go through the vehicle controller's own reset boundary.
    /// </summary>
    public sealed class AiDrivingInputProvider : MonoBehaviour, IDrivingInputProvider
    {
        private const float RejoinSteeringLimit = 0.6f;
        private const float RejoinExitHeadingError = 25f;
        private const float MinimumReactionInterval = 0.02f;

        [SerializeField] private AiDriverConfiguration configuration;
        [SerializeField] private RacingLine racingLine;
        [SerializeField] private MonoBehaviour vehicleController;
        [SerializeField] private VehicleResetter vehicleResetter;
        [SerializeField] private RaceCoordinator raceCoordinator;
        [SerializeField] private RaceParticipant raceParticipant;
        [SerializeField] private Transform vehicleTransform;
#pragma warning disable CS0414 // Retained for serialized prefab references; resets now place the car on the ideal line.
        [SerializeField] private Transform recoveryPose;
#pragma warning restore CS0414

        [Header("Ideal Path")]
        [SerializeField, Range(2f, 5f)] private float idealSampleSpacing = 4f;
        [SerializeField, Min(0)] private int idealBackwardSampleWindow = 8;
        [SerializeField, Min(1)] private int idealForwardSampleWindow = 48;
        [SerializeField, Min(0f)] private float idealSpeedSoftening = 2f;
        [SerializeField, Min(0f), Tooltip("Metres per second the driver moves sideways between lines.")] private float lateralOffsetRate = 2.5f;

        [Header("Rejoin")]
        [SerializeField, Min(0f)] private float rejoinMinimumLookahead = 7f;
        [SerializeField, Min(0f)] private float rejoinTargetSpeed = 9f;

        [Header("Braking And Obstacles")]
        [SerializeField, Min(0.1f), Tooltip("Deceleration the vehicle can sustain under full braking (m/s²).")] private float brakingDeceleration = 4.5f;
        [SerializeField, Range(0.1f, 1f), Tooltip("Strongest brake input the driver uses; full brakes destabilise the car at speed.")] private float maximumBrake = 0.75f;
        [SerializeField] private LayerMask obstacleLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.05f)] private float obstacleProbeRadius = 0.8f;
        [SerializeField, Min(0f)] private float obstacleProbeHeight = 0.6f;

        [Header("Recovery")]
        [SerializeField, Range(-1f, 1f), Tooltip("Up alignment at or below which the car counts as flipped; 0.4 includes lying on its side.")] private float upsideDownDotThreshold = 0.4f;
        [SerializeField, Min(0f)] private float upsideDownResetDuration = 1.5f;
        [SerializeField, Min(0f)] private float recoveryPoseVerticalClearance = 0.75f;

        private readonly AiPathTracker _tracker = new AiPathTracker();
        private readonly AiTrafficPerception _perception = new AiTrafficPerception();
        private readonly AiObstacleSensor _obstacleSensor = new AiObstacleSensor();
        private readonly AiTacticalPlanner _planner = new AiTacticalPlanner();
        private readonly AiDriverPersonality _personality = new AiDriverPersonality();
        private readonly AiDriftController _drift = new AiDriftController();
        private readonly AiVehicleControl _control = new AiVehicleControl();
        private readonly AiRecoverySupervisor _recovery = new AiRecoverySupervisor();

        private IVehicleRuntime _vehicleRuntime;
        private Rigidbody _body;
        private bool _racingLineValid;
        private float _lateralOffset;
        private float _tacticalOffset;
        private float _trafficSpeedLimit = float.PositiveInfinity;
        private float _obstacleSpeedLimit = float.PositiveInfinity;
        private float _tacticalTimer;

        public DrivingInput CurrentInput => _control.Input;
        public AiDriverConfiguration Configuration => configuration;
        public AiDrivingState DrivingState { get; private set; } = AiDrivingState.Racing;
        public AiResetReason LastResetReason => _recovery.LastResetReason;
        public int ResetCount => _recovery.ResetCount;

        /// <summary>Racer id the driver's personality is seeded from; distinct ids give distinct drivers.</summary>
        public string PersonalityId => _personality.SeedId;

        /// <summary>Times the driver has left the track this session; a tuning and debugging aid.</summary>
        public int OffTrackCount => _recovery.OffTrackCount;

        /// <summary>Recent recovery events, oldest first; a tuning and debugging aid.</summary>
        public IReadOnlyList<AiIncident> Incidents => _recovery.Incidents;

        public float CurrentSpeed { get; private set; }
        public float TargetSpeed => _control.TargetSpeed;
        public float SteeringCommand => _control.SteeringCommand;
        public float ThrottleCommand => _control.ThrottleCommand;
        public float BrakeCommand => _control.BrakeCommand;
        public float DistanceFromRacingLine => _tracker.DistanceFromLine;
        public float CrossTrackError => _tracker.CrossTrackError;
        public float ProjectedPathDistance => _tracker.DistanceAlongLap;
        public float LateralOffset => _lateralOffset;
        public float StuckTimer => _recovery.StuckTimer;
        public float OffTrackTimer => _recovery.OffTrackTimer;
        public bool IsMistakeActive => _personality.IsMistakeActive;
        public bool IsDrifting => _drift.IsDrifting;
        public float DriftSlipAngle => _drift.SlipAngle;
        public int DriftCount => _drift.DriftCount;
        public int DriftMistakeCount => _drift.MistakeCount;
        public bool IsResetPending => _recovery.IsResetPending;
        public int IdealPathSampleCount => _tracker.Path != null ? _tracker.Path.SampleCount : 0;
        public int CurrentPathSampleIndex => _tracker.CurrentSampleIndex;

        private bool IsUpsideDown => _recovery.IsUpsideDown(upsideDownResetDuration);

        private AiControlSettings ControlSettings => new AiControlSettings(
            idealSpeedSoftening,
            brakingDeceleration,
            maximumBrake,
            rejoinMinimumLookahead,
            rejoinTargetSpeed,
            RejoinSteeringLimit);

        private AiResetContext ResetContext => new AiResetContext(
            _tracker,
            vehicleResetter,
            configuration,
            raceCoordinator,
            raceParticipant,
            recoveryPoseVerticalClearance);

        public void Configure(AiDriverConfiguration driverConfiguration, RacingLine route, IVehicleRuntime runtime, VehicleResetter resetBoundary, RaceCoordinator coordinator, RaceParticipant participant, Transform controlledTransform, Transform recoveryTransform)
        {
            configuration = driverConfiguration;
            racingLine = route;
            _vehicleRuntime = runtime;
            vehicleController = runtime as MonoBehaviour;
            vehicleResetter = resetBoundary;
            raceCoordinator = coordinator;
            raceParticipant = participant;
            vehicleTransform = controlledTransform != null ? controlledTransform : transform;
            recoveryPose = recoveryTransform;
            _body = vehicleTransform.GetComponentInParent<Rigidbody>();
            Restart();
        }

        private void Awake()
        {
            if (vehicleTransform == null)
            {
                vehicleTransform = transform;
            }

            TryValidateVehicleRuntime(out _);
            if (vehicleResetter == null)
            {
                vehicleResetter = GetComponent<VehicleResetter>();
            }

            if (raceParticipant == null)
            {
                raceParticipant = GetComponent<RaceParticipant>();
            }

            _body = vehicleTransform.GetComponentInParent<Rigidbody>();
        }

        private void OnEnable()
        {
            Restart();
        }

        private void Update()
        {
            if (!CanDrive())
            {
                ResetDecisionState();
                return;
            }

            if (_tracker.Path == null && !InitializePath())
            {
                _control.SetNeutral();
                return;
            }

            var deltaTime = Time.deltaTime;
            var tuning = configuration.CreateTuning();
            _recovery.Tick(deltaTime, vehicleTransform, upsideDownDotThreshold);
            if (DrivingState == AiDrivingState.PostResetGrace && !_recovery.InGrace)
            {
                DrivingState = AiDrivingState.Racing;
            }

            if (_recovery.IsResetPending)
            {
                UpdatePendingReset(deltaTime);
                return;
            }

            if (!_tracker.TryProject(vehicleTransform.position, idealBackwardSampleWindow, idealForwardSampleWindow))
            {
                _control.SetNeutral();
                return;
            }

            CurrentSpeed = GetSpeed();
            _recovery.UpdateOffTrack(_tracker.DistanceFromLine, configuration.OffTrackDistance, deltaTime);

            if (DrivingState == AiDrivingState.Reversing)
            {
                UpdateReversing(tuning, deltaTime);
                return;
            }

            // "Stuck" means the track would let the car move and it does not, including when it is waiting
            // behind a stopped car; otherwise two cars waiting on each other would never escalate.
            var expectedToMove = _control.ThrottleCommand > 0.2f || _control.FreeSpeed > AiRacecraftRules.SlowCarSpeed;
            _recovery.UpdateStuck(expectedToMove, CurrentSpeed, tuning, deltaTime);
            if (_recovery.UpdateMissedCheckpoint(_tracker.GetGapToNextCheckpoint(raceCoordinator, raceParticipant), _tracker.Path.LapLength, deltaTime))
            {
                RequestReset(AiResetReason.MissedCheckpoint);
                return;
            }

            var action = _recovery.Evaluate(IsUpsideDown, tuning, configuration, out var reason);
            if (action == AiRecoveryAction.Reset)
            {
                RequestReset(reason);
                return;
            }

            if (action == AiRecoveryAction.Reverse)
            {
                BeginReversing();
                return;
            }

            var rejoining = UpdateRejoinState();
            _tacticalTimer -= deltaTime;
            if (_tacticalTimer <= 0f)
            {
                _tacticalTimer = Mathf.Max(MinimumReactionInterval, configuration.ReactionTime);
                UpdateTactics(rejoining);
            }

            _personality.Update(deltaTime, configuration, _control.SteeringCommand);
            UpdateLateralOffset(deltaTime, rejoining);
            var driftAllowed = !rejoining && DrivingState != AiDrivingState.PostResetGrace && DrivingState != AiDrivingState.Avoiding;
            _drift.Update(
                _tracker.Path,
                _tracker.Projection,
                CurrentSpeed,
                _vehicleRuntime != null ? _vehicleRuntime.CurrentForwardSpeed : CurrentSpeed,
                GetLateralSpeed(),
                _body != null ? _body.angularVelocity.y * Mathf.Rad2Deg : 0f,
                driftAllowed,
                configuration,
                deltaTime);
            _control.Drive(
                _tracker,
                vehicleTransform,
                CurrentSpeed,
                GetLateralSpeed(),
                _lateralOffset,
                _trafficSpeedLimit,
                _obstacleSpeedLimit,
                rejoining,
                _personality,
                _drift,
                configuration,
                tuning,
                ControlSettings,
                deltaTime);
            _recovery.CaptureSafePose(deltaTime, vehicleTransform, CurrentSpeed, upsideDownResetDuration, ResetContext);
        }

        public void ConsumeResetRequest()
        {
            _control.ConsumeResetRequest();
            _recovery.NotifyResetAttempted();
        }

        public void ConsumeRecoveryRequest()
        {
            _control.ConsumeRecoveryRequest();
        }

        private void Restart()
        {
            _racingLineValid = racingLine != null && racingLine.IsValid;
            var seedId = raceParticipant != null && !string.IsNullOrEmpty(raceParticipant.RacerId) ? raceParticipant.RacerId : name;
            _personality.Seed(seedId);
            _drift.Seed(seedId);
            ResetDecisionState();
            InitializePath();
        }

        private bool InitializePath()
        {
            _perception.Clear();
            if (!_racingLineValid || vehicleTransform == null
                || !_tracker.TryBuild(racingLine, idealSampleSpacing, configuration, vehicleTransform.position, raceCoordinator))
            {
                return false;
            }

            if (_tracker.TryProject(vehicleTransform.position, idealBackwardSampleWindow, idealForwardSampleWindow))
            {
                _recovery.CaptureSafePoseNow(ResetContext);
            }

            return true;
        }

        private bool UpdateRejoinState()
        {
            var rejoining = DrivingState == AiDrivingState.Rejoining;
            var aligned = Mathf.Abs(IdealRacingDriverRules.CalculateSignedHeadingError(vehicleTransform.forward, _tracker.Projection.Tangent)) < RejoinExitHeadingError;
            // A drift runs wide by design; it straightens up on its own before the driver judges a rejoin.
            var canEnterRejoin = DrivingState != AiDrivingState.PostResetGrace && !_drift.IsDrifting;
            switch (_recovery.UpdateRejoin(rejoining, canEnterRejoin, _tracker.DistanceFromLine, aligned, configuration.OffTrackDistance))
            {
                case AiRejoinTransition.Enter:
                    RecordIncident(AiIncidentKind.OffTrack);
                    DrivingState = AiDrivingState.Rejoining;
                    _planner.Reset();
                    _tacticalOffset = 0f;
                    return true;
                case AiRejoinTransition.Stay:
                    return true;
                case AiRejoinTransition.Exit:
                    DrivingState = AiDrivingState.Racing;
                    return false;
                default:
                    return false;
            }
        }

        private void UpdateTactics(bool rejoining)
        {
            _perception.Refresh(_tracker.Path, _tracker.Projection, raceParticipant, raceCoordinator != null ? raceCoordinator.Participants : null);
            _trafficSpeedLimit = float.PositiveInfinity;
            _obstacleSpeedLimit = _obstacleSensor.ProbeSpeedLimit(vehicleTransform, _body, CurrentSpeed, obstacleLayers, obstacleProbeRadius, obstacleProbeHeight, brakingDeceleration);
            if (rejoining)
            {
                return;
            }

            var input = new AiTacticalInput(
                _tracker.CrossTrackError,
                _lateralOffset,
                CurrentSpeed,
                _control.FreeSpeed,
                _control.StraightSpeed,
                configuration.Aggression,
                configuration.OvertakeLateralOffset,
                configuration.MaximumLateralOffset,
                configuration.FollowTimeGap);
            var decision = _planner.Decide(input, _perception.Cars);
            _tacticalOffset = decision.LateralOffset;
            _trafficSpeedLimit = decision.TrafficSpeedLimit;
            if (DrivingState != AiDrivingState.PostResetGrace)
            {
                DrivingState = decision.State;
            }
        }

        private void UpdateLateralOffset(float deltaTime, bool rejoining)
        {
            var maximumOffset = configuration.MaximumLateralOffset;
            var target = rejoining
                ? 0f
                : Mathf.Clamp(_tacticalOffset + _personality.CalculateWander(Time.time, configuration) + _personality.MistakeOffset, -maximumOffset, maximumOffset);
            _lateralOffset = Mathf.MoveTowards(_lateralOffset, target, lateralOffsetRate * deltaTime);
        }

        private void BeginReversing()
        {
            RecordIncident(AiIncidentKind.Reverse);
            DrivingState = AiDrivingState.Reversing;
            _recovery.BeginReversing(configuration.ReverseDuration);
            _control.CutThrottle();
        }

        private void UpdateReversing(AiDriverTuning tuning, float deltaTime)
        {
            if (IsUpsideDown)
            {
                RequestReset(AiResetReason.UpsideDown);
                return;
            }

            _control.Reverse(vehicleTransform, _tracker.Projection.Tangent, tuning, deltaTime);
            if (_recovery.UpdateReversing(deltaTime))
            {
                DrivingState = AiDrivingState.Racing;
                _tacticalTimer = 0f;
            }
        }

        private void RequestReset(AiResetReason reason)
        {
            DrivingState = AiDrivingState.Resetting;
            _recovery.BeginReset(reason, ResetContext);
            _control.RequestVehicleReset();
        }

        private void UpdatePendingReset(float deltaTime)
        {
            var context = ResetContext;
            var progress = _recovery.UpdatePendingReset(deltaTime, vehicleTransform.position, vehicleController != null, context, out var reissue);
            if (reissue)
            {
                _control.RequestVehicleReset();
            }

            if (progress != AiResetProgress.Completed)
            {
                return;
            }

            RecordIncident(AiIncidentKind.Reset);
            ClearDynamicState();
            DrivingState = AiDrivingState.PostResetGrace;
            _tracker.Reanchor(_recovery.ResetSampleIndex, vehicleTransform.position);
            _tracker.TryProject(vehicleTransform.position, idealBackwardSampleWindow, idealForwardSampleWindow);
        }

        private void RecordIncident(AiIncidentKind kind)
        {
            var raceTime = raceCoordinator != null ? raceCoordinator.ElapsedRaceTime : Time.time;
            _recovery.RecordIncident(kind, _tracker.DistanceAlongLap, CurrentSpeed, _control.TargetSpeed, raceTime);
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

            return raceCoordinator.State == RaceState.Racing
                && (raceParticipant == null || !raceCoordinator.TryGetProgress(raceParticipant, out var progress) || !progress.HasFinished);
        }

        private float GetSpeed() => _vehicleRuntime != null ? Mathf.Abs(_vehicleRuntime.CurrentForwardSpeed) : 0f;

        private float GetLateralSpeed() => _body != null ? Vector3.Dot(_body.linearVelocity, vehicleTransform.right) : 0f;

        /// <summary>Drops everything about the current manoeuvre; recovery counters and history survive.</summary>
        private void ClearDynamicState()
        {
            _control.Reset();
            _planner.Reset();
            _personality.ClearMistake();
            _drift.Reset();
            _recovery.ClearMotionTimers();
            _lateralOffset = 0f;
            _tacticalOffset = 0f;
            _trafficSpeedLimit = float.PositiveInfinity;
            _obstacleSpeedLimit = float.PositiveInfinity;
            _tacticalTimer = 0f;
            CurrentSpeed = 0f;
        }

        private void ResetDecisionState()
        {
            ClearDynamicState();
            _recovery.Reset();
            DrivingState = AiDrivingState.Racing;
        }

        private void OnDrawGizmosSelected()
        {
            var path = _tracker.Path;
            if (path == null || vehicleTransform == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            for (var index = 0; index < path.SampleCount; index++)
            {
                Gizmos.DrawLine(path.GetSample(index).Position, path.GetSample(index + 1).Position);
            }

            if (!_tracker.HasProjection)
            {
                return;
            }

            Gizmos.color = Color.white;
            Gizmos.DrawSphere(_tracker.Projection.Position, 0.35f);
            Gizmos.DrawLine(vehicleTransform.position, _tracker.Projection.Position);
            Gizmos.color = StateColor(DrivingState);
            Gizmos.DrawSphere(_control.SteeringTarget, 0.55f);
            Gizmos.DrawLine(vehicleTransform.position, _control.SteeringTarget);
            Gizmos.DrawWireSphere(vehicleTransform.position + Vector3.up * 2.5f, 0.4f);
            if (_recovery.HasResetPosition)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(_recovery.ResetPosition, new Vector3(1.5f, 0.5f, 3f));
            }
        }

        private static Color StateColor(AiDrivingState state)
        {
            switch (state)
            {
                case AiDrivingState.Following: return Color.yellow;
                case AiDrivingState.Overtaking: return new Color(1f, 0.5f, 0f);
                case AiDrivingState.Defending: return Color.blue;
                case AiDrivingState.Avoiding: return Color.red;
                case AiDrivingState.Rejoining: return Color.magenta;
                case AiDrivingState.Reversing: return Color.gray;
                case AiDrivingState.Resetting: return Color.black;
                case AiDrivingState.PostResetGrace: return Color.green;
                default: return Color.white;
            }
        }

        public bool TryValidateVehicleRuntime(out string message)
        {
            if (!TryResolveVehicleRuntime(out var runtime, out message))
            {
                _vehicleRuntime = null;
                return false;
            }

            _vehicleRuntime = runtime;
            vehicleController = runtime as MonoBehaviour;
            return true;
        }

        internal bool TryValidateVehicleRuntimeReference(out string message) => TryResolveVehicleRuntime(out _, out message);

        private bool TryResolveVehicleRuntime(out IVehicleRuntime runtime, out string message)
        {
            return VehicleRuntimeResolver.TryResolveExactlyOne(this, vehicleController, out runtime, out message);
        }
    }
}
