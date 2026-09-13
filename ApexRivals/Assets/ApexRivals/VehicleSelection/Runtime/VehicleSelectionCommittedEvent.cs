namespace ApexRivals.VehicleSelection.Runtime
{
    public readonly struct VehicleSelectionCommittedEvent
    {
        public VehicleSelectionCommittedEvent(string selectedVehicleId)
        {
            SelectedVehicleId = selectedVehicleId;
        }

        public string SelectedVehicleId { get; }
    }
}
