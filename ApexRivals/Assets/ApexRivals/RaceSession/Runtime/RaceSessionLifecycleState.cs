namespace ApexRivals.RaceSession.Runtime
{
    public enum RaceSessionLifecycleState
    {
        Uninitialized,
        Preparing,
        Ready,
        Starting,
        Racing,
        Paused,
        ShowingResults,
        Retrying,
        Exiting,
        Failed,
        Disposed
    }
}
