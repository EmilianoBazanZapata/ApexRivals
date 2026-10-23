using System;
using System.Threading.Tasks;
using UnityObject = UnityEngine.Object;
using ApexRivals.SceneFlow.Runtime;

namespace ApexRivals.UI.Runtime
{
    public sealed class MainMenuPresenter : IDisposable
    {
        private IMainMenuView _view;
        private readonly SceneFlowService _sceneFlow;
        private readonly IInitialVehicleSelectionProgress _selectionProgress;
        private readonly IPresentationScreenNavigator _screenNavigator;
        private readonly IApplicationExit _applicationExit;
        private bool _transitioning;
        private PresentationFailure _failure;
        private string _messageKey = string.Empty;

        public MainMenuPresenter(
            IMainMenuView view,
            SceneFlowService sceneFlow,
            IInitialVehicleSelectionProgress selectionProgress,
            IPresentationScreenNavigator screenNavigator,
            IApplicationExit applicationExit)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
            _selectionProgress = selectionProgress ?? throw new ArgumentNullException(nameof(selectionProgress));
            _screenNavigator = screenNavigator ?? throw new ArgumentNullException(nameof(screenNavigator));
            _applicationExit = applicationExit ?? throw new ArgumentNullException(nameof(applicationExit));
        }

        public MainMenuViewModel Current { get; private set; }

        private bool _disposed;
        private bool CanRender => !_disposed && _view != null
            && (_view is not UnityObject unityView || unityView != null);

        public void Dispose()
        {
            _disposed = true;
            _view = null;
        }

        public void Present()
        {
            if (!CanRender) return;

            Current = CreateModel();
            _view.Render(Current);
        }

        public async Task StartOrContinue()
        {
            if (!CanRunCommand())
            {
                return;
            }

            if (!_selectionProgress.HasCommittedVehicleSelection)
            {
                await RunTransition(_sceneFlow.OpenGarage, "ui.main.openGarageFailed");
                return;
            }

            // Play always goes through the track select screen before racing.
            await RunTransition(_sceneFlow.OpenTrackSelection, "ui.main.startRaceFailed");
        }

        public Task OpenGarage()
        {
            return CanRunCommand()
                ? RunTransition(_sceneFlow.OpenGarage, "ui.main.openGarageFailed")
                : Task.CompletedTask;
        }

        public void OpenSettings()
        {
            if (!CanRunCommand())
            {
                return;
            }

            _failure = PresentationFailure.None;
            _messageKey = string.Empty;
            _screenNavigator.Show(PresentationScreenState.Settings);
            Present();
        }

        public void Quit()
        {
            if (!CanRunCommand())
            {
                return;
            }

            _applicationExit.Quit();
        }

        private async Task RunTransition(Func<Task<SceneTransitionResult>> transition, string failureKey)
        {
            _transitioning = true;
            _failure = PresentationFailure.None;
            _messageKey = string.Empty;
            Present();

            var result = await transition();
            if (!CanRender) return;
            _transitioning = false;

            if (!result.Succeeded)
            {
                _failure = PresentationFailure.NavigationFailed;
                _messageKey = failureKey;
            }

            Present();
        }

        private bool CanRunCommand()
        {
            return CanRender && !_transitioning && _sceneFlow.State != ApplicationState.Transitioning;
        }

        private MainMenuViewModel CreateModel()
        {
            var hasSelection = _selectionProgress.HasCommittedVehicleSelection;
            return new MainMenuViewModel(
                hasSelection ? MainMenuStartTarget.Race : MainMenuStartTarget.InitialVehicleSelection,
                !hasSelection,
                !_transitioning,
                hasSelection && !_transitioning,
                !_transitioning,
                !_transitioning,
                _transitioning,
                _failure,
                _messageKey);
        }
    }
}
