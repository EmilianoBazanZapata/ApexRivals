namespace ApexRivals.Race.Runtime
{
    public readonly struct PositionChangedEvent
    {
        public PositionChangedEvent(string racerId, int position)
        {
            RacerId = racerId;
            Position = position;
        }

        public string RacerId { get; }
        public int Position { get; }
    }
}
