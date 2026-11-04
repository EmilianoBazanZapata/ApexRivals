namespace ApexRivals.VehicleSelection.Runtime
{
    public readonly struct VehicleSelectionResult
    {
        public VehicleSelectionResult(VehicleSelectionStatus status, string previousVehicleId, string selectedVehicleId, string message)
        {
            Status = status;
            PreviousVehicleId = previousVehicleId;
            SelectedVehicleId = selectedVehicleId;
            Message = message;
        }

        public VehicleSelectionStatus Status { get; }
        public string PreviousVehicleId { get; }
        public string SelectedVehicleId { get; }
        public string Message { get; }
        public bool Succeeded => Status == VehicleSelectionStatus.Succeeded;
    }
}
