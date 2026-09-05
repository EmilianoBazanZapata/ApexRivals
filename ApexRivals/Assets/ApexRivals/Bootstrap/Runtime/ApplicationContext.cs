using ApexRivals.Garage.Runtime;
using ApexRivals.Progression.Runtime;
using ApexRivals.SaveSystem.Runtime;
using ApexRivals.SceneFlow.Runtime;

namespace ApexRivals.Bootstrap.Runtime
{
    public sealed class ApplicationContext
    {
        public ApplicationContext(
            SceneFlowService navigation,
            PlayerProgressionState progressionState,
            SelectedVehicleState selectedVehicleState,
            PlayerProfileSaveService saveService,
            GarageService garageService = null)
        {
            Navigation = navigation;
            ProgressionState = progressionState;
            SelectedVehicleState = selectedVehicleState;
            SaveService = saveService;
            GarageService = garageService;
        }

        public SceneFlowService Navigation { get; }
        public PlayerProgressionState ProgressionState { get; }
        public SelectedVehicleState SelectedVehicleState { get; }
        public PlayerProfileSaveService SaveService { get; }
        public GarageService GarageService { get; }
    }
}
