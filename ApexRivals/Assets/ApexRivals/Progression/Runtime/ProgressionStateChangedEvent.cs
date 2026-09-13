namespace ApexRivals.Progression.Runtime
{
    public readonly struct ProgressionStateChangedEvent
    {
        public ProgressionStateChangedEvent(int currency, int completedRaceCount)
        {
            Currency = currency;
            CompletedRaceCount = completedRaceCount;
        }

        public int Currency { get; }
        public int CompletedRaceCount { get; }
    }
}
