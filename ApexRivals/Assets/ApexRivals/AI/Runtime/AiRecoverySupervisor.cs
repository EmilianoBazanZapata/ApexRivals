using System.Collections.Generic;
using ApexRivals.AI.Configuration;
using ApexRivals.Race.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;

namespace ApexRivals.AI.Runtime
{
    public enum AiRejoinTransition
    {
        None,
        Enter,
        Stay,
        Exit
    }

    public enum AiResetProgress
    {
        Waiting,
        Completed
    }

    /// <summary>Everything a reset needs to know about the car, its track position and the race.</summary>
    public readonly struct AiResetContext
    {
        public AiResetContext(
            AiPathTracker tracker,
            VehicleResetter resetter,
            AiDriverConfiguration configuration,
            RaceCoordinator coordinator,
            RaceParticipant participant,
            float verticalClearance)
        {
            Tracker = tracker;
            Resetter = resetter;
            Configuration = configuration;
            Coordinator = coordinator;
            Participant = participant;
            VerticalClearance = verticalClearance;
        }

        public AiPathTracker Tracker { get; }
        public VehicleResetter Resetter { get; }
        public AiDriverConfiguration Configuration { get; }
        public RaceCoordinator Coordinator { get; }
        public RaceParticipant Participant { get; }
        public float VerticalClearance { get; }
    }

    /// <summary>
    /// Watches for a driver that cannot make progress and escalates: rejoin the track, reverse out of a
    /// trap, and finally reset the car onto the ideal line (past the trouble spot, never past the next
    /// checkpoint), retrying beside the spot when another car occupies it. Keeps the incident log.
    /// </summary>
    public sealed class AiRecoverySupervisor
    {
        private const int MaximumIncidents = 64;
        private const float SafePoseCaptureInterval = 0.5f;
        private const float ResetArrivalTolerance = 2f;
        private const float ResetRetryInterval = 0.5f;
        private const float ReverseMemoryDuration = 6f;
        private const float ResetCooldownDuration = 3f;
        private const float RepeatedResetWindow = 25f;
        private const float RepeatedResetRadius = 30f;
        private const float CheckpointResetMargin = 5f;
        private const float MissedCheckpointMinimumBehind = 20f;
        private const float MissedCheckpointMaximumBehind = 200f;
        private const float MissedCheckpointConfirmDuration = 0.5f;
        private const float MissedCheckpointLeadIn = 10f;
        private const float RejoinEnterFraction = 0.6f;
        private const float RejoinExitFraction = 0.3f;
        private const float SafePoseMinimumSpeed = 2f;
        private const float SafePoseMinimumAlignment = 0.7f;

        private readonly List<AiIncident> _incidents = new List<AiIncident>();

        private float _stuckTimer;
        private float _offTrackTimer;
        private bool _offTrackEpisode;
        private float _upsideDownTimer;
        private float _reverseRemaining;
        private float _reverseMemory;
        private float _graceRemaining;
        private float _cooldownRemaining;
        private float _safePoseTimer;
        private float _missedCheckpointTimer;
        private float _elapsed;

        private bool _resetAttempted;
        private float _retryTimer;
        private int _resetAttempt;
        private float _resetAdvance;
        private int _repeatedResets;
        private float _lastResetSpot;
        private float _lastResetTime = float.NegativeInfinity;
        private int _resetSampleIndex = -1;

        public bool IsResetPending { get; private set; }
        public bool HasResetPosition { get; private set; }
        public Vector3 ResetPosition { get; private set; }
        public AiResetReason LastResetReason { get; private set; }
        public int ResetCount { get; private set; }
        public int OffTrackCount { get; private set; }
        public IReadOnlyList<AiIncident> Incidents => _incidents;
        public float StuckTimer => _stuckTimer;
        public float OffTrackTimer => _offTrackTimer;
        public bool InGrace => _graceRemaining > 0f;

        /// <summary>Clears per-attempt state; counters and the incident log survive for the whole session.</summary>
        public void Reset()
        {
            ClearMotionTimers();
            IsResetPending = false;
            _resetAttempted = false;
            _reverseMemory = 0f;
            _graceRemaining = 0f;
            _cooldownRemaining = 0f;
        }

        public void ClearMotionTimers()
        {
            _stuckTimer = 0f;
            _offTrackTimer = 0f;
            _offTrackEpisode = false;
            _upsideDownTimer = 0f;
            _reverseRemaining = 0f;
            _missedCheckpointTimer = 0f;
        }

        /// <summary>Advances the clocks. Returns true on the frame the post-reset grace ends.</summary>
        public bool Tick(float deltaTime, Transform vehicle, float upsideDownThreshold)
        {
            _elapsed += Mathf.Max(0f, deltaTime);
            var wasInGrace = _graceRemaining > 0f;
            _graceRemaining = Mathf.Max(0f, _graceRemaining - deltaTime);
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - deltaTime);
            _reverseMemory = Mathf.Max(0f, _reverseMemory - deltaTime);
            _upsideDownTimer = AiRecoveryRules.IsUpsideDown(Vector3.Dot(vehicle.up, Vector3.up), upsideDownThreshold)
                ? _upsideDownTimer + deltaTime
                : 0f;
            return wasInGrace && _graceRemaining <= 0f;
        }

        public bool IsUpsideDown(float flippedDuration) => _upsideDownTimer >= flippedDuration;

        public void UpdateOffTrack(float distanceFromLine, float offTrackDistance, float deltaTime, bool alignedWithTrack = true, bool allowRejoin = true)
        {
            // Count the entire failed rejoin, including reversing and small swings toward the line.
            // Only a real return (close AND aligned) earns a fresh recovery window.
            if (!_offTrackEpisode)
            {
                _offTrackEpisode = distanceFromLine > offTrackDistance
                    || (allowRejoin && distanceFromLine > offTrackDistance * RejoinEnterFraction);
            }

            if (distanceFromLine <= offTrackDistance * RejoinExitFraction && alignedWithTrack)
            {
                _offTrackEpisode = false;
            }

            _offTrackTimer = _offTrackEpisode ? _offTrackTimer + Mathf.Max(0f, deltaTime) : 0f;
        }

        public bool IsOffTrackResetDue(float duration) => duration > 0f && _offTrackTimer >= duration;

        /// <summary>True once the car has been past its next checkpoint long enough to be sure it missed it.</summary>
        public bool UpdateMissedCheckpoint(float gapToNextCheckpoint, float lapLength, float deltaTime)
        {
            _missedCheckpointTimer = AiRecoveryRules.IsCheckpointMissed(gapToNextCheckpoint, lapLength, MissedCheckpointMinimumBehind, MissedCheckpointMaximumBehind)
                ? _missedCheckpointTimer + deltaTime
                : 0f;
            return _missedCheckpointTimer >= MissedCheckpointConfirmDuration;
        }

        public void UpdateStuck(bool expectedToMove, float speed, AiDriverTuning tuning, float deltaTime)
        {
            _stuckTimer = AiDriverRules.UpdateStuckTimer(true, expectedToMove, speed, tuning.StuckSpeedThreshold, deltaTime, _stuckTimer);
        }

        /// <summary>The escalation step to take now, suppressed during post-reset grace and cooldown.</summary>
        public AiRecoveryAction Evaluate(bool upsideDown, AiDriverTuning tuning, AiDriverConfiguration configuration, out AiResetReason reason)
        {
            if (IsOffTrackResetDue(configuration.OffTrackResetDuration))
            {
                reason = AiResetReason.OffTrack;
                return AiRecoveryAction.Reset;
            }

            if (_graceRemaining > 0f || _cooldownRemaining > 0f)
            {
                reason = AiResetReason.None;
                return AiRecoveryAction.None;
            }

            return AiRecoveryRules.ResolveRecoveryAction(
                upsideDown,
                _stuckTimer,
                tuning.StuckDetectionDuration,
                _reverseMemory > 0f,
                _offTrackTimer,
                configuration.OffTrackResetDuration,
                out reason);
        }

        /// <summary>
        /// Off-track hysteresis: enter beyond 60 % of the off-track distance, leave only when back within
        /// 30 % and pointing along the track, so the car does not swing straight back across.
        /// </summary>
        public AiRejoinTransition UpdateRejoin(bool rejoining, bool canEnter, float distanceFromLine, bool alignedWithTrack, float offTrackDistance)
        {
            if (rejoining)
            {
                return distanceFromLine <= offTrackDistance * RejoinExitFraction && alignedWithTrack
                    ? AiRejoinTransition.Exit
                    : AiRejoinTransition.Stay;
            }

            if (canEnter && distanceFromLine > offTrackDistance * RejoinEnterFraction)
            {
                OffTrackCount++;
                return AiRejoinTransition.Enter;
            }

            return AiRejoinTransition.None;
        }

        public void BeginReversing(float reverseDuration)
        {
            LastResetReason = AiResetReason.StuckNoProgress;
            _reverseRemaining = Mathf.Max(0.1f, reverseDuration);
            _reverseMemory = ReverseMemoryDuration;
            _stuckTimer = 0f;
        }

        /// <summary>Returns true when the reverse manoeuvre has run its course.</summary>
        public bool UpdateReversing(float deltaTime)
        {
            _reverseRemaining -= deltaTime;
            if (_reverseRemaining > 0f)
            {
                return false;
            }

            _stuckTimer = 0f;
            return true;
        }

        /// <summary>Starts a reset: chooses where the car goes and arms the vehicle request.</summary>
        public void BeginReset(AiResetReason reason, in AiResetContext context)
        {
            LastResetReason = reason;
            IsResetPending = true;
            _resetAttempted = false;
            _resetAttempt = 0;
            _retryTimer = ResetRetryInterval;

            // Repeated failures at the same spot mean something on the track keeps trapping the car
            // (a ramp edge, a wreck): place it progressively further past that spot.
            var tracker = context.Tracker;
            var spot = tracker.DistanceAlongLap;
            var repeated = _elapsed - _lastResetTime < RepeatedResetWindow
                && Mathf.Abs(AiRacecraftRules.WrapLapGap(spot, _lastResetSpot, tracker.Path.LapLength)) < RepeatedResetRadius;
            _repeatedResets = repeated ? _repeatedResets + 1 : 0;
            _lastResetSpot = spot;
            _lastResetTime = _elapsed;
            var gapToNextCheckpoint = tracker.GetGapToNextCheckpoint(context.Coordinator, context.Participant);
            _resetAdvance = reason == AiResetReason.MissedCheckpoint
                ? AiRecoveryRules.CalculateMissedCheckpointResetAdvance(gapToNextCheckpoint, MissedCheckpointLeadIn)
                : AiRecoveryRules.ClampResetAdvanceToCheckpoint(
                    AiRecoveryRules.CalculateResetAdvance(reason, _repeatedResets),
                    gapToNextCheckpoint,
                    CheckpointResetMargin);
            _missedCheckpointTimer = 0f;
            PlaceResetPose(context);
        }

        /// <summary>The vehicle read the request and tried to reset.</summary>
        public void NotifyResetAttempted()
        {
            if (IsResetPending)
            {
                _resetAttempted = true;
            }
        }

        /// <summary>
        /// Waits for the car to arrive at the reset pose. Returns true via <paramref name="reissueRequest"/>
        /// when the request must be sent again (the pose was blocked), or Completed once the car is placed.
        /// </summary>
        public AiResetProgress UpdatePendingReset(float deltaTime, Vector3 vehiclePosition, bool hasController, in AiResetContext context, out bool reissueRequest)
        {
            reissueRequest = false;
            if (HasResetPosition && _resetAttempted && Vector3.Distance(vehiclePosition, ResetPosition) <= ResetArrivalTolerance)
            {
                CompleteReset(context.Configuration);
                return AiResetProgress.Completed;
            }

            _retryTimer -= deltaTime;
            if (_retryTimer > 0f)
            {
                return AiResetProgress.Waiting;
            }

            _retryTimer = ResetRetryInterval;
            if (!hasController && context.Resetter != null)
            {
                // Without a controller to read the request, fall back to the resetter directly.
                if (context.Resetter.TryResetVehicle())
                {
                    CompleteReset(context.Configuration);
                    return AiResetProgress.Completed;
                }

                return AiResetProgress.Waiting;
            }

            // The reset pose may be occupied by another car; try again beside it.
            _resetAttempted = false;
            _resetAttempt++;
            PlaceResetPose(context);
            reissueRequest = true;
            return AiResetProgress.Waiting;
        }

        /// <summary>Path sample the car was reset onto; -1 when unknown.</summary>
        public int ResetSampleIndex => _resetSampleIndex;

        /// <summary>Periodically remembers a safe pose on the line, the fallback when no projection is available at reset time.</summary>
        public void CaptureSafePose(float deltaTime, Transform vehicle, float speed, float flippedDuration, in AiResetContext context)
        {
            _safePoseTimer -= deltaTime;
            if (_safePoseTimer > 0f)
            {
                return;
            }

            _safePoseTimer = SafePoseCaptureInterval;
            var tracker = context.Tracker;
            var aligned = Vector3.Dot(Vector3.ProjectOnPlane(vehicle.forward, Vector3.up).normalized, tracker.Projection.Tangent) > SafePoseMinimumAlignment;
            if (tracker.DistanceFromLine <= context.Configuration.OffTrackDistance * RejoinExitFraction && aligned && !IsUpsideDown(flippedDuration) && speed > SafePoseMinimumSpeed)
            {
                CaptureSafePoseNow(context);
            }
        }

        public void CaptureSafePoseNow(in AiResetContext context)
        {
            var tracker = context.Tracker;
            if (context.Resetter == null || !tracker.HasProjection)
            {
                return;
            }

            ResetPosition = tracker.Projection.Position + Vector3.up * context.VerticalClearance;
            HasResetPosition = true;
            context.Resetter.CaptureResetPose(ResetPosition, Quaternion.LookRotation(tracker.Projection.Tangent, Vector3.up));
        }

        public void RecordIncident(AiIncidentKind kind, float lapDistance, float speed, float targetSpeed, float raceTime)
        {
            if (_incidents.Count >= MaximumIncidents)
            {
                _incidents.RemoveAt(0);
            }

            _incidents.Add(new AiIncident(kind, lapDistance, speed, targetSpeed, raceTime));
        }

        private void CompleteReset(AiDriverConfiguration configuration)
        {
            ResetCount++;
            IsResetPending = false;
            _resetAttempted = false;
            ClearMotionTimers();
            _graceRemaining = configuration.PostResetGraceDuration;
            _cooldownRemaining = ResetCooldownDuration;
        }

        private void PlaceResetPose(in AiResetContext context)
        {
            var tracker = context.Tracker;
            if (context.Resetter == null || !tracker.HasProjection)
            {
                _resetSampleIndex = -1;
                return;
            }

            var path = tracker.Path;
            var point = path.GetPointAhead(tracker.Projection, _resetAdvance, out var sampleIndex, out _);
            var tangent = path.GetSample(sampleIndex).Forward;
            var right = Vector3.Cross(Vector3.up, tangent).normalized;
            var lateral = AiRecoveryRules.CalculateResetLateralOffset(_resetAttempt, context.Configuration.MaximumLateralOffset);
            ResetPosition = point + right * lateral + Vector3.up * context.VerticalClearance;
            HasResetPosition = true;
            _resetSampleIndex = sampleIndex;
            context.Resetter.CaptureResetPose(ResetPosition, Quaternion.LookRotation(tangent, Vector3.up));
        }
    }
}
