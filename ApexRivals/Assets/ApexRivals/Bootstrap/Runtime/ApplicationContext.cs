using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Runtime;
using ApexRivals.VehicleSelection.Runtime;

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
            GarageService garageService = null)
        {
            Navigation = navigation;
            ProgressionState = progressionState;
            SelectedVehicleState = selectedVehicleState;
            SaveService = saveService;
            VehicleCatalog = vehicleCatalog;
            VehicleSelectionService = vehicleSelectionService;
            GarageService = garageService;
        }

        public SceneFlowService Navigation { get; }
        public PlayerProgressionState ProgressionState { get; }
        public SelectedVehicleState SelectedVehicleState { get; }
        public PlayerProfileSaveService SaveService { get; }
        public VehicleCatalog VehicleCatalog { get; }
        public VehicleSelectionService VehicleSelectionService { get; }
        public GarageService GarageService { get; }
    }
}
