namespace ApexRivals.SceneFlow.Runtime
{
    public readonly struct RaceStartCheckResult
    {
        private RaceStartCheckResult(bool allowed, string message)
        {
            Allowed = allowed;
            Message = message ?? string.Empty;
        }

        public bool Allowed { get; }
        public string Message { get; }

        public static RaceStartCheckResult Allow() => new RaceStartCheckResult(true, string.Empty);

        public static RaceStartCheckResult Reject(string message) => new RaceStartCheckResult(false, message);
    }
}
