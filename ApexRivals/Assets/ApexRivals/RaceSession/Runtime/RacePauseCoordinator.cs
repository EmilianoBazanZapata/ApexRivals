using System;
using ApexRivals.SceneFlow.Runtime;

namespace ApexRivals.RaceSession.Runtime
{
    // Application-owned so a transition keeps the same frozen clock while its
    // old session unloads and the replacement is installed. Only these three
    // race-specific requests share the underlying time-scale controller.
    public sealed class RacePauseCoordinator : IDisposable
    {
        private readonly ITimeScaleController _clock;
        private readonly SceneFlowService _navigation;
        private int _sessionId;
        private bool _sessionAttached;
        private bool _sessionPaused;
        private bool _fallbackPaused;
        private bool _transitionPaused;
        private bool _holdingClock;
        private bool _disposed;

        public RacePauseCoordinator(ITimeScaleController clock, SceneFlowService navigation = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _navigation = navigation;
            if (_navigation != null) _navigation.StateChanged += OnNavigationChanged;
        }

        public SessionPause CreateSession()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RacePauseCoordinator));
            _sessionId++;
            _sessionAttached = true;
            _sessionPaused = false;
            _fallbackPaused = false;
            _transitionPaused = _navigation?.State == ApplicationState.Transitioning;
            SynchronizeClock();
            return new SessionPause(this, _sessionId);
        }

        private void OnNavigationChanged(ApplicationStateChangedEvent change)
        {
            _transitionPaused = change.CurrentState == ApplicationState.Transitioning
                && (_sessionAttached || change.PreviousState == ApplicationState.Race || _transitionPaused);
            SynchronizeClock();
        }

        private void SynchronizeClock()
        {
            var mustFreeze = _sessionPaused || _fallbackPaused || _transitionPaused;
            if (mustFreeze && !_holdingClock)
            {
                _holdingClock = true;
                _clock.Pause();
            }
            else if (!mustFreeze && _holdingClock)
            {
                _holdingClock = false;
                _clock.Resume();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_navigation != null) _navigation.StateChanged -= OnNavigationChanged;
            _sessionAttached = _sessionPaused = _fallbackPaused = _transitionPaused = false;
            SynchronizeClock();
        }

        // A session can only release its own requests. It cannot release the
        // transition or affect a newer session after replacement/disposal.
        public sealed class SessionPause : ITimeScaleController, IDisposable
        {
            private readonly RacePauseCoordinator _owner;
            private readonly int _id;
            private bool _disposed;
            internal SessionPause(RacePauseCoordinator owner, int id) { _owner = owner; _id = id; }
            private bool IsCurrent => !_disposed && !_owner._disposed && _owner._sessionId == _id;
            public float TimeScale => _owner._clock.TimeScale;
            public void Pause()
            {
                if (!IsCurrent) return;
                _owner._sessionPaused = true;
                _owner.SynchronizeClock();
            }
            public void Resume()
            {
                if (!IsCurrent) return;
                _owner._sessionPaused = false;
                _owner.SynchronizeClock();
            }
            public bool TryOpenFallbackPause()
            {
                if (!IsCurrent) return false;
                _owner._fallbackPaused = true;
                _owner.SynchronizeClock();
                return true;
            }
            public void CloseFallbackPause()
            {
                if (!IsCurrent) return;
                _owner._fallbackPaused = false;
                _owner.SynchronizeClock();
            }
            public void Dispose()
            {
                if (!IsCurrent) { _disposed = true; return; }
                _disposed = true;
                _owner._sessionAttached = false;
                _owner._sessionPaused = _owner._fallbackPaused = false;
                _owner.SynchronizeClock();
            }
        }
    }
}
