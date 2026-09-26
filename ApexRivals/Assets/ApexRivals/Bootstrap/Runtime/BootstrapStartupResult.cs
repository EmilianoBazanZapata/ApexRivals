namespace ApexRivals.Bootstrap.Runtime
{
    public readonly struct BootstrapStartupResult
    {
        public BootstrapStartupResult(BootstrapStartupStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public BootstrapStartupStatus Status { get; }
        public string Message { get; }
        public bool Succeeded => Status != BootstrapStartupStatus.Failed;
    }
}
