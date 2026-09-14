namespace ApexRivals.Progression.Runtime
{
    public readonly struct DifficultyTierResolutionResult
    {
        public DifficultyTierResolutionResult(bool succeeded, RaceProgressionTier tier, string message)
        {
            Succeeded = succeeded;
            Tier = tier;
            Message = message;
        }

        public bool Succeeded { get; }
        public RaceProgressionTier Tier { get; }
        public string Message { get; }
    }
}
