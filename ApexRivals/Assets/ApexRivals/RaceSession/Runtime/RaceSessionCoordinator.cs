using System;
using System.Threading.Tasks;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.SceneFlow.Runtime;

namespace ApexRivals.RaceSession.Runtime
{
    public sealed class RaceSessionCoordinator : IDisposable
    {
        private readonly IRaceSessionSetup _setup;
        private readonly IRaceSessionRaceController _raceController;
        private readonly RaceRewardService _rewardService;
        private readonly PlayerProgressionState _progressionState;
        private readonly RaceProgressionService _raceProgressionService;
        private readonly ISaveCheckpoint _saveCheckpoint;
        private readonly IRaceSessionNavigation _navigation;
        private readonly RaceSessionState _raceSessionState;
        private readonly ITimeScaleController _timeScale;

        private int _attemptId;
        private bool _hasPreparedAttempt;
        private bool _hasStartedAttempt;
        private bool _finishProcessed;
        private bool _resultsEmitted;
        private bool _subscriptionsActive;
        private RaceSessionLifecycleState _stateBeforePause = RaceSessionLifecycleState.Racing;
        private string _playerParticipantId = string.Empty;
        private int _participantCount;
        private RaceSessionResultsSnapshot _lastResults;

        public RaceSessionCoordinator(
            IRaceSessionSetup setup,
            IRaceSessionRaceController raceController,
            RaceRewardService rewardService,
            PlayerProgressionState progressionState,
            ISaveCheckpoint saveCheckpoint,
            IRaceSessionNavigation navigation,
            RaceSessionState raceSessionState,
            ITimeScaleController timeScale,
            RaceProgressionService raceProgressionService)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _raceController = raceController ?? throw new ArgumentNullException(nameof(raceController));
            _rewardService = rewardService ?? throw new ArgumentNullException(nameof(rewardService));
            _progressionState = progressionState ?? throw new ArgumentNullException(nameof(progressionState));
            _raceProgressionService = raceProgressionService ?? throw new ArgumentNullException(nameof(raceProgressionService));
            _saveCheckpoint = saveCheckpoint ?? throw new ArgumentNullException(nameof(saveCheckpoint));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _raceSessionState = raceSessionState ?? throw new ArgumentNullException(nameof(raceSessionState));
            _timeScale = timeScale ?? throw new ArgumentNullException(nameof(timeScale));
            State = RaceSessionLifecycleState.Uninitialized;
        }

        public event Action<RaceSessionLifecycleState, RaceSessionLifecycleState> StateChanged;

        public event Action<PositionChangedEvent> PositionChanged;

        public event Action<RaceSessionResultsSnapshot> ResultsReady;

        public RaceSessionLifecycleState State { get; private set; }
        public RaceSessionResultsSnapshot LastResults => _lastResults;
        public RaceSessionCommandResult LastCommandResult { get; private set; } = RaceSessionCommandResult.Success();
        public RaceHudSnapshot Hud => _raceController.CreateHudSnapshot(_playerParticipantId);
        public RacePauseSnapshot PauseState => new RacePauseSnapshot(State == RaceSessionLifecycleState.Paused, State == RaceSessionLifecycleState.Racing || State == RaceSessionLifecycleState.Paused, State == RaceSessionLifecycleState.Racing || State == RaceSessionLifecycleState.Paused || State == RaceSessionLifecycleState.ShowingResults);
        public UnityEngine.Transform PlayerCameraTarget => _raceController.PlayerCameraTarget;
        public UnityEngine.Rigidbody PlayerVehicleRigidbody => _raceController.PlayerVehicleRigidbody;
        public ApexRivals.Vehicle.Runtime.IDrivingInputProvider PlayerInputProvider => _raceController.PlayerInputProvider;
        public ApexRivals.Vehicle.Runtime.IVehicleTelemetry PlayerVehicleTelemetry => _raceController.PlayerVehicleTelemetry;

        public RaceSessionCommandResult Prepare()
        {
            if (State == RaceSessionLifecycleState.Disposed)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.Disposed, "The race session has been disposed.");
            }

            if (State != RaceSessionLifecycleState.Uninitialized)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.InvalidState, "Race preparation can only start from an uninitialized session.");
            }

            SetState(RaceSessionLifecycleState.Preparing);
            _attemptId++;
            ResetAttemptState(clearResult: true);

            var setupResult = _setup.Prepare();
            if (!setupResult.Succeeded)
            {
                CleanupAttempt();
                SetState(RaceSessionLifecycleState.Failed);
                return SetLastResult(RaceSessionCommandResult.Failure(RaceSessionCommandStatus.SetupFailed, setupResult.Message));
            }

            if (setupResult.Output == null)
            {
                CleanupAttempt();
                SetState(RaceSessionLifecycleState.Failed);
                return SetLastResult(RaceSessionCommandResult.Failure(RaceSessionCommandStatus.SetupFailed, "Race setup did not return a setup output."));
            }

            if (setupResult.Output.Player == null || string.IsNullOrWhiteSpace(setupResult.PlayerParticipantId))
            {
                CleanupAttempt();
                SetState(RaceSessionLifecycleState.Failed);
                return SetLastResult(RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingPlayer, "Race setup did not provide the player participant."));
            }

            if (setupResult.Output.Participants == null || setupResult.Output.Participants.Count == 0)
            {
                CleanupAttempt();
                SetState(RaceSessionLifecycleState.Failed);
                return SetLastResult(RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingPlayer, "Race setup did not provide race participants."));
            }

            if (setupResult.Output.DrivingGates == null || setupResult.Output.DrivingGates.Count == 0)
            {
                CleanupAttempt();
                SetState(RaceSessionLifecycleState.Failed);
                return SetLastResult(RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingDrivingGate, "Race setup did not provide driving gates."));
            }

            if (!_raceController.ConfigureForSession(setupResult.Output, setupResult.PlayerParticipantId, out var configureMessage))
            {
                CleanupAttempt();
                SetState(RaceSessionLifecycleState.Failed);
                return SetLastResult(RaceSessionCommandResult.Failure(RaceSessionCommandStatus.MissingRaceCoordinator, configureMessage));
            }

            _playerParticipantId = setupResult.PlayerParticipantId;
            _participantCount = setupResult.Output.Participants.Count;
            _hasPreparedAttempt = true;
            Subscribe();
            SetState(RaceSessionLifecycleState.Ready);
            return SetLastResult(RaceSessionCommandResult.Success());
        }

        public RaceSessionCommandResult Start()
        {
            if (State == RaceSessionLifecycleState.Disposed)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.Disposed, "The race session has been disposed.");
            }

            if (State != RaceSessionLifecycleState.Ready || !_hasPreparedAttempt)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.InvalidState, "Race cannot start before setup succeeds.");
            }

            if (_hasStartedAttempt)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.AlreadyProcessed, "Race start has already been requested.");
            }

            _hasStartedAttempt = true;
            SetState(RaceSessionLifecycleState.Starting);
            if (!_raceController.StartCountdown())
            {
                SetState(RaceSessionLifecycleState.Failed);
                CleanupAttempt();
                return SetLastResult(RaceSessionCommandResult.Failure(RaceSessionCommandStatus.StartFailed, "Race countdown could not start."));
            }

            return SetLastResult(RaceSessionCommandResult.Success());
        }

        public RaceSessionCommandResult Pause()
        {
            if (State == RaceSessionLifecycleState.Disposed)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.Disposed, "The race session has been disposed.");
            }

            if (State == RaceSessionLifecycleState.Paused)
            {
                return RaceSessionCommandResult.Success();
            }

            if (State != RaceSessionLifecycleState.Racing && State != RaceSessionLifecycleState.Starting)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.InvalidState, "Race can only be paused while racing or starting.");
            }

            _raceController.BlockDriving();
            _timeScale.Pause();
            _navigation.PauseRace();
            _stateBeforePause = State;
            SetState(RaceSessionLifecycleState.Paused);
            return RaceSessionCommandResult.Success();
        }

        public RaceSessionCommandResult Resume()
        {
            if (State == RaceSessionLifecycleState.Disposed)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.Disposed, "The race session has been disposed.");
            }

            if (State != RaceSessionLifecycleState.Paused)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.InvalidState, "Race can only resume from pause.");
            }

            _timeScale.Resume();
            _navigation.ResumeRace();
            var resumedState = _stateBeforePause == RaceSessionLifecycleState.Starting
                ? RaceSessionLifecycleState.Starting
                : RaceSessionLifecycleState.Racing;
            if (resumedState == RaceSessionLifecycleState.Racing)
            {
                _raceController.AllowDriving();
            }

            SetState(resumedState);
            return RaceSessionCommandResult.Success();
        }

        public async Task<RaceSessionCommandResult> Retry()
        {
            if (State == RaceSessionLifecycleState.Disposed)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.Disposed, "The race session has been disposed.");
            }

            if (State != RaceSessionLifecycleState.ShowingResults && State != RaceSessionLifecycleState.Paused && State != RaceSessionLifecycleState.Racing)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.InvalidState, "Retry is only valid during racing, pause or results.");
            }

            SetState(RaceSessionLifecycleState.Retrying);
            _timeScale.Resume();
            CleanupAttempt();
            var navigationResult = await _navigation.RetryRace();
            if (!navigationResult.Succeeded)
            {
                SetState(RaceSessionLifecycleState.Failed);
                return SetLastResult(RaceSessionCommandResult.Failure(RaceSessionCommandStatus.NavigationFailed, navigationResult.Message));
            }

            SetState(RaceSessionLifecycleState.Uninitialized);
            var prepareResult = Prepare();
            if (!prepareResult.Succeeded)
            {
                return prepareResult;
            }

            return Start();
        }

        public Task<RaceSessionCommandResult> ReturnToGarage()
        {
            return Exit(_navigation.ReturnToGarage);
        }

        public Task<RaceSessionCommandResult> ReturnToMainMenu()
        {
            return Exit(_navigation.ReturnToMainMenu);
        }

        public void Dispose()
        {
            if (State == RaceSessionLifecycleState.Disposed)
            {
                return;
            }

            _timeScale.Resume();
            CleanupAttempt();
            SetState(RaceSessionLifecycleState.Disposed);
        }

        private async Task<RaceSessionCommandResult> Exit(Func<Task<SceneTransitionResult>> navigate)
        {
            if (State == RaceSessionLifecycleState.Disposed)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.Disposed, "The race session has been disposed.");
            }

            if (State != RaceSessionLifecycleState.Racing && State != RaceSessionLifecycleState.Paused && State != RaceSessionLifecycleState.ShowingResults)
            {
                return RaceSessionCommandResult.Failure(RaceSessionCommandStatus.InvalidState, "Race exit is only valid during racing, pause or results.");
            }

            SetState(RaceSessionLifecycleState.Exiting);
            _timeScale.Resume();
            _raceController.BlockDriving();
            Unsubscribe();
            _setup.Cleanup();

            var navigationResult = await navigate();
            if (!navigationResult.Succeeded)
            {
                SetState(RaceSessionLifecycleState.Failed);
                return SetLastResult(RaceSessionCommandResult.Failure(RaceSessionCommandStatus.NavigationFailed, navigationResult.Message));
            }

            _raceSessionState.Clear();
            SetState(RaceSessionLifecycleState.Disposed);
            return RaceSessionCommandResult.Success();
        }

        private void HandleRaceStarted(RaceStartedEvent raceStarted)
        {
            if (State != RaceSessionLifecycleState.Starting)
            {
                return;
            }

            SetState(RaceSessionLifecycleState.Racing);
        }

        private void HandleRacerFinished(RacerFinishedEvent racerFinished)
        {
            if (State == RaceSessionLifecycleState.Disposed || State == RaceSessionLifecycleState.Failed || _finishProcessed)
            {
                return;
            }

            if (!string.Equals(racerFinished.Result.RacerId, _playerParticipantId, StringComparison.Ordinal))
            {
                return;
            }

            _finishProcessed = true;
            _raceController.BlockDriving();

            if (!_rewardService.TryAwardReward(racerFinished.Result, out var reward))
            {
                LastCommandResult = RaceSessionCommandResult.Failure(RaceSessionCommandStatus.RewardFailed, "Race reward could not be calculated or awarded.");
                SetState(RaceSessionLifecycleState.Failed);
                CleanupAttempt();
                return;
            }

            var progressionResult = _raceProgressionService.RegisterCompletedPlayerRace();
            if (!progressionResult.Succeeded)
            {
                LastCommandResult = RaceSessionCommandResult.Failure(RaceSessionCommandStatus.ProgressionFailed, progressionResult.Message);
                SetState(RaceSessionLifecycleState.Failed);
                CleanupAttempt();
                return;
            }

            var saveResult = _saveCheckpoint.Save();
            LastCommandResult = saveResult.Succeeded
                ? RaceSessionCommandResult.Success()
                : RaceSessionCommandResult.Failure(RaceSessionCommandStatus.SaveFailedAfterReward, saveResult.Message);
            var result = new RaceSessionResult(
                racerFinished.Result,
                reward,
                _participantCount,
                _progressionState.Currency,
                progressionResult.CompletedRaceCount,
                progressionResult.PreviousTier,
                progressionResult.CurrentTier,
                progressionResult.NewTierReached,
                saveResult.Succeeded,
                saveResult.Message);
            _raceSessionState.SetResult(result);
            _lastResults = new RaceSessionResultsSnapshot(
                racerFinished.Result,
                _participantCount,
                reward,
                _progressionState.Currency,
                progressionResult.CompletedRaceCount,
                progressionResult.PreviousTier,
                progressionResult.CurrentTier,
                progressionResult.NewTierReached,
                saveResult.Succeeded,
                saveResult.Message);
            _navigation.ShowResults(result);
            SetState(RaceSessionLifecycleState.ShowingResults);

            if (!_resultsEmitted)
            {
                _resultsEmitted = true;
                ResultsReady?.Invoke(_lastResults);
            }
        }

        private void HandlePositionChanged(PositionChangedEvent positionChanged)
        {
            if (string.Equals(positionChanged.RacerId, _playerParticipantId, StringComparison.Ordinal))
            {
                PositionChanged?.Invoke(positionChanged);
            }
        }

        private void Subscribe()
        {
            if (_subscriptionsActive)
            {
                return;
            }

            _raceController.RaceStarted += HandleRaceStarted;
            _raceController.PositionChanged += HandlePositionChanged;
            _raceController.RacerFinished += HandleRacerFinished;
            _subscriptionsActive = true;
        }

        private void Unsubscribe()
        {
            if (!_subscriptionsActive)
            {
                return;
            }

            _raceController.RaceStarted -= HandleRaceStarted;
            _raceController.PositionChanged -= HandlePositionChanged;
            _raceController.RacerFinished -= HandleRacerFinished;
            _subscriptionsActive = false;
        }

        private void CleanupAttempt()
        {
            _raceController.BlockDriving();
            Unsubscribe();
            _setup.Cleanup();
            ResetAttemptState(clearResult: false);
        }

        private void ResetAttemptState(bool clearResult)
        {
            _hasPreparedAttempt = false;
            _hasStartedAttempt = false;
            _finishProcessed = false;
            _resultsEmitted = false;
            _playerParticipantId = string.Empty;
            _participantCount = 0;

            if (clearResult)
            {
                _raceSessionState.Clear();
                _lastResults = default;
            }
        }

        private void SetState(RaceSessionLifecycleState state)
        {
            if (State == state)
            {
                return;
            }

            var previousState = State;
            State = state;
            StateChanged?.Invoke(previousState, State);
        }

        private RaceSessionCommandResult SetLastResult(RaceSessionCommandResult result)
        {
            LastCommandResult = result;
            return result;
        }
    }
}
