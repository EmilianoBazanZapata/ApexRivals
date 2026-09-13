using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.UI.Runtime;
using ApexRivals.Vehicle.Configuration;
using ApexRivals.Vehicle.Runtime;
using ApexRivals.VehicleSelection.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.UI
{
    public sealed class UiPresentationTests
    {
        [Test]
        public async Task MainMenu_StartRoutesToGarageUntilVehicleIsConfirmed()
        {
            var context = CreateUiContext();
            await context.SceneFlow.OpenMainMenu();
            var progress = new FakeSelectionProgress();
            var presenter = new MainMenuPresenter(new FakeMainMenuView(), context.SceneFlow, progress, context.ScreenNavigator, new FakeApplicationExit());

            await presenter.StartOrContinue();

            Assert.That(context.SceneFlow.State, Is.EqualTo(ApplicationState.Garage));
            Assert.That(context.Loader.LastSceneId, Is.EqualTo(ContentSceneId.Garage));
        }

        [Test]
        public async Task MainMenu_StartRoutesToRaceWhenSelectionIsCommitted()
        {
            var context = CreateUiContext();
            await context.SceneFlow.OpenMainMenu();
            var progress = new FakeSelectionProgress { HasCommittedVehicleSelection = true };
            var presenter = new MainMenuPresenter(new FakeMainMenuView(), context.SceneFlow, progress, context.ScreenNavigator, new FakeApplicationExit());

            await presenter.StartOrContinue();

            Assert.That(context.SceneFlow.State, Is.EqualTo(ApplicationState.Race));
            Assert.That(context.Loader.LastSceneId, Is.EqualTo(ContentSceneId.Race));
        }

        [Test]
        public async Task MainMenu_NavigationFailureIsPresented()
        {
            var context = CreateUiContext();
            await context.SceneFlow.OpenMainMenu();
            context.Loader.FailNextLoad = true;
            var presenter = new MainMenuPresenter(new FakeMainMenuView(), context.SceneFlow, new FakeSelectionProgress { HasCommittedVehicleSelection = true }, context.ScreenNavigator, new FakeApplicationExit());

            await presenter.StartOrContinue();

            Assert.That(presenter.Current.Failure, Is.EqualTo(PresentationFailure.NavigationFailed));
        }

        [Test]
        public async Task MainMenu_CommandsAreBlockedDuringTransition()
        {
            var loader = new FakeSceneLoader();
            var context = CreateUiContext(loader);
            await context.SceneFlow.OpenMainMenu();
            loader.CompleteImmediately = false;
            var exit = new FakeApplicationExit();
            var presenter = new MainMenuPresenter(new FakeMainMenuView(), context.SceneFlow, new FakeSelectionProgress { HasCommittedVehicleSelection = true }, context.ScreenNavigator, exit);

            var startTask = presenter.StartOrContinue();
            presenter.Quit();
            loader.CompletePendingLoad(true);
            await startTask;

            Assert.That(exit.QuitCount, Is.Zero);
        }

        [Test]
        public void VehicleSelection_PresentsAvailableVehicles()
        {
            var context = CreateUiContext();
            var presenter = new VehicleSelectionPresenter(new FakeVehicleSelectionView(), context.VehicleSelection, context.SceneFlow);

            presenter.Present();

            Assert.That(presenter.Current.Vehicles.Count, Is.EqualTo(2));
            Assert.That(presenter.Current.Vehicles[0].VehicleId, Is.EqualTo("vanguard"));
            Assert.That(presenter.Current.Vehicles[0].EffectiveStats.Acceleration, Is.GreaterThan(0f));
        }

        [Test]
        public void VehicleSelection_ValidSelectionUpdatesState()
        {
            var context = CreateUiContext();
            var presenter = new VehicleSelectionPresenter(new FakeVehicleSelectionView(), context.VehicleSelection, context.SceneFlow);
            presenter.Present();

            presenter.Select("striker");

            Assert.That(presenter.Current.SelectedVehicleId, Is.EqualTo("striker"));
            Assert.That(presenter.Current.Failure, Is.EqualTo(PresentationFailure.None));
        }

        [Test]
        public void VehicleSelection_InvalidSelectionDisplaysFailure()
        {
            var context = CreateUiContext();
            var presenter = new VehicleSelectionPresenter(new FakeVehicleSelectionView(), context.VehicleSelection, context.SceneFlow);

            presenter.Select("missing");

            Assert.That(presenter.Current.Failure, Is.EqualTo(PresentationFailure.InvalidVehicleSelection));
        }

        [Test]
        public async Task VehicleSelection_ConfirmationRequestsRaceFlow()
        {
            var context = CreateUiContext();
            await context.SceneFlow.OpenMainMenu();
            var presenter = new VehicleSelectionPresenter(new FakeVehicleSelectionView(), context.VehicleSelection, context.SceneFlow);

            presenter.ConfirmSelection();
            await presenter.Continue();

            Assert.That(context.SceneFlow.State, Is.EqualTo(ApplicationState.Race));
        }

        [Test]
        public void Garage_MapsCurrencyUpgradeLevelsAndMaximum()
        {
            var context = CreateUiContext(startingCurrency: 100, vehicleCommitted: true);
            var presenter = new GaragePresenter(new FakeGarageView(), context.Garage, context.VehicleSelection, context.SceneFlow);

            presenter.Present();

            Assert.That(presenter.Current.Currency, Is.EqualTo(100));
            Assert.That(presenter.Current.Engine.CurrentLevel, Is.EqualTo(0));
            Assert.That(presenter.Current.Engine.MaximumLevel, Is.EqualTo(2));
            Assert.That(presenter.Current.Engine.NextPrice, Is.EqualTo(100));
            Assert.That(presenter.Current.Engine.CanPurchase, Is.True);
        }

        [Test]
        public void Garage_SuccessfulPurchaseRefreshesModel()
        {
            var context = CreateUiContext(startingCurrency: 200, vehicleCommitted: true);
            var presenter = new GaragePresenter(new FakeGarageView(), context.Garage, context.VehicleSelection, context.SceneFlow);

            presenter.PurchaseEngine();

            Assert.That(presenter.Current.Currency, Is.EqualTo(100));
            Assert.That(presenter.Current.Engine.CurrentLevel, Is.EqualTo(1));
            Assert.That(presenter.Current.PurchaseStatus, Is.EqualTo(PresentationStatus.Succeeded));
            Assert.That(presenter.Current.EffectiveStats.Acceleration, Is.GreaterThan(CreateBaseStats().Acceleration));
        }

        [Test]
        public void Garage_InsufficientCurrencyIsPresented()
        {
            var context = CreateUiContext(startingCurrency: 50, vehicleCommitted: true);
            var presenter = new GaragePresenter(new FakeGarageView(), context.Garage, context.VehicleSelection, context.SceneFlow);

            presenter.PurchaseEngine();

            Assert.That(presenter.Current.PurchaseStatus, Is.EqualTo(PresentationStatus.InsufficientCurrency));
            Assert.That(presenter.Current.Engine.CurrentLevel, Is.Zero);
        }

        [Test]
        public void Garage_DuplicatePurchaseInputIsRejectedWhileActive()
        {
            var context = CreateUiContext(startingCurrency: 200, vehicleCommitted: true);
            var view = new ReentrantGarageView();
            var presenter = new GaragePresenter(view, context.Garage, context.VehicleSelection, context.SceneFlow);
            view.PurchaseAgain = presenter.PurchaseEngine;

            presenter.PurchaseEngine();

            Assert.That(context.Progression.GetUpgradeLevel(UpgradeType.Engine), Is.EqualTo(1));
        }

        [Test]
        public void RaceHud_MapsLapPositionCountdownAndRaceTime()
        {
            var source = new FakeRaceHudSource();
            var presenter = new RaceHudPresenter(new FakeRaceHudView(), source);

            source.SnapshotValue = new RaceHudSnapshot(2, 3, 1, 4, 2.5f, 12.75f);
            source.RacingInputActiveValue = true;
            source.RaiseLapCompleted();

            Assert.That(presenter.Current.CurrentLap, Is.EqualTo(2));
            Assert.That(presenter.Current.CurrentPosition, Is.EqualTo(1));
            Assert.That(presenter.Current.CountdownValue, Is.EqualTo(2.5f));
            Assert.That(presenter.Current.ElapsedRaceTime, Is.EqualTo(12.75f));
            Assert.That(source.MutationCount, Is.Zero);
        }

        [Test]
        public async Task Results_MapsValuesAndDoesNotApplyReward()
        {
            var context = CreateRaceSessionContext();
            context.SceneFlow.RaceSession.SetResult(new RaceSessionResult(
                new RaceResult("Player", 2, 71f),
                300,
                4,
                900,
                2,
                new RaceProgressionTier("rookie", "Rookie", 0, "rookie"),
                new RaceProgressionTier("amateur", "Amateur", 2, "amateur"),
                true,
                true,
                string.Empty));
            var presenter = new ResultsPresenter(new FakeResultsView(), context.Session, context.SceneFlow);
            var before = context.Progression.Currency;

            presenter.Present();
            await Task.CompletedTask;

            Assert.That(presenter.Current.FinalPosition, Is.EqualTo(2));
            Assert.That(presenter.Current.ParticipantCount, Is.EqualTo(4));
            Assert.That(presenter.Current.EarnedReward, Is.EqualTo(300));
            Assert.That(presenter.Current.CompletedRaceCount, Is.EqualTo(2));
            Assert.That(presenter.Current.PreviousTierId, Is.EqualTo("rookie"));
            Assert.That(presenter.Current.CurrentTierId, Is.EqualTo("amateur"));
            Assert.That(presenter.Current.NewTierReached, Is.True);
            Assert.That(context.Progression.Currency, Is.EqualTo(before));
        }

        [Test]
        public async Task Results_RetryCallsRaceSessionOnceAndDuplicateInputIsBlocked()
        {
            var context = CreateRaceSessionContext();
            StartRace(context);
            context.RaceController.RaiseRacerFinished(new RaceResult("Player", 1, 60f));
            var presenter = new ResultsPresenter(new FakeResultsView(), context.Session, context.SceneFlow);

            var first = presenter.Retry();
            var second = presenter.Retry();
            Assert.That(context.Navigation.RetryCount, Is.EqualTo(1));
            context.Navigation.CompleteRetry();
            await Task.WhenAll(first, second);

            Assert.That(context.Navigation.RetryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Results_ExitActionsCallSceneFlowOnce()
        {
            var context = CreateRaceSessionContext();
            StartRace(context);
            context.RaceController.RaiseRacerFinished(new RaceResult("Player", 1, 60f));
            var presenter = new ResultsPresenter(new FakeResultsView(), context.Session, context.SceneFlow);

            await presenter.ReturnToMainMenu();

            Assert.That(context.Navigation.ReturnToMainMenuCount, Is.EqualTo(1));
        }

        [Test]
        public void Pause_PauseAndResumeCallRaceSession()
        {
            var context = CreateRaceSessionContext();
            StartRace(context);
            var presenter = new PausePresenter(new FakePauseView(), context.Session, context.SceneFlow);

            presenter.Pause();
            presenter.Resume();

            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
            Assert.That(context.TimeScale.PauseCount, Is.EqualTo(1));
            Assert.That(context.TimeScale.ResumeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Pause_ResultsSuppressesPause()
        {
            var context = CreateRaceSessionContext();
            StartRace(context);
            await context.SceneFlow.OpenMainMenu();
            await context.SceneFlow.StartRace();
            context.SceneFlow.ShowRaceResults(new RaceSessionResult(new RaceResult("Player", 1, 60f), 500));
            var presenter = new PausePresenter(new FakePauseView(), context.Session, context.SceneFlow);

            presenter.Pause();

            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
        }

        [Test]
        public async Task Pause_LeavingWhilePausedUsesSafeExitPath()
        {
            var context = CreateRaceSessionContext();
            StartRace(context);
            var presenter = new PausePresenter(new FakePauseView(), context.Session, context.SceneFlow);

            presenter.Pause();
            await presenter.ReturnToMainMenu();

            Assert.That(context.Navigation.ReturnToMainMenuCount, Is.EqualTo(1));
            Assert.That(context.TimeScale.TimeScale, Is.EqualTo(1f));
        }

        [Test]
        public async Task InputNavigation_BlocksAndRestoresGameplayInput()
        {
            var context = CreateUiContext();
            await context.SceneFlow.OpenMainMenu();
            await context.SceneFlow.StartRace();
            var blocker = new FakeGameplayInputBlocker();
            var coordinator = new UiNavigationCoordinator(new FakeUiInputSource(), blocker, new FakeSelectionStore(), context.SceneFlow);

            context.SceneFlow.PauseRace();
            coordinator.RefreshGameplayInputBlock();
            Assert.That(blocker.GameplayInputBlocked, Is.True);

            context.SceneFlow.ResumeRace();
            coordinator.RefreshGameplayInputBlock();
            Assert.That(blocker.GameplayInputBlocked, Is.False);

            context.SceneFlow.ShowRaceResults(new RaceSessionResult(new RaceResult("Player", 1, 60f), 500));
            coordinator.RefreshGameplayInputBlock();
            Assert.That(blocker.GameplayInputBlocked, Is.True);
        }

        [Test]
        public void InputNavigation_SubscriptionsAreRemovedOnDispose()
        {
            var context = CreateUiContext();
            var input = new FakeUiInputSource();
            var store = new FakeSelectionStore();
            var coordinator = new UiNavigationCoordinator(input, new FakeGameplayInputBlocker(), store, context.SceneFlow);

            coordinator.Dispose();
            input.RaiseCanceled();

            Assert.That(store.RestoreCount, Is.Zero);
        }

        private static UiContext CreateUiContext(FakeSceneLoader loader = null, int startingCurrency = 0, bool vehicleCommitted = false)
        {
            var activeLoader = loader ?? new FakeSceneLoader();
            var sceneFlow = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), activeLoader);
            var progression = new PlayerProgressionState(startingCurrency);
            var selected = new SelectedVehicleState("vanguard", vehicleCommitted);
            var vehicleSelection = new VehicleSelectionService(CreateCatalog(), selected, new FakeSelectionSaveCheckpoint(), progression, CreateEngineDefinition(), CreateHandlingDefinition());
            var garage = new GarageService(progression, CreateEngineDefinition(), CreateHandlingDefinition(), CreateBaseStats());
            return new UiContext(sceneFlow, activeLoader, new PresentationScreenNavigator(), progression, vehicleSelection, garage);
        }

        private static RaceContext CreateRaceSessionContext()
        {
            var setup = new FakeRaceSessionSetup();
            var raceController = new FakeRaceSessionRaceController();
            var progression = new PlayerProgressionState();
            var rewardService = new RaceRewardService(progression, new RaceRewardTable(new Dictionary<int, int> { [1] = 500 }, 100));
            var save = new FakeSaveCheckpoint();
            var navigation = new FakeRaceSessionNavigation();
            var sceneFlow = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), new FakeSceneLoader());
            var timeScale = new FakeTimeScaleController();
            var session = new RaceSessionCoordinator(setup, raceController, rewardService, progression, save, navigation, sceneFlow.RaceSession, timeScale, CreateRaceProgressionService(progression));
            return new RaceContext(session, setup, raceController, progression, navigation, sceneFlow, timeScale);
        }

        private static RaceProgressionService CreateRaceProgressionService(PlayerProgressionState progression)
        {
            return new RaceProgressionService(
                progression,
                new RaceProgressionTable(new[]
                {
                    new RaceProgressionTier("rookie", "Rookie", 0, "rookie"),
                    new RaceProgressionTier("amateur", "Amateur", 2, "amateur")
                }),
                new Dictionary<string, ApexRivals.AI.Configuration.AiDriverConfiguration>
                {
                    ["rookie"] = ScriptableObject.CreateInstance<ApexRivals.AI.Configuration.AiDriverConfiguration>(),
                    ["amateur"] = ScriptableObject.CreateInstance<ApexRivals.AI.Configuration.AiDriverConfiguration>()
                });
        }

        private static void StartRace(RaceContext context)
        {
            context.Session.Prepare();
            context.Session.Start();
            context.RaceController.RaiseRaceStarted();
        }

        private static VehicleCatalog CreateCatalog()
        {
            return new VehicleCatalog(new[]
            {
                CreateVehicle("starter", true, false),
                CreateVehicle("vanguard", true),
                CreateVehicle("striker", true),
                CreateVehicle("locked", false)
            }, "starter");
        }

        private static VehicleDefinitionData CreateVehicle(string id, bool available, bool hasPrefab = true)
        {
            var configuration = ScriptableObject.CreateInstance<WheelVehicleConfiguration>();
            configuration.performanceAcceleration = 10f;
            configuration.performanceTopSpeed = 20f;
            configuration.performanceSteering = 30f;
            return new VehicleDefinitionData(id, id, string.Empty, configuration, hasPrefab ? new GameObject($"{id}Prefab") : null, null, available);
        }

        private static UpgradeDefinitionData CreateEngineDefinition()
        {
            return new UpgradeDefinitionData(UpgradeType.Engine, new[]
            {
                new UpgradeLevel(100, new UpgradeStatModifier(1.5f, 1.2f, 1f, 0f, 0f)),
                new UpgradeLevel(150, new UpgradeStatModifier(2f, 1.4f, 1f, 0f, 0f))
            });
        }

        private static UpgradeDefinitionData CreateHandlingDefinition()
        {
            return new UpgradeDefinitionData(UpgradeType.Handling, new[]
            {
                new UpgradeLevel(100, new UpgradeStatModifier(1f, 1f, 1.2f, 0.05f, 0.05f)),
                new UpgradeLevel(150, new UpgradeStatModifier(1f, 1f, 1.4f, 0.1f, 0.1f))
            });
        }

        private static VehiclePerformanceStats CreateBaseStats()
        {
            return new VehiclePerformanceStats(40f, 30f, 90f, 0.8f, 0.4f);
        }

        private sealed class UiContext
        {
            public UiContext(
                SceneFlowService sceneFlow,
                FakeSceneLoader loader,
                PresentationScreenNavigator screenNavigator,
                PlayerProgressionState progression,
                VehicleSelectionService vehicleSelection,
                GarageService garage)
            {
                SceneFlow = sceneFlow;
                Loader = loader;
                ScreenNavigator = screenNavigator;
                Progression = progression;
                VehicleSelection = vehicleSelection;
                Garage = garage;
            }

            public SceneFlowService SceneFlow { get; }
            public FakeSceneLoader Loader { get; }
            public PresentationScreenNavigator ScreenNavigator { get; }
            public PlayerProgressionState Progression { get; }
            public VehicleSelectionService VehicleSelection { get; }
            public GarageService Garage { get; }
        }

        private sealed class RaceContext
        {
            public RaceContext(
                RaceSessionCoordinator session,
                FakeRaceSessionSetup setup,
                FakeRaceSessionRaceController raceController,
                PlayerProgressionState progression,
                FakeRaceSessionNavigation navigation,
                SceneFlowService sceneFlow,
                FakeTimeScaleController timeScale)
            {
                Session = session;
                Setup = setup;
                RaceController = raceController;
                Progression = progression;
                Navigation = navigation;
                SceneFlow = sceneFlow;
                TimeScale = timeScale;
            }

            public RaceSessionCoordinator Session { get; }
            public FakeRaceSessionSetup Setup { get; }
            public FakeRaceSessionRaceController RaceController { get; }
            public PlayerProgressionState Progression { get; }
            public FakeRaceSessionNavigation Navigation { get; }
            public SceneFlowService SceneFlow { get; }
            public FakeTimeScaleController TimeScale { get; }
        }

        private sealed class FakeMainMenuView : IMainMenuView
        {
            public MainMenuViewModel Last { get; private set; }

            public void Render(MainMenuViewModel viewModel)
            {
                Last = viewModel;
            }
        }

        private sealed class FakeVehicleSelectionView : IVehicleSelectionView
        {
            public void Render(VehicleSelectionViewModel viewModel)
            {
            }
        }

        private class FakeGarageView : IGarageView
        {
            public virtual void Render(GarageViewModel viewModel)
            {
            }
        }

        private sealed class ReentrantGarageView : IGarageView
        {
            private bool _called;

            public Action PurchaseAgain { get; set; }

            public void Render(GarageViewModel viewModel)
            {
                if (_called || !viewModel.OperationActive)
                {
                    return;
                }

                _called = true;
                PurchaseAgain?.Invoke();
            }
        }

        private sealed class FakeRaceHudView : IRaceHudView
        {
            public void Render(RaceHudViewModel viewModel)
            {
            }
        }

        private sealed class FakeResultsView : IResultsView
        {
            public void Render(ResultsViewModel viewModel)
            {
            }
        }

        private sealed class FakePauseView : IPauseView
        {
            public void Render(PauseViewModel viewModel)
            {
            }
        }

        private sealed class FakeApplicationExit : IApplicationExit
        {
            public int QuitCount { get; private set; }

            public void Quit()
            {
                QuitCount++;
            }
        }

        private sealed class FakeSelectionProgress : IInitialVehicleSelectionProgress
        {
            public bool HasCommittedVehicleSelection { get; set; }
        }

        private sealed class FakeSelectionSaveCheckpoint : IVehicleSelectionSaveCheckpoint
        {
            public VehicleSelectionSaveResult Save()
            {
                return VehicleSelectionSaveResult.Success();
            }
        }

        private sealed class FakeRaceHudSource : IRaceHudSource
        {
            // The test double exposes the complete contract; individual tests raise only the events they need.
#pragma warning disable CS0067
            public event Action<CountdownChangedEvent> CountdownChanged;
            public event Action<LapCompletedEvent> LapCompleted;
            public event Action<PositionChangedEvent> PositionChanged;
            public event Action<RaceStartedEvent> RaceStarted;
            public event Action<RacerFinishedEvent> RacerFinished;
#pragma warning restore CS0067

            public RaceHudSnapshot SnapshotValue { get; set; }
            public bool RacingInputActiveValue { get; set; }
            public int MutationCount { get; private set; }
            public RaceHudSnapshot Snapshot => SnapshotValue;
            public bool RacingInputActive => RacingInputActiveValue;

            public void RaiseLapCompleted()
            {
                LapCompleted?.Invoke(new LapCompletedEvent("Player", 1, 3));
            }
        }

        private sealed class FakeUiInputSource : IUiInputSource
        {
#pragma warning disable CS0067
            public event Action Submitted;
            public event Action Canceled;
            public event Action PausePressed;
#pragma warning restore CS0067

            public void RaiseCanceled()
            {
                Canceled?.Invoke();
            }
        }

        private sealed class FakeGameplayInputBlocker : IGameplayInputBlocker
        {
            public bool GameplayInputBlocked { get; private set; }

            public void SetGameplayInputBlocked(bool blocked)
            {
                GameplayInputBlocked = blocked;
            }
        }

        private sealed class FakeSelectionStore : IUiSelectionStore
        {
            public string CurrentSelectionId { get; private set; }
            public int RestoreCount { get; private set; }

            public void Select(string selectionId)
            {
                CurrentSelectionId = selectionId;
            }

            public void RestorePreviousSelection()
            {
                RestoreCount++;
            }
        }

        private sealed class FakeSceneLoader : IContentSceneLoader
        {
            private TaskCompletionSource<ContentSceneLoadResult> _pendingLoad;
            private ContentSceneId _pendingSceneId;
            private string _pendingSceneName;

            public bool CompleteImmediately { get; set; } = true;
            public bool FailNextLoad { get; set; }
            public int LoadCount { get; private set; }
            public ContentSceneId LastSceneId { get; private set; }
            public float Progress { get; private set; }
            public bool IsLoading { get; private set; }
            public string BootstrapSceneName => "Bootstrap";
            public string CurrentContentSceneName { get; private set; }

            public Task<ContentSceneLoadResult> LoadContentSceneAsync(ContentSceneId sceneId, string sceneName, bool reloadCurrentScene, CancellationToken cancellationToken = default)
            {
                LoadCount++;
                LastSceneId = sceneId;
                IsLoading = true;
                _pendingSceneId = sceneId;
                _pendingSceneName = sceneName;

                if (CompleteImmediately)
                {
                    return Task.FromResult(CompletePendingLoad(!FailNextLoad));
                }

                _pendingLoad = new TaskCompletionSource<ContentSceneLoadResult>();
                return _pendingLoad.Task;
            }

            public ContentSceneLoadResult CompletePendingLoad(bool succeeded)
            {
                IsLoading = false;
                Progress = succeeded ? 1f : 0f;
                FailNextLoad = false;
                var result = succeeded
                    ? ContentSceneLoadResult.Success(_pendingSceneId, _pendingSceneName, CurrentContentSceneName)
                    : ContentSceneLoadResult.Failure(ContentSceneLoadStatus.LoadFailed, _pendingSceneId, _pendingSceneName, "Load failed.");

                if (succeeded)
                {
                    CurrentContentSceneName = _pendingSceneName;
                }

                _pendingLoad?.SetResult(result);
                _pendingLoad = null;
                return result;
            }
        }

        private sealed class FakeRaceSessionSetup : IRaceSessionSetup
        {
            public RaceSessionSetupResult Prepare()
            {
                var playerObject = new GameObject("PlayerVehicle");
                var playerComposition = playerObject.AddComponent<RaceVehicleComposition>();
                var playerParticipant = playerObject.AddComponent<RaceParticipant>();
                var playerGate = playerObject.AddComponent<ApexRivals.Input.Runtime.DrivingInputGate>();
                playerComposition.ConfigureReferences(null, null, playerParticipant, playerGate, null, null, playerObject.transform, playerObject.transform, playerObject.transform, null);
                return RaceSessionSetupResult.Success(
                    new RaceSetupOutput(playerComposition, Array.Empty<RaceVehicleComposition>(), new[] { playerParticipant }, playerObject.transform, new[] { playerGate }),
                    "Player");
            }

            public void Cleanup()
            {
            }
        }

        private sealed class FakeRaceSessionRaceController : IRaceSessionRaceController
        {
            public event Action<RaceStartedEvent> RaceStarted;
            public event Action<RacerFinishedEvent> RacerFinished;

            public Transform PlayerCameraTarget => null;
            public Rigidbody PlayerVehicleRigidbody => null;
            public IDrivingInputProvider PlayerInputProvider => null;
            public IVehicleTelemetry PlayerVehicleTelemetry => null;

            public bool ConfigureForSession(RaceSetupOutput setupOutput, string playerParticipantId, out string message)
            {
                message = string.Empty;
                return true;
            }

            public bool StartCountdown()
            {
                return true;
            }

            public void BlockDriving()
            {
            }

            public void AllowDriving()
            {
            }

            public RaceHudSnapshot CreateHudSnapshot(string playerParticipantId)
            {
                return new RaceHudSnapshot(1, 3, 1, 1, 0f, 0f);
            }

            public void RaiseRaceStarted()
            {
                RaceStarted?.Invoke(new RaceStartedEvent(0f));
            }

            public void RaiseRacerFinished(RaceResult result)
            {
                RacerFinished?.Invoke(new RacerFinishedEvent(result));
            }
        }

        private sealed class FakeSaveCheckpoint : ISaveCheckpoint
        {
            public SaveCheckpointResult Save()
            {
                return SaveCheckpointResult.Success();
            }
        }

        private sealed class FakeRaceSessionNavigation : IRaceSessionNavigation
        {
            private TaskCompletionSource<SceneTransitionResult> _pendingRetry;

            public int RetryCount { get; private set; }
            public int ReturnToMainMenuCount { get; private set; }

            public Task<SceneTransitionResult> RetryRace()
            {
                RetryCount++;
                _pendingRetry = new TaskCompletionSource<SceneTransitionResult>();
                return _pendingRetry.Task;
            }

            public void CompleteRetry()
            {
                _pendingRetry?.SetResult(new SceneTransitionResult(SceneTransitionStatus.Succeeded, ApplicationState.Race, ApplicationState.Race, ContentSceneId.Race, string.Empty));
            }

            public Task<SceneTransitionResult> ReturnToGarage()
            {
                return Task.FromResult(new SceneTransitionResult(SceneTransitionStatus.Succeeded, ApplicationState.Race, ApplicationState.Garage, ContentSceneId.Garage, string.Empty));
            }

            public Task<SceneTransitionResult> ReturnToMainMenu()
            {
                ReturnToMainMenuCount++;
                return Task.FromResult(new SceneTransitionResult(SceneTransitionStatus.Succeeded, ApplicationState.Race, ApplicationState.MainMenu, ContentSceneId.MainMenu, string.Empty));
            }

            public bool ShowResults(RaceSessionResult result)
            {
                return true;
            }

            public bool PauseRace()
            {
                return true;
            }

            public bool ResumeRace()
            {
                return true;
            }
        }

        private sealed class FakeTimeScaleController : ITimeScaleController
        {
            public float TimeScale { get; private set; } = 1f;
            public int PauseCount { get; private set; }
            public int ResumeCount { get; private set; }

            public void Pause()
            {
                PauseCount++;
                TimeScale = 0f;
            }

            public void Resume()
            {
                ResumeCount++;
                TimeScale = 1f;
            }
        }
    }
}
