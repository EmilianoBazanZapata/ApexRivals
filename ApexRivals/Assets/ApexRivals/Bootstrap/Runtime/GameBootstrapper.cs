using System.Collections;
using ApexRivals.UI.Runtime;
using ApexRivals.Audio.Runtime;
using ApexRivals.Career.Configuration;
using ApexRivals.Career.Runtime;
using ApexRivals.Composition.Runtime;
using ApexRivals.Tracks.Configuration;
using ApexRivals.Progression.Configuration;
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
using UnityEngine.SceneManagement;

namespace ApexRivals.Bootstrap.Runtime
{
    // The scene loader and the loading screen live next to the bootstrapper in the Bootstrap scene.
    [RequireComponent(typeof(UnityContentSceneLoader), typeof(SceneLoadingOverlay))]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private SceneFlowConfiguration sceneFlowConfiguration;
        [SerializeField] private VehicleCatalogDefinition vehicleCatalogDefinition;
        [SerializeField] private UpgradeDefinition engineUpgradeDefinition;
        [SerializeField] private UpgradeDefinition handlingUpgradeDefinition;
        [SerializeField] private RaceProgressionDefinition raceProgressionDefinition;
        [SerializeField, Tooltip("Optional. With the track catalog, enables the career (car ownership, unlocks, objectives, final).")]
        private CareerDefinition careerDefinition;
        [SerializeField] private TrackCatalogDefinition trackCatalogDefinition;
        [SerializeField] private UnityContentSceneLoader sceneLoader;
        [SerializeField, Tooltip("Music, UI cues and the audio bus; follows the audio settings.")] private GameAudio gameAudio;
        [SerializeField] private string[] knownVehicleIds = { SelectedVehicleState.DefaultVehicleId };

        private IContentSceneInstaller[] _activeSceneInstallers = System.Array.Empty<IContentSceneInstaller>();
        private Scene _activeInstallerScene;

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
            var progressionState = new PlayerProgressionState();
            var selectedVehicleState = new SelectedVehicleState();
            var careerState = careerDefinition != null ? new CareerState() : null;
            var storage = new PlayerProfileFileStorage(Application.persistentDataPath, PlayerProfileFileStorage.DefaultFileName);
            var saveService = new PlayerProfileSaveService(progressionState, selectedVehicleState, storage, CreateKnownVehicleIds(vehicleCatalog), careerState);
            var settingsService = new SettingsService(new PlayerPrefsSettingsStorage(), new UnityDisplaySettingsAdapter());
            var audio = gameAudio != null ? gameAudio : GameAudio.Current;
            if (audio != null)
            {
                audio.Bind(settingsService);
            }
            var saveCheckpoint = new PlayerProfileSaveCheckpoint(saveService);
            CareerService career = null;
            if (careerState != null)
            {
                var careerMessage = CreateCareerService(vehicleCatalog, careerState, progressionState, selectedVehicleState, saveCheckpoint, out career);
                if (career == null)
                {
                    StartupStatus = BootstrapStartupStatus.Failed;
                    return System.Threading.Tasks.Task.FromResult(new BootstrapStartupResult(BootstrapStartupStatus.Failed, careerMessage));
                }
            }

            var vehicleSelectionService = new VehicleSelectionService(vehicleCatalog, selectedVehicleState, saveCheckpoint, progressionState, ownership: career);
            var garageService = CreateGarageService(vehicleCatalog, vehicleSelectionService, progressionState);
            if (garageService != null && career != null)
            {
                garageService.UpgradePurchased += (_, _) => career.EvaluateMilestones();
            }
            var raceProgressionService = CreateRaceProgressionService(progressionState);
            if (raceProgressionService == null)
            {
                StartupStatus = BootstrapStartupStatus.Failed;
                return System.Threading.Tasks.Task.FromResult(new BootstrapStartupResult(BootstrapStartupStatus.Failed, "Race progression configuration is not assigned."));
            }

            var raceProgressionValidation = raceProgressionService.Validate();
            if (!raceProgressionValidation.Succeeded)
            {
                StartupStatus = BootstrapStartupStatus.Failed;
                return System.Threading.Tasks.Task.FromResult(new BootstrapStartupResult(BootstrapStartupStatus.Failed, raceProgressionValidation.Message));
            }

            var loadingOverlay = GetComponent<SceneLoadingOverlay>();
            loadingOverlay.Bind(GetComponent<BootstrapUiInputSystem>());
            var presentedLoader = new LoadingContentSceneLoader(sceneLoader, loadingOverlay, Debug.LogException);
            var sceneFlowService = new SceneFlowService(
                sceneFlowConfiguration.CreateSceneMap(),
                presentedLoader,
                saveCheckpoint,
                career);

            Context = new ApplicationContext(
                sceneFlowService,
                progressionState,
                selectedVehicleState,
                saveService,
                vehicleCatalog,
                vehicleSelectionService,
                garageService,
                settingsService,
                raceProgressionService,
                career: career,
                loadingPresentation: loadingOverlay);

            sceneLoader.ContentSceneUnloading += OnContentSceneUnloading;
            sceneLoader.ContentSceneLoaded += OnContentSceneLoaded;

            // The career grants the starter car and records milestones already met by the loaded profile.
            var bootstrapService = new ApplicationBootstrapService(saveService, sceneFlowService, settingsService, career != null ? career.Initialize : null);
            var startTask = bootstrapService.StartAsync();
            return CompleteStartupAsync(startTask, bootstrapService, vehicleSelectionService);
        }

        private void OnDestroy()
        {
            if (sceneLoader != null)
            {
                sceneLoader.ContentSceneUnloading -= OnContentSceneUnloading;
                sceneLoader.ContentSceneLoaded -= OnContentSceneLoaded;
            }

            UninstallActiveContentScene();
            Context?.PauseCoordinator.Dispose();
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
                EnsureCurrentContentSceneInstalled();

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

        private GarageService CreateGarageService(
            VehicleCatalog vehicleCatalog,
            VehicleSelectionService vehicleSelectionService,
            PlayerProgressionState progressionState)
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
                () => vehicleSelectionService.CurrentSelectedDefinition?.BasePerformanceStats ?? defaultVehicle.BasePerformanceStats);
        }

        private string CreateCareerService(
            VehicleCatalog vehicleCatalog,
            CareerState careerState,
            PlayerProgressionState progressionState,
            SelectedVehicleState selectedVehicleState,
            PlayerProfileSaveCheckpoint saveCheckpoint,
            out CareerService career)
        {
            career = null;
            if (trackCatalogDefinition == null)
            {
                return "The career needs the track catalog.";
            }

            if (careerDefinition.FinaleRaceSetup == null)
            {
                return "The career final has no race setup.";
            }

            var maximumLevels = new System.Collections.Generic.Dictionary<UpgradeType, int>();
            if (engineUpgradeDefinition != null) maximumLevels[UpgradeType.Engine] = engineUpgradeDefinition.CreateData().MaximumLevel;
            if (handlingUpgradeDefinition != null) maximumLevels[UpgradeType.Handling] = handlingUpgradeDefinition.CreateData().MaximumLevel;

            var configuration = careerDefinition.CreateConfiguration();
            var trackCatalog = trackCatalogDefinition.CreateCatalog();
            var validation = configuration.Validate(vehicleCatalog, trackCatalog, maximumLevels);
            if (!validation.Succeeded)
            {
                return validation.Message;
            }

            career = new CareerService(
                configuration,
                careerState,
                progressionState,
                selectedVehicleState,
                vehicleCatalog,
                trackCatalog,
                saveCheckpoint,
                careerDefinition.FinaleRaceSetup);
            return string.Empty;
        }

        private RaceProgressionService CreateRaceProgressionService(PlayerProgressionState progressionState)
        {
            if (raceProgressionDefinition == null)
            {
                return null;
            }

            return new RaceProgressionService(
                progressionState,
                raceProgressionDefinition.CreateTable(),
                raceProgressionDefinition.CreateAiConfigurationMap());
        }

        private void OnContentSceneUnloading(Scene scene)
        {
            if (!_activeInstallerScene.IsValid() || _activeInstallerScene.handle == scene.handle)
            {
                UninstallActiveContentScene();
            }
        }

        private void OnContentSceneLoaded(Scene scene)
        {
            InstallContentScene(scene);
        }

        private void EnsureCurrentContentSceneInstalled()
        {
            if (_activeSceneInstallers.Length > 0 || Context == null || sceneLoader == null)
            {
                return;
            }

            var contentSceneName = sceneLoader.CurrentContentSceneName;
            if (string.IsNullOrWhiteSpace(contentSceneName))
            {
                return;
            }

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded && scene.name == contentSceneName)
                {
                    InstallContentScene(scene);
                    return;
                }
            }
        }

        private void InstallContentScene(Scene scene)
        {
            if (Context == null || !scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            if (_activeInstallerScene.IsValid() && _activeInstallerScene.handle == scene.handle && _activeSceneInstallers.Length > 0)
            {
                return;
            }

            if (_activeSceneInstallers.Length > 0)
            {
                UninstallActiveContentScene();
            }

            var roots = scene.GetRootGameObjects();
            var installers = new System.Collections.Generic.List<IContentSceneInstaller>();
            var behaviours = new System.Collections.Generic.List<MonoBehaviour>();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                behaviours.Clear();
                roots[rootIndex].GetComponentsInChildren(true, behaviours);
                for (var behaviourIndex = 0; behaviourIndex < behaviours.Count; behaviourIndex++)
                {
                    if (behaviours[behaviourIndex] is IContentSceneInstaller installer)
                    {
                        installers.Add(installer);
                    }
                }
            }

            installers.Sort(CompareInstallers);
            _activeSceneInstallers = installers.ToArray();
            for (var index = 0; index < _activeSceneInstallers.Length; index++)
            {
                _activeSceneInstallers[index].Install(Context);
            }

            _activeInstallerScene = scene;
        }

        private void UninstallActiveContentScene()
        {
            for (var index = _activeSceneInstallers.Length - 1; index >= 0; index--)
            {
                _activeSceneInstallers[index]?.Uninstall();
            }

            _activeSceneInstallers = System.Array.Empty<IContentSceneInstaller>();
            _activeInstallerScene = default;
        }

        private static int CompareInstallers(IContentSceneInstaller left, IContentSceneInstaller right)
        {
            return GetInstallerPriority(left).CompareTo(GetInstallerPriority(right));
        }

        private static int GetInstallerPriority(IContentSceneInstaller installer)
        {
            if (installer is ApexRivals.Environment.Runtime.RaceWeatherController) return 20;
            return installer is RaceSessionSceneInstaller ? 0 : 10;
        }
    }
}
