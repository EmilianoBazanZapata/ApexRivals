using System.Threading.Tasks;
using ApexRivals.Bootstrap.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Runtime;
using NUnit.Framework;

namespace ApexRivals.Tests.EditMode.SceneFlow
{
    public sealed class ApplicationBootstrapServiceTests
    {
        [Test]
        public async Task SuccessfulProfileLoadNavigatesToMainMenu()
        {
            var storage = new MemoryStorage();
            storage.WriteSave($"{{\"schemaVersion\":{PlayerProfileSaveData.CurrentSchemaVersion},\"currency\":50,\"engineUpgradeLevel\":1,\"handlingUpgradeLevel\":0,\"completedRaceCount\":0,\"selectedVehicleId\":\"starter\",\"vehicleSelectionCommitted\":true}}");
            var context = CreateBootstrapContext(storage);

            var result = await context.BootstrapService.StartAsync();

            Assert.That(result.Status, Is.EqualTo(BootstrapStartupStatus.LoadedProfile));
            Assert.That(context.Navigation.State, Is.EqualTo(ApplicationState.MainMenu));
            Assert.That(context.ProgressionState.Currency, Is.EqualTo(50));
            Assert.That(context.SelectedVehicleState.HasCommittedVehicleSelection, Is.True);
            Assert.That(context.SelectedVehicleState.SelectedVehicleId, Is.EqualTo(SelectedVehicleState.DefaultVehicleId));
        }

        [Test]
        public async Task NewProfileDefaultsNavigateToMainMenu()
        {
            var context = CreateBootstrapContext(new MemoryStorage());

            var result = await context.BootstrapService.StartAsync();

            Assert.That(result.Status, Is.EqualTo(BootstrapStartupStatus.CreatedDefaultProfile));
            Assert.That(context.Navigation.State, Is.EqualTo(ApplicationState.MainMenu));
            Assert.That(context.ProgressionState.Currency, Is.EqualTo(0));
            Assert.That(context.SelectedVehicleState.HasCommittedVehicleSelection, Is.False);
            Assert.That(context.SelectedVehicleState.SelectedVehicleId, Is.Empty);
        }

        [Test]
        public async Task FatalStartupFailureProducesFailedState()
        {
            var storage = new MemoryStorage();
            storage.WriteSave("{bad json");
            var context = CreateBootstrapContext(storage);

            var result = await context.BootstrapService.StartAsync();

            Assert.That(result.Status, Is.EqualTo(BootstrapStartupStatus.Failed));
            Assert.That(context.Navigation.State, Is.EqualTo(ApplicationState.Failed));
        }

        private static BootstrapTestContext CreateBootstrapContext(MemoryStorage storage)
        {
            var progressionState = new PlayerProgressionState();
            var selectedVehicleState = new SelectedVehicleState();
            var saveService = new PlayerProfileSaveService(
                progressionState,
                selectedVehicleState,
                storage,
                new[] { SelectedVehicleState.DefaultVehicleId });
            var sceneMap = new SceneFlowSceneMap("MainMenu", "Garage", "Race");
            var navigation = new SceneFlowService(sceneMap, new ImmediateSceneLoader());
            var bootstrapService = new ApplicationBootstrapService(saveService, navigation);
            return new BootstrapTestContext(progressionState, selectedVehicleState, navigation, bootstrapService);
        }

        private readonly struct BootstrapTestContext
        {
            public BootstrapTestContext(
                PlayerProgressionState progressionState,
                SelectedVehicleState selectedVehicleState,
                SceneFlowService navigation,
                ApplicationBootstrapService bootstrapService)
            {
                ProgressionState = progressionState;
                SelectedVehicleState = selectedVehicleState;
                Navigation = navigation;
                BootstrapService = bootstrapService;
            }

            public PlayerProgressionState ProgressionState { get; }
            public SelectedVehicleState SelectedVehicleState { get; }
            public SceneFlowService Navigation { get; }
            public ApplicationBootstrapService BootstrapService { get; }
        }

        private sealed class ImmediateSceneLoader : IContentSceneLoader
        {
            public float Progress => 1f;
            public bool IsLoading => false;
            public string BootstrapSceneName => "Bootstrap";
            public string CurrentContentSceneName { get; private set; }

            public Task<ContentSceneLoadResult> LoadContentSceneAsync(
                ContentSceneId sceneId,
                string sceneName,
                bool reloadCurrentScene,
                System.Threading.CancellationToken cancellationToken = default)
            {
                CurrentContentSceneName = sceneName;
                return Task.FromResult(ContentSceneLoadResult.Success(sceneId, sceneName, string.Empty));
            }
        }

        private sealed class MemoryStorage : IPlayerProfileSaveStorage
        {
            private string _savePayload;

            public bool SaveExists()
            {
                return _savePayload != null;
            }

            public bool BackupExists()
            {
                return false;
            }

            public SaveStorageReadResult ReadSave()
            {
                return new SaveStorageReadResult(_savePayload != null, _savePayload, _savePayload == null ? "No save." : string.Empty);
            }

            public SaveStorageReadResult ReadBackup()
            {
                return new SaveStorageReadResult(false, string.Empty, "No backup.");
            }

            public SaveStorageWriteResult WriteSave(string payload)
            {
                _savePayload = payload;
                return new SaveStorageWriteResult(true, string.Empty);
            }

            public SaveStorageWriteResult DeleteSave()
            {
                _savePayload = null;
                return new SaveStorageWriteResult(true, string.Empty);
            }
        }
    }
}
