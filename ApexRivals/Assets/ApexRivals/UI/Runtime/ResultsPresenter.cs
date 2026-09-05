using System;
using System.Threading.Tasks;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;

namespace ApexRivals.UI.Runtime
{
    public sealed class ResultsPresenter
    {
        private readonly IResultsView _view;
        private readonly RaceSessionCoordinator _raceSession;
        private readonly SceneFlowService _sceneFlow;
        private bool _transitioning;
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
            Current = CreateModel();
            _view.Render(Current);
        }

        public Task Retry()
        {
            return RunOnce(_raceSession.Retry, PresentationFailure.RetryFailed, "ui.results.retryFailed");
        }

        public Task ReturnToGarage()
        {
            return RunOnce(_raceSession.ReturnToGarage, PresentationFailure.NavigationFailed, "ui.results.returnGarageFailed");
        }

        public Task ReturnToMainMenu()
        {
            return RunOnce(_raceSession.ReturnToMainMenu, PresentationFailure.NavigationFailed, "ui.results.returnMainMenuFailed");
        }

        private async Task RunOnce(Func<Task<RaceSessionCommandResult>> command, PresentationFailure failure, string failureKey)
        {
            if (_transitioning)
            {
                return;
            }

            _transitioning = true;
            _failure = PresentationFailure.None;
            _messageKey = string.Empty;
            Present();
            var result = await command();
            _transitioning = false;

            if (!result.Succeeded)
            {
                _failure = failure;
                _messageKey = failureKey;
            }

            Present();
        }

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
                _transitioning,
                _failure,
                _messageKey);
        }
    }
}
