namespace ApexRivals.SceneFlow.Runtime
{
    public readonly struct ApplicationStateChangedEvent
    {
        public ApplicationStateChangedEvent(ApplicationState previousState, ApplicationState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
        }

        public ApplicationState PreviousState { get; }
        public ApplicationState CurrentState { get; }
    }
}
