using System;
using ApexRivals.SceneFlow.Runtime;

namespace ApexRivals.UI.Runtime
{
    public sealed class UiNavigationCoordinator : IDisposable
    {
        private readonly IUiInputSource _inputSource;
        private readonly IGameplayInputBlocker _gameplayInput;
        private readonly IUiSelectionStore _selectionStore;
        private readonly SceneFlowService _sceneFlow;
        private bool _disposed;

        public UiNavigationCoordinator(
            IUiInputSource inputSource,
            IGameplayInputBlocker gameplayInput,
            IUiSelectionStore selectionStore,
            SceneFlowService sceneFlow)
        {
            _inputSource = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            _gameplayInput = gameplayInput ?? throw new ArgumentNullException(nameof(gameplayInput));
            _selectionStore = selectionStore ?? throw new ArgumentNullException(nameof(selectionStore));
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));

            _inputSource.Submitted += OnSubmitted;
            _inputSource.Canceled += OnCanceled;
            _inputSource.PausePressed += OnPausePressed;
            _sceneFlow.StateChanged += OnApplicationStateChanged;
            RefreshGameplayInputBlock();
        }

        public void SetInitialSelection(string selectionId)
        {
            _selectionStore.Select(selectionId);
        }

        public void RestoreSelection()
        {
            _selectionStore.RestorePreviousSelection();
        }

        public void RefreshGameplayInputBlock()
        {
            var shouldBlock = _sceneFlow.RacePresentationState == RacePresentationState.Paused
                || _sceneFlow.RacePresentationState == RacePresentationState.Results
                || _sceneFlow.State == ApplicationState.Transitioning;
            _gameplayInput.SetGameplayInputBlocked(shouldBlock);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _inputSource.Submitted -= OnSubmitted;
            _inputSource.Canceled -= OnCanceled;
            _inputSource.PausePressed -= OnPausePressed;
            _sceneFlow.StateChanged -= OnApplicationStateChanged;
            _disposed = true;
        }

        private void OnSubmitted()
        {
        }

        private void OnCanceled()
        {
            RestoreSelection();
        }

        private void OnPausePressed()
        {
            if (_sceneFlow.State == ApplicationState.Transitioning)
            {
                return;
            }

            RefreshGameplayInputBlock();
        }

        private void OnApplicationStateChanged(ApplicationStateChangedEvent stateChanged)
        {
            RefreshGameplayInputBlock();
        }
    }
}
