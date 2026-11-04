using System;
using System.Threading.Tasks;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using UnityObject = UnityEngine.Object;

namespace ApexRivals.UI.Runtime
{
    public sealed class ResultsPresenter : IDisposable
    {
        private IResultsView _view;
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
            if (!CanRender)
            {
                return Task.CompletedTask;
            }

            return RunOnce(_raceSession.Retry, PresentationFailure.RetryFailed, "ui.results.retryFailed");
        }

        /// <summary>CONTINUE: back to Track Selection to pick the next race.</summary>
        public async Task Continue()
        {
            if (!CanRender || _transitioning)
            {
                return;
            }

            _sceneFlow.RequestTrackSelection();
            await RunOnce(_raceSession.ReturnToGarage, PresentationFailure.NavigationFailed, "ui.results.continueFailed");
            if (_failure != PresentationFailure.None)
            {
                // The garage was not loaded, so nothing consumed the request; drop it.
                _sceneFlow.ConsumeTrackSelectionRequest();
            }
        }

        public Task ReturnToGarage()
        {
            if (!CanRender)
            {
                return Task.CompletedTask;
            }

            return RunOnce(_raceSession.ReturnToGarage, PresentationFailure.NavigationFailed, "ui.results.returnGarageFailed");
        }

        public Task ReturnToMainMenu()
        {
            if (!CanRender)
            {
                return Task.CompletedTask;
            }

            return RunOnce(_raceSession.ReturnToMainMenu, PresentationFailure.NavigationFailed, "ui.results.returnMainMenuFailed");
        }

        public void Dispose()
        {
            _disposed = true;
            _view = null;
        }

        private async Task RunOnce(Func<Task<RaceSessionCommandResult>> command, PresentationFailure failure, string failureKey)
        {
            if (!CanRender || _transitioning)
            {
                return;
            }

            _transitioning = true;
            _failure = PresentationFailure.None;
            _messageKey = string.Empty;
            Present();
            var result = await command();
            if (!CanRender)
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

        private bool CanRender => !_disposed && _view != null
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
                _messageKey,
                CareerViewText.FormatOutcomeTitle(result.CareerOutcome),
                CreateCareerBody(result),
                result.CareerOutcome.CareerCompleted,
                CareerPercent(result.CareerOutcome));
        }

        private static int CareerPercent(ApexRivals.Career.Runtime.CareerRaceOutcome outcome)
        {
            if (outcome == null || outcome.Status == ApexRivals.Career.Runtime.CareerRaceOutcomeStatus.None) return -1;
            return outcome.CareerCompleted ? 100 : Math.Max(0, Math.Min(100, outcome.Summary.CompletionPercent));
        }

        private string CreateCareerBody(RaceSessionResult result)
        {
            var body = CareerViewText.FormatOutcomeBody(result.CareerOutcome);
            if (!_sceneFlow.RaceSession.HasResult || result.SaveSucceeded)
            {
                return body;
            }

            // Progress stays in memory and is saved again before leaving, but it is not on disk yet.
            var warning = "PROGRESS NOT SAVED YET: " + result.SaveMessage;
            return string.IsNullOrEmpty(body) ? warning : body + "\n" + warning;
        }
    }
}
