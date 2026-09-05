using System;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;

namespace ApexRivals.UI.Runtime
{
    public sealed class RaceHudPresenter : IDisposable
    {
        private readonly IRaceHudView _view;
        private readonly IRaceHudSource _source;
        private bool _disposed;

        public RaceHudPresenter(IRaceHudView view, IRaceHudSource source)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _source.CountdownChanged += OnCountdownChanged;
            _source.LapCompleted += OnLapCompleted;
            _source.PositionChanged += OnPositionChanged;
            _source.RaceStarted += OnRaceStarted;
            _source.RacerFinished += OnRacerFinished;
        }

        public RaceHudViewModel Current { get; private set; }

        public void Present()
        {
            Current = CreateModel(_source.Snapshot, _source.RacingInputActive);
            _view.Render(Current);
        }

        public void UpdateElapsedTimeSnapshot()
        {
            Present();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _source.CountdownChanged -= OnCountdownChanged;
            _source.LapCompleted -= OnLapCompleted;
            _source.PositionChanged -= OnPositionChanged;
            _source.RaceStarted -= OnRaceStarted;
            _source.RacerFinished -= OnRacerFinished;
            _disposed = true;
        }

        private void OnCountdownChanged(CountdownChangedEvent raceEvent)
        {
            Present();
        }

        private void OnLapCompleted(LapCompletedEvent raceEvent)
        {
            Present();
        }

        private void OnPositionChanged(PositionChangedEvent raceEvent)
        {
            Present();
        }

        private void OnRaceStarted(RaceStartedEvent raceEvent)
        {
            Present();
        }

        private void OnRacerFinished(RacerFinishedEvent raceEvent)
        {
            Present();
        }

        private static RaceHudViewModel CreateModel(RaceHudSnapshot snapshot, bool racingInputActive)
        {
            var countdownState = snapshot.CountdownRemaining > 0f
                ? CountdownDisplayState.CountingDown
                : CountdownDisplayState.Hidden;
            return new RaceHudViewModel(
                countdownState,
                snapshot.CountdownRemaining,
                snapshot.CurrentLap,
                snapshot.TotalLaps,
                snapshot.CurrentPosition,
                snapshot.ParticipantCount,
                snapshot.RaceTime,
                racingInputActive);
        }
    }
}
