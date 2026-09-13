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
        [SerializeField] private ApexRivals.Camera.Runtime.WheelVehicleFollowCamera followCamera;

        private ApplicationContext _context;
        private RaceHudPresenter _hudPresenter;
        private PausePresenter _pausePresenter;
        private ResultsPresenter _resultsPresenter;
        private RaceSessionHudSource _hudSource;
        private InputAction _pauseAction;
        private double _lastPauseInputTime = double.NegativeInfinity;
        private bool _fallbackPauseActive;
        private float _fallbackPreviousTimeScale = 1f;
        private CursorLockMode _fallbackPreviousCursorLockMode;
        private bool _fallbackPreviousCursorVisible;
        private bool _subscribed;

        private void OnEnable()
        {
            CreatePauseAction();
        }

        private void OnDisable()
        {
            ReleasePauseAction();
        }

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
            if (followCamera != null)
            {
                followCamera.SetTarget(session.PlayerCameraTarget);
                followCamera.SetVehicleRigidbody(session.PlayerVehicleRigidbody);
                followCamera.SetInputProvider(session.PlayerInputProvider);
            }

            _pausePresenter = new PausePresenter(pauseView, session, context.Navigation);
            _resultsPresenter = new ResultsPresenter(resultsView, session, context.Navigation);
            pauseView.Bind(_pausePresenter);
            pauseView.ResumeRequested += OnPauseViewResumeRequested;
            resultsView.Bind(_resultsPresenter);
            BindHud(session);

            session.StateChanged += OnSessionStateChanged;
            session.ResultsReady += OnResultsReady;
            context.Navigation.StateChanged += OnApplicationStateChanged;
            _subscribed = true;
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

            if (_subscribed && pauseView != null)
            {
                pauseView.ResumeRequested -= OnPauseViewResumeRequested;
            }

            if (_subscribed && _context != null && _context.Navigation != null)
            {
                _context.Navigation.StateChanged -= OnApplicationStateChanged;
            }

            _subscribed = false;
            ReleasePauseAction();

            _pausePresenter?.Dispose();
            _resultsPresenter?.Dispose();
            _hudPresenter?.Dispose();
            _hudSource?.Dispose();
            hudView?.BindTelemetry(null);
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
            hudView.BindTelemetry(session.PlayerVehicleTelemetry);
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

        private void ReleasePauseAction()
        {
            if (_pauseAction == null)
            {
                return;
            }

            _pauseAction.performed -= OnPausePerformed;
            _pauseAction.Disable();
            _pauseAction.Dispose();
            _pauseAction = null;
            _lastPauseInputTime = double.NegativeInfinity;
            CloseFallbackPause();
        }

        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            if (!context.ReadValueAsButton() || context.time - _lastPauseInputTime < 0.2d)
            {
                return;
            }

            if (_context == null
                || _context.Navigation.RacePresentationState == RacePresentationState.Results)
            {
                return;
            }

            var session = raceSessionInstaller.Session;
            if (session == null)
            {
                return;
            }

            if (session.State == RaceSessionLifecycleState.Paused)
            {
                if (context.control != null && context.control.path.Contains("/escape"))
                {
                    return;
                }

                _lastPauseInputTime = context.time;
                _pausePresenter?.Resume();
                return;
            }

            if (_fallbackPauseActive)
            {
                _lastPauseInputTime = context.time;
                CloseFallbackPause();
                return;
            }

            _lastPauseInputTime = context.time;
            _pausePresenter?.Pause();

            if (session.State != RaceSessionLifecycleState.Paused)
            {
                OpenFallbackPause();
            }
        }

        private void OnSessionStateChanged(RaceSessionLifecycleState previous, RaceSessionLifecycleState current)
        {
            if (_context == null)
            {
                return;
            }

            if (current == RaceSessionLifecycleState.Paused)
            {
                _fallbackPauseActive = false;
                screenNavigator.Show(PresentationScreenState.Paused);
            }
            else if (current == RaceSessionLifecycleState.Racing || current == RaceSessionLifecycleState.Starting)
            {
                _fallbackPauseActive = false;
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
                CloseFallbackPause();
                screenNavigator.Show(PresentationScreenState.Results);
            }
        }

        private void OnPauseViewResumeRequested()
        {
            if (_fallbackPauseActive)
            {
                CloseFallbackPause();
            }
        }

        private void OpenFallbackPause()
        {
            if (_fallbackPauseActive)
            {
                return;
            }

            _fallbackPreviousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            _fallbackPreviousCursorLockMode = Cursor.lockState;
            _fallbackPreviousCursorVisible = Cursor.visible;
            _fallbackPauseActive = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            screenNavigator.Show(PresentationScreenState.Paused);

            if (pauseView != null)
            {
                pauseView.Render(new PauseViewModel(
                    true,
                    false,
                    true,
                    false,
                    true,
                    PresentationFailure.None,
                    string.Empty));
            }
        }

        private void CloseFallbackPause()
        {
            if (!_fallbackPauseActive)
            {
                return;
            }

            Time.timeScale = _fallbackPreviousTimeScale > 0f ? _fallbackPreviousTimeScale : 1f;
            Cursor.lockState = _fallbackPreviousCursorLockMode;
            Cursor.visible = _fallbackPreviousCursorVisible;
            _fallbackPauseActive = false;
            screenNavigator.Show(PresentationScreenState.Racing);
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
