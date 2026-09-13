using System;
using System.Threading.Tasks;

namespace ApexRivals.SceneFlow.Runtime
{
    public sealed class SceneFlowService
    {
        private readonly SceneFlowSceneMap _sceneMap;
        private readonly IContentSceneLoader _sceneLoader;
        private readonly ISaveCheckpoint _saveCheckpoint;
        private bool _transitionInProgress;

        public SceneFlowService(SceneFlowSceneMap sceneMap, IContentSceneLoader sceneLoader, ISaveCheckpoint saveCheckpoint = null)
        {
            _sceneMap = sceneMap ?? throw new ArgumentNullException(nameof(sceneMap));
            _sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            _saveCheckpoint = saveCheckpoint;
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

        public Task<SceneTransitionResult> OpenMainMenu()
        {
            return NavigateAsync(ContentSceneId.MainMenu, ApplicationState.MainMenu, CanOpenMainMenu(), false, ShouldSaveBeforeLeavingCurrentState());
        }

        public Task<SceneTransitionResult> OpenGarage()
        {
            return NavigateAsync(ContentSceneId.Garage, ApplicationState.Garage, State == ApplicationState.MainMenu, false, false);
        }

        public Task<SceneTransitionResult> StartRace()
        {
            var shouldSave = State == ApplicationState.Garage;
            return NavigateAsync(ContentSceneId.Race, ApplicationState.Race, State == ApplicationState.MainMenu || State == ApplicationState.Garage, false, shouldSave);
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
            bool saveBeforeTransition)
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

            if (!_sceneMap.TryGetSceneName(targetSceneId, out var sceneName))
            {
                return new SceneTransitionResult(SceneTransitionStatus.MissingSceneConfiguration, previousState, State, targetSceneId, "The requested content scene is not configured.");
            }

            if (saveBeforeTransition && _saveCheckpoint != null)
            {
                var saveResult = _saveCheckpoint.Save();
                if (!saveResult.Succeeded)
                {
                    return new SceneTransitionResult(SceneTransitionStatus.SaveFailed, previousState, State, targetSceneId, saveResult.Message);
                }
            }

            var previousPresentationState = RacePresentationState;
            _transitionInProgress = true;
            SetState(ApplicationState.Transitioning);

            var loadResult = await _sceneLoader.LoadContentSceneAsync(targetSceneId, sceneName, reloadCurrentScene);
            _transitionInProgress = false;

            if (!loadResult.Succeeded)
            {
                RacePresentationState = previousPresentationState;
                SetState(previousState);
                return new SceneTransitionResult(SceneTransitionStatus.SceneLoadFailed, previousState, State, targetSceneId, loadResult.Message);
            }

            CurrentContentScene = targetSceneId;
            if (targetState == ApplicationState.Race)
            {
                RaceSession.Clear();
                RacePresentationState = RacePresentationState.Racing;
            }
            else
            {
                RacePresentationState = RacePresentationState.None;
            }

            SetState(targetState);
            return new SceneTransitionResult(SceneTransitionStatus.Succeeded, previousState, State, targetSceneId, string.Empty);
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
