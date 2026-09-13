namespace ApexRivals.Progression.Runtime
{
    public enum RaceProgressionConfigurationStatus
    {
        Valid,
        MissingConfiguration,
        MissingInitialTier,
        DuplicateTierId,
        NegativeThreshold,
        UnorderedThreshold,
        MissingAiConfiguration
    }
}
