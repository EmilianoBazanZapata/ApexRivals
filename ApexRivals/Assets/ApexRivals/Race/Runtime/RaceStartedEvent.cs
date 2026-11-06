namespace ApexRivals.Race.Runtime
{
    public readonly struct RaceStartedEvent
    {
        public RaceStartedEvent(float elapsedTime)
        {
            ElapsedTime = elapsedTime;
        }

        public float ElapsedTime { get; }
    }
}
