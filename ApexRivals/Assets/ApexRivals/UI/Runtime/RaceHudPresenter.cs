using System;
using ApexRivals.RaceSession.Runtime;

namespace ApexRivals.UI.Runtime
{
    public sealed class RaceHudPresenter : IDisposable
    {
        private IRaceHudView _view;
        private readonly IRaceHudSource _source;
        private bool _disposed;

        public RaceHudPresenter(IRaceHudView view, IRaceHudSource source)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _source.RefreshRequested += Present;
        }

        public RaceHudViewModel Current { get; private set; }

        public void Present()
        {
            if (_disposed || _view == null
                || (_view is UnityEngine.Object unityView && unityView == null)) return;
            Current = CreateModel(_source.Snapshot, _source.RacingInputActive);
            _view.Render(Current);
        }

        // The view polls while active for clock/countdown and progress without a notification.
        public void UpdateContinuousSnapshot()
        {
            Present();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _source.RefreshRequested -= Present;
            _disposed = true;
            _view = null;
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
