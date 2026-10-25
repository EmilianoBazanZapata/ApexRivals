using ApexRivals.Career.Runtime;
using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.RaceSession.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.Settings.Runtime;
using ApexRivals.VehicleSelection.Runtime;
using ApexRivals.UI.Runtime;

namespace ApexRivals.Bootstrap.Runtime
{
    public sealed class ApplicationContext
    {
        public ApplicationContext(
            SceneFlowService navigation,
            PlayerProgressionState progressionState,
            SelectedVehicleState selectedVehicleState,
            PlayerProfileSaveService saveService,
            VehicleCatalog vehicleCatalog = null,
            VehicleSelectionService vehicleSelectionService = null,
            GarageService garageService = null,
            SettingsService settingsService = null,
            RaceProgressionService raceProgressionService = null,
            RacePauseCoordinator pauseCoordinator = null,
            CareerService career = null,
            ISceneLoadingPresentation loadingPresentation = null)
        {
            Navigation = navigation;
            ProgressionState = progressionState;
            SelectedVehicleState = selectedVehicleState;
            SaveService = saveService;
            SettingsService = settingsService;
            VehicleCatalog = vehicleCatalog;
            VehicleSelectionService = vehicleSelectionService;
            GarageService = garageService;
            RaceProgressionService = raceProgressionService;
            Career = career;
            LoadingPresentation = loadingPresentation;
            PauseCoordinator = pauseCoordinator ?? new RacePauseCoordinator(new UnityTimeScaleController(), navigation);
        }

        public SceneFlowService Navigation { get; }
        public ISceneLoadingPresentation LoadingPresentation { get; }
        public PlayerProgressionState ProgressionState { get; }
        public SelectedVehicleState SelectedVehicleState { get; }
        public PlayerProfileSaveService SaveService { get; }
        public SettingsService SettingsService { get; }
        public VehicleCatalog VehicleCatalog { get; }
        public VehicleSelectionService VehicleSelectionService { get; }
        public GarageService GarageService { get; }
        public RaceProgressionService RaceProgressionService { get; }
        public RacePauseCoordinator PauseCoordinator { get; }

        /// <summary>Career rules (ownership, unlocks, objectives, final); null when no career is configured.</summary>
        public CareerService Career { get; }
    }
}
