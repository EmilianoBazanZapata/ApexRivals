namespace ApexRivals.SceneFlow.Runtime
{
    public readonly struct SceneTransitionResult
    {
        public SceneTransitionResult(
            SceneTransitionStatus status,
            ApplicationState previousState,
            ApplicationState currentState,
            ContentSceneId? targetSceneId,
            string message)
        {
            Status = status;
            PreviousState = previousState;
            CurrentState = currentState;
            TargetSceneId = targetSceneId;
            Message = message;
        }

        public SceneTransitionStatus Status { get; }
        public ApplicationState PreviousState { get; }
        public ApplicationState CurrentState { get; }
        public ContentSceneId? TargetSceneId { get; }
        public string Message { get; }
        public bool Succeeded => Status == SceneTransitionStatus.Succeeded;
    }
}
