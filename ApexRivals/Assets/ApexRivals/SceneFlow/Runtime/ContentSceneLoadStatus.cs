namespace ApexRivals.SceneFlow.Runtime
{
    public enum ContentSceneLoadStatus
    {
        Succeeded,
        DuplicateLoad,
        ConcurrentLoad,
        MissingSceneName,
        LoadFailed
    }
}
