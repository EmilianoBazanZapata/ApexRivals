using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApexRivals.Race.Runtime;
using ApexRivals.SceneFlow.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.SceneFlow
{
    public sealed class SceneFlowServiceTests
    {
        [Test]
        public void StartsInBootstrappingState()
        {
            var service = CreateService();

            Assert.That(service.State, Is.EqualTo(ApplicationState.Bootstrapping));
            Assert.That(service.RacePresentationState, Is.EqualTo(RacePresentationState.None));
        }

        [Test]
        public async Task MainMenuCanOpenGarage()
        {
            var service = CreateService();
            await service.OpenMainMenu();

            var result = await service.OpenGarage();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(service.State, Is.EqualTo(ApplicationState.Garage));
            Assert.That(service.CurrentContentScene, Is.EqualTo(ContentSceneId.Garage));
        }

        [Test]
        public async Task GarageCanReturnToMainMenu()
        {
            var saveCheckpoint = new FakeSaveCheckpoint();
            var service = CreateService(saveCheckpoint: saveCheckpoint);
            await service.OpenMainMenu();
            await service.OpenGarage();

            var result = await service.OpenMainMenu();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(service.State, Is.EqualTo(ApplicationState.MainMenu));
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public async Task MainMenuCanStartRace()
        {
            var service = CreateService();
            await service.OpenMainMenu();

            var result = await service.StartRace();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(service.State, Is.EqualTo(ApplicationState.Race));
            Assert.That(service.RacePresentationState, Is.EqualTo(RacePresentationState.Racing));
        }

        [Test]
        public async Task GarageCanStartRaceAndRequestsSave()
        {
            var saveCheckpoint = new FakeSaveCheckpoint();
            var service = CreateService(saveCheckpoint: saveCheckpoint);
            await service.OpenMainMenu();
            await service.OpenGarage();

            var result = await service.StartRace();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(service.State, Is.EqualTo(ApplicationState.Race));
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RaceCanRetryRace()
        {
            var saveCheckpoint = new FakeSaveCheckpoint();
            var loader = new FakeSceneLoader();
            var service = CreateService(loader, saveCheckpoint);
            await service.OpenMainMenu();
            await service.StartRace();

            var result = await service.RetryRace();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(service.State, Is.EqualTo(ApplicationState.Race));
            Assert.That(loader.LoadedSceneIds, Is.EqualTo(new[] { ContentSceneId.MainMenu, ContentSceneId.Race, ContentSceneId.Race }));
            Assert.That(saveCheckpoint.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RaceCanReturnToGarage()
        {
            var service = CreateService();
            await service.OpenMainMenu();
            await service.StartRace();

            var result = await service.ReturnFromRaceToGarage();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(service.State, Is.EqualTo(ApplicationState.Garage));
            Assert.That(service.RacePresentationState, Is.EqualTo(RacePresentationState.None));
        }

        [Test]
        public async Task RaceCanReturnToMainMenu()
        {
            var service = CreateService();
            await service.OpenMainMenu();
            await service.StartRace();

            var result = await service.ReturnFromRaceToMainMenu();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(service.State, Is.EqualTo(ApplicationState.MainMenu));
        }

        [Test]
        public async Task InvalidTransitionIsRejected()
        {
            var service = CreateService();

            var result = await service.OpenGarage();

            Assert.That(result.Status, Is.EqualTo(SceneTransitionStatus.InvalidTransition));
            Assert.That(service.State, Is.EqualTo(ApplicationState.Bootstrapping));
        }

        [Test]
        public async Task DuplicateTransitionIsRejected()
        {
            var service = CreateService();
            await service.OpenMainMenu();

            var result = await service.OpenMainMenu();

            Assert.That(result.Status, Is.EqualTo(SceneTransitionStatus.DuplicateTransition));
            Assert.That(service.State, Is.EqualTo(ApplicationState.MainMenu));
        }

        [Test]
        public async Task ConcurrentTransitionRequestsAreRejected()
        {
            var loader = new FakeSceneLoader { CompleteImmediately = false };
            var service = CreateService(loader);

            var firstTransition = service.OpenMainMenu();
            var duplicate = await service.OpenGarage();
            loader.CompletePendingLoad(true);
            await firstTransition;

            Assert.That(duplicate.Status, Is.EqualTo(SceneTransitionStatus.ConcurrentTransition));
        }

        [Test]
        public async Task LoadingFailureRestoresPreviousValidState()
        {
            var loader = new FakeSceneLoader();
            var service = CreateService(loader);
            await service.OpenMainMenu();
            loader.FailNextLoad = true;

            var result = await service.OpenGarage();

            Assert.That(result.Status, Is.EqualTo(SceneTransitionStatus.SceneLoadFailed));
            Assert.That(service.State, Is.EqualTo(ApplicationState.MainMenu));
            Assert.That(service.CurrentContentScene, Is.EqualTo(ContentSceneId.MainMenu));
        }

        [Test]
        public async Task CurrentSceneIsUnloadedOnlyAfterReplacementIsReady()
        {
            var loader = new FakeSceneLoader();
            var service = CreateService(loader);
            await service.OpenMainMenu();

            await service.OpenGarage();

            Assert.That(loader.UnloadedAfterLoaded, Is.EqualTo(new[] { "MainMenu" }));
        }

        [Test]
        public async Task BootstrapSceneIsNeverUnloadedByContentNavigation()
        {
            var loader = new FakeSceneLoader();
            var service = CreateService(loader);

            await service.OpenMainMenu();
            await service.OpenGarage();
            await service.StartRace();

            Assert.That(loader.UnloadedAfterLoaded, Does.Not.Contain(loader.BootstrapSceneName));
        }

        [Test]
        public async Task NavigationCallersUseStableSceneIds()
        {
            var loader = new FakeSceneLoader();
            var service = CreateService(loader);

            await service.OpenMainMenu();
            await service.OpenGarage();

            Assert.That(loader.LoadedSceneIds, Is.EqualTo(new[] { ContentSceneId.MainMenu, ContentSceneId.Garage }));
        }

        [Test]
        public async Task SaveCheckpointFailureBlocksTransition()
        {
            var saveCheckpoint = new FakeSaveCheckpoint { ShouldFail = true };
            var service = CreateService(saveCheckpoint: saveCheckpoint);
            await service.OpenMainMenu();
            await service.OpenGarage();

            var result = await service.StartRace();

            Assert.That(result.Status, Is.EqualTo(SceneTransitionStatus.SaveFailed));
            Assert.That(service.State, Is.EqualTo(ApplicationState.Garage));
        }

        [Test]
        public async Task ResultsRemainRacePresentationState()
        {
            var service = CreateService();
            await service.OpenMainMenu();
            await service.StartRace();

            var shown = service.ShowRaceResults(new RaceSessionResult(new RaceResult("Player", 1, 72.5f), 100));

            Assert.That(shown, Is.True);
            Assert.That(service.State, Is.EqualTo(ApplicationState.Race));
            Assert.That(service.RacePresentationState, Is.EqualTo(RacePresentationState.Results));
            Assert.That(service.RaceSession.Result.EarnedReward, Is.EqualTo(100));
        }

        [Test]
        public async Task PauseStateCanResumeBeforeRaceNavigation()
        {
            var service = CreateService();
            await service.OpenMainMenu();
            await service.StartRace();

            var paused = service.PauseRace();
            var resumed = service.ResumeRace();

            Assert.That(paused, Is.True);
            Assert.That(resumed, Is.True);
            Assert.That(service.RacePresentationState, Is.EqualTo(RacePresentationState.Racing));
        }

        private static SceneFlowService CreateService(FakeSceneLoader loader = null, FakeSaveCheckpoint saveCheckpoint = null)
        {
            var sceneMap = new SceneFlowSceneMap("MainMenu", "Garage", "Race");
            return new SceneFlowService(sceneMap, loader ?? new FakeSceneLoader(), saveCheckpoint);
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

        private sealed class FakeSceneLoader : IContentSceneLoader
        {
            private TaskCompletionSource<ContentSceneLoadResult> _pendingLoad;
            private ContentSceneId _pendingSceneId;
            private string _pendingSceneName;

            public bool CompleteImmediately { get; set; } = true;
            public bool FailNextLoad { get; set; }
            public float Progress { get; private set; }
            public bool IsLoading { get; private set; }
            public string BootstrapSceneName => "Bootstrap";
            public string CurrentContentSceneName { get; private set; }
            public List<ContentSceneId> LoadedSceneIds { get; } = new List<ContentSceneId>();
            public List<string> UnloadedAfterLoaded { get; } = new List<string>();

            public Task<ContentSceneLoadResult> LoadContentSceneAsync(
                ContentSceneId sceneId,
                string sceneName,
                bool reloadCurrentScene,
                CancellationToken cancellationToken = default)
            {
                if (IsLoading)
                {
                    return Task.FromResult(ContentSceneLoadResult.Failure(ContentSceneLoadStatus.ConcurrentLoad, sceneId, sceneName, "Already loading."));
                }

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
                var previousSceneName = CurrentContentSceneName;
                IsLoading = false;
                Progress = succeeded ? 1f : 0f;
                FailNextLoad = false;

                var result = succeeded
                    ? ContentSceneLoadResult.Success(_pendingSceneId, _pendingSceneName, previousSceneName)
                    : ContentSceneLoadResult.Failure(ContentSceneLoadStatus.LoadFailed, _pendingSceneId, _pendingSceneName, "Load failed.");

                if (succeeded)
                {
                    LoadedSceneIds.Add(_pendingSceneId);
                    CurrentContentSceneName = _pendingSceneName;
                    if (!string.IsNullOrEmpty(previousSceneName) && previousSceneName != BootstrapSceneName)
                    {
                        UnloadedAfterLoaded.Add(previousSceneName);
                    }
                }

                _pendingLoad?.SetResult(result);
                _pendingLoad = null;
                return result;
            }
        }
    }
}
