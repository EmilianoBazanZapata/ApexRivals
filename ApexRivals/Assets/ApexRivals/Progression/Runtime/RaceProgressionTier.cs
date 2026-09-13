namespace ApexRivals.Progression.Runtime
{
    public readonly struct RaceProgressionTier
    {
        public RaceProgressionTier(string tierId, string displayName, int requiredCompletedRaceCount, string aiConfigurationId)
        {
            TierId = string.IsNullOrWhiteSpace(tierId) ? string.Empty : tierId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? TierId : displayName.Trim();
            RequiredCompletedRaceCount = requiredCompletedRaceCount;
            AiConfigurationId = string.IsNullOrWhiteSpace(aiConfigurationId) ? string.Empty : aiConfigurationId.Trim();
        }

        public string TierId { get; }
        public string DisplayName { get; }
        public int RequiredCompletedRaceCount { get; }
        public string AiConfigurationId { get; }
    }
}
