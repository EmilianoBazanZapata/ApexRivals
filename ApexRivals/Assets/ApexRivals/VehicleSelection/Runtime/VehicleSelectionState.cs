using System.Collections.Generic;

namespace ApexRivals.VehicleSelection.Runtime
{
    public readonly struct VehicleSelectionState
    {
        public VehicleSelectionState(string selectedVehicleId, VehicleSelectionItemState selectedVehicle, IReadOnlyList<VehicleSelectionItemState> vehicles)
        {
            SelectedVehicleId = selectedVehicleId;
            SelectedVehicle = selectedVehicle;
            Vehicles = vehicles;
        }

        public string SelectedVehicleId { get; }
        public VehicleSelectionItemState SelectedVehicle { get; }
        public IReadOnlyList<VehicleSelectionItemState> Vehicles { get; }
    }
}
