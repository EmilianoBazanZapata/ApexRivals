namespace ApexRivals.Progression.Runtime
{
    public readonly struct DifficultyTierChangedEvent
    {
        public DifficultyTierChangedEvent(RaceProgressionTier previousTier, RaceProgressionTier currentTier)
        {
            PreviousTier = previousTier;
            CurrentTier = currentTier;
        }

        public RaceProgressionTier PreviousTier { get; }
        public RaceProgressionTier CurrentTier { get; }
    }
}
