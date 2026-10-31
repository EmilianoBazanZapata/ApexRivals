using System;
using System.Threading;
using System.Threading.Tasks;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.UI
{
    public sealed class LoadingContentSceneLoaderTests
    {
        [TestCase(ContentSceneId.Race, false, "Garage", true)]
        [TestCase(ContentSceneId.MainMenu, false, "Race", true)]
        [TestCase(ContentSceneId.MainMenu, false, "", false)]
        [TestCase(ContentSceneId.Race, true, "Race", false)]
        [TestCase(ContentSceneId.Garage, false, "MainMenu", false)]
        public async Task CoversOnlyRequestedContentTransitions(ContentSceneId target, bool reload, string current, bool covered)
        {
            var view = new Presentation();
            var loader = new Loader { CurrentContentSceneName = current };
            await new LoadingContentSceneLoader(loader, view).LoadContentSceneAsync(target, "Destination", reload);
            Assert.That(view.Shown, Is.EqualTo(covered ? 1 : 0));
            Assert.That(view.Hidden, Is.EqualTo(view.Shown));
            Assert.That(view.Succeeded, Is.EqualTo(covered));
        }

        [Test]
        public async Task WaitsForRenderedPresentationBeforeLoadingAndKeepsItUntilCompletion()
        {
            var view = new Presentation { Ready = new TaskCompletionSource<bool>() };
            var loader = new Loader { Completion = new TaskCompletionSource<ContentSceneLoadResult>() };
            var pending = new LoadingContentSceneLoader(loader, view).LoadContentSceneAsync(ContentSceneId.Race, "RaceDunes", false);
            Assert.That(loader.Calls, Is.Zero);
            Assert.That(view.Visible, Is.True);
            view.Ready.SetResult(true);
            await Task.Yield();
            Assert.That(loader.Calls, Is.EqualTo(1));
            Assert.That(view.Hidden, Is.Zero);
            loader.Completion.SetResult(ContentSceneLoadResult.Success(ContentSceneId.Race, "RaceDunes", "Garage"));
            await pending;
            Assert.That(view.Visible, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task FailureAndCancellationReleasePresentation(bool cancel)
        {
            var view = new Presentation();
            var loader = new Loader { Fail = true, Cancel = cancel };
            var decorated = new LoadingContentSceneLoader(loader, view);
            try { await decorated.LoadContentSceneAsync(ContentSceneId.Race, "Missing", false); }
            catch (OperationCanceledException) { Assert.That(cancel, Is.True); }
            Assert.That(view.Visible, Is.False);
            Assert.That(view.Hidden, Is.EqualTo(1));
            Assert.That(view.Succeeded, Is.False);
        }

        [Test]
        public async Task PresentationExceptionsCannotStrandNavigation()
        {
            var view = new Presentation { Throw = true };
            var loader = new Loader();
            var result = await new LoadingContentSceneLoader(loader, view, _ => throw new Exception()).LoadContentSceneAsync(ContentSceneId.Race, "Race", false);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(view.Visible, Is.False);
        }

        [Test]
        public async Task GarageToMainMenuSkipsTheOverlayButRaceToMainMenuKeepsIt()
        {
            var view = new Presentation();
            var loader = new Loader { CurrentContentSceneName = "" };
            var decorated = new LoadingContentSceneLoader(loader, view);
            await decorated.LoadContentSceneAsync(ContentSceneId.MainMenu, "MainMenu", false);
            await decorated.LoadContentSceneAsync(ContentSceneId.Garage, "Garage", false);
            await decorated.LoadContentSceneAsync(ContentSceneId.MainMenu, "MainMenu", false);
            Assert.That(view.Shown, Is.Zero);

            await decorated.LoadContentSceneAsync(ContentSceneId.Garage, "Garage", false);
            await decorated.LoadContentSceneAsync(ContentSceneId.Race, "Race", false);
            await decorated.LoadContentSceneAsync(ContentSceneId.MainMenu, "MainMenu", false);
            Assert.That(view.Shown, Is.EqualTo(2));
        }

        [Test]
        public async Task RejectedAndDuplicateRequestsNeverAcquireAnotherOverlay()
        {
            var view = new Presentation();
            var loader = new Loader { CurrentContentSceneName = "" };
            var checkpoint = new Checkpoint();
            var flow = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), new LoadingContentSceneLoader(loader, view), checkpoint);
            await flow.OpenMainMenu();
            await flow.OpenMainMenu();
            await flow.OpenGarage();
            checkpoint.Fail = true;
            Assert.That((await flow.StartRace()).Succeeded, Is.False);
            Assert.That(view.Shown, Is.Zero);
            checkpoint.Fail = false;
            loader.Completion = new TaskCompletionSource<ContentSceneLoadResult>();
            var first = flow.StartRace();
            Assert.That((await flow.StartRace()).Status, Is.EqualTo(SceneTransitionStatus.ConcurrentTransition));
            Assert.That(view.Shown, Is.EqualTo(1));
            loader.Completion.SetResult(ContentSceneLoadResult.Success(ContentSceneId.Race, "Race", "Garage"));
            await first;
            Assert.That(view.Visible, Is.False);
        }

        private sealed class Checkpoint : ISaveCheckpoint
        {
            public bool Fail;
            public SaveCheckpointResult Save() => Fail ? SaveCheckpointResult.Failure("Rejected") : SaveCheckpointResult.Success();
        }

        private sealed class Presentation : ISceneLoadingPresentation
        {
            public TaskCompletionSource<bool> Ready;
            public int Shown, Hidden;
            public bool Visible, Succeeded, Throw;
            public bool IsVisible => Visible;
            public Task ShowAsync()
            {
                Shown++;
                Visible = true;
                if (Throw) throw new Exception("Presentation failure");
                return Ready?.Task ?? Task.CompletedTask;
            }
            public void Hide(bool succeeded)
            {
                Hidden++;
                Visible = false;
                Succeeded = succeeded;
                if (Throw) throw new Exception("Presentation cleanup failure");
            }
        }

        private sealed class Loader : IContentSceneLoader
        {
            public TaskCompletionSource<ContentSceneLoadResult> Completion;
            public bool Fail, Cancel;
            public int Calls;
            public float Progress => 0f;
            public bool IsLoading => false;
            public string BootstrapSceneName => "Bootstrap";
            public string CurrentContentSceneName { get; set; } = "Garage";
            public async Task<ContentSceneLoadResult> LoadContentSceneAsync(ContentSceneId id, string name, bool reload, CancellationToken token = default)
            {
                Calls++;
                if (Cancel) throw new OperationCanceledException();
                var result = Completion != null ? await Completion.Task : Fail
                    ? ContentSceneLoadResult.Failure(ContentSceneLoadStatus.LoadFailed, id, name, "Missing")
                    : ContentSceneLoadResult.Success(id, name, CurrentContentSceneName);
                if (result.Succeeded) CurrentContentSceneName = name;
                return result;
            }
        }
    }
}
