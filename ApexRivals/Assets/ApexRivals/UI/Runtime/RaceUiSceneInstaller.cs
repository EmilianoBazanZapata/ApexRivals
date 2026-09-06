using ApexRivals.Bootstrap.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RaceUiSceneInstaller : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField] private UguiScreenNavigator screenNavigator;
        [SerializeField] private RaceSessionSceneInstaller raceSessionInstaller;
        [SerializeField] private RaceHudUguiView hudView;
        [SerializeField] private PauseUguiView pauseView;
        [SerializeField] private ResultsUguiView resultsView;

        private ApplicationContext _context;
        private RaceHudPresenter _hudPresenter;
        private PausePresenter _pausePresenter;
        private ResultsPresenter _resultsPresenter;
        private RaceSessionHudSource _hudSource;
        private InputAction _pauseAction;
        private bool _subscribed;

        public void Install(ApplicationContext context)
        {
            _context = context;
            if (context == null || context.Navigation == null || raceSessionInstaller == null || raceSessionInstaller.Session == null)
            {
                Debug.LogWarning("Race UI is present but race session is not available yet.", this);
                return;
            }

            if (screenNavigator == null || hudView == null || pauseView == null || resultsView == null)
            {
                Debug.LogError("Race UI has missing serialized references.", this);
                return;
            }

            var session = raceSessionInstaller.Session;
            _pausePresenter = new PausePresenter(pauseView, session, context.Navigation);
            _resultsPresenter = new ResultsPresenter(resultsView, session, context.Navigation);
            pauseView.Bind(_pausePresenter);
            resultsView.Bind(_resultsPresenter);
            BindHud(session);

            session.StateChanged += OnSessionStateChanged;
            session.ResultsReady += OnResultsReady;
            context.Navigation.StateChanged += OnApplicationStateChanged;
            _subscribed = true;
            CreatePauseAction();
            screenNavigator.Show(PresentationScreenState.Racing);
            _pausePresenter.Present();
            _resultsPresenter.Present();
            _hudPresenter?.Present();
        }

        public void Uninstall()
        {
            if (_subscribed && raceSessionInstaller != null && raceSessionInstaller.Session != null)
            {
                raceSessionInstaller.Session.StateChanged -= OnSessionStateChanged;
                raceSessionInstaller.Session.ResultsReady -= OnResultsReady;
            }

            if (_subscribed && _context != null && _context.Navigation != null)
            {
                _context.Navigation.StateChanged -= OnApplicationStateChanged;
            }

            _subscribed = false;
            if (_pauseAction != null)
            {
                _pauseAction.performed -= OnPausePerformed;
                _pauseAction.Disable();
                _pauseAction.Dispose();
                _pauseAction = null;
            }

            _hudPresenter?.Dispose();
            _hudSource?.Dispose();
            _hudSource = null;
            _hudPresenter = null;
            _pausePresenter = null;
            _resultsPresenter = null;
            _context = null;
        }

        private void BindHud(RaceSessionCoordinator session)
        {
            _hudSource = new RaceSessionHudSource(session);
            _hudPresenter = new RaceHudPresenter(hudView, _hudSource);
            hudView.Bind(_hudPresenter);
        }

        private void CreatePauseAction()
        {
            if (_pauseAction != null)
            {
                return;
            }

            _pauseAction = new InputAction("Pause", InputActionType.Button);
            _pauseAction.AddBinding("<Keyboard>/escape");
            _pauseAction.AddBinding("<Gamepad>/start");
            _pauseAction.performed += OnPausePerformed;
            _pauseAction.Enable();
        }

        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            if (_context == null || _context.Navigation.State == ApplicationState.Transitioning || _context.Navigation.RacePresentationState == RacePresentationState.Results)
            {
                return;
            }

            if (raceSessionInstaller.Session != null && raceSessionInstaller.Session.State == RaceSessionLifecycleState.Paused)
            {
                _pausePresenter?.Resume();
                return;
            }

            _pausePresenter?.Pause();
        }

        private void OnSessionStateChanged(RaceSessionLifecycleState previous, RaceSessionLifecycleState current)
        {
            if (_context == null)
            {
                return;
            }

            if (current == RaceSessionLifecycleState.Paused)
            {
                screenNavigator.Show(PresentationScreenState.Paused);
            }
            else if (current == RaceSessionLifecycleState.Racing || current == RaceSessionLifecycleState.Starting)
            {
                screenNavigator.Show(PresentationScreenState.Racing);
            }

            _pausePresenter?.Present();
        }

        private void OnResultsReady(RaceSessionResultsSnapshot results)
        {
            screenNavigator.Show(PresentationScreenState.Results);
            _resultsPresenter?.Present();
            _pausePresenter?.Present();
        }

        private void OnApplicationStateChanged(ApplicationStateChangedEvent stateChanged)
        {
            if (_context != null && _context.Navigation.RacePresentationState == RacePresentationState.Results)
            {
                screenNavigator.Show(PresentationScreenState.Results);
            }
        }

        private sealed class RaceSessionHudSource : IRaceHudSource, System.IDisposable
        {
            private readonly RaceSessionCoordinator _session;

            public RaceSessionHudSource(RaceSessionCoordinator session)
            {
                _session = session;
                _session.StateChanged += OnStateChanged;
            }

            public event System.Action<ApexRivals.Race.Runtime.CountdownChangedEvent> CountdownChanged;
            public event System.Action<ApexRivals.Race.Runtime.LapCompletedEvent> LapCompleted;
            public event System.Action<ApexRivals.Race.Runtime.PositionChangedEvent> PositionChanged;
            public event System.Action<ApexRivals.Race.Runtime.RaceStartedEvent> RaceStarted;
            public event System.Action<ApexRivals.Race.Runtime.RacerFinishedEvent> RacerFinished;

            public RaceHudSnapshot Snapshot => _session.Hud;
            public bool RacingInputActive => _session.State == RaceSessionLifecycleState.Racing;

            public void Dispose()
            {
                _session.StateChanged -= OnStateChanged;
            }

            private void OnStateChanged(RaceSessionLifecycleState previous, RaceSessionLifecycleState current)
            {
                if (current == RaceSessionLifecycleState.Racing)
                {
                    RaceStarted?.Invoke(new ApexRivals.Race.Runtime.RaceStartedEvent(0f));
                }
            }
        }
    }
}
