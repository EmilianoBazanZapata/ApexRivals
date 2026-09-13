namespace ApexRivals.VehicleSelection.Runtime
{
    public readonly struct VehicleSelectionSaveResult
    {
        public VehicleSelectionSaveResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public bool Succeeded { get; }
        public string Message { get; }

        public static VehicleSelectionSaveResult Success()
        {
            return new VehicleSelectionSaveResult(true, string.Empty);
        }

        public static VehicleSelectionSaveResult Failure(string message)
        {
            return new VehicleSelectionSaveResult(false, message);
        }
    }
}
