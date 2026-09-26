using ApexRivals.AI.Configuration;
using ApexRivals.Race.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AiDrivingInputProvider : MonoBehaviour, IDrivingInputProvider
    {
        [SerializeField] private AiDriverConfiguration configuration;
        [SerializeField] private RacingLine racingLine;
        [SerializeField] private MonoBehaviour vehicleController;
        [SerializeField] private VehicleResetter vehicleResetter;
        [SerializeField] private RaceCoordinator raceCoordinator;
        [SerializeField] private RaceParticipant raceParticipant;
        [SerializeField] private Transform vehicleTransform;
        [SerializeField] private Transform recoveryPose;

        [Header("Ideal Deterministic Driver")]
        [SerializeField] private bool useIdealDriver = true;
        [SerializeField, Range(2f, 5f)] private float idealSampleSpacing = 4f;
        [SerializeField, Min(0f)] private float idealMinimumLookahead = 10f;
        [SerializeField, Min(0f)] private float idealMaximumLookahead = 18f;
        [SerializeField, Min(0f)] private float idealRejoinMinimumLookahead = 7f;
        [SerializeField, Min(0f)] private float idealTargetReachedRadius = 3f;
        [SerializeField, Min(0f)] private float idealHeadingGain = 1f;
        [SerializeField, Min(0f)] private float idealCrossTrackGain = 1.6f;
        [SerializeField, Min(0f)] private float idealSpeedSoftening = 2f;
        [SerializeField, Range(0.1f, 1f)] private float idealDriverSpeedFactor = 1f;
        [SerializeField, Min(0f)] private float idealCurvatureSpeedScale = 18f;
        [SerializeField, Min(0f)] private float idealBrakingPreviewDistance = 36f;
        [SerializeField, Min(0f)] private float idealThrottleGain = 1.4f;
        [SerializeField, Min(0f)] private float idealBrakeGain = 1.2f;
        [SerializeField, Min(0)] private int idealBackwardSampleWindow = 8;
        [SerializeField, Min(1)] private int idealForwardSampleWindow = 48;

        [Header("Route Recovery")]
        [SerializeField, Min(0f)] private float offTrackEnterDistance = 12f;
        [SerializeField, Min(0f)] private float offTrackExitDistance = 6f;
        [SerializeField, Min(0f)] private float rejoinTargetSpeed = 12f;
        [SerializeField, Range(0f, 180f)] private float rejoinMaximumHeadingError = 60f;
        [SerializeField, Min(0f)] private float recoveryProgressDistance = 0.25f;
        [SerializeField, Min(0f)] private float recoveryHeadingProgressDegrees = 4f;
        [SerializeField, Min(0f)] private float recoveryWorldMovementDistance = 0.15f;
        [SerializeField, Min(0f)] private float reverseDuration = 1.25f;
        [SerializeField, Range(-1f, 1f)] private float trustedRouteMinimumHeadingAlignment = 0.25f;
        [SerializeField, Min(0)] private int recoveryBackwardWaypointWindow = 2;
        [SerializeField, Min(1)] private int recoveryForwardWaypointWindow = 10;
        [SerializeField, Min(0)] private int recoverySegmentForwardMargin = 2;
        [Header("Local Arc Lookahead")]
        [SerializeField, Min(0f)] private float racingMinimumArcLookahead = 6f;
        [SerializeField, Min(0f)] private float racingMaximumArcLookahead = 16f;
        [SerializeField, Min(0f)] private float rejoiningMinimumArcLookahead = 4f;
        [SerializeField, Min(0f)] private float rejoiningMaximumArcLookahead = 8f;
        [SerializeField, Min(1)] private int curvatureSampleSegmentCount = 3;
        [SerializeField, Range(0f, 1f)] private float minimumArcChordRatio = 0.55f;
        [SerializeField, Min(0f)] private float postResetGraceDuration = 2f;
        [SerializeField, Min(0f)] private float resetCooldownDuration = 5f;
        [SerializeField, Range(-1f, 0f)] private float upsideDownDotThreshold = -0.35f;
        [SerializeField, Min(0f)] private float upsideDownResetDuration = 1.5f;
        [SerializeField, Min(0f)] private float recoveryPoseVerticalClearance = 0.75f;

        private DrivingInput _currentInput = DrivingInput.Neutral;
        private int _currentWaypointIndex;
        private int _lastTrustedRacingLineIndex;
        private int _lastSafeRacingLineIndex;
        private int _globalNearestWaypointIndex = -1;
        private int _nearestLineIndex = -1;
        private int _acceptedRecoveryCandidateIndex = -1;
        private int _rejoinTargetIndex = -1;
        private int _steeringTargetIndex = -1;
        private int _recoveryArcStartSegmentIndex = -1;
        private int _steeringArcStartSegmentIndex = -1;
        private int _validSegmentStartIndex = -1;
        private int _validSegmentEndIndex = -1;
        private int _lastValidCheckpointIndex = -1;
        private int _expectedCheckpointIndex = -1;
        private int[] _checkpointWaypointIndices;
        private float _smoothedSteering;
        private float _smoothedThrottle;
        private float _smoothedBrake;
        private float _recoveryFailureTimer;
        private float _upsideDownTimer;
        private float _reverseTimer;
        private float _postResetGraceRemaining;
        private float _resetCooldownRemaining;
        private float _globalNearestDiagnosticRefreshTimer;
        private float _recoveryReferenceDistanceToLine;
        private float _recoveryReferenceDistanceToTarget;
        private float _recoveryReferenceHeadingError;
        private bool _racingLineValid;
        private bool _hasRejoinProgress;
        private bool _hasReversedDuringRecovery;
        private bool _hasProgressSegment;
        private IVehicleRuntime _vehicleRuntime;
        private Vector3 _globalNearestPoint;
        private Vector3 _nearestLinePoint;
        private Vector3 _trackForward;
        private Vector3 _acceptedRecoveryCandidatePosition;
        private Vector3 _rejoinTargetPosition;
        private Vector3 _steeringTargetPosition;
        private Vector3 _arcLookaheadStartPoint;
        private Vector3 _recoveryReferencePosition;
        private bool _hasRecoveryProgressReference;
        private IdealRacingPath _idealPath;
        private IdealRacingPath.Projection _idealProjection;
        private int[] _idealCheckpointPathIndices;
        private int _currentPathSampleIndex;
        private int _lastSafePathSampleIndex;
        private int _targetPathSampleIndex;
        private bool _hasIdealProjection;
        private float _targetAge;
        private bool _targetReached;
        private bool _targetPassed;

        public DrivingInput CurrentInput => _currentInput;
        public AiDriverConfiguration Configuration => configuration;
        public AiDrivingState DrivingState { get; private set; } = AiDrivingState.Racing;
        public int CurrentRacingLineIndex => _currentWaypointIndex;
        public int LastTrustedRacingLineIndex => _lastTrustedRacingLineIndex;
        public int ForwardIndexDelta => useIdealDriver && _idealPath != null && _targetPathSampleIndex >= 0
            ? IdealRacingDriverRules.ForwardIndexDistance(_currentPathSampleIndex, _targetPathSampleIndex, _idealPath.SampleCount)
            : racingLine != null && _rejoinTargetIndex >= 0
                ? AiRecoveryRules.GetForwardIndexDistance(_lastTrustedRacingLineIndex, _rejoinTargetIndex, racingLine.WaypointCount)
            : 0;
        public int LastValidCheckpointIndex => _lastValidCheckpointIndex;
        public int ExpectedCheckpointIndex => _expectedCheckpointIndex;
        public int ValidSegmentStartIndex => _validSegmentStartIndex;
        public int ValidSegmentEndIndex => _validSegmentEndIndex;
        public int GlobalNearestWaypointIndex => _globalNearestWaypointIndex;
        public int NearestLineIndex => _nearestLineIndex;
        public int AcceptedRecoveryCandidateIndex => _acceptedRecoveryCandidateIndex;
        public int RejoinTargetIndex => _rejoinTargetIndex;
        public int SteeringTargetIndex => _steeringTargetIndex;
        public float DistanceFromRacingLine { get; private set; }
        public float RejoinTargetDistance { get; private set; }
        public float HeadingError { get; private set; }
        public float HeadingAlignment { get; private set; }
        public float DesiredLookaheadDistance { get; private set; }
        public float ActualArcLookaheadDistance { get; private set; }
        public float DirectDistanceToTarget { get; private set; }
        public float ArcDistanceToTarget => ActualArcLookaheadDistance;
        public float ChordRatio { get; private set; }
        public float UpcomingCurvature { get; private set; }
        public float RecoveryFailureTimer => _recoveryFailureTimer;
        public float RecoveryProgressTimer => _recoveryFailureTimer;
        public float StuckTimer => _recoveryFailureTimer;
        public float ReverseTimer => _reverseTimer;
        public float UpsideDownTimer => _upsideDownTimer;
        public float PostResetGraceRemaining => _postResetGraceRemaining;
        public float ResetCooldownRemaining => _resetCooldownRemaining;
        public bool ResetRequested { get; private set; }
        public bool IsOffTrack => DistanceFromRacingLine > offTrackEnterDistance;
        public bool IsRejoining => DrivingState == AiDrivingState.Rejoining;
        public bool IsReverseRecoveryActive => DrivingState == AiDrivingState.Reversing;
        public bool RecoveryProgressDetected { get; private set; }
        public AiResetReason LastResetReason { get; private set; }
        public bool UsesIdealDriver => useIdealDriver;
        public int CurrentPathSampleIndex => _currentPathSampleIndex;
        public int CurrentTargetIndex => _targetPathSampleIndex;
        public int TargetPathSampleIndex => _targetPathSampleIndex;
        public int IdealPathSampleCount => _idealPath != null ? _idealPath.SampleCount : 0;
        public float IdealPathLength => _idealPath != null ? _idealPath.LapLength : 0f;
        public float IdealPathAverageSpacing => _idealPath != null ? _idealPath.AverageSpacing : 0f;
        public float ProjectedPathDistance => _hasIdealProjection ? _idealProjection.DistanceAlongLap : 0f;
        public float CrossTrackError => _hasIdealProjection ? _idealProjection.CrossTrackError : 0f;
        public float SignedHeadingError { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float TargetSpeed { get; private set; }
        public float UpcomingMinimumSpeed { get; private set; }
        public float SteeringCommand { get; private set; }
        public float ThrottleCommand { get; private set; }
        public float BrakeCommand { get; private set; }
        public float IdealDriverSpeedFactor => idealDriverSpeedFactor;
        public float MinimumLookaheadDistance => idealMinimumLookahead;
        public float RejoinMinimumLookaheadDistance => idealRejoinMinimumLookahead;
        public float TargetAge => _targetAge;
        public bool TargetReached => _targetReached;
        public bool TargetPassed => _targetPassed;

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
            _racingLineValid = racingLine != null && racingLine.IsValid;
            BuildCheckpointWaypointMapping();
            ResetDecisionState();

            if (_racingLineValid)
            {
                _currentWaypointIndex = racingLine.WrapIndex(_currentWaypointIndex);
                _lastTrustedRacingLineIndex = _currentWaypointIndex;
                _lastSafeRacingLineIndex = _currentWaypointIndex;
                RefreshRouteContext(vehicleTransform.position);
                CaptureSafeRecoveryPose();
                InitializeIdealDriver();
            }
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
        }

        private void OnEnable()
        {
            _racingLineValid = racingLine != null && racingLine.IsValid;
            BuildCheckpointWaypointMapping();
            ResetDecisionState();
            InitializeIdealDriver();
        }

        private void Update()
        {
            ResetRequested = false;
            if (!CanDrive())
            {
                ResetDecisionState();
                return;
            }

            var deltaTime = Time.deltaTime;
            var tuning = configuration.CreateTuning();
            if (useIdealDriver)
            {
                UpdateIdealDriving(tuning, deltaTime);
                return;
            }

            _postResetGraceRemaining = Mathf.Max(0f, _postResetGraceRemaining - deltaTime);
            _resetCooldownRemaining = Mathf.Max(0f, _resetCooldownRemaining - deltaTime);
            _globalNearestDiagnosticRefreshTimer -= deltaTime;
            RefreshRouteContext(vehicleTransform.position);
            UpdateUpsideDownTimer(deltaTime);

            if (DrivingState == AiDrivingState.PostResetGrace)
            {
                UpdatePostResetGrace(tuning, deltaTime);
                return;
            }

            var resetReason = AiRecoveryRules.GetResetReason(_recoveryFailureTimer, tuning.StuckDetectionDuration, _hasReversedDuringRecovery, IsUpsideDown());
            if (AiRecoveryRules.CanRequestPhysicalReset(resetReason != AiResetReason.None, _postResetGraceRemaining, _resetCooldownRemaining))
            {
                ResetToLastSafeRacePose(resetReason);
                return;
            }

            if (DrivingState == AiDrivingState.Racing && AiRecoveryRules.ShouldEnterRejoin(DistanceFromRacingLine, offTrackEnterDistance))
            {
                BeginRejoining();
            }

            switch (DrivingState)
            {
                case AiDrivingState.Rejoining:
                    UpdateRejoining(tuning, deltaTime);
                    return;
                case AiDrivingState.Reversing:
                    UpdateReversing(deltaTime);
                    return;
                default:
                    UpdateNormalDriving(tuning, deltaTime, true);
                    return;
            }
        }

        public void ConsumeResetRequest()
        {
            _currentInput = _currentInput.WithoutResetRequest();
        }

        public void ConsumeRecoveryRequest()
        {
            _currentInput = _currentInput.WithoutRecoveryRequest();
        }

        private void InitializeIdealDriver()
        {
            _idealPath = null;
            _idealCheckpointPathIndices = null;
            _hasIdealProjection = false;
            _currentPathSampleIndex = 0;
            _lastSafePathSampleIndex = 0;
            _targetPathSampleIndex = 0;
            ClearIdealPersistentTarget();

            if (!_racingLineValid || configuration == null || vehicleTransform == null)
            {
                return;
            }

            var tuning = configuration.CreateTuning();
            if (!IdealRacingPath.TryBuild(
                    racingLine,
                    idealSampleSpacing,
                    tuning.TargetSpeed,
                    tuning.MinimumCornerSpeed,
                    idealCurvatureSpeedScale,
                    out _idealPath))
            {
                return;
            }

            _currentPathSampleIndex = _idealPath.FindNearestSegment(vehicleTransform.position);
            _lastSafePathSampleIndex = _currentPathSampleIndex;
            BuildIdealCheckpointPathMapping();
            if (TryProjectIdealPath(vehicleTransform.position))
            {
                SelectPersistentIdealTarget(CalculateIdealLookahead(tuning));
            }
            CaptureIdealSafeRecoveryPose();
        }

        private void BuildIdealCheckpointPathMapping()
        {
            if (_idealPath == null || raceCoordinator == null || raceCoordinator.OrderedCheckpoints == null)
            {
                return;
            }

            var checkpoints = raceCoordinator.OrderedCheckpoints;
            _idealCheckpointPathIndices = new int[checkpoints.Count];
            for (var index = 0; index < checkpoints.Count; index++)
            {
                var checkpoint = checkpoints[index];
                _idealCheckpointPathIndices[index] = checkpoint != null
                    ? _idealPath.FindNearestSegment(checkpoint.Position)
                    : -1;
            }
        }

        private void UpdateIdealDriving(AiDriverTuning tuning, float deltaTime)
        {
            if (_idealPath == null)
            {
                InitializeIdealDriver();
                if (_idealPath == null)
                {
                    _currentInput = DrivingInput.Neutral;
                    return;
                }
            }

            _postResetGraceRemaining = Mathf.Max(0f, _postResetGraceRemaining - deltaTime);
            _resetCooldownRemaining = Mathf.Max(0f, _resetCooldownRemaining - deltaTime);
            UpdateUpsideDownTimer(deltaTime);
            if (!TryProjectIdealPath(vehicleTransform.position))
            {
                _currentInput = DrivingInput.Neutral;
                return;
            }

            CurrentSpeed = GetSpeed();
            var sample = _idealPath.GetSample(_idealProjection.SegmentIndex);
            HeadingAlignment = Vector3.Dot(
                Vector3.ProjectOnPlane(vehicleTransform.forward, Vector3.up).normalized,
                Vector3.ProjectOnPlane(_idealProjection.Tangent, Vector3.up).normalized);
            DistanceFromRacingLine = _idealProjection.Distance;
            UpcomingCurvature = sample.Curvature;

            UpdatePersistentIdealTarget(CalculateIdealLookahead(tuning), deltaTime);

            var profileSpeed = sample.TargetSpeed * idealDriverSpeedFactor;
            UpcomingMinimumSpeed = _idealPath.GetMinimumTargetSpeedAhead(_idealProjection, idealBrakingPreviewDistance) * idealDriverSpeedFactor;
            TargetSpeed = Mathf.Min(profileSpeed, UpcomingMinimumSpeed);
            var steeringDirection = Vector3.ProjectOnPlane(_steeringTargetPosition - vehicleTransform.position, Vector3.up);
            if (steeringDirection.sqrMagnitude < 0.0001f)
            {
                steeringDirection = _idealProjection.Tangent;
            }

            SignedHeadingError = IdealRacingDriverRules.CalculateSignedHeadingError(vehicleTransform.forward, steeringDirection);
            HeadingError = Mathf.Abs(SignedHeadingError);
            SteeringCommand = IdealRacingDriverRules.CalculateSteering(
                SignedHeadingError,
                _idealProjection.CrossTrackError,
                CurrentSpeed,
                idealHeadingGain,
                idealCrossTrackGain,
                idealSpeedSoftening);
            var speedCommand = IdealRacingDriverRules.CalculateSpeedCommand(CurrentSpeed, TargetSpeed, idealThrottleGain, idealBrakeGain);
            ThrottleCommand = speedCommand.Throttle;
            BrakeCommand = speedCommand.Brake;
            ApplySmoothedInput(SteeringCommand, ThrottleCommand, BrakeCommand, false, tuning, deltaTime);

            CaptureIdealSafeRecoveryPose();
            var expectedToMove = ThrottleCommand > 0.2f;
            _recoveryFailureTimer = AiDriverRules.UpdateStuckTimer(
                true,
                expectedToMove,
                CurrentSpeed,
                tuning.StuckSpeedThreshold,
                deltaTime,
                _recoveryFailureTimer);
            RecoveryProgressDetected = _recoveryFailureTimer <= 0f;

            if (_postResetGraceRemaining > 0f)
            {
                DrivingState = AiDrivingState.PostResetGrace;
                return;
            }

            DrivingState = AiDrivingState.Racing;
            var resetReason = AiRecoveryRules.GetResetReason(
                _recoveryFailureTimer,
                tuning.StuckDetectionDuration,
                false,
                IsUpsideDown());
            if (AiRecoveryRules.CanRequestPhysicalReset(resetReason != AiResetReason.None, 0f, _resetCooldownRemaining))
            {
                ResetToLastSafeRacePose(resetReason);
            }
        }

        private float CalculateIdealLookahead(AiDriverTuning tuning)
        {
            var curvature = _idealPath != null && _hasIdealProjection
                ? _idealPath.GetSample(_idealProjection.SegmentIndex).Curvature
                : 0f;
            var calculated = IdealRacingDriverRules.CalculateLookahead(
                CurrentSpeed,
                Mathf.Max(0.1f, tuning.TargetSpeed * idealDriverSpeedFactor),
                curvature,
                idealMinimumLookahead,
                idealMaximumLookahead);
            return Mathf.Max(idealMinimumLookahead, calculated);
        }

        private void UpdatePersistentIdealTarget(float desiredLookahead, float deltaTime)
        {
            _targetAge += Mathf.Max(0f, deltaTime);
            var hasTarget = _targetPathSampleIndex >= 0 && _targetPathSampleIndex < _idealPath.SampleCount;
            _targetReached = hasTarget && IdealRacingDriverRules.HasReachedPersistentTarget(
                Vector3.Distance(vehicleTransform.position, _steeringTargetPosition),
                idealTargetReachedRadius);
            _targetPassed = hasTarget && IdealRacingDriverRules.HasPassedPersistentTarget(
                vehicleTransform.position,
                _steeringTargetPosition,
                _idealPath.GetSample(_targetPathSampleIndex).Forward,
                _currentPathSampleIndex,
                _targetPathSampleIndex,
                GetPersistentTargetProgressWindow(),
                _idealPath.SampleCount);
            var targetBehindProgress = hasTarget && IdealRacingDriverRules.IsTargetAtOrBehindProgress(
                _currentPathSampleIndex,
                _targetPathSampleIndex,
                GetPersistentTargetProgressWindow(),
                _idealPath.SampleCount);

            if (IdealRacingDriverRules.ShouldAdvancePersistentTarget(hasTarget, _targetReached, _targetPassed, targetBehindProgress))
            {
                SelectPersistentIdealTarget(desiredLookahead);
                return;
            }

            UpdatePersistentTargetDiagnostics();
        }

        private void SelectPersistentIdealTarget(float desiredLookahead, float minimumLookahead = -1f)
        {
            var hardMinimum = minimumLookahead >= 0f ? minimumLookahead : idealMinimumLookahead;
            DesiredLookaheadDistance = Mathf.Max(hardMinimum, desiredLookahead);
            _targetPathSampleIndex = _idealPath.GetForwardSampleAtArcDistance(
                _idealProjection,
                DesiredLookaheadDistance,
                out _);
            _steeringTargetIndex = _targetPathSampleIndex;
            _steeringTargetPosition = _idealPath.GetSample(_targetPathSampleIndex).Position;
            _targetAge = 0f;
            UpdatePersistentTargetDiagnostics();
        }

        private void UpdatePersistentTargetDiagnostics()
        {
            ActualArcLookaheadDistance = _idealPath.GetArcDistanceToSample(_idealProjection, _targetPathSampleIndex);
            DirectDistanceToTarget = Vector3.Distance(vehicleTransform.position, _steeringTargetPosition);
            ChordRatio = ActualArcLookaheadDistance > 0.001f
                ? DirectDistanceToTarget / ActualArcLookaheadDistance
                : 1f;
        }

        private int GetPersistentTargetProgressWindow()
        {
            return Mathf.Max(2, Mathf.CeilToInt(idealMaximumLookahead / Mathf.Max(0.1f, _idealPath.AverageSpacing)) + 2);
        }

        private void ClearIdealPersistentTarget()
        {
            _targetPathSampleIndex = -1;
            _steeringTargetIndex = -1;
            _steeringTargetPosition = Vector3.zero;
            _targetAge = 0f;
            _targetReached = false;
            _targetPassed = false;
        }

        private bool TryProjectIdealPath(Vector3 position)
        {
            if (_idealPath == null || !_idealPath.TryProjectLocal(
                    position,
                    _currentPathSampleIndex,
                    idealBackwardSampleWindow,
                    idealForwardSampleWindow,
                    out var projection))
            {
                _hasIdealProjection = false;
                return false;
            }

            var forwardDistance = IdealRacingDriverRules.ForwardIndexDistance(
                _currentPathSampleIndex,
                projection.SegmentIndex,
                _idealPath.SampleCount);
            if (forwardDistance <= idealForwardSampleWindow)
            {
                _currentPathSampleIndex = projection.SegmentIndex;
            }

            _idealProjection = projection;
            _hasIdealProjection = true;
            return true;
        }

        private void CaptureIdealSafeRecoveryPose()
        {
            if (vehicleResetter == null || !_hasIdealProjection || IsUpsideDown() || _idealProjection.Distance > offTrackExitDistance)
            {
                return;
            }

            vehicleResetter.CaptureResetPose(
                _idealProjection.Position + Vector3.up * recoveryPoseVerticalClearance,
                Quaternion.LookRotation(_idealProjection.Tangent, Vector3.up));
            _lastSafePathSampleIndex = _currentPathSampleIndex;
        }

        private void UpdateNormalDriving(AiDriverTuning tuning, float deltaTime, bool allowRecoveryTransitions)
        {
            AdvanceWaypointIfReached();
            UpdateLastTrustedRacingLineIndex();
            var targetPosition = UpdateRacingArcSteeringTarget(tuning);
            var lookAheadIndex = racingLine.WrapIndex(_currentWaypointIndex + configuration.LookAheadWaypointCount);
            var lookAheadPosition = racingLine.GetWaypointPosition(lookAheadIndex);
            var steering = AiDriverRules.CalculateSteering(vehicleTransform.InverseTransformPoint(targetPosition), tuning.SteeringSensitivity);
            HeadingError = CalculateHeadingError(targetPosition - vehicleTransform.position);
            var cornerSeverity = AiDriverRules.CalculateCornerSeverity(targetPosition - vehicleTransform.position, lookAheadPosition - targetPosition);
            var speed = GetSpeed();
            var decision = AiDriverRules.CreateDecision(speed, steering, cornerSeverity, tuning);

            ApplySmoothedInput(decision.Steering, decision.Throttle, decision.Brake, decision.Drift, tuning, deltaTime);
            UpdateRecoveryProgress(Vector3.Distance(vehicleTransform.position, targetPosition), decision.Throttle > 0.2f, deltaTime);

            if (allowRecoveryTransitions && _recoveryFailureTimer >= tuning.StuckDetectionDuration)
            {
                BeginRejoining();
                UpdateRejoining(tuning, deltaTime);
                return;
            }

            if (DistanceFromRacingLine <= offTrackExitDistance)
            {
                CaptureSafeRecoveryPose();
            }
        }

        private void UpdateRejoining(AiDriverTuning tuning, float deltaTime)
        {
            AdvanceSequentialRecoveryTargetIfReached();
            var steering = AiDriverRules.CalculateSteering(vehicleTransform.InverseTransformPoint(_rejoinTargetPosition), tuning.SteeringSensitivity);
            RejoinTargetDistance = Vector3.Distance(vehicleTransform.position, _rejoinTargetPosition);
            HeadingError = CalculateHeadingError(_rejoinTargetPosition - vehicleTransform.position);
            var speed = GetSpeed();
            var targetSpeed = Mathf.Min(tuning.TargetSpeed, rejoinTargetSpeed);
            var throttle = CalculateRejoinThrottle(HeadingError, speed, targetSpeed);
            var brake = speed > targetSpeed ? Mathf.Clamp01((speed - targetSpeed) / Mathf.Max(targetSpeed, 0.001f)) : 0f;
            ApplySmoothedInput(steering, throttle, brake, false, tuning, deltaTime);
            UpdateRecoveryProgress(RejoinTargetDistance, true, deltaTime);

            if (AiRecoveryRules.ShouldReverse(_recoveryFailureTimer, tuning.StuckDetectionDuration, _hasReversedDuringRecovery))
            {
                DrivingState = AiDrivingState.Reversing;
                _reverseTimer = 0f;
                _recoveryFailureTimer = 0f;
                return;
            }

            if (AiRecoveryRules.CanResumeRacing(DistanceFromRacingLine, offTrackExitDistance, CalculateHeadingError(_trackForward), rejoinMaximumHeadingError, _hasRejoinProgress))
            {
                DrivingState = AiDrivingState.Racing;
                _currentWaypointIndex = _rejoinTargetIndex;
                _lastTrustedRacingLineIndex = _rejoinTargetIndex;
                _recoveryFailureTimer = 0f;
                CaptureSafeRecoveryPose();
            }
        }

        private void UpdateReversing(float deltaTime)
        {
            _reverseTimer += Mathf.Max(0f, deltaTime);
            var steering = AiDriverRules.CalculateSteering(vehicleTransform.InverseTransformPoint(_rejoinTargetPosition), 1f);
            _currentInput = new DrivingInput(0f, 1f, -steering, false, false);

            if (_reverseTimer < reverseDuration)
            {
                return;
            }

            _hasReversedDuringRecovery = true;
            DrivingState = AiDrivingState.Rejoining;
            _recoveryFailureTimer = 0f;
            ResetRecoveryProgressReference();
        }

        private void UpdatePostResetGrace(AiDriverTuning tuning, float deltaTime)
        {
            UpdateNormalDriving(tuning, deltaTime, false);
            if (_postResetGraceRemaining > 0f)
            {
                return;
            }

            if (AiRecoveryRules.ShouldEnterRejoin(DistanceFromRacingLine, offTrackEnterDistance))
            {
                DrivingState = AiDrivingState.Rejoining;
                InitializeSequentialRecoveryTarget();
            }
            else
            {
                DrivingState = AiDrivingState.Racing;
            }

            _recoveryFailureTimer = 0f;
            ResetRecoveryProgressReference();
        }

        private void BeginRejoining()
        {
            DrivingState = AiDrivingState.Rejoining;
            _hasRejoinProgress = false;
            _hasReversedDuringRecovery = false;
            _recoveryFailureTimer = 0f;
            InitializeSequentialRecoveryTarget();
            ResetRecoveryProgressReference();
        }

        private void InitializeSequentialRecoveryTarget()
        {
            _lastTrustedRacingLineIndex = racingLine.WrapIndex(_lastTrustedRacingLineIndex);
            _recoveryArcStartSegmentIndex = _lastTrustedRacingLineIndex;
            _acceptedRecoveryCandidateIndex = _lastTrustedRacingLineIndex;
            _acceptedRecoveryCandidatePosition = racingLine.GetWaypointPosition(_lastTrustedRacingLineIndex);
            UpdateRecoveryArcSteeringTarget();
        }

        private void AdvanceSequentialRecoveryTargetIfReached()
        {
            if (_rejoinTargetIndex < 0)
            {
                InitializeSequentialRecoveryTarget();
                return;
            }

            var reachDistance = Mathf.Max(configuration.WaypointReachDistance, racingLine.WaypointReachDistance);
            var localTarget = vehicleTransform.InverseTransformPoint(_rejoinTargetPosition);
            if (localTarget.z > 0f && localTarget.sqrMagnitude > reachDistance * reachDistance)
            {
                return;
            }

            // The target segment becomes the next local anchor only after the AI reaches it.
            // This preserves the RacingLine sequence instead of retargeting a distant valid index.
            var nextStartIndex = _rejoinTargetIndex;
            if (!AiRecoveryRules.CanAdvanceSequentialRecoveryTarget(nextStartIndex, _lastTrustedRacingLineIndex, recoveryForwardWaypointWindow, racingLine.WaypointCount)
                || !IsRecoveryTargetCompatibleWithProgress(nextStartIndex))
            {
                return;
            }

            _recoveryArcStartSegmentIndex = nextStartIndex;
            UpdateRecoveryArcSteeringTarget();
        }

        private bool IsRecoveryTargetCompatibleWithProgress(int targetIndex)
        {
            if (!AiRecoveryRules.IsWaypointIndexInsideRecoveryWindow(
                    targetIndex,
                    _lastTrustedRacingLineIndex,
                    recoveryBackwardWaypointWindow,
                    recoveryForwardWaypointWindow,
                    racingLine.WaypointCount))
            {
                return false;
            }

            return !_hasProgressSegment || AiRecoveryRules.IsWaypointIndexWithinProgressSegment(
                targetIndex,
                _validSegmentStartIndex,
                _validSegmentEndIndex,
                recoverySegmentForwardMargin,
                racingLine.WaypointCount);
        }

        private Vector3 UpdateRacingArcSteeringTarget(AiDriverTuning tuning)
        {
            // _currentWaypointIndex is the next breadcrumb. Start on the segment the
            // vehicle is actually traversing so projection and arc distance stay local.
            var startIndex = racingLine.WrapIndex(_currentWaypointIndex - 1);
            var curvature = racingLine.EstimateUpcomingCurvature(startIndex, curvatureSampleSegmentCount);
            var desiredDistance = AiRecoveryRules.CalculateAdaptiveArcLookahead(
                GetSpeed(),
                Mathf.Max(tuning.TargetSpeed, 0.001f),
                racingMinimumArcLookahead,
                racingMaximumArcLookahead,
                curvature);
            return CalculateLocalArcSteeringTarget(
                startIndex,
                desiredDistance,
                racingMinimumArcLookahead,
                recoveryForwardWaypointWindow,
                false);
        }

        private void UpdateRecoveryArcSteeringTarget()
        {
            _recoveryArcStartSegmentIndex = racingLine.WrapIndex(_recoveryArcStartSegmentIndex);
            var curvature = racingLine.EstimateUpcomingCurvature(_recoveryArcStartSegmentIndex, curvatureSampleSegmentCount);
            var desiredDistance = AiRecoveryRules.CalculateAdaptiveArcLookahead(
                GetSpeed(),
                Mathf.Max(rejoinTargetSpeed, 0.001f),
                rejoiningMinimumArcLookahead,
                rejoiningMaximumArcLookahead,
                curvature);
            var target = CalculateLocalArcSteeringTarget(
                _recoveryArcStartSegmentIndex,
                desiredDistance,
                rejoiningMinimumArcLookahead,
                recoveryForwardWaypointWindow,
                true);
            _rejoinTargetPosition = target;
            _rejoinTargetIndex = _steeringTargetIndex;
        }

        private Vector3 CalculateLocalArcSteeringTarget(int startSegmentIndex, float desiredDistance, float minimumDistance, int maximumSegments, bool constrainToRecoveryProgress)
        {
            var requestedDistance = Mathf.Max(0f, desiredDistance);
            var minimumLookahead = Mathf.Min(requestedDistance, Mathf.Max(0f, minimumDistance));
            var targetPosition = racingLine.GetWaypointPosition(startSegmentIndex);
            var targetIndex = startSegmentIndex;
            var projectedStart = targetPosition;
            var actualArcDistance = 0f;

            // A chord substantially shorter than its path arc means a bend is too sharp for this target.
            // Shorten locally, never by choosing another branch or a farther waypoint.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (!racingLine.TryGetArcLookaheadTarget(
                        vehicleTransform.position,
                        startSegmentIndex,
                        requestedDistance,
                        maximumSegments,
                        out targetIndex,
                        out projectedStart,
                        out targetPosition,
                        out actualArcDistance))
                {
                    break;
                }

                if (constrainToRecoveryProgress && !IsRecoveryTargetCompatibleWithProgress(targetIndex))
                {
                    if (requestedDistance <= minimumLookahead + 0.001f)
                    {
                        targetIndex = startSegmentIndex;
                        targetPosition = racingLine.GetWaypointPosition(startSegmentIndex);
                        projectedStart = targetPosition;
                        actualArcDistance = 0f;
                        break;
                    }

                    requestedDistance = minimumLookahead;
                    continue;
                }

                var directDistance = Vector3.Distance(vehicleTransform.position, targetPosition);
                var chordRatio = actualArcDistance > 0.001f ? directDistance / actualArcDistance : 1f;
                if (!AiRecoveryRules.ShouldShortenArcLookahead(chordRatio, minimumArcChordRatio)
                    || requestedDistance <= minimumLookahead + 0.001f)
                {
                    break;
                }

                requestedDistance = Mathf.Max(minimumLookahead, requestedDistance * 0.5f);
            }

            _steeringArcStartSegmentIndex = racingLine.WrapIndex(startSegmentIndex);
            _steeringTargetIndex = racingLine.WrapIndex(targetIndex);
            _arcLookaheadStartPoint = projectedStart;
            _steeringTargetPosition = targetPosition;
            DesiredLookaheadDistance = requestedDistance;
            ActualArcLookaheadDistance = actualArcDistance;
            DirectDistanceToTarget = Vector3.Distance(vehicleTransform.position, targetPosition);
            ChordRatio = actualArcDistance > 0.001f ? DirectDistanceToTarget / actualArcDistance : 1f;
            UpcomingCurvature = racingLine.EstimateUpcomingCurvature(startSegmentIndex, curvatureSampleSegmentCount);
            return targetPosition;
        }

        private void UpdateRecoveryProgress(float targetDistance, bool expectedToMove, float deltaTime)
        {
            if (!_hasRecoveryProgressReference)
            {
                CaptureRecoveryProgressReference(targetDistance);
                RecoveryProgressDetected = false;
                _recoveryFailureTimer = 0f;
                return;
            }

            var hasMadeProgress = AiRecoveryRules.HasMeaningfulRecoveryProgress(
                _recoveryReferenceDistanceToLine,
                DistanceFromRacingLine,
                _recoveryReferenceDistanceToTarget,
                targetDistance,
                _recoveryReferenceHeadingError,
                HeadingError,
                _recoveryReferencePosition,
                vehicleTransform.position,
                recoveryProgressDistance,
                recoveryHeadingProgressDegrees,
                recoveryWorldMovementDistance);
            RecoveryProgressDetected = hasMadeProgress;
            if (hasMadeProgress)
            {
                _hasRejoinProgress = true;
                CaptureRecoveryProgressReference(targetDistance);
            }

            _recoveryFailureTimer = AiRecoveryRules.UpdateNoProgressTimer(hasMadeProgress, expectedToMove, deltaTime, _recoveryFailureTimer);
        }

        private void CaptureRecoveryProgressReference(float targetDistance)
        {
            _recoveryReferenceDistanceToLine = DistanceFromRacingLine;
            _recoveryReferenceDistanceToTarget = targetDistance;
            _recoveryReferenceHeadingError = HeadingError;
            _recoveryReferencePosition = vehicleTransform.position;
            _hasRecoveryProgressReference = true;
        }

        private void ResetRecoveryProgressReference()
        {
            _hasRecoveryProgressReference = false;
            RecoveryProgressDetected = false;
        }

        private void RefreshRouteContext(Vector3 position)
        {
            if (_globalNearestWaypointIndex < 0 || _globalNearestDiagnosticRefreshTimer <= 0f)
            {
                // Diagnostics only: this result never changes trusted or recovery target indices.
                racingLine.TryGetNearestPoint(position, out _globalNearestWaypointIndex, out _globalNearestPoint, out _, out _);
                _globalNearestDiagnosticRefreshTimer = 0.5f;
            }

            RefreshProgressSegment();
            if (_hasProgressSegment && racingLine.TryGetNearestPointInProgressRange(
                    position,
                    _validSegmentStartIndex,
                    _validSegmentEndIndex,
                    recoverySegmentForwardMargin,
                    out _nearestLineIndex,
                    out _nearestLinePoint,
                    out _trackForward,
                    out var distance))
            {
                DistanceFromRacingLine = distance;
                HeadingAlignment = CalculateHeadingAlignment(_trackForward);
                return;
            }

            // A missing progress mapping must never fall back to the global nearest route branch.
            _nearestLineIndex = racingLine.WrapIndex(_currentWaypointIndex);
            _nearestLinePoint = racingLine.GetWaypointPosition(_nearestLineIndex);
            _trackForward = racingLine.GetWaypointForward(_nearestLineIndex);
            DistanceFromRacingLine = Vector3.Distance(position, _nearestLinePoint);
            HeadingAlignment = CalculateHeadingAlignment(_trackForward);
        }

        private void BuildCheckpointWaypointMapping()
        {
            _checkpointWaypointIndices = null;
            if (!_racingLineValid || raceCoordinator == null || raceCoordinator.OrderedCheckpoints == null || raceCoordinator.OrderedCheckpoints.Count == 0)
            {
                return;
            }

            var checkpoints = raceCoordinator.OrderedCheckpoints;
            _checkpointWaypointIndices = new int[checkpoints.Count];
            for (var index = 0; index < _checkpointWaypointIndices.Length; index++)
            {
                _checkpointWaypointIndices[index] = -1;
            }

            for (var index = 0; index < checkpoints.Count; index++)
            {
                var checkpoint = checkpoints[index];
                if (checkpoint == null || checkpoint.CheckpointIndex < 0 || checkpoint.CheckpointIndex >= _checkpointWaypointIndices.Length)
                {
                    _checkpointWaypointIndices = null;
                    return;
                }

                _checkpointWaypointIndices[checkpoint.CheckpointIndex] = racingLine.GetNearestWaypointIndex(checkpoint.Position);
            }
        }

        private void RefreshProgressSegment()
        {
            _hasProgressSegment = false;
            _validSegmentStartIndex = -1;
            _validSegmentEndIndex = -1;
            _lastValidCheckpointIndex = -1;
            _expectedCheckpointIndex = -1;

            if (_checkpointWaypointIndices == null
                || raceCoordinator == null
                || raceParticipant == null
                || !raceCoordinator.TryGetProgress(raceParticipant, out var progress))
            {
                return;
            }

            var checkpointCount = _checkpointWaypointIndices.Length;
            var expectedIndex = progress.NextExpectedCheckpointIndex;
            if (expectedIndex < 0 || expectedIndex >= checkpointCount || _checkpointWaypointIndices[expectedIndex] < 0)
            {
                return;
            }

            var lastIndex = progress.LastValidCheckpointIndex >= 0
                ? progress.LastValidCheckpointIndex
                : checkpointCount - 1;
            if (lastIndex < 0 || lastIndex >= checkpointCount || _checkpointWaypointIndices[lastIndex] < 0)
            {
                return;
            }

            _lastValidCheckpointIndex = progress.LastValidCheckpointIndex;
            _expectedCheckpointIndex = expectedIndex;
            _validSegmentStartIndex = _checkpointWaypointIndices[lastIndex];
            _validSegmentEndIndex = _checkpointWaypointIndices[expectedIndex];
            _hasProgressSegment = true;
        }

        private void CaptureSafeRecoveryPose()
        {
            if (vehicleResetter == null || !_racingLineValid || !_hasProgressSegment || IsUpsideDown())
            {
                return;
            }

            vehicleResetter.CaptureResetPose(_nearestLinePoint + Vector3.up * recoveryPoseVerticalClearance, Quaternion.LookRotation(_trackForward, Vector3.up));
            _lastSafeRacingLineIndex = _lastTrustedRacingLineIndex;
        }

        private void ResetToLastSafeRacePose(AiResetReason resetReason)
        {
            if (vehicleResetter == null)
            {
                return;
            }

            ClearRecoveryState();
            vehicleResetter.ResetVehicle();
            ResetRequested = true;
            LastResetReason = resetReason;
            _postResetGraceRemaining = postResetGraceDuration;
            _resetCooldownRemaining = resetCooldownDuration;
            DrivingState = AiDrivingState.PostResetGrace;

            if (useIdealDriver && _idealPath != null)
            {
                ReacquireIdealPathAfterReset();
                CaptureIdealSafeRecoveryPose();
                return;
            }

            _lastTrustedRacingLineIndex = racingLine.WrapIndex(_lastSafeRacingLineIndex);
            _currentWaypointIndex = _lastTrustedRacingLineIndex;
            RefreshRouteContext(vehicleTransform.position);
            InitializeSequentialRecoveryTarget();
            CaptureSafeRecoveryPose();
        }

        private void ReacquireIdealPathAfterReset()
        {
            var anchor = _lastSafePathSampleIndex;
            if (anchor < 0 && _idealCheckpointPathIndices != null && raceCoordinator != null && raceParticipant != null
                && raceCoordinator.TryGetProgress(raceParticipant, out var progress)
                && progress.LastValidCheckpointIndex >= 0
                && progress.LastValidCheckpointIndex < _idealCheckpointPathIndices.Length)
            {
                anchor = _idealCheckpointPathIndices[progress.LastValidCheckpointIndex];
            }

            _currentPathSampleIndex = IdealRacingDriverRules.ResolveResetAnchorIndex(anchor, 0, _idealPath.SampleCount);
            ClearIdealPersistentTarget();
            if (TryProjectIdealPath(vehicleTransform.position))
            {
                SelectPersistentIdealTarget(idealRejoinMinimumLookahead, idealRejoinMinimumLookahead);
            }
        }

        private void UpdateUpsideDownTimer(float deltaTime)
        {
            _upsideDownTimer = AiRecoveryRules.IsUpsideDown(Vector3.Dot(vehicleTransform.up, Vector3.up), upsideDownDotThreshold)
                ? _upsideDownTimer + Mathf.Max(0f, deltaTime)
                : 0f;
        }

        private bool IsUpsideDown()
        {
            return _upsideDownTimer >= upsideDownResetDuration;
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

        private void AdvanceWaypointIfReached()
        {
            var reachDistance = Mathf.Max(configuration.WaypointReachDistance, racingLine.WaypointReachDistance);
            var position = vehicleTransform.position;
            for (var step = 0; step < racingLine.WaypointCount; step++)
            {
                if (Vector3.Distance(position, racingLine.GetWaypointPosition(_currentWaypointIndex)) > reachDistance)
                {
                    return;
                }

                _currentWaypointIndex = racingLine.WrapIndex(_currentWaypointIndex + 1);
            }
        }

        private void UpdateLastTrustedRacingLineIndex()
        {
            if (DrivingState != AiDrivingState.Racing || DistanceFromRacingLine > offTrackExitDistance)
            {
                return;
            }

            var currentForward = racingLine.GetWaypointForward(_currentWaypointIndex);
            if (CalculateHeadingAlignment(currentForward) < trustedRouteMinimumHeadingAlignment)
            {
                return;
            }

            if (_hasProgressSegment && !AiRecoveryRules.IsWaypointIndexWithinProgressSegment(
                    _currentWaypointIndex,
                    _validSegmentStartIndex,
                    _validSegmentEndIndex,
                    recoverySegmentForwardMargin,
                    racingLine.WaypointCount))
            {
                return;
            }

            _lastTrustedRacingLineIndex = _currentWaypointIndex;
        }

        private void ApplySmoothedInput(float steering, float throttle, float brake, bool drift, AiDriverTuning tuning, float deltaTime)
        {
            _smoothedSteering = SmoothValue(_smoothedSteering, steering, tuning.SteeringSmoothing, deltaTime);
            _smoothedThrottle = SmoothValue(_smoothedThrottle, throttle, tuning.ThrottleSmoothing, deltaTime);
            _smoothedBrake = SmoothValue(_smoothedBrake, brake, tuning.BrakeSmoothing, deltaTime);
            _currentInput = new DrivingInput(_smoothedThrottle, _smoothedBrake, _smoothedSteering, drift, false);
        }

        private float GetSpeed() => _vehicleRuntime != null ? Mathf.Abs(_vehicleRuntime.CurrentForwardSpeed) : 0f;

        private float CalculateHeadingError(Vector3 direction)
        {
            var planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            var planarForward = Vector3.ProjectOnPlane(vehicleTransform.forward, Vector3.up);
            return planarDirection.sqrMagnitude < 0.0001f || planarForward.sqrMagnitude < 0.0001f
                ? 180f
                : Vector3.Angle(planarForward, planarDirection);
        }

        private float CalculateHeadingAlignment(Vector3 direction)
        {
            var planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            var planarForward = Vector3.ProjectOnPlane(vehicleTransform.forward, Vector3.up);
            return planarDirection.sqrMagnitude < 0.0001f || planarForward.sqrMagnitude < 0.0001f
                ? -1f
                : Vector3.Dot(planarForward.normalized, planarDirection.normalized);
        }

        private static float CalculateRejoinThrottle(float headingError, float speed, float targetSpeed)
        {
            if (headingError >= 120f)
            {
                return 0f;
            }

            var alignment = 1f - Mathf.Clamp01(headingError / 120f);
            return Mathf.Clamp01(Mathf.Lerp(0.2f, 0.65f, alignment) - Mathf.Max(0f, speed / Mathf.Max(targetSpeed, 0.001f) - 1f));
        }

        private void ClearRecoveryState()
        {
            _currentInput = DrivingInput.Neutral;
            _smoothedSteering = 0f;
            _smoothedThrottle = 0f;
            _smoothedBrake = 0f;
            _recoveryFailureTimer = 0f;
            _upsideDownTimer = 0f;
            _reverseTimer = 0f;
            _globalNearestDiagnosticRefreshTimer = 0f;
            ResetRecoveryProgressReference();
            _hasRejoinProgress = false;
            _hasReversedDuringRecovery = false;
            _acceptedRecoveryCandidateIndex = -1;
            _rejoinTargetIndex = -1;
            _steeringTargetIndex = -1;
            _recoveryArcStartSegmentIndex = -1;
            _steeringArcStartSegmentIndex = -1;
            _acceptedRecoveryCandidatePosition = Vector3.zero;
            _rejoinTargetPosition = Vector3.zero;
            _steeringTargetPosition = Vector3.zero;
            _arcLookaheadStartPoint = Vector3.zero;
            DesiredLookaheadDistance = 0f;
            ActualArcLookaheadDistance = 0f;
            DirectDistanceToTarget = 0f;
            ChordRatio = 1f;
            UpcomingCurvature = 0f;
            SignedHeadingError = 0f;
            CurrentSpeed = 0f;
            TargetSpeed = 0f;
            UpcomingMinimumSpeed = 0f;
            SteeringCommand = 0f;
            ThrottleCommand = 0f;
            BrakeCommand = 0f;
            ClearIdealPersistentTarget();
            ResetRequested = false;
        }

        private void ResetDecisionState()
        {
            ClearRecoveryState();
            _postResetGraceRemaining = 0f;
            _resetCooldownRemaining = 0f;
            LastResetReason = AiResetReason.None;
            DrivingState = AiDrivingState.Racing;
        }

        private static float SmoothValue(float current, float target, float smoothing, float deltaTime)
        {
            if (smoothing <= 0f)
            {
                return target;
            }

            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-smoothing * deltaTime));
        }

        private void OnDrawGizmosSelected()
        {
            if (vehicleTransform == null || racingLine == null || !_racingLineValid)
            {
                return;
            }

            if (useIdealDriver && _idealPath != null)
            {
                DrawIdealDriverGizmos();
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(racingLine.GetWaypointPosition(_currentWaypointIndex), 0.35f);
            Gizmos.color = Color.gray;
            Gizmos.DrawSphere(_globalNearestPoint, 0.35f);
            Gizmos.DrawLine(vehicleTransform.position, _globalNearestPoint);
            Gizmos.color = Color.yellow;
            for (var offset = -recoveryBackwardWaypointWindow; offset <= recoveryForwardWaypointWindow; offset++)
            {
                var index = racingLine.WrapIndex(_lastTrustedRacingLineIndex + offset);
                var point = racingLine.GetWaypointPosition(index);
                Gizmos.DrawSphere(point, 0.22f);
                if (offset > -recoveryBackwardWaypointWindow)
                {
                    var previousPoint = racingLine.GetWaypointPosition(racingLine.WrapIndex(_lastTrustedRacingLineIndex + offset - 1));
                    Gizmos.DrawLine(previousPoint, point);
                }
            }

            Gizmos.DrawSphere(_acceptedRecoveryCandidatePosition, 0.45f);
            if (_steeringArcStartSegmentIndex >= 0 && _steeringTargetIndex >= 0)
            {
                var previousPoint = _arcLookaheadStartPoint;
                for (var offset = 0; offset <= recoveryForwardWaypointWindow; offset++)
                {
                    var segmentIndex = racingLine.WrapIndex(_steeringArcStartSegmentIndex + offset);
                    if (segmentIndex == _steeringTargetIndex)
                    {
                        Gizmos.DrawLine(previousPoint, _steeringTargetPosition);
                        break;
                    }

                    var segmentEnd = racingLine.GetWaypointPosition(segmentIndex + 1);
                    Gizmos.DrawLine(previousPoint, segmentEnd);
                    previousPoint = segmentEnd;
                }
            }

            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(_steeringTargetPosition, 0.6f);
            Gizmos.DrawLine(vehicleTransform.position, _steeringTargetPosition);
            Gizmos.DrawRay(_nearestLinePoint, _trackForward * 3f);
        }

        private void DrawIdealDriverGizmos()
        {
            Gizmos.color = Color.cyan;
            for (var index = 0; index < _idealPath.SampleCount; index++)
            {
                Gizmos.DrawLine(
                    _idealPath.GetSample(index).Position,
                    _idealPath.GetSample(index + 1).Position);
            }

            if (!_hasIdealProjection)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            var previous = _idealProjection.Position;
            for (var offset = 0; offset < _idealPath.SampleCount; offset++)
            {
                var sampleIndex = IdealRacingDriverRules.WrapIndex(_currentPathSampleIndex + offset + 1, _idealPath.SampleCount);
                var point = _idealPath.GetSample(sampleIndex).Position;
                Gizmos.DrawLine(previous, point);
                if (sampleIndex == _targetPathSampleIndex)
                {
                    break;
                }

                previous = point;
            }

            Gizmos.color = Color.white;
            Gizmos.DrawSphere(_idealProjection.Position, 0.35f);
            Gizmos.DrawLine(vehicleTransform.position, _idealProjection.Position);
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(_steeringTargetPosition, 0.55f);
            Gizmos.DrawLine(vehicleTransform.position, _steeringTargetPosition);
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
