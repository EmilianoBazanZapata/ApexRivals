namespace ApexRivals.VehicleSelection.Runtime
{
    public readonly struct VehicleLookupResult
    {
        public VehicleLookupResult(VehicleCatalogStatus status, VehicleDefinitionData vehicle, string message)
        {
            Status = status;
            Vehicle = vehicle;
            Message = message;
        }

        public VehicleCatalogStatus Status { get; }
        public VehicleDefinitionData Vehicle { get; }
        public string Message { get; }
        public bool Succeeded => Status == VehicleCatalogStatus.Succeeded;
    }
}
