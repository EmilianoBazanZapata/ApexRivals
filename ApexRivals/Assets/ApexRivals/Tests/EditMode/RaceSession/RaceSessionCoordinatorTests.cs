using System;
using System.Threading.Tasks;
using ApexRivals.AI.Configuration;
using ApexRivals.Input.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.RaceSetup.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Vehicle.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.RaceSession
{
    public sealed class RaceSessionCoordinatorTests
    {
        [Test]
        public void StartsUninitialized()
        {
            var context = CreateContext();

            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.Uninitialized));
        }

        [Test]
        public void ValidSetupReachesReady()
        {
            var context = CreateContext();

            var result = context.Session.Prepare();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.Ready));
            Assert.That(context.RaceController.ConfigureCount, Is.EqualTo(1));
        }

        [Test]
        public void InvalidSetupReachesFailedAndCleans()
        {
            var context = CreateContext();
            context.Setup.ShouldFailPrepare = true;

            var result = context.Session.Prepare();

            Assert.That(result.Status, Is.EqualTo(RaceSessionCommandStatus.SetupFailed));
            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.Failed));
            Assert.That(context.Setup.CleanupCount, Is.EqualTo(1));
            Assert.That(context.RaceController.BlockDrivingCount, Is.EqualTo(1));
        }

        [Test]
        public void StartBeforeSetupIsRejected()
        {
            var context = CreateContext();

            var result = context.Session.Start();

            Assert.That(result.Status, Is.EqualTo(RaceSessionCommandStatus.InvalidState));
        }

        [Test]
        public void SuccessfulStartUsesRaceCoordinatorOnce()
        {
            var context = CreateContext();
            context.Session.Prepare();

            var result = context.Session.Start();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(context.RaceController.StartCountdownCount, Is.EqualTo(1));
            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.Starting));
        }

        [Test]
        public void RaceStartedEventMovesSessionToRacing()
        {
            var context = CreateContext();
            context.Session.Prepare();
            context.Session.Start();

            context.RaceController.RaiseRaceStarted();

            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
        }

        [Test]
        public void DuplicateStartIsRejected()
        {
            var context = CreateContext();
            context.Session.Prepare();
            context.Session.Start();

            var result = context.Session.Start();

            Assert.That(result.Status, Is.EqualTo(RaceSessionCommandStatus.InvalidState));
            Assert.That(context.RaceController.StartCountdownCount, Is.EqualTo(1));
        }

        [Test]
        public void DisposedSessionRejectsCommands()
        {
            var context = CreateContext();
            context.Session.Dispose();

            var result = context.Session.Prepare();

            Assert.That(result.Status, Is.EqualTo(RaceSessionCommandStatus.Disposed));
        }

        [Test]
        public void PlayerVehicleTelemetry_DelegatesToRaceController()
        {
            var context = CreateContext();
            var telemetry = new FakeVehicleTelemetry { SpeedKph = 55f, CurrentGear = 2, EngineRpm = 3000f };
            context.RaceController.PlayerVehicleTelemetry = telemetry;

            Assert.That(context.Session.PlayerVehicleTelemetry, Is.SameAs(telemetry));
        }

        [Test]
        public void PlayerPositionChangesAreForwardedToTheHudPipeline()
        {
            var context = CreateContext();
            var receivedPosition = 0;
            context.Session.PositionChanged += positionChanged => receivedPosition = positionChanged.Position;
            context.Session.Prepare();

            context.RaceController.RaisePositionChanged("AI_01", 1);
            context.RaceController.RaisePositionChanged("Player", 2);

            Assert.That(receivedPosition, Is.EqualTo(2));
        }

        [Test]
        public void PlayerResultIsProcessedOnceAndRewardAddedOnce()
        {
            var context = CreateContext();
            StartRace(context);

            context.RaceController.RaiseRacerFinished(new RaceResult("Player", 1, 65f));
            context.RaceController.RaiseRacerFinished(new RaceResult("Player", 1, 65f));

            Assert.That(context.Progression.Currency, Is.EqualTo(500));
            Assert.That(context.Progression.CompletedRaceCount, Is.EqualTo(1));
            Assert.That(context.Save.SaveCount, Is.EqualTo(1));
            Assert.That(context.ResultsEventCount, Is.EqualTo(1));
            Assert.That(context.Navigation.ShowResultsCount, Is.EqualTo(1));
        }

        [Test]
        public void ResultsStateContainsExpectedValues()
        {
            var context = CreateContext();
            StartRace(context);

            context.RaceController.RaiseRacerFinished(new RaceResult("Player", 2, 72.5f));

            var result = context.Navigation.LastResult;
            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.ShowingResults));
            Assert.That(result.FinalPosition, Is.EqualTo(2));
            Assert.That(result.FinishTime, Is.EqualTo(72.5f));
            Assert.That(result.EarnedReward, Is.EqualTo(300));
            Assert.That(result.TotalParticipants, Is.EqualTo(2));
            Assert.That(result.UpdatedCurrency, Is.EqualTo(300));
            Assert.That(result.CompletedRaceCount, Is.EqualTo(1));
            Assert.That(result.CurrentTier.TierId, Is.EqualTo("rookie"));
            Assert.That(context.SceneRaceState.HasResult, Is.True);
        }

        [Test]
        public void AiFinishingBeforePlayerDoesNotAwardReward()
        {
            var context = CreateContext();
            StartRace(context);

            context.RaceController.RaiseRacerFinished(new RaceResult("AI_01", 1, 60f));

            Assert.That(context.Progression.Currency, Is.Zero);
            Assert.That(context.Progression.CompletedRaceCount, Is.Zero);
            Assert.That(context.Save.SaveCount, Is.Zero);
            Assert.That(context.Navigation.ShowResultsCount, Is.Zero);
            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
        }

        [Test]
        public void SaveFailureAfterRewardKeepsResultsAndMarksSaveFailure()
        {
            var context = CreateContext();
            context.Save.ShouldFail = true;
            StartRace(context);

            context.RaceController.RaiseRacerFinished(new RaceResult("Player", 1, 65f));

            Assert.That(context.Progression.Currency, Is.EqualTo(500));
            Assert.That(context.Progression.CompletedRaceCount, Is.EqualTo(1));
            Assert.That(context.Navigation.LastResult.SaveSucceeded, Is.False);
            Assert.That(context.Navigation.LastResult.SaveMessage, Is.EqualTo("Save failed."));
            Assert.That(context.Session.LastCommandResult.Status, Is.EqualTo(RaceSessionCommandStatus.SaveFailedAfterReward));
            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.ShowingResults));
        }

        [Test]
        public async Task RetryClearsTransientResultAndStartsFreshAttempt()
        {
            var context = CreateContext();
            StartRace(context);
            context.RaceController.RaiseRacerFinished(new RaceResult("Player", 1, 65f));

            var retry = await context.Session.Retry();

            Assert.That(retry.Succeeded, Is.True);
            Assert.That(context.SceneRaceState.HasResult, Is.False);
            Assert.That(context.Navigation.RetryCount, Is.EqualTo(1));
            Assert.That(context.Setup.CleanupCount, Is.EqualTo(1));
            Assert.That(context.Setup.PrepareCount, Is.EqualTo(2));
            Assert.That(context.RaceController.StartCountdownCount, Is.EqualTo(2));
            Assert.That(context.Progression.Currency, Is.EqualTo(500));
            Assert.That(context.Progression.CompletedRaceCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RetryDoesNotReAwardPreviousResult()
        {
            var context = CreateContext();
            StartRace(context);
            context.RaceController.RaiseRacerFinished(new RaceResult("Player", 1, 65f));

            await context.Session.Retry();

            Assert.That(context.Progression.Currency, Is.EqualTo(500));
            Assert.That(context.Save.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ExitRestoresTimeScaleCleansAndNavigatesThroughSceneFlow()
        {
            var context = CreateContext();
            StartRace(context);
            context.Session.Pause();

            var result = await context.Session.ReturnToMainMenu();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(context.TimeScale.TimeScale, Is.EqualTo(1f));
            Assert.That(context.Setup.CleanupCount, Is.EqualTo(1));
            Assert.That(context.Navigation.ReturnToMainMenuCount, Is.EqualTo(1));
            Assert.That(context.SceneRaceState.HasResult, Is.False);
        }

        [Test]
        public async Task ReturnToGarageRequestsNavigation()
        {
            var context = CreateContext();
            StartRace(context);

            var result = await context.Session.ReturnToGarage();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(context.Navigation.ReturnToGarageCount, Is.EqualTo(1));
            Assert.That(context.Progression.CompletedRaceCount, Is.Zero);
        }

        [Test]
        public async Task RetryBeforeFinishDoesNotIncrementProgression()
        {
            var context = CreateContext();
            StartRace(context);

            var result = await context.Session.Retry();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(context.Progression.CompletedRaceCount, Is.Zero);
        }

        [Test]
        public async Task NavigationFailureReturnsExplicitFailure()
        {
            var context = CreateContext();
            context.Navigation.FailNextNavigation = true;
            StartRace(context);

            var result = await context.Session.ReturnToMainMenu();

            Assert.That(result.Status, Is.EqualTo(RaceSessionCommandStatus.NavigationFailed));
            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.Failed));
        }

        [Test]
        public void PauseAllowedOnlyWhileRacing()
        {
            var context = CreateContext();

            var beforeStart = context.Session.Pause();
            StartRace(context);
            var duringRace = context.Session.Pause();

            Assert.That(beforeStart.Status, Is.EqualTo(RaceSessionCommandStatus.InvalidState));
            Assert.That(duringRace.Succeeded, Is.True);
            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.Paused));
            Assert.That(context.RaceController.BlockDrivingCount, Is.EqualTo(1));
        }

        [Test]
        public void DuplicatePauseIsSafe()
        {
            var context = CreateContext();
            StartRace(context);

            context.Session.Pause();
            var duplicate = context.Session.Pause();

            Assert.That(duplicate.Succeeded, Is.True);
            Assert.That(context.TimeScale.TimeScale, Is.Zero);
            Assert.That(context.RaceController.BlockDrivingCount, Is.EqualTo(1));
        }

        [Test]
        public void ResumeRestoresTimeScale()
        {
            var context = CreateContext();
            StartRace(context);
            context.Session.Pause();

            var result = context.Session.Resume();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(context.TimeScale.TimeScale, Is.EqualTo(1f));
            Assert.That(context.Session.State, Is.EqualTo(RaceSessionLifecycleState.Racing));
        }

        [Test]
        public async Task RetryWhilePausedRestoresTimeScale()
        {
            var context = CreateContext();
            StartRace(context);
            context.Session.Pause();

            await context.Session.Retry();

            Assert.That(context.TimeScale.TimeScale, Is.EqualTo(1f));
        }

        [Test]
        public async Task ExitWhilePausedRestoresTimeScale()
        {
            var context = CreateContext();
            StartRace(context);
            context.Session.Pause();

            await context.Session.ReturnToMainMenu();

            Assert.That(context.TimeScale.TimeScale, Is.EqualTo(1f));
        }

        [Test]
        public void DisposeWhilePausedRestoresTimeScaleAndIgnoresLateCallbacks()
        {
            var context = CreateContext();
            StartRace(context);
            context.Session.Pause();

            context.Session.Dispose();
            context.RaceController.RaiseRacerFinished(new RaceResult("Player", 1, 65f));

            Assert.That(context.TimeScale.TimeScale, Is.EqualTo(1f));
            Assert.That(context.Progression.Currency, Is.Zero);
            Assert.That(context.Save.SaveCount, Is.Zero);
        }

        [Test]
        public void CleanupCanExecuteMoreThanOnceSafely()
        {
            var context = CreateContext();
            StartRace(context);

            context.Session.Dispose();
            context.Session.Dispose();

            Assert.That(context.Setup.CleanupCount, Is.EqualTo(1));
        }

        private static void StartRace(TestContext context)
        {
            context.Session.Prepare();
            context.Session.Start();
            context.RaceController.RaiseRaceStarted();
        }

        private TestContext CreateContext()
        {
            var setup = new FakeRaceSessionSetup();
            var raceController = new FakeRaceSessionRaceController();
            var progression = new PlayerProgressionState();
            var rewardService = new RaceRewardService(progression, new RaceRewardTable(
                new System.Collections.Generic.Dictionary<int, int>
                {
                    { 1, 500 },
                    { 2, 300 },
                    { 3, 200 }
                },
                100));
            var save = new FakeSaveCheckpoint();
            var navigation = new FakeRaceSessionNavigation();
            var raceState = new RaceSessionState();
            var timeScale = new FakeTimeScaleController();
            var raceProgression = CreateRaceProgressionService(progression);
            var session = new RaceSessionCoordinator(setup, raceController, rewardService, progression, save, navigation, raceState, timeScale, raceProgression);

            var context = new TestContext(session, setup, raceController, progression, save, navigation, raceState, timeScale);
            session.ResultsReady += _ => context.ResultsEventCount++;
            return context;
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
                new System.Collections.Generic.Dictionary<string, AiDriverConfiguration>
                {
                    ["rookie"] = ScriptableObject.CreateInstance<AiDriverConfiguration>(),
                    ["amateur"] = ScriptableObject.CreateInstance<AiDriverConfiguration>()
                });
        }

        private sealed class TestContext
        {
            public TestContext(
                RaceSessionCoordinator session,
                FakeRaceSessionSetup setup,
                FakeRaceSessionRaceController raceController,
                PlayerProgressionState progression,
                FakeSaveCheckpoint save,
                FakeRaceSessionNavigation navigation,
                RaceSessionState sceneRaceState,
                FakeTimeScaleController timeScale)
            {
                Session = session;
                Setup = setup;
                RaceController = raceController;
                Progression = progression;
                Save = save;
                Navigation = navigation;
                SceneRaceState = sceneRaceState;
                TimeScale = timeScale;
            }

            public RaceSessionCoordinator Session { get; }
            public FakeRaceSessionSetup Setup { get; }
            public FakeRaceSessionRaceController RaceController { get; }
            public PlayerProgressionState Progression { get; }
            public FakeSaveCheckpoint Save { get; }
            public FakeRaceSessionNavigation Navigation { get; }
            public RaceSessionState SceneRaceState { get; }
            public FakeTimeScaleController TimeScale { get; }
            public int ResultsEventCount { get; set; }
        }

        private sealed class FakeRaceSessionSetup : IRaceSessionSetup
        {
            public int PrepareCount { get; private set; }
            public int CleanupCount { get; private set; }
            public bool ShouldFailPrepare { get; set; }

            public RaceSessionSetupResult Prepare()
            {
                PrepareCount++;
                if (ShouldFailPrepare)
                {
                    return RaceSessionSetupResult.Failure("Setup failed.");
                }

                return RaceSessionSetupResult.Success(CreateOutput(), "Player");
            }

            public void Cleanup()
            {
                CleanupCount++;
            }

            private static RaceSetupOutput CreateOutput()
            {
                var playerObject = new GameObject("PlayerVehicle");
                var playerComposition = playerObject.AddComponent<RaceVehicleComposition>();
                var playerParticipant = playerObject.AddComponent<RaceParticipant>();
                var playerGate = playerObject.AddComponent<DrivingInputGate>();
                playerComposition.ConfigureReferences(null, null, playerParticipant, playerGate, null, null, playerObject.transform, playerObject.transform, playerObject.transform, null);

                var aiObject = new GameObject("AiVehicle");
                var aiComposition = aiObject.AddComponent<RaceVehicleComposition>();
                var aiParticipant = aiObject.AddComponent<RaceParticipant>();
                var aiGate = aiObject.AddComponent<DrivingInputGate>();
                aiComposition.ConfigureReferences(null, null, aiParticipant, aiGate, null, null, aiObject.transform, aiObject.transform, aiObject.transform, null);

                return new RaceSetupOutput(
                    playerComposition,
                    new[] { aiComposition },
                    new[] { playerParticipant, aiParticipant },
                    playerObject.transform,
                    new[] { playerGate, aiGate });
            }
        }

        private sealed class FakeVehicleTelemetry : IVehicleTelemetry
        {
            public float SpeedKph { get; set; }
            public int CurrentGear { get; set; }
            public float EngineRpm { get; set; }
            public bool IsReversing { get; set; }
        }

        private sealed class FakeRaceSessionRaceController : IRaceSessionRaceController
        {
            public event Action<RaceStartedEvent> RaceStarted;
            public event Action<PositionChangedEvent> PositionChanged;
            public event Action<RacerFinishedEvent> RacerFinished;

            public int ConfigureCount { get; private set; }
            public int StartCountdownCount { get; private set; }
            public int BlockDrivingCount { get; private set; }
            public Transform PlayerCameraTarget { get; private set; }
            public Rigidbody PlayerVehicleRigidbody => null;
            public IDrivingInputProvider PlayerInputProvider => null;
            public IVehicleTelemetry PlayerVehicleTelemetry { get; set; }

            public bool ConfigureForSession(RaceSetupOutput setupOutput, string playerParticipantId, out string message)
            {
                ConfigureCount++;
                PlayerCameraTarget = setupOutput.PlayerCameraTarget;
                message = string.Empty;
                return true;
            }

            public bool StartCountdown()
            {
                StartCountdownCount++;
                return true;
            }

            public void BlockDriving()
            {
                BlockDrivingCount++;
            }

            public void AllowDriving()
            {
            }

            public RaceHudSnapshot CreateHudSnapshot(string playerParticipantId)
            {
                return new RaceHudSnapshot(1, 3, 1, 2, 0f, 12f);
            }

            public void RaiseRaceStarted()
            {
                RaceStarted?.Invoke(new RaceStartedEvent(0f));
            }

            public void RaisePositionChanged(string racerId, int position)
            {
                PositionChanged?.Invoke(new PositionChangedEvent(racerId, position));
            }

            public void RaiseRacerFinished(RaceResult result)
            {
                RacerFinished?.Invoke(new RacerFinishedEvent(result));
            }
        }

        private sealed class FakeSaveCheckpoint : ISaveCheckpoint
        {
            public int SaveCount { get; private set; }
            public bool ShouldFail { get; set; }

            public SaveCheckpointResult Save()
            {
                SaveCount++;
                return ShouldFail ? SaveCheckpointResult.Failure("Save failed.") : SaveCheckpointResult.Success();
            }
        }

        private sealed class FakeRaceSessionNavigation : IRaceSessionNavigation
        {
            public int RetryCount { get; private set; }
            public int ReturnToGarageCount { get; private set; }
            public int ReturnToMainMenuCount { get; private set; }
            public int ShowResultsCount { get; private set; }
            public bool FailNextNavigation { get; set; }
            public RaceSessionResult LastResult { get; private set; }

            public Task<SceneTransitionResult> RetryRace()
            {
                RetryCount++;
                return Complete(ContentSceneId.Race);
            }

            public Task<SceneTransitionResult> ReturnToGarage()
            {
                ReturnToGarageCount++;
                return Complete(ContentSceneId.Garage);
            }

            public Task<SceneTransitionResult> ReturnToMainMenu()
            {
                ReturnToMainMenuCount++;
                return Complete(ContentSceneId.MainMenu);
            }

            public bool ShowResults(RaceSessionResult result)
            {
                ShowResultsCount++;
                LastResult = result;
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

            private Task<SceneTransitionResult> Complete(ContentSceneId target)
            {
                if (FailNextNavigation)
                {
                    FailNextNavigation = false;
                    return Task.FromResult(new SceneTransitionResult(SceneTransitionStatus.SceneLoadFailed, ApplicationState.Race, ApplicationState.Race, target, "Navigation failed."));
                }

                return Task.FromResult(new SceneTransitionResult(SceneTransitionStatus.Succeeded, ApplicationState.Race, target == ContentSceneId.Garage ? ApplicationState.Garage : ApplicationState.MainMenu, target, string.Empty));
            }
        }

        private sealed class FakeTimeScaleController : ITimeScaleController
        {
            public float TimeScale { get; private set; } = 1f;

            public void Pause()
            {
                TimeScale = 0f;
            }

            public void Resume()
            {
                TimeScale = 1f;
            }
        }
    }
}
