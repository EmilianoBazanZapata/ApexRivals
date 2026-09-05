using System;
using ApexRivals.Input.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;

namespace ApexRivals.UI.Runtime
{
    public sealed class RaceCoordinatorHudSource : IRaceHudSource, IDisposable
    {
        private readonly RaceCoordinator _raceCoordinator;
        private readonly RaceParticipant _playerParticipant;
        private readonly DrivingInputGate _playerDrivingGate;
        private readonly int _participantCount;
        private bool _disposed;

        public RaceCoordinatorHudSource(RaceCoordinator raceCoordinator, RaceParticipant playerParticipant, DrivingInputGate playerDrivingGate, int participantCount)
        {
            _raceCoordinator = raceCoordinator ?? throw new ArgumentNullException(nameof(raceCoordinator));
            _playerParticipant = playerParticipant ?? throw new ArgumentNullException(nameof(playerParticipant));
            _playerDrivingGate = playerDrivingGate;
            _participantCount = Math.Max(0, participantCount);
            _raceCoordinator.CountdownChanged += OnCountdownChanged;
            _raceCoordinator.LapCompleted += OnLapCompleted;
            _raceCoordinator.PositionChanged += OnPositionChanged;
            _raceCoordinator.RaceStarted += OnRaceStarted;
            _raceCoordinator.RacerFinished += OnRacerFinished;
        }

        public event Action<CountdownChangedEvent> CountdownChanged;
        public event Action<LapCompletedEvent> LapCompleted;
        public event Action<PositionChangedEvent> PositionChanged;
        public event Action<RaceStartedEvent> RaceStarted;
        public event Action<RacerFinishedEvent> RacerFinished;

        public RaceHudSnapshot Snapshot
        {
            get
            {
                var currentLap = 0;
                if (_raceCoordinator.TryGetProgress(_playerParticipant, out var progress))
                {
                    currentLap = progress.CurrentLap;
                }

                return new RaceHudSnapshot(
                    currentLap,
                    _raceCoordinator.TotalLaps,
                    _raceCoordinator.GetLivePosition(_playerParticipant.RacerId),
                    _participantCount,
                    _raceCoordinator.CountdownRemaining,
                    _raceCoordinator.ElapsedRaceTime);
            }
        }

        public bool RacingInputActive => _playerDrivingGate != null && _playerDrivingGate.DrivingAllowed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _raceCoordinator.CountdownChanged -= OnCountdownChanged;
            _raceCoordinator.LapCompleted -= OnLapCompleted;
            _raceCoordinator.PositionChanged -= OnPositionChanged;
            _raceCoordinator.RaceStarted -= OnRaceStarted;
            _raceCoordinator.RacerFinished -= OnRacerFinished;
            _disposed = true;
        }

        private void OnCountdownChanged(CountdownChangedEvent raceEvent)
        {
            CountdownChanged?.Invoke(raceEvent);
        }

        private void OnLapCompleted(LapCompletedEvent raceEvent)
        {
            if (raceEvent.RacerId == _playerParticipant.RacerId)
            {
                LapCompleted?.Invoke(raceEvent);
            }
        }

        private void OnPositionChanged(PositionChangedEvent raceEvent)
        {
            if (raceEvent.RacerId == _playerParticipant.RacerId)
            {
                PositionChanged?.Invoke(raceEvent);
            }
        }

        private void OnRaceStarted(RaceStartedEvent raceEvent)
        {
            RaceStarted?.Invoke(raceEvent);
        }

        private void OnRacerFinished(RacerFinishedEvent raceEvent)
        {
            if (raceEvent.Result.RacerId == _playerParticipant.RacerId)
            {
                RacerFinished?.Invoke(raceEvent);
            }
        }
    }
}
