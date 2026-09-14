namespace ApexRivals.SceneFlow.Runtime
{
    public enum SceneTransitionStatus
    {
        Succeeded,
        InvalidTransition,
        DuplicateTransition,
        ConcurrentTransition,
        MissingSceneConfiguration,
        SceneLoadFailed,
        SaveFailed
    }
}
