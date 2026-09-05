namespace ApexRivals.RaceSession.Runtime
{
    public enum RaceSessionCommandStatus
    {
        Succeeded,
        InvalidState,
        MissingConfiguration,
        SetupFailed,
        MissingPlayer,
        MissingRaceCoordinator,
        MissingDrivingGate,
        StartFailed,
        RewardFailed,
        SaveFailedAfterReward,
        NavigationFailed,
        AlreadyProcessed,
        Disposed
    }
}
