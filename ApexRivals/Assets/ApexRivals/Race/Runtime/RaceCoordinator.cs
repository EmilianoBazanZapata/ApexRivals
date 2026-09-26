using System;
using System.Collections.Generic;
using ApexRivals.Input.Runtime;
using ApexRivals.Race.Configuration;
using UnityEngine;

namespace ApexRivals.Race.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RaceCoordinator : MonoBehaviour
    {
        [SerializeField]
        private RaceDefinition raceDefinition;

        [SerializeField]
        private RaceCheckpoint[] orderedCheckpoints = Array.Empty<RaceCheckpoint>();

        [Header("Start Progress")]
        [SerializeField, Min(-1)]
        [Tooltip("Checkpoint already satisfied when every racer spawns on the start/finish area. Set to -1 for tracks whose grid is elsewhere.")]
        private int initiallyConsumedCheckpointIndex = 0;

        [SerializeField]
        private DrivingInputGate[] drivingGates = Array.Empty<DrivingInputGate>();

        [SerializeField]
        private RaceParticipant raceCompletionParticipant;

        [SerializeField, Min(0.05f)]
        private float positionUpdateInterval = 0.25f;

        [SerializeField]
        private bool startCountdownOnEnable = true;

        [Header("Debug State")]
        [SerializeField]
        private RaceState state = RaceState.NotStarted;

        [SerializeField]
        private float countdownRemaining;

        [SerializeField]
        private float elapsedRaceTime;

        [SerializeField]
        private string leaderRacerId = string.Empty;

        private readonly Dictionary<RaceParticipant, RacerProgress> _progressByParticipant = new Dictionary<RaceParticipant, RacerProgress>();
        private readonly List<RaceParticipant> _participants = new List<RaceParticipant>();
        private readonly List<RacerPositionSnapshot> _positionSnapshots = new List<RacerPositionSnapshot>();
        private readonly List<Vector3> _checkpointPositions = new List<Vector3>();
        private readonly Dictionary<string, int> _livePositionsByRacerId = new Dictionary<string, int>();
        private readonly List<RaceResult> _results = new List<RaceResult>();

        private float _positionUpdateTimer;
        private bool _raceStartedRaised;
        private bool _raceFinishedRaised;

        public event Action<CountdownChangedEvent> CountdownChanged;
        public event Action<RaceStartedEvent> RaceStarted;
        public event Action<CheckpointPassedEvent> CheckpointPassed;
        public event Action<LapCompletedEvent> LapCompleted;
        public event Action<PositionChangedEvent> PositionChanged;
        public event Action<RacerFinishedEvent> RacerFinished;
        public event Action<RaceFinishedEvent> RaceFinished;

        public RaceState State => state;
        public float CountdownRemaining => countdownRemaining;
        public float ElapsedRaceTime => elapsedRaceTime;
        public IReadOnlyList<RaceResult> Results => _results;

        // The race-completion participant is configured as the human player (see
        // ConfigureSession / RaceCoordinatorSessionAdapter). Exposed read-only so
        // presentation-only systems (e.g. checkpoint visuals) can identify the
        // player's progress without a second, duplicate notion of "who is the player".
        public RaceParticipant PlayerParticipant => raceCompletionParticipant;

        // Read-only view of the authoritative ordered checkpoint list, for
        // presentation systems that need to map a checkpoint index to its
        // GameObject without maintaining their own duplicate array.
        public IReadOnlyList<RaceCheckpoint> OrderedCheckpoints => orderedCheckpoints;

        public int TotalLaps => raceDefinition != null ? raceDefinition.TotalLaps : 3;
        private float CountdownDuration => raceDefinition != null ? raceDefinition.CountdownDuration : 3f;

        private void Awake()
        {
            CacheCheckpointPositions();
            SetDrivingAllowed(false);
            countdownRemaining = CountdownDuration;
        }

        private void OnEnable()
        {
            if (startCountdownOnEnable)
            {
                StartCountdown();
            }
        }

        private void Update()
        {
            if (state == RaceState.Countdown)
            {
                UpdateCountdown();
                return;
            }

            if (state != RaceState.Racing)
            {
                return;
            }

            elapsedRaceTime += Time.deltaTime;
            _positionUpdateTimer -= Time.deltaTime;

            if (_positionUpdateTimer <= 0f)
            {
                _positionUpdateTimer = positionUpdateInterval;
                RecalculatePositions();
            }
        }

        public bool StartCountdown()
        {
            if (state != RaceState.NotStarted || !ValidateCheckpointSequence())
            {
                return false;
            }

            state = RaceState.Countdown;
            countdownRemaining = CountdownDuration;
            elapsedRaceTime = 0f;
            _positionUpdateTimer = 0f;
            SetDrivingAllowed(false);
            CountdownChanged?.Invoke(new CountdownChangedEvent(countdownRemaining));
            return true;
        }

        public void ConfigureSession(
            IReadOnlyList<RaceParticipant> participants,
            IReadOnlyList<DrivingInputGate> gates,
            RaceParticipant completionParticipant)
        {
            ResetSessionState();
            raceCompletionParticipant = completionParticipant;
            drivingGates = CopyGates(gates);
            SetDrivingAllowed(false);

            if (participants == null)
            {
                return;
            }

            for (var index = 0; index < participants.Count; index++)
            {
                RegisterRacer(participants[index]);
            }
        }

        public void ResetSessionState()
        {
            state = RaceState.NotStarted;
            countdownRemaining = CountdownDuration;
            elapsedRaceTime = 0f;
            leaderRacerId = string.Empty;
            _positionUpdateTimer = 0f;
            _raceStartedRaised = false;
            _raceFinishedRaised = false;
            _progressByParticipant.Clear();
            _participants.Clear();
            _positionSnapshots.Clear();
            _livePositionsByRacerId.Clear();
            _results.Clear();
            CacheCheckpointPositions();
            SetDrivingAllowed(false);
        }

        public void SetSessionDrivingAllowed(bool isAllowed)
        {
            SetDrivingAllowed(isAllowed);
        }

        public void RegisterRacer(RaceParticipant participant)
        {
            if (participant == null || _progressByParticipant.ContainsKey(participant))
            {
                return;
            }

            var progress = new RacerProgress(
                participant.RacerId,
                orderedCheckpoints.Length,
                GetInitiallyConsumedCheckpointIndex());
            _progressByParticipant.Add(participant, progress);
            _participants.Add(participant);
            RecalculatePositions();
        }

        public void UnregisterRacer(RaceParticipant participant)
        {
            if (participant == null || !_progressByParticipant.Remove(participant))
            {
                return;
            }

            _participants.Remove(participant);
            RecalculatePositions();
        }

        public CheckpointPassResult TryPassCheckpoint(RaceParticipant participant, RaceCheckpoint checkpoint)
        {
            if (state == RaceState.Finished)
            {
                return new CheckpointPassResult(CheckpointPassStatus.RaceAlreadyFinished, false, false);
            }

            if (state != RaceState.Racing || participant == null || checkpoint == null || !_progressByParticipant.TryGetValue(participant, out var progress))
            {
                return new CheckpointPassResult(CheckpointPassStatus.InvalidCheckpoint, false, false);
            }

            var nextFinishPosition = _results.Count + 1;
            var result = progress.PassCheckpoint(
                checkpoint.CheckpointIndex,
                orderedCheckpoints.Length,
                TotalLaps,
                elapsedRaceTime,
                nextFinishPosition);

            if (!result.Accepted)
            {
                return result;
            }

            CheckpointPassed?.Invoke(new CheckpointPassedEvent(progress.RacerId, checkpoint.CheckpointIndex, progress.CurrentLap));

            if (result.LapCompleted)
            {
                LapCompleted?.Invoke(new LapCompletedEvent(progress.RacerId, progress.CompletedLaps, TotalLaps));
            }

            if (result.RacerFinished)
            {
                var raceResult = new RaceResult(progress.RacerId, progress.FinalPosition, progress.FinishTime);
                _results.Add(raceResult);
                RacerFinished?.Invoke(new RacerFinishedEvent(raceResult));

                if (ShouldFinishRace(participant))
                {
                    _raceFinishedRaised = true;
                    state = RaceState.Finished;
                    SetDrivingAllowed(false);
                    RaceFinished?.Invoke(new RaceFinishedEvent(raceResult));
                }
            }

            RecalculatePositions();
            return result;
        }

        private bool ShouldFinishRace(RaceParticipant participant)
        {
            if (_raceFinishedRaised)
            {
                return false;
            }

            return raceCompletionParticipant == null || raceCompletionParticipant == participant;
        }

        public bool TryGetProgress(RaceParticipant participant, out RacerProgress progress)
        {
            return _progressByParticipant.TryGetValue(participant, out progress);
        }

        public int GetLivePosition(string racerId)
        {
            return _livePositionsByRacerId.TryGetValue(racerId, out var position) ? position : 0;
        }

        private void UpdateCountdown()
        {
            countdownRemaining = Mathf.Max(0f, countdownRemaining - Time.deltaTime);
            CountdownChanged?.Invoke(new CountdownChangedEvent(countdownRemaining));

            if (countdownRemaining > 0f)
            {
                return;
            }

            state = RaceState.Racing;
            elapsedRaceTime = 0f;
            SetDrivingAllowed(true);

            if (!_raceStartedRaised)
            {
                _raceStartedRaised = true;
                RaceStarted?.Invoke(new RaceStartedEvent(elapsedRaceTime));
            }

            RecalculatePositions();
        }

        private bool ValidateCheckpointSequence()
        {
            var checkpointIndices = new int[orderedCheckpoints.Length];
            for (var index = 0; index < orderedCheckpoints.Length; index++)
            {
                if (orderedCheckpoints[index] == null)
                {
                    return false;
                }

                checkpointIndices[index] = orderedCheckpoints[index].CheckpointIndex;
            }

            return RaceRules.IsCheckpointSequenceValid(checkpointIndices);
        }

        private int GetInitiallyConsumedCheckpointIndex()
        {
            return initiallyConsumedCheckpointIndex >= 0 && initiallyConsumedCheckpointIndex < orderedCheckpoints.Length
                ? initiallyConsumedCheckpointIndex
                : -1;
        }

        private void CacheCheckpointPositions()
        {
            _checkpointPositions.Clear();

            for (var index = 0; index < orderedCheckpoints.Length; index++)
            {
                if (orderedCheckpoints[index] != null)
                {
                    _checkpointPositions.Add(orderedCheckpoints[index].Position);
                }
            }
        }

        private void RecalculatePositions()
        {
            _positionSnapshots.Clear();

            for (var index = 0; index < _participants.Count; index++)
            {
                var participant = _participants[index];
                if (participant == null || !_progressByParticipant.TryGetValue(participant, out var progress))
                {
                    continue;
                }

                _positionSnapshots.Add(new RacerPositionSnapshot(progress, participant.ProgressTransform.position));
            }

            _positionSnapshots.Sort((left, right) => RaceRules.CompareRacePosition(left, right, _checkpointPositions));

            for (var index = 0; index < _positionSnapshots.Count; index++)
            {
                var position = index + 1;
                var racerId = _positionSnapshots[index].Progress.RacerId;

                if (!_livePositionsByRacerId.TryGetValue(racerId, out var previousPosition) || previousPosition != position)
                {
                    _livePositionsByRacerId[racerId] = position;
                    PositionChanged?.Invoke(new PositionChangedEvent(racerId, position));
                }
            }

            leaderRacerId = _positionSnapshots.Count > 0 ? _positionSnapshots[0].Progress.RacerId : string.Empty;
        }

        private void SetDrivingAllowed(bool isAllowed)
        {
            for (var index = 0; index < drivingGates.Length; index++)
            {
                if (drivingGates[index] != null)
                {
                    drivingGates[index].SetDrivingAllowed(isAllowed);
                }
            }
        }

        private static DrivingInputGate[] CopyGates(IReadOnlyList<DrivingInputGate> gates)
        {
            if (gates == null)
            {
                return Array.Empty<DrivingInputGate>();
            }

            var copiedGates = new DrivingInputGate[gates.Count];
            for (var index = 0; index < gates.Count; index++)
            {
                copiedGates[index] = gates[index];
            }

            return copiedGates;
        }
    }
}
