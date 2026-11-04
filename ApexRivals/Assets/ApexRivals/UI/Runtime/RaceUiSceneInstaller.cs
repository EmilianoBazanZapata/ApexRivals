using ApexRivals.Composition.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Vehicle.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RaceUiSceneInstaller : MonoBehaviour, IContentSceneInstaller
    {
        [SerializeField, Tooltip("Authored session bindings. If unassigned, standalone prototypes retain Escape / Start.")] private InputActionAsset sessionInputActions;
        [SerializeField] private string pauseActionPath = "Session/Pause";

        [SerializeField] private UguiScreenNavigator screenNavigator;
        [SerializeField] private RaceSessionSceneInstaller raceSessionInstaller;
        [SerializeField] private RaceHudUguiView hudView;
        [SerializeField] private VehicleRecoveryPromptUguiView vehicleRecoveryPromptView;
        [SerializeField] private PauseUguiView pauseView;
        [SerializeField] private ResultsUguiView resultsView;
        [SerializeField, Tooltip("Optional. Settings screen opened from the pause menu; Back returns to the pause menu.")] private SettingsUguiView settingsView;
        [SerializeField] private ApexRivals.Camera.Runtime.WheelVehicleFollowCamera followCamera;
        [Header("Minimap (optional)")]
        [SerializeField] private RaceMinimapUguiView minimapView;
        [SerializeField] private ApexRivals.Race.Runtime.RaceCoordinator raceCoordinator;
        [SerializeField] private ApexRivals.AI.Runtime.RacingLine racingLine;

        private ApplicationContext _context;
        private BootstrapUiInputSystem _uiInputOwner;
        private RaceSessionCoordinator _session;
        private RaceHudPresenter _hudPresenter;
        private RaceStandingsTracker _standingsTracker;
        private VehicleRecoveryPromptPresenter _vehicleRecoveryPromptPresenter;
        private PausePresenter _pausePresenter;
        private ResultsPresenter _resultsPresenter;
        private SettingsPresenter _settingsPresenter;
        private RaceSessionHudSource _hudSource;
        private InputAction _pauseAction;
        private double _lastPauseInputTime = double.NegativeInfinity;
        private bool _fallbackPauseActive;
        private bool _subscribed;

        private void OnDestroy() => Uninstall();

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
            if (_subscribed)
            {
                Uninstall();
                CreatePauseAction();
            }
            _context = context;
            _uiInputOwner = FindFirstObjectByType<BootstrapUiInputSystem>();
            if (context == null || context.Navigation == null || raceSessionInstaller == null || raceSessionInstaller.Session == null)
            {
                Debug.LogWarning("Race UI is present but race session is not available yet.", this);
                return;
            }

            if (screenNavigator == null || hudView == null || vehicleRecoveryPromptView == null || pauseView == null || resultsView == null)
            {
                Debug.LogError("Race UI has missing serialized references.", this);
                return;
            }

            var session = raceSessionInstaller.Session;
            _session = session;
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
            if (settingsView != null && context.SettingsService != null)
            {
                _settingsPresenter = new SettingsPresenter(settingsView, context.SettingsService);
                settingsView.Bind(_settingsPresenter, screenNavigator, PresentationScreenState.Paused);
                pauseView.SettingsRequested += OnPauseViewSettingsRequested;
            }

            resultsView.Bind(_resultsPresenter);
            BindHud(session);
            BindVehicleRecoveryPrompt(session);

            session.StateChanged += OnSessionStateChanged;
            session.ResultsReady += OnResultsReady;
            context.Navigation.StateChanged += OnApplicationStateChanged;
            _subscribed = true;
            screenNavigator.Show(session.State == RaceSessionLifecycleState.Ready
                ? PresentationScreenState.Transitioning : PresentationScreenState.Racing);
            _pausePresenter.Present();
            _resultsPresenter.Present();
            _hudPresenter?.Present();
        }

        public void Uninstall()
        {
            if (_subscribed && _session != null)
            {
                _session.StateChanged -= OnSessionStateChanged;
                _session.ResultsReady -= OnResultsReady;
            }

            if (_subscribed && pauseView != null)
            {
                pauseView.ResumeRequested -= OnPauseViewResumeRequested;
                pauseView.SettingsRequested -= OnPauseViewSettingsRequested;
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
            _vehicleRecoveryPromptPresenter?.Dispose();
            _hudSource?.Dispose();
            if (pauseView != null) pauseView.Bind(null);
            if (resultsView != null) resultsView.Bind(null);
            if (settingsView != null) settingsView.Bind(null, null, PresentationScreenState.Paused);
            _settingsPresenter = null;
            hudView?.Bind(null);
            hudView?.BindTelemetry(null);
            hudView?.BindNitro(null);
            hudView?.BindStandings(null);
            if (resultsView != null) resultsView.BindTiming(null);
            _standingsTracker?.Dispose();
            _standingsTracker = null;
            if (minimapView != null) minimapView.Bind(null);
            _hudSource = null;
            _hudPresenter = null;
            _vehicleRecoveryPromptPresenter = null;
            _pausePresenter = null;
            _resultsPresenter = null;
            _context = null;
            _session = null;
        }

        private void BindHud(RaceSessionCoordinator session)
        {
            _hudSource = new RaceSessionHudSource(session);
            _hudPresenter = new RaceHudPresenter(hudView, _hudSource);
            hudView.Bind(_hudPresenter);
            hudView.BindTelemetry(session.PlayerVehicleTelemetry);
            var player = session.PlayerVehicleRigidbody;
            hudView.BindNitro(player != null ? player.GetComponent<PlayerVehicleNitro>() : null);
            if (raceCoordinator != null)
            {
                _standingsTracker = new RaceStandingsTracker(raceCoordinator);
                hudView.BindStandings(_standingsTracker);
                if (resultsView != null) resultsView.BindTiming(_standingsTracker);
            }

            if (minimapView != null && raceCoordinator != null && racingLine != null)
            {
                minimapView.Bind(new RaceCoordinatorMinimapSource(raceCoordinator, racingLine));
            }
        }

        private void BindVehicleRecoveryPrompt(RaceSessionCoordinator session)
        {
            var playerRigidbody = session.PlayerVehicleRigidbody;
            var detector = playerRigidbody != null
                ? playerRigidbody.GetComponent<VehicleRoofRecoveryDetector>()
                : null;
            if (detector == null)
            {
                vehicleRecoveryPromptView.Render(false);
                Debug.LogWarning("Race UI could not resolve the player VehicleRoofRecoveryDetector.", this);
                return;
            }

            _vehicleRecoveryPromptPresenter = new VehicleRecoveryPromptPresenter(vehicleRecoveryPromptView, detector);
            _vehicleRecoveryPromptPresenter.Present();
        }

        private void CreatePauseAction()
        {
            if (_pauseAction != null)
            {
                return;
            }

            if (sessionInputActions != null)
            {
                var authored = sessionInputActions.FindAction(pauseActionPath, false);
                if (authored == null) { Debug.LogError("Pause action missing: " + pauseActionPath, this); return; }
                var map = authored.actionMap.Clone();
                map.bindingMask = authored.actionMap.bindingMask ?? sessionInputActions.bindingMask;
                map.devices = authored.actionMap.devices ?? sessionInputActions.devices;
                _pauseAction = map.FindAction(authored.name, true);
                _pauseAction.bindingMask = authored.bindingMask;
            }
            else
            {
                _pauseAction = new InputAction("Pause", InputActionType.Button);
                _pauseAction.AddBinding("<Keyboard>/escape");
                _pauseAction.AddBinding("<Gamepad>/start");
            }
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
                || raceSessionInstaller.Introduction != null && raceSessionInstaller.Introduction.SuppressCommands
                || _context.LoadingPresentation?.IsVisible == true
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
                if (_uiInputOwner != null && _uiInputOwner.CanHandleCancelInput && _uiInputOwner.IsCancelControl(context.control))
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
            pauseView?.SuppressCancelForCurrentFrame();
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
                CloseFallbackPause();
                screenNavigator.Show(PresentationScreenState.Paused);
            }
            else if (current == RaceSessionLifecycleState.Racing || current == RaceSessionLifecycleState.Starting)
            {
                // Resuming straight from Settings (gamepad Start) drops edits that were never applied.
                if (screenNavigator.CurrentScreen == PresentationScreenState.Settings)
                {
                    _settingsPresenter?.Cancel();
                }

                CloseFallbackPause();
                screenNavigator.Show(PresentationScreenState.Racing);
            }
            else if (current == RaceSessionLifecycleState.ShowingResults || current == RaceSessionLifecycleState.Disposed)
            {
                CloseFallbackPause();
                if (current == RaceSessionLifecycleState.ShowingResults) screenNavigator.Show(PresentationScreenState.Results);
            }

            _pausePresenter?.Present();
        }

        private void OnResultsReady(RaceSessionResult results)
        {
            screenNavigator.Show(PresentationScreenState.Results);
            _resultsPresenter?.Present();
            _pausePresenter?.Present();
        }

        private void OnApplicationStateChanged(ApplicationStateChangedEvent stateChanged)
        {
            if (stateChanged.CurrentState != ApplicationState.Transitioning && _session != null
                && _session.State != RaceSessionLifecycleState.Retrying && _session.State != RaceSessionLifecycleState.Exiting)
                CloseFallbackPause();
            if (_context != null && _context.Navigation.RacePresentationState == RacePresentationState.Results)
            {
                CloseFallbackPause();
                screenNavigator.Show(PresentationScreenState.Results);
            }
        }

        private void OnPauseViewSettingsRequested()
        {
            if (_settingsPresenter == null || _session == null || _session.State != RaceSessionLifecycleState.Paused)
            {
                return;
            }

            _settingsPresenter.Present();
            screenNavigator.Show(PresentationScreenState.Settings);
        }

        private void OnPauseViewResumeRequested()
        {
            // Cancel may resume before this event's Pause callback is delivered.
            _lastPauseInputTime = UnityEngine.InputSystem.LowLevel.InputState.currentTime;
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

            if (_session == null || !_session.TryOpenFallbackPause()) return;
            _fallbackPauseActive = true;
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

            _fallbackPauseActive = false;
            _session?.CloseFallbackPause();
            var screen = _session?.State == RaceSessionLifecycleState.Paused ? PresentationScreenState.Paused
                : _session?.State == RaceSessionLifecycleState.ShowingResults ? PresentationScreenState.Results : PresentationScreenState.Racing;
            screenNavigator.Show(screen);
        }

        private sealed class RaceSessionHudSource : IRaceHudSource, System.IDisposable
        {
            private readonly RaceSessionCoordinator _session;

            public RaceSessionHudSource(RaceSessionCoordinator session)
            {
                _session = session;
                _session.StateChanged += OnStateChanged;
                _session.PositionChanged += OnPositionChanged;
            }

            public event System.Action RefreshRequested;

            public RaceHudSnapshot Snapshot => _session.Hud;
            public bool RacingInputActive => _session.State == RaceSessionLifecycleState.Racing;

            public void Dispose()
            {
                _session.StateChanged -= OnStateChanged;
                _session.PositionChanged -= OnPositionChanged;
            }

            private void OnStateChanged(RaceSessionLifecycleState previous, RaceSessionLifecycleState current)
            {
                RefreshRequested?.Invoke();
            }

            private void OnPositionChanged(ApexRivals.Race.Runtime.PositionChangedEvent positionChanged)
            {
                RefreshRequested?.Invoke();
            }
        }
    }
}
