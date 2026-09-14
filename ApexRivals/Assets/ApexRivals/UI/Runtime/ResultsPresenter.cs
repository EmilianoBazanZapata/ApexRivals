using System;
using System.Threading.Tasks;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using UnityObject = UnityEngine.Object;

namespace ApexRivals.UI.Runtime
{
    public sealed class ResultsPresenter : IDisposable
    {
        private readonly IResultsView _view;
        private readonly RaceSessionCoordinator _raceSession;
        private readonly SceneFlowService _sceneFlow;
        private bool _transitioning;
        private bool _disposed;
        private PresentationFailure _failure;
        private string _messageKey = string.Empty;

        public ResultsPresenter(IResultsView view, RaceSessionCoordinator raceSession, SceneFlowService sceneFlow)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _raceSession = raceSession ?? throw new ArgumentNullException(nameof(raceSession));
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
        }

        public ResultsViewModel Current { get; private set; }

        public void Present()
        {
            if (!CanRender)
            {
                return;
            }

            Current = CreateModel();
            _view.Render(Current);
        }

        public Task Retry()
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            return RunOnce(_raceSession.Retry, PresentationFailure.RetryFailed, "ui.results.retryFailed");
        }

        public Task ReturnToGarage()
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            return RunOnce(_raceSession.ReturnToGarage, PresentationFailure.NavigationFailed, "ui.results.returnGarageFailed");
        }

        public Task ReturnToMainMenu()
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            return RunOnce(_raceSession.ReturnToMainMenu, PresentationFailure.NavigationFailed, "ui.results.returnMainMenuFailed");
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private async Task RunOnce(Func<Task<RaceSessionCommandResult>> command, PresentationFailure failure, string failureKey)
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

        private ResultsViewModel CreateModel()
        {
            var result = _sceneFlow.RaceSession.HasResult
                ? _sceneFlow.RaceSession.Result
                : default;
            return new ResultsViewModel(
                result.FinalPosition,
                result.TotalParticipants,
                result.FinishTime,
                result.EarnedReward,
                result.UpdatedCurrency,
                result.CompletedRaceCount,
                result.PreviousTier.TierId,
                result.PreviousTier.DisplayName,
                result.CurrentTier.TierId,
                result.CurrentTier.DisplayName,
                result.NewTierReached,
                _transitioning,
                _failure,
                _messageKey);
        }
    }
}
