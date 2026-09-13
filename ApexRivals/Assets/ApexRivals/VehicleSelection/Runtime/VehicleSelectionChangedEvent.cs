namespace ApexRivals.VehicleSelection.Runtime
{
    public readonly struct VehicleSelectionChangedEvent
    {
        public VehicleSelectionChangedEvent(string previousVehicleId, string selectedVehicleId)
        {
            PreviousVehicleId = previousVehicleId;
            SelectedVehicleId = selectedVehicleId;
        }

        public string PreviousVehicleId { get; }
        public string SelectedVehicleId { get; }
    }
}
