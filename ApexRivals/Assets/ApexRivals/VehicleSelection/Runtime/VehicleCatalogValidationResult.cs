namespace ApexRivals.VehicleSelection.Runtime
{
    public readonly struct VehicleCatalogValidationResult
    {
        public VehicleCatalogValidationResult(VehicleCatalogStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public VehicleCatalogStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == VehicleCatalogStatus.Succeeded;
    }
}
