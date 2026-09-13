using System;
using System.Threading.Tasks;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using UnityObject = UnityEngine.Object;

namespace ApexRivals.UI.Runtime
{
    public sealed class PausePresenter : IDisposable
    {
        private readonly IPauseView _view;
        private readonly RaceSessionCoordinator _raceSession;
        private readonly SceneFlowService _sceneFlow;
        private bool _transitioning;
        private bool _disposed;
        private PresentationFailure _failure;
        private string _messageKey = string.Empty;

        public PausePresenter(IPauseView view, RaceSessionCoordinator raceSession, SceneFlowService sceneFlow)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _raceSession = raceSession ?? throw new ArgumentNullException(nameof(raceSession));
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
        }

        public PauseViewModel Current { get; private set; }

        public void Present()
        {
            if (!CanRender)
            {
                return;
            }

            Current = CreateModel();
            _view.Render(Current);
        }

        public void Pause()
        {
            if (_disposed)
            {
                return;
            }

            if (_transitioning || _sceneFlow.State == ApplicationState.Transitioning || _sceneFlow.RacePresentationState == RacePresentationState.Results)
            {
                return;
            }

            var result = _raceSession.Pause();
            if (!result.Succeeded)
            {
                _failure = PresentationFailure.NavigationFailed;
                _messageKey = "ui.pause.pauseFailed";
            }

            Present();
        }

        public void Resume()
        {
            if (_disposed)
            {
                return;
            }

            if (_transitioning)
            {
                return;
            }

            var result = _raceSession.Resume();
            if (!result.Succeeded)
            {
                _failure = PresentationFailure.NavigationFailed;
                _messageKey = "ui.pause.resumeFailed";
            }
            else
            {
                _failure = PresentationFailure.None;
                _messageKey = string.Empty;
            }

            Present();
        }

        public Task Retry()
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            return RunTransition(_raceSession.Retry, PresentationFailure.RetryFailed, "ui.pause.retryFailed");
        }

        public Task ReturnToMainMenu()
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            return RunReturnToMainMenu();
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private async Task RunReturnToMainMenu()
        {
            if (_disposed || _transitioning)
            {
                return;
            }

            _transitioning = true;
            _failure = PresentationFailure.None;
            _messageKey = string.Empty;
            Present();

            var transition = await _sceneFlow.ForceReturnToMainMenu();
            if (_disposed)
            {
                return;
            }

            _transitioning = false;

            if (transition.Succeeded)
            {
                return;
            }

            if (!transition.Succeeded)
            {
                _failure = PresentationFailure.NavigationFailed;
                _messageKey = "ui.pause.returnMainMenuFailed";
            }

            Present();
        }

        private async Task RunTransition(Func<Task<RaceSessionCommandResult>> command, PresentationFailure failure, string failureKey)
        {
            if (_disposed || _transitioning)
            {
                return;
            }

            _transitioning = true;
            _failure = PresentationFailure.None;
            _messageKey = string.Empty;
            Present();
            var result = await command();
            if (_disposed)
            {
                return;
            }

            _transitioning = false;

            if (!result.Succeeded)
            {
                _failure = failure;
                _messageKey = failureKey;
            }

            Present();
        }

        private bool CanRender => !_disposed
            && (_view is not UnityObject unityView || unityView != null);

        private PauseViewModel CreateModel()
        {
            var pause = _raceSession.PauseState;
            var resultsVisible = _sceneFlow.RacePresentationState == RacePresentationState.Results;
            return new PauseViewModel(
                !resultsVisible && _raceSession.State == RaceSessionLifecycleState.Paused,
                _transitioning,
                !resultsVisible && pause.CanResume && !_transitioning,
                !resultsVisible && pause.CanRetry && !_transitioning,
                !resultsVisible && pause.CanReturnToMainMenu && !_transitioning,
                _failure,
                _messageKey);
        }
    }
}
