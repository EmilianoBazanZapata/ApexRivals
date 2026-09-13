namespace ApexRivals.Progression.Runtime
{
    public readonly struct RaceProgressionChangedEvent
    {
        public RaceProgressionChangedEvent(int previousCompletedRaceCount, int completedRaceCount)
        {
            PreviousCompletedRaceCount = previousCompletedRaceCount;
            CompletedRaceCount = completedRaceCount;
        }

        public int PreviousCompletedRaceCount { get; }
        public int CompletedRaceCount { get; }
    }
}
