namespace ApexRivals.SceneFlow.Runtime
{
    /// <summary>Rejects race starts the game rules do not allow (locked circuit, car not owned).</summary>
    public interface IRaceStartGuard
    {
        RaceStartCheckResult CheckRaceStart(string raceSceneName);
    }
}
