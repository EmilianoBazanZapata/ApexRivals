using System.Collections;
using ApexRivals.Garage.Configuration;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Configuration;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Settings.Runtime;
using ApexRivals.VehicleSelection.Configuration;
using ApexRivals.VehicleSelection.Runtime;
using UnityEngine;

namespace ApexRivals.Bootstrap.Runtime
{
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private SceneFlowConfiguration sceneFlowConfiguration;
        [SerializeField] private VehicleCatalogDefinition vehicleCatalogDefinition;
        [SerializeField] private UpgradeDefinition engineUpgradeDefinition;
        [SerializeField] private UpgradeDefinition handlingUpgradeDefinition;
        [SerializeField] private UnityContentSceneLoader sceneLoader;
        [SerializeField] private string[] knownVehicleIds = { SelectedVehicleState.DefaultVehicleId };

        public ApplicationContext Context { get; private set; }
        public BootstrapStartupStatus StartupStatus { get; private set; } = BootstrapStartupStatus.NotStarted;

        private IEnumerator Start()
        {
            var startTask = StartAsync();
            while (!startTask.IsCompleted)
            {
                yield return null;
            }

            if (startTask.IsFaulted)
            {
                StartupStatus = BootstrapStartupStatus.Failed;
                Debug.LogException(startTask.Exception);
            }
        }

        public System.Threading.Tasks.Task<BootstrapStartupResult> StartAsync()
        {
            if (sceneFlowConfiguration == null)
            {
                StartupStatus = BootstrapStartupStatus.Failed;
                return System.Threading.Tasks.Task.FromResult(new BootstrapStartupResult(BootstrapStartupStatus.Failed, "Scene flow configuration is not assigned."));
            }

            if (vehicleCatalogDefinition == null)
            {
                StartupStatus = BootstrapStartupStatus.Failed;
                return System.Threading.Tasks.Task.FromResult(new BootstrapStartupResult(BootstrapStartupStatus.Failed, "Vehicle catalog configuration is not assigned."));
            }

            var vehicleCatalog = vehicleCatalogDefinition.CreateCatalog();
            var catalogValidation = vehicleCatalog.Validate();
            if (!catalogValidation.Succeeded)
            {
                StartupStatus = BootstrapStartupStatus.Failed;
                return System.Threading.Tasks.Task.FromResult(new BootstrapStartupResult(BootstrapStartupStatus.Failed, catalogValidation.Message));
            }

            if (sceneLoader == null)
            {
                sceneLoader = GetComponent<UnityContentSceneLoader>();
            }

            if (sceneLoader == null)
            {
                sceneLoader = gameObject.AddComponent<UnityContentSceneLoader>();
            }

            var progressionState = new PlayerProgressionState();
            var selectedVehicleState = new SelectedVehicleState();
            var storage = new PlayerProfileFileStorage(Application.persistentDataPath);
            var saveService = new PlayerProfileSaveService(progressionState, selectedVehicleState, storage, CreateKnownVehicleIds(vehicleCatalog));
            var settingsService = new SettingsService(new PlayerPrefsSettingsStorage(), new UnityDisplaySettingsAdapter());
            var saveCheckpoint = new PlayerProfileSaveCheckpoint(saveService);
            var vehicleSelectionService = new VehicleSelectionService(vehicleCatalog, selectedVehicleState, saveCheckpoint, progressionState);
            var garageService = CreateGarageService(vehicleCatalog, progressionState);
            var sceneFlowService = new SceneFlowService(
                sceneFlowConfiguration.CreateSceneMap(),
                sceneLoader,
                saveCheckpoint);

            Context = new ApplicationContext(
                sceneFlowService,
                progressionState,
                selectedVehicleState,
                saveService,
                vehicleCatalog,
                vehicleSelectionService,
                garageService,
                settingsService);

            var bootstrapService = new ApplicationBootstrapService(saveService, sceneFlowService, settingsService);
            var startTask = bootstrapService.StartAsync();
            return CompleteStartupAsync(startTask, bootstrapService, vehicleSelectionService);
        }

        private async System.Threading.Tasks.Task<BootstrapStartupResult> CompleteStartupAsync(
            System.Threading.Tasks.Task<BootstrapStartupResult> startTask,
            ApplicationBootstrapService bootstrapService,
            VehicleSelectionService vehicleSelectionService)
        {
            var result = await startTask;
            StartupStatus = bootstrapService.StartupStatus;
            if (!result.Succeeded)
            {
                Debug.LogError(result.Message);
            }
            else
            {
                var selectionResult = vehicleSelectionService.EnsurePersistedSelectionIsValid();
                if (!selectionResult.Succeeded)
                {
                    StartupStatus = BootstrapStartupStatus.Failed;
                    Context.Navigation.MarkFailed();
                    return new BootstrapStartupResult(BootstrapStartupStatus.Failed, selectionResult.Message);
                }
            }

            return result;
        }

        private string[] CreateKnownVehicleIds(VehicleCatalog vehicleCatalog)
        {
            var vehicles = vehicleCatalog.Vehicles;
            var ids = new string[vehicles.Count];
            for (var index = 0; index < vehicles.Count; index++)
            {
                ids[index] = vehicles[index].StableId;
            }

            return ids.Length > 0 ? ids : knownVehicleIds;
        }

        private GarageService CreateGarageService(VehicleCatalog vehicleCatalog, PlayerProgressionState progressionState)
        {
            if (engineUpgradeDefinition == null || handlingUpgradeDefinition == null)
            {
                return null;
            }

            var defaultVehicle = vehicleCatalog.ResolveSavedOrDefault(SelectedVehicleState.DefaultVehicleId);
            if (defaultVehicle == null || defaultVehicle.BaseConfiguration == null)
            {
                return null;
            }

            return new GarageService(
                progressionState,
                engineUpgradeDefinition.CreateData(),
                handlingUpgradeDefinition.CreateData(),
                defaultVehicle.BaseStats);
        }
    }
}
