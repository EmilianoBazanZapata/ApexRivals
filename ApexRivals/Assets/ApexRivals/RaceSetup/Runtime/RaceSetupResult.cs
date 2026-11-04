namespace ApexRivals.RaceSetup.Runtime
{
    public readonly struct RaceSetupResult
    {
        public RaceSetupResult(RaceSetupStatus status, RaceSetupOutput output, string message)
        {
            Status = status;
            Output = output;
            Message = message;
        }

        public RaceSetupStatus Status { get; }
        public RaceSetupOutput Output { get; }
        public string Message { get; }
        public bool Succeeded => Status == RaceSetupStatus.Succeeded;
    }
}
