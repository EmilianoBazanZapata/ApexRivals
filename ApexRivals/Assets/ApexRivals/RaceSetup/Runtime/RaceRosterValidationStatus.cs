namespace ApexRivals.RaceSetup.Runtime
{
    public enum RaceRosterValidationStatus
    {
        Succeeded,
        MissingPlayer,
        MultiplePlayers,
        MissingParticipantId,
        DuplicateParticipantId,
        DuplicateGridIndex,
        NegativeGridIndex,
        UnknownVehicleId,
        MissingAiConfiguration,
        InsufficientSpawnPoints
    }
}
