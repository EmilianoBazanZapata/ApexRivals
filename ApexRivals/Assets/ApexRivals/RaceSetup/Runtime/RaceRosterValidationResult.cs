namespace ApexRivals.RaceSetup.Runtime
{
    public readonly struct RaceRosterValidationResult
    {
        public RaceRosterValidationResult(RaceRosterValidationStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public RaceRosterValidationStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == RaceRosterValidationStatus.Succeeded;
    }
}
