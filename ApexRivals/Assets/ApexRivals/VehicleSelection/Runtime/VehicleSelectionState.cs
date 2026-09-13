using System.Collections.Generic;

namespace ApexRivals.VehicleSelection.Runtime
{
    public readonly struct VehicleSelectionState
    {
        public VehicleSelectionState(
            string selectedVehicleId,
            VehicleSelectionItemState selectedVehicle,
            IReadOnlyList<VehicleSelectionItemState> vehicles,
            bool hasCommittedVehicleSelection)
        {
            SelectedVehicleId = selectedVehicleId;
            SelectedVehicle = selectedVehicle;
            Vehicles = vehicles;
            HasCommittedVehicleSelection = hasCommittedVehicleSelection;
        }

        public string SelectedVehicleId { get; }
        public VehicleSelectionItemState SelectedVehicle { get; }
        public IReadOnlyList<VehicleSelectionItemState> Vehicles { get; }
        public bool HasCommittedVehicleSelection { get; }
    }
}
