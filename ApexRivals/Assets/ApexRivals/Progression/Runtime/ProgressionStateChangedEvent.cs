namespace ApexRivals.Progression.Runtime
{
    public readonly struct ProgressionStateChangedEvent
    {
        public ProgressionStateChangedEvent(int currency)
        {
            Currency = currency;
        }

        public int Currency { get; }
    }
}
