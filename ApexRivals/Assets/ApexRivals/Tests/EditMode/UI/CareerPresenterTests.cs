using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApexRivals.Career.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.Race.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Tests.EditMode.Career;
using ApexRivals.UI.Runtime;
using ApexRivals.Vehicle.Runtime;
using ApexRivals.VehicleSelection.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.UI
{
    /// <summary>Career presentation on the real presenters: they only read the career and forward requests.</summary>
    public sealed class CareerPresenterTests
    {
        private readonly FixtureObjects _objects = new FixtureObjects();

        [TearDown]
        public void TearDown()
        {
            _objects.Dispose();
        }

        [Test]
        public async Task RaceSelectShowsLockedCircuitsWithTheirRequirementAndTheFinalEntry()
        {
            var context = await CreateContext();

            context.TrackSelection.Present();
            var tracks = context.TrackSelection.Current.Tracks;

            Assert.That(tracks.Count, Is.EqualTo(4));
            Assert.That(tracks[0].IsLocked, Is.False);
            Assert.That(tracks[1].IsLocked, Is.True);
            Assert.That(tracks[1].LockText, Is.EqualTo("REQUIRES: WIN AT APEX CIRCUIT"));
            Assert.That(tracks[3].IsFinale, Is.True);
            Assert.That(tracks[3].DisplayName, Is.EqualTo("Apex Rivals Final"));
            Assert.That(tracks[3].IsLocked, Is.True);
            Assert.That(tracks[3].LockText, Does.Contain("(0/8)"));
            Assert.That(context.TrackSelection.Current.ObjectiveText, Is.EqualTo("CAREER 0% - NEXT: FINISH A RACE AT APEX CIRCUIT"));
        }

        [Test]
        public async Task StartingALockedCircuitLoadsNothingAndExplainsWhy()
        {
            var context = await CreateContext();
            context.TrackSelection.Present();

            context.TrackSelection.SelectTrack(1);
            await context.TrackSelection.StartRace();

            Assert.That(context.TrackSelection.Current.CanStartRace, Is.False);
            Assert.That(context.Loader.LoadedSceneNames, Has.No.Member("RaceDunes"));
            Assert.That(context.TrackSelection.Current.Feedback, Does.Contain("Win at Apex Circuit"));
        }

        [Test]
        public async Task TheFinalEntryLoadsItsCircuitAsTheFinal()
        {
            var context = await CreateContext();
            context.Career.QualifyForFinale();
            context.TrackSelection.Present();

            context.TrackSelection.SelectTrack(3);
            await context.TrackSelection.StartRace();
            var attempt = context.Career.Service.BeginAttempt("RaceSuzuka", "vanguard");

            Assert.That(context.Loader.LoadedSceneNames[context.Loader.LoadedSceneNames.Count - 1], Is.EqualTo("RaceSuzuka"));
            Assert.That(attempt.IsFinale, Is.True);
        }

        [Test]
        public async Task GarageBrowsesALockedCarWithoutEquippingItAndBuysItThroughTheCareer()
        {
            var context = await CreateContext();
            context.Garage.Present();

            context.Garage.EquipNextVehicle();
            var locked = context.Garage.Current.Career;
            Assert.That(locked.CandidateVehicleName, Is.EqualTo("striker"));
            Assert.That(locked.CandidateOwned, Is.False);
            Assert.That(locked.CandidateStatus, Is.EqualTo("LOCKED - $ 1,800 - REQUIRES: WIN AT APEX CIRCUIT"));
            Assert.That(locked.CanBuyCandidate, Is.False);
            Assert.That(context.Career.Selected.SelectedVehicleId, Is.EqualTo("vanguard"));

            context.Career.Race("circuit", 1);
            context.Garage.Present();
            Assert.That(context.Garage.Current.Career.CandidateStatus, Is.EqualTo("FOR SALE - $ 1,800 - NEED $ 1,300 MORE"));
            context.Garage.BuyCandidateVehicle();
            Assert.That(context.Garage.Current.Career.Feedback, Is.EqualTo("NOT ENOUGH CURRENCY"));
            Assert.That(context.Career.Service.IsVehicleOwned("striker"), Is.False);

            context.Career.Progression.AddCurrency(1300);
            context.Garage.BuyCandidateVehicle();
            context.Garage.BuyCandidateVehicle();

            Assert.That(context.Career.Service.IsVehicleOwned("striker"), Is.True);
            Assert.That(context.Career.Selected.SelectedVehicleId, Is.EqualTo("striker"), "A bought car is equipped.");
            Assert.That(context.Career.Progression.Currency, Is.EqualTo(300), "Charged once, own-two-cars bonus once.");
            Assert.That(context.Garage.Current.Career.CandidateStatus, Is.EqualTo("OWNED"));
        }

        [Test]
        public async Task GarageAnnouncesUpgradeObjectivesOnce()
        {
            var context = await CreateContext();
            context.Career.Progression.AddCurrency(200);
            context.Garage.Present();

            context.Garage.PurchaseEngine();

            Assert.That(context.Garage.Current.Career.Feedback, Is.EqualTo("OBJECTIVE COMPLETE: BUY AN ENGINE UPGRADE  +$ 150"));
            Assert.That(context.Career.Service.ConsumeAnnouncements(), Is.Empty);
        }

        [Test]
        public void ResultsTextCelebratesTheCareerAndReportsWhatChanged()
        {
            var career = new CareerTestFixture(_objects);
            var first = career.Race("circuit", 1);
            career.QualifyForFinale();
            var final = career.Race("crossover", 1, finale: true);

            Assert.That(CareerViewText.FormatOutcomeTitle(first), Is.EqualTo("CAREER 22%"));
            Assert.That(CareerViewText.FormatOutcomeBody(first), Does.Contain("OBJECTIVE COMPLETE: WIN AT APEX CIRCUIT  +$ 300"));
            Assert.That(CareerViewText.FormatOutcomeBody(first), Does.Contain("NEW CIRCUIT UNLOCKED: DUNES CIRCUIT"));
            Assert.That(CareerViewText.FormatOutcomeBody(first), Does.Contain("NEXT: BUY AN ENGINE UPGRADE"));
            Assert.That(CareerViewText.FormatOutcomeTitle(final), Is.EqualTo("CHAMPION! CAREER COMPLETE - 100%"));
            Assert.That(CareerViewText.FormatOutcomeBody(final), Does.Contain("keep racing"));
            Assert.That(CareerViewText.FormatOutcomeTitle(CareerRaceOutcome.None), Is.Empty);
        }

        private async Task<Context> CreateContext()
        {
            var career = new CareerTestFixture(_objects);
            var loader = new RecordingSceneLoader();
            var sceneFlow = new SceneFlowService(new SceneFlowSceneMap("MainMenu", "Garage", "Race"), loader, null, career.Service);
            await sceneFlow.OpenMainMenu();
            await sceneFlow.OpenGarage();
            var navigator = new PresentationScreenNavigator(PresentationScreenState.Garage);
            var trackSelection = new TrackSelectionPresenter(new FakeTrackSelectionView(), career.Tracks, sceneFlow, navigator, career.Service);
            var engine = new UpgradeDefinitionData(UpgradeType.Engine, new[] { new UpgradeLevel(200, UpgradeStatModifier.Identity) });
            var handling = new UpgradeDefinitionData(UpgradeType.Handling, new[] { new UpgradeLevel(200, UpgradeStatModifier.Identity), new UpgradeLevel(350, UpgradeStatModifier.Identity) });
            var selection = new VehicleSelectionService(career.Vehicles, career.Selected, ownership: career.Service);
            var garageService = new GarageService(career.Progression, engine, handling, default(VehiclePerformanceStats));
            garageService.UpgradePurchased += (_, _) => career.Service.EvaluateMilestones();
            var garage = new GaragePresenter(new FakeGarageView(), garageService, selection, sceneFlow, trackSelection, career.Service);
            return new Context(career, loader, trackSelection, garage);
        }

        private sealed class Context
        {
            public Context(CareerTestFixture career, RecordingSceneLoader loader, TrackSelectionPresenter trackSelection, GaragePresenter garage)
            {
                Career = career;
                Loader = loader;
                TrackSelection = trackSelection;
                Garage = garage;
            }

            public CareerTestFixture Career { get; }
            public RecordingSceneLoader Loader { get; }
            public TrackSelectionPresenter TrackSelection { get; }
            public GaragePresenter Garage { get; }
        }

        private sealed class FakeTrackSelectionView : ITrackSelectionView
        {
            public void Render(TrackSelectionViewModel viewModel)
            {
            }
        }

        private sealed class FakeGarageView : IGarageView
        {
            public void Render(GarageViewModel viewModel)
            {
            }
        }

        private sealed class RecordingSceneLoader : IContentSceneLoader
        {
            public float Progress => 0f;
            public bool IsLoading => false;
            public string BootstrapSceneName => "Bootstrap";
            public string CurrentContentSceneName { get; private set; } = string.Empty;
            public List<string> LoadedSceneNames { get; } = new List<string>();

            public Task<ContentSceneLoadResult> LoadContentSceneAsync(ContentSceneId sceneId, string sceneName, bool reloadCurrentScene, CancellationToken cancellationToken = default)
            {
                var unloaded = CurrentContentSceneName;
                CurrentContentSceneName = sceneName;
                LoadedSceneNames.Add(sceneName);
                return Task.FromResult(ContentSceneLoadResult.Success(sceneId, sceneName, unloaded));
            }
        }
    }
}
