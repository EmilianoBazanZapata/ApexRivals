namespace ApexRivals.RaceSession.Runtime
{
    public readonly struct RaceSessionCommandResult
    {
        public RaceSessionCommandResult(RaceSessionCommandStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public RaceSessionCommandStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status == RaceSessionCommandStatus.Succeeded;

        public static RaceSessionCommandResult Success()
        {
            return new RaceSessionCommandResult(RaceSessionCommandStatus.Succeeded, string.Empty);
        }

        public static RaceSessionCommandResult Failure(RaceSessionCommandStatus status, string message)
        {
            return new RaceSessionCommandResult(status, message);
        }
    }
}
