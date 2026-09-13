namespace ApexRivals.Race.Runtime
{
    public readonly struct LapCompletedEvent
    {
        public LapCompletedEvent(string racerId, int completedLap, int totalLaps)
        {
            RacerId = racerId;
            CompletedLap = completedLap;
            TotalLaps = totalLaps;
        }

        public string RacerId { get; }
        public int CompletedLap { get; }
        public int TotalLaps { get; }
    }
}
