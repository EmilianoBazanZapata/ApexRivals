using System;
using System.Threading.Tasks;

namespace ApexRivals.SceneFlow.Runtime
{
    public sealed class SceneFlowService
    {
        private readonly SceneFlowSceneMap _sceneMap;
        private readonly IContentSceneLoader _sceneLoader;
        private readonly ISaveCheckpoint _saveCheckpoint;
        private readonly IRaceStartGuard _raceStartGuard;
        private bool _transitionInProgress;

        /// <param name="raceStartGuard">Optional rules check run before every new race (not retries).</param>
        public SceneFlowService(SceneFlowSceneMap sceneMap, IContentSceneLoader sceneLoader, ISaveCheckpoint saveCheckpoint = null, IRaceStartGuard raceStartGuard = null)
        {
            _sceneMap = sceneMap ?? throw new ArgumentNullException(nameof(sceneMap));
            _sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            _saveCheckpoint = saveCheckpoint;
            _raceStartGuard = raceStartGuard;
            State = ApplicationState.Bootstrapping;
            RacePresentationState = RacePresentationState.None;
            RaceSession = new RaceSessionState();
        }

        public event Action<ApplicationStateChangedEvent> StateChanged;

        public ApplicationState State { get; private set; }
        public ContentSceneId? CurrentContentScene { get; private set; }
        public RacePresentationState RacePresentationState { get; private set; }
        public RaceSessionState RaceSession { get; }
        public float TransitionProgress => _sceneLoader.Progress;

        /// <summary>Race content scene chosen on the track select screen; empty uses the configured race scene.</summary>
        public string SelectedRaceSceneName { get; private set; } = string.Empty;

        /// <summary>Chooses the scene that <see cref="StartRace"/> and <see cref="RetryRace"/> load.</summary>
        public bool SelectRaceScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || _transitionInProgress)
            {
                return false;
            }

            SelectedRaceSceneName = sceneName;
            return true;
        }

        public Task<SceneTransitionResult> OpenMainMenu()
        {
            return NavigateAsync(ContentSceneId.MainMenu, ApplicationState.MainMenu, CanOpenMainMenu(), false, ShouldSaveBeforeLeavingCurrentState());
        }

        public Task<SceneTransitionResult> OpenGarage()
        {
            return NavigateAsync(ContentSceneId.Garage, ApplicationState.Garage, State == ApplicationState.MainMenu, false, false);
        }

        /// <summary>True between <see cref="OpenTrackSelection"/> and the garage UI consuming the request.</summary>
        public bool TrackSelectionRequested { get; private set; }

        /// <summary>Opens the garage straight on its track select screen (main menu Play).</summary>
        public async Task<SceneTransitionResult> OpenTrackSelection()
        {
            TrackSelectionRequested = true;
            var result = await OpenGarage();
            if (!result.Succeeded)
            {
                TrackSelectionRequested = false;
            }

            return result;
        }

        /// <summary>Returns whether the track select screen was requested and clears the request.</summary>
        public bool ConsumeTrackSelectionRequest()
        {
            var requested = TrackSelectionRequested;
            TrackSelectionRequested = false;
            return requested;
        }

        public Task<SceneTransitionResult> StartRace()
        {
            var shouldSave = State == ApplicationState.Garage;
            return NavigateAsync(ContentSceneId.Race, ApplicationState.Race, State == ApplicationState.MainMenu || State == ApplicationState.Garage, false, shouldSave, true);
        }

        public Task<SceneTransitionResult> ReturnFromRaceToGarage()
        {
            return NavigateAsync(ContentSceneId.Garage, ApplicationState.Garage, State == ApplicationState.Race, false, true);
        }

        public Task<SceneTransitionResult> ReturnFromRaceToMainMenu()
        {
            return NavigateAsync(ContentSceneId.MainMenu, ApplicationState.MainMenu, State == ApplicationState.Race, false, true);
        }

        public Task<SceneTransitionResult> ForceReturnToMainMenu()
        {
            return NavigateAsync(ContentSceneId.MainMenu, ApplicationState.MainMenu, true, false, ShouldSaveBeforeLeavingCurrentState());
        }

        public Task<SceneTransitionResult> RetryRace()
        {
            return NavigateAsync(ContentSceneId.Race, ApplicationState.Race, State == ApplicationState.Race, true, true);
        }

        public bool PauseRace()
        {
            if (State != ApplicationState.Race || RacePresentationState != RacePresentationState.Racing)
            {
                return false;
            }

            RacePresentationState = RacePresentationState.Paused;
            return true;
        }

        public bool ResumeRace()
        {
            if (State != ApplicationState.Race || RacePresentationState != RacePresentationState.Paused)
            {
                return false;
            }

            RacePresentationState = RacePresentationState.Racing;
            return true;
        }

        public bool ShowRaceResults(RaceSessionResult result)
        {
            if (State != ApplicationState.Race)
            {
                return false;
            }

            RaceSession.SetResult(result);
            return ShowStoredRaceResults();
        }

        // Session completion already owns the retained result; navigation only presents it.
        internal bool ShowStoredRaceResults()
        {
            if (State != ApplicationState.Race)
            {
                return false;
            }

            RacePresentationState = RacePresentationState.Results;
            return true;
        }

        public void MarkFailed()
        {
            SetState(ApplicationState.Failed);
        }

        private async Task<SceneTransitionResult> NavigateAsync(
            ContentSceneId targetSceneId,
            ApplicationState targetState,
            bool isAllowed,
            bool reloadCurrentScene,
            bool saveBeforeTransition,
            bool checkRaceStart = false)
        {
            var previousState = State;

            if (_transitionInProgress || State == ApplicationState.Transitioning || _sceneLoader.IsLoading)
            {
                return new SceneTransitionResult(SceneTransitionStatus.ConcurrentTransition, previousState, State, targetSceneId, "A scene transition is already in progress.");
            }

            if (CurrentContentScene == targetSceneId && !reloadCurrentScene)
            {
                return new SceneTransitionResult(SceneTransitionStatus.DuplicateTransition, previousState, State, targetSceneId, "The requested content scene is already active.");
            }

            if (!isAllowed)
            {
                return new SceneTransitionResult(SceneTransitionStatus.InvalidTransition, previousState, State, targetSceneId, "The requested transition is not valid from the current application state.");
            }

            if (!TryGetSceneName(targetSceneId, out var sceneName))
            {
                return new SceneTransitionResult(SceneTransitionStatus.MissingSceneConfiguration, previousState, State, targetSceneId, "The requested content scene is not configured.");
            }

            if (checkRaceStart && _raceStartGuard != null)
            {
                var check = _raceStartGuard.CheckRaceStart(sceneName);
                if (!check.Allowed)
                {
                    return new SceneTransitionResult(SceneTransitionStatus.RaceStartRejected, previousState, State, targetSceneId, check.Message);
                }
            }

            if (saveBeforeTransition && _saveCheckpoint != null)
            {
                try
                {
                    var saveResult = _saveCheckpoint.Save();
                    if (!saveResult.Succeeded)
                    {
                        return new SceneTransitionResult(SceneTransitionStatus.SaveFailed, previousState, State, targetSceneId, saveResult.Message);
                    }
                }
                catch (Exception exception)
                {
                    return new SceneTransitionResult(SceneTransitionStatus.SaveFailed, previousState, State, targetSceneId, exception.Message);
                }
            }

            var previousPresentationState = RacePresentationState;
            _transitionInProgress = true;
            try
            {
                SetState(ApplicationState.Transitioning);
                var loadResult = await _sceneLoader.LoadContentSceneAsync(targetSceneId, sceneName, reloadCurrentScene);
                if (!loadResult.Succeeded)
                {
                    RacePresentationState = previousPresentationState;
                    SetState(previousState);
                    return new SceneTransitionResult(SceneTransitionStatus.SceneLoadFailed, previousState, State, targetSceneId, loadResult.Message);
                }

                CurrentContentScene = targetSceneId;
                RaceSession.Clear();
                if (targetState == ApplicationState.Race)
                {
                    RacePresentationState = RacePresentationState.Racing;
                }
                else
                {
                    RacePresentationState = RacePresentationState.None;
                }

                SetState(targetState);
                return new SceneTransitionResult(SceneTransitionStatus.Succeeded, previousState, State, targetSceneId, string.Empty);
            }
            catch (Exception exception)
            {
                RacePresentationState = previousPresentationState;
                SetState(previousState);
                return new SceneTransitionResult(SceneTransitionStatus.SceneLoadFailed, previousState, State, targetSceneId, exception.Message);
            }
            finally
            {
                _transitionInProgress = false;
            }
        }

        private bool TryGetSceneName(ContentSceneId sceneId, out string sceneName)
        {
            if (sceneId == ContentSceneId.Race && !string.IsNullOrWhiteSpace(SelectedRaceSceneName))
            {
                sceneName = SelectedRaceSceneName;
                return true;
            }

            return _sceneMap.TryGetSceneName(sceneId, out sceneName);
        }

        private bool CanOpenMainMenu()
        {
            return State == ApplicationState.Bootstrapping || State == ApplicationState.Garage;
        }

        private bool ShouldSaveBeforeLeavingCurrentState()
        {
            return State == ApplicationState.Garage || State == ApplicationState.Race;
        }

        private void SetState(ApplicationState state)
        {
            if (State == state)
            {
                return;
            }

            var previousState = State;
            State = state;
            StateChanged?.Invoke(new ApplicationStateChangedEvent(previousState, State));
        }
    }
}
