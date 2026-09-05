using System;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;

namespace ApexRivals.UI.Runtime
{
    public interface IMainMenuView
    {
        void Render(MainMenuViewModel viewModel);
    }

    public interface IVehicleSelectionView
    {
        void Render(VehicleSelectionViewModel viewModel);
    }

    public interface IGarageView
    {
        void Render(GarageViewModel viewModel);
    }

    public interface IRaceHudView
    {
        void Render(RaceHudViewModel viewModel);
    }

    public interface IResultsView
    {
        void Render(ResultsViewModel viewModel);
    }

    public interface IPauseView
    {
        void Render(PauseViewModel viewModel);
    }

    public interface ISettingsView
    {
        void Render(SettingsViewModel viewModel);
    }

    public interface IApplicationExit
    {
        void Quit();
    }

    public interface IInitialVehicleSelectionProgress
    {
        bool HasCommittedVehicleSelection { get; }
    }

    public interface IPresentationScreenNavigator
    {
        PresentationScreenState CurrentScreen { get; }
        void Show(PresentationScreenState screenState);
    }

    public interface IRaceHudSource
    {
        event Action<CountdownChangedEvent> CountdownChanged;
        event Action<LapCompletedEvent> LapCompleted;
        event Action<PositionChangedEvent> PositionChanged;
        event Action<RaceStartedEvent> RaceStarted;
        event Action<RacerFinishedEvent> RacerFinished;

        RaceHudSnapshot Snapshot { get; }
        bool RacingInputActive { get; }
    }

    public interface IUiInputSource
    {
        event Action Submitted;
        event Action Canceled;
        event Action PausePressed;
    }

    public interface IGameplayInputBlocker
    {
        bool GameplayInputBlocked { get; }
        void SetGameplayInputBlocked(bool blocked);
    }

    public interface IUiSelectionStore
    {
        string CurrentSelectionId { get; }
        void Select(string selectionId);
        void RestorePreviousSelection();
    }

    public sealed class PresentationScreenNavigator : IPresentationScreenNavigator
    {
        public PresentationScreenNavigator(PresentationScreenState initialScreen = PresentationScreenState.Main)
        {
            CurrentScreen = initialScreen;
        }

        public PresentationScreenState CurrentScreen { get; private set; }

        public void Show(PresentationScreenState screenState)
        {
            CurrentScreen = screenState;
        }
    }
}
