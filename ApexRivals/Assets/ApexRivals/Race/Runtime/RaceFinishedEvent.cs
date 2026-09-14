namespace ApexRivals.Race.Runtime
{
    public readonly struct RaceFinishedEvent
    {
        public RaceFinishedEvent(RaceResult winnerResult)
        {
            WinnerResult = winnerResult;
        }

        public RaceResult WinnerResult { get; }
    }
}
