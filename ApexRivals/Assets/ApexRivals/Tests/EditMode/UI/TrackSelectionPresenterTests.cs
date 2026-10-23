using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Tracks.Runtime;
using ApexRivals.UI.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ApexRivals.Tests.EditMode.UI
{
    public sealed class TrackSelectionPresenterTests
    {
        [Test]
        public async Task Present_MapsCatalogAndSelectsDefaultTrack()
        {
            var context = await CreateContext("valley");

            context.Presenter.Present();

            var model = context.View.Last;
            Assert.That(model.Tracks.Count, Is.EqualTo(3));
            Assert.That(model.SelectedIndex, Is.EqualTo(1));
            Assert.That(model.Selected.DisplayName, Is.EqualTo("Pine Valley"));
            Assert.That(model.Selected.Location, Is.EqualTo("Mountain Pass"));
            Assert.That(model.Selected.LengthText, Is.EqualTo("2.4 KM"));
            Assert.That(model.Selected.Laps, Is.EqualTo(3));
            Assert.That(model.Selected.MinimapPoints.Count, Is.EqualTo(3));
            Assert.That(model.CanStartRace, Is.True);
        }

        [Test]
        public async Task SelectNextAndPrevious_WrapAroundTheCatalog()
        {
            var context = await CreateContext("canyon");
            context.Presenter.Present();

            context.Presenter.SelectNextTrack();
            Assert.That(context.Presenter.Current.Selected.TrackId, Is.EqualTo("harbor"));

            context.Presenter.SelectPreviousTrack();
            context.Presenter.SelectPreviousTrack();
            Assert.That(context.Presenter.Current.Selected.TrackId, Is.EqualTo("valley"));
        }

        [Test]
        public async Task SelectTrack_IgnoresOutOfRangeIndices()
        {
            var context = await CreateContext();
            context.Presenter.Present();
            var renders = context.View.RenderCount;

            context.Presenter.SelectTrack(7);
            context.Presenter.SelectTrack(-1);

            Assert.That(context.Presenter.Current.SelectedIndex, Is.Zero);
            Assert.That(context.View.RenderCount, Is.EqualTo(renders));
        }

        [Test]
        public async Task OpenAndBack_SwitchBetweenTrackSelectionAndGarage()
        {
            var context = await CreateContext();

            context.Presenter.Open();
            Assert.That(context.Navigator.CurrentScreen, Is.EqualTo(PresentationScreenState.TrackSelection));
            Assert.That(context.View.RenderCount, Is.GreaterThan(0));

            context.Presenter.Back();
            Assert.That(context.Navigator.CurrentScreen, Is.EqualTo(PresentationScreenState.Garage));
            Assert.That(context.SceneFlow.State, Is.EqualTo(ApplicationState.Garage));
        }

        [Test]
        public async Task StartRace_LoadsTheSelectedTracksScene()
        {
            var context = await CreateContext();
            context.Presenter.Present();
            context.Presenter.SelectNextTrack();

            await context.Presenter.StartRace();

            Assert.That(context.SceneFlow.State, Is.EqualTo(ApplicationState.Race));
            Assert.That(context.Loader.LoadedSceneNames[context.Loader.LoadedSceneNames.Count - 1], Is.EqualTo("ValleyScene"));
            Assert.That(context.SceneFlow.SelectedRaceSceneName, Is.EqualTo("ValleyScene"));
        }

        [Test]
        public async Task StartRace_FailureIsPresentedAndKeepsTheScreenUsable()
        {
            var context = await CreateContext();
            context.Presenter.Present();
            context.Loader.FailNextLoad = true;

            await context.Presenter.StartRace();

            Assert.That(context.SceneFlow.State, Is.EqualTo(ApplicationState.Garage));
            Assert.That(context.Presenter.Current.Status, Is.EqualTo(PresentationStatus.Invalid));
            Assert.That(context.Presenter.Current.MessageKey, Is.EqualTo("ui.trackSelect.startRaceFailed"));
            Assert.That(context.Presenter.Current.OperationActive, Is.False);
            Assert.That(context.Presenter.Current.CanStartRace, Is.True);
        }

        [Test]
        public async Task StartRace_WithEmptyCatalogDoesNotNavigate()
        {
            var context = await CreateContext(tracks: new TrackDefinitionData[0]);
            context.Presenter.Present();

            await context.Presenter.StartRace();

            Assert.That(context.SceneFlow.State, Is.EqualTo(ApplicationState.Garage));
            Assert.That(context.Presenter.Current.HasSelection, Is.False);
            Assert.That(context.Presenter.Current.MessageKey, Is.EqualTo("ui.trackSelect.noTrack"));
        }

        [Test]
        public async Task Constructor_ReopensOnTheLastRacedTrack()
        {
            var context = await CreateContext();
            Assert.That(context.SceneFlow.SelectRaceScene("CanyonScene"), Is.True);

            var presenter = new TrackSelectionPresenter(new FakeTrackSelectionView(), context.Catalog, context.SceneFlow, context.Navigator);
            presenter.Present();

            Assert.That(presenter.Current.Selected.TrackId, Is.EqualTo("canyon"));
        }

        [Test]
        public async Task SceneFlow_RetryReloadsTheSelectedRaceScene()
        {
            var context = await CreateContext();
            context.SceneFlow.SelectRaceScene("CanyonScene");
            await context.SceneFlow.StartRace();

            await context.SceneFlow.RetryRace();

            Assert.That(context.Loader.LoadedSceneNames, Is.EqualTo(new[] { "MainMenu", "Garage", "CanyonScene", "CanyonScene" }));
            Assert.That(context.SceneFlow.SelectRaceScene(" "), Is.False);
        }

        [Test]
        public async Task SceneFlow_WithoutSelectionUsesTheConfiguredRaceScene()
        {
            var context = await CreateContext();

            await context.SceneFlow.StartRace();

            Assert.That(context.Loader.LoadedSceneNames[context.Loader.LoadedSceneNames.Count - 1], Is.EqualTo("Race"));
        }

        [Test]
        public async Task Dispose_StopsRendering()
        {
            var context = await CreateContext();
            context.Presenter.Dispose();

            context.Presenter.Present();
            context.Presenter.SelectNextTrack();

            Assert.That(context.View.RenderCount, Is.Zero);
        }

        private static async Task<Context> CreateContext(string defaultTrackId = null, TrackDefinitionData[] tracks = null)
        {
            var loader = new RecordingSceneLoader();
            var sceneFlow = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), loader);
            await sceneFlow.OpenMainMenu();
            await sceneFlow.OpenGarage();
            var catalog = new TrackCatalog(tracks ?? new[]
            {
                Track("harbor", "Harbor Run", "HarborScene"),
                Track("valley", "Pine Valley", "ValleyScene", 2400f),
                Track("canyon", "Canyon Loop", "CanyonScene")
            }, defaultTrackId);
            var view = new FakeTrackSelectionView();
            var navigator = new PresentationScreenNavigator(PresentationScreenState.Garage);
            var presenter = new TrackSelectionPresenter(view, catalog, sceneFlow, navigator);
            return new Context(presenter, view, navigator, sceneFlow, loader, catalog);
        }

        private static TrackDefinitionData Track(string id, string name, string scene, float length = 1900f)
        {
            return new TrackDefinitionData(id, name, "Mountain Pass", length, 3, "Forest", "Asphalt", scene, null,
                new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f) });
        }

        private sealed class Context
        {
            public Context(TrackSelectionPresenter presenter, FakeTrackSelectionView view, PresentationScreenNavigator navigator, SceneFlowService sceneFlow, RecordingSceneLoader loader, TrackCatalog catalog)
            {
                Presenter = presenter;
                View = view;
                Navigator = navigator;
                SceneFlow = sceneFlow;
                Loader = loader;
                Catalog = catalog;
            }

            public TrackSelectionPresenter Presenter { get; }
            public FakeTrackSelectionView View { get; }
            public PresentationScreenNavigator Navigator { get; }
            public SceneFlowService SceneFlow { get; }
            public RecordingSceneLoader Loader { get; }
            public TrackCatalog Catalog { get; }
        }

        private sealed class FakeTrackSelectionView : ITrackSelectionView
        {
            public TrackSelectionViewModel Last { get; private set; }
            public int RenderCount { get; private set; }

            public void Render(TrackSelectionViewModel viewModel)
            {
                Last = viewModel;
                RenderCount++;
            }
        }

        private sealed class RecordingSceneLoader : IContentSceneLoader
        {
            public bool FailNextLoad { get; set; }
            public float Progress => 0f;
            public bool IsLoading => false;
            public string BootstrapSceneName => "Bootstrap";
            public string CurrentContentSceneName { get; private set; } = string.Empty;
            public List<string> LoadedSceneNames { get; } = new List<string>();

            public Task<ContentSceneLoadResult> LoadContentSceneAsync(
                ContentSceneId sceneId,
                string sceneName,
                bool reloadCurrentScene,
                CancellationToken cancellationToken = default)
            {
                if (FailNextLoad)
                {
                    FailNextLoad = false;
                    return Task.FromResult(ContentSceneLoadResult.Failure(ContentSceneLoadStatus.LoadFailed, sceneId, sceneName, "Load failed."));
                }

                var unloaded = CurrentContentSceneName;
                CurrentContentSceneName = sceneName;
                LoadedSceneNames.Add(sceneName);
                return Task.FromResult(ContentSceneLoadResult.Success(sceneId, sceneName, unloaded));
            }
        }
    }
}
