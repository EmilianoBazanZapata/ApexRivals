namespace ApexRivals.Race.Runtime
{
    public readonly struct RacerFinishedEvent
    {
        public RacerFinishedEvent(RaceResult result)
        {
            Result = result;
        }

        public RaceResult Result { get; }
    }
}
