namespace ApexRivals.SceneFlow.Runtime
{
    public readonly struct ContentSceneLoadResult
    {
        public ContentSceneLoadResult(
            ContentSceneLoadStatus status,
            ContentSceneId sceneId,
            string sceneName,
            string unloadedSceneName,
            string message)
        {
            Status = status;
            SceneId = sceneId;
            SceneName = sceneName;
            UnloadedSceneName = unloadedSceneName;
            Message = message;
        }

        public ContentSceneLoadStatus Status { get; }
        public ContentSceneId SceneId { get; }
        public string SceneName { get; }
        public string UnloadedSceneName { get; }
        public string Message { get; }
        public bool Succeeded => Status == ContentSceneLoadStatus.Succeeded;

        public static ContentSceneLoadResult Success(ContentSceneId sceneId, string sceneName, string unloadedSceneName)
        {
            return new ContentSceneLoadResult(ContentSceneLoadStatus.Succeeded, sceneId, sceneName, unloadedSceneName, string.Empty);
        }

        public static ContentSceneLoadResult Failure(ContentSceneLoadStatus status, ContentSceneId sceneId, string sceneName, string message)
        {
            return new ContentSceneLoadResult(status, sceneId, sceneName, string.Empty, message);
        }
    }
}
