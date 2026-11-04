namespace ApexRivals.Race.Runtime
{
    public readonly struct CountdownChangedEvent
    {
        public CountdownChangedEvent(float remainingSeconds)
        {
            RemainingSeconds = remainingSeconds;
        }

        public float RemainingSeconds { get; }
    }
}
