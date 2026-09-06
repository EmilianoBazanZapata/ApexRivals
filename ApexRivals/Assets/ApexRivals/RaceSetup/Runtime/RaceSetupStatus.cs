namespace ApexRivals.RaceSetup.Runtime
{
    public enum RaceSetupStatus
    {
        Succeeded,
        AlreadySetup,
        InvalidRoster,
        MissingSpawnPose,
        MissingVehicleDefinition,
        MissingVehiclePrefab,
        MissingVehicleComposition,
        MissingAiConfiguration,
        DifficultyResolutionFailed,
        MissingRacingLine,
        SpawnFailed
    }
}
